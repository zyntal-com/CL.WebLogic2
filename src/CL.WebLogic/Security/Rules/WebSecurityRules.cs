using CL.WebLogic.Hosting.Services;
using CL.WebLogic.Security.Models;

namespace CL.WebLogic.Security.Rules;

/// <summary>
/// Loads and caches app-managed IP rules and DNSBL zones. Provider results are cached for
/// <c>Security.IpRules.ProviderCacheSeconds</c> and shared by all requests (one load at a time).
/// A failing provider keeps the last good rules and is retried after a short delay; config rules
/// always apply.
/// </summary>
public sealed class WebSecurityRules : IWebSecurityRulesInvalidator, IDisposable
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(10);

    private readonly IWebIpRuleProvider[] _ipProviders;
    private readonly IWebReputationListProvider[] _zoneProviders;
    private readonly IWebLogicContext _context;
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _ipLoad = new(1, 1);
    private readonly SemaphoreSlim _zoneLoad = new(1, 1);
    private readonly HashSet<string> _reportedInvalid = new(StringComparer.Ordinal);
    private Snapshot<IReadOnlyList<WebIpRule>>? _ipRules;
    private Snapshot<IReadOnlyList<WebReputationZone>>? _zones;
    private (WebSecuritySettings Settings, IReadOnlyList<WebIpRule> Rules, WebIpRuleSet Set)? _compiled;
    private long _generation;

    /// <summary>Creates the service.</summary>
    public WebSecurityRules(IEnumerable<IWebIpRuleProvider> ipProviders, IEnumerable<IWebReputationListProvider> zoneProviders, IWebLogicContext context, TimeProvider? time = null)
    {
        _ipProviders = [.. ipProviders ?? []];
        _zoneProviders = [.. zoneProviders ?? []];
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _time = time ?? TimeProvider.System;
    }

    /// <summary>Changes on every <see cref="Invalidate"/> (caches keyed on rules compare it).</summary>
    public long Generation => Interlocked.Read(ref _generation);

    /// <summary>True when reputation zone providers are registered.</summary>
    public bool HasZoneProviders => _zoneProviders.Length > 0;

    /// <inheritdoc/>
    public void Invalidate()
    {
        // Mark stale rather than drop: if the reload fails, the last good rules stay in force.
        _ipRules = _ipRules is { } rules ? rules with { RefreshAt = DateTimeOffset.MinValue } : null;
        _zones = _zones is { } zones ? zones with { RefreshAt = DateTimeOffset.MinValue } : null;
        Interlocked.Increment(ref _generation);
        _context.Logger.Debug("Security rules invalidated; providers reload on the next request.");
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _ipLoad.Dispose();
        _zoneLoad.Dispose();
    }

    /// <summary>The compiled rules for <paramref name="settings"/> (config plus providers).</summary>
    public async ValueTask<WebIpRuleSet> GetIpRulesAsync(WebSecuritySettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var now = _time.GetUtcNow();
        var rules = _ipProviders.Length == 0
            ? []
            : await LoadAsync(_ipLoad, () => _ipRules, s => _ipRules = s, LoadIpRulesAsync, settings, now, cancellationToken).ConfigureAwait(false);

        if (_compiled is { } c && ReferenceEquals(c.Settings, settings) && ReferenceEquals(c.Rules, rules) && now < c.Set.ValidUntil)
            return c.Set;
        var set = WebIpRuleSet.Compile(settings, rules, now, ReportInvalid);
        _compiled = (settings, rules, set);
        return set;
    }

    /// <summary>The zones from the providers (empty = use the CL.NetUtils configuration).</summary>
    public ValueTask<IReadOnlyList<WebReputationZone>> GetZonesAsync(WebSecuritySettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return _zoneProviders.Length == 0
            ? ValueTask.FromResult<IReadOnlyList<WebReputationZone>>([])
            : LoadAsync(_zoneLoad, () => _zones, s => _zones = s, LoadZonesAsync, settings, _time.GetUtcNow(), cancellationToken);
    }

    private async ValueTask<IReadOnlyList<T>> LoadAsync<T>(SemaphoreSlim gate, Func<Snapshot<IReadOnlyList<T>>?> read, Action<Snapshot<IReadOnlyList<T>>> write,
        Func<CancellationToken, Task<IReadOnlyList<T>>> load, WebSecuritySettings settings, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (read() is { } fresh && now < fresh.RefreshAt)
            return fresh.Value;

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var current = read();
            if (current is not null && now < current.RefreshAt)
                return current.Value;

            // Loading is shared by every waiting request, so one client's abort must not cancel it.
            using var timeout = new CancellationTokenSource(LoadTimeout);
            try
            {
                var value = await load(timeout.Token).ConfigureAwait(false);
                var ttl = TimeSpan.FromSeconds(Math.Max(1, settings.Config.IpRules.ProviderCacheSeconds));
                write(new Snapshot<IReadOnlyList<T>>(value, _time.GetUtcNow() + ttl));
                return value;
            }
            catch (Exception ex)
            {
                _context.Logger.Warning($"Loading security rules failed ({ex.GetType().Name}: {ex.Message}); keeping the previous rules and retrying in {RetryDelay.TotalSeconds:0}s.");
                var kept = current?.Value ?? [];
                write(new Snapshot<IReadOnlyList<T>>(kept, _time.GetUtcNow() + RetryDelay));
                return kept;
            }
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<IReadOnlyList<WebIpRule>> LoadIpRulesAsync(CancellationToken cancellationToken)
    {
        var all = new List<WebIpRule>();
        foreach (var provider in _ipProviders)
            all.AddRange(await provider.GetRulesAsync(cancellationToken).ConfigureAwait(false) ?? []);
        return all;
    }

    private async Task<IReadOnlyList<WebReputationZone>> LoadZonesAsync(CancellationToken cancellationToken)
    {
        var all = new List<WebReputationZone>();
        foreach (var provider in _zoneProviders)
            all.AddRange(await provider.GetZonesAsync(cancellationToken).ConfigureAwait(false) ?? []);
        return [.. all.Where(z => !string.IsNullOrWhiteSpace(z.Zone)).DistinctBy(z => (z.Zone.Trim().ToLowerInvariant(), z.Ipv6, z.Fallback))];
    }

    private void ReportInvalid(WebIpRule rule)
    {
        lock (_reportedInvalid)
        {
            if (_reportedInvalid.Count < 1000 && _reportedInvalid.Add(rule.Id ?? ""))
                _context.Logger.Warning($"IP rule '{rule.Id}' is ignored: set an id and exactly one of a valid network or a two-letter country (exempt rules need a network).");
        }
    }

    private sealed record Snapshot<T>(T Value, DateTimeOffset RefreshAt);
}
