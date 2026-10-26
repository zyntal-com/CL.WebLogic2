using CL.NetUtils;
using CL.NetUtils.Models;
using CL.WebLogic.Configuration.Models;
using CL.WebLogic.Hosting.Services;
using CL.WebLogic.Security.Models;
using CL.WebLogic.Security.Pipeline;
using CL.WebLogic.Security.Rules;
using CL.WebLogic.Security.Services;
using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Security.Stages;

/// <summary>
/// DNS blocklist checks through CL.NetUtils with a bounded verdict cache. Blocking mode waits up
/// to the timeout; Background mode never delays a request (a listed address is refused from its
/// next request). Lookup errors never block (fail open) and are cached briefly. Zones come from
/// <see cref="IWebReputationListProvider"/>s when they return any, else from CL.NetUtils' config.
/// Addresses matched by an exempt IP rule are not checked.
/// </summary>
public sealed class ReputationStage : IWebSecurityStage, IDisposable
{
    private readonly SemaphoreSlim _backgroundSlots = new(16, 16);
    private readonly IWebLibraryResolver _libraries;
    private readonly IWebLogicContext _context;
    private readonly WebSecurityRules _rules;
    private readonly TimeProvider? _time;
    private (long Generation, BoundedTtlCache<string> Cache)? _verdicts;

    /// <summary>Creates the stage.</summary>
    public ReputationStage(IWebLibraryResolver libraries, IWebLogicContext context, WebSecurityRules rules, TimeProvider? time = null)
    {
        _libraries = libraries ?? throw new ArgumentNullException(nameof(libraries));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        _time = time;
    }

    /// <inheritdoc/>
    public string Name => "reputation";

    /// <inheritdoc/>
    public WebSecurityPhase Phase => WebSecurityPhase.PreRouting;

    /// <inheritdoc/>
    public int Order => 350;

    /// <inheritdoc/>
    public async ValueTask<WebSecurityDecision> ExecuteAsync(WebSecurityContext context, CancellationToken cancellationToken)
    {
        var config = context.Settings.Config.Reputation;
        if (!config.Enabled || context.ClientIp is null || context.IpExempt)
            return WebSecurityDecision.Continue;

        var ip = context.ClientIp.ToString();
        var cache = Verdicts(config);
        if (cache.TryGet(ip, out var listedOn))
            return Verdict(listedOn);

        var netUtils = _libraries.GetLibrary<NetUtilsLibrary>();
        if (netUtils is null)
            return WebSecurityDecision.Continue;

        if (config.Mode == ReputationMode.Background)
        {
            if (_backgroundSlots.Wait(0, CancellationToken.None))
                _ = CheckInBackgroundAsync(netUtils, ip, context.Settings, cache);
            return WebSecurityDecision.Continue;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(config.TimeoutMilliseconds);
        try
        {
            return Verdict(await CheckAsync(netUtils, ip, context.Settings, cache, timeout.Token).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return WebSecurityDecision.Continue; // over budget: let it through, the lookup is not cached
        }
    }

    /// <inheritdoc/>
    public void Dispose() => _backgroundSlots.Dispose();

    /// <summary>The verdict cache (zone name, or empty when not listed); replaced when the rules are invalidated.</summary>
    private BoundedTtlCache<string> Verdicts(ReputationConfig config)
    {
        var generation = _rules.Generation;
        if (_verdicts is { } current && current.Generation == generation)
            return current.Cache;
        var fresh = new BoundedTtlCache<string>(config.CacheSize, _time);
        _verdicts = (generation, fresh);
        return fresh;
    }

    private static WebSecurityDecision Verdict(string listedOn) =>
        listedOn.Length > 0 ? WebSecurityDecision.Reject(StatusCodes.Status403Forbidden, "dnsbl-listed", "dnsbl:" + listedOn) : WebSecurityDecision.Continue;

    private async Task CheckInBackgroundAsync(NetUtilsLibrary netUtils, string ip, WebSecuritySettings settings, BoundedTtlCache<string> cache)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await CheckAsync(netUtils, ip, settings, cache, timeout.Token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _context.Logger.Debug($"Background DNSBL check failed: {ex.Message}");
        }
        finally
        {
            _backgroundSlots.Release();
        }
    }

    private async Task<string> CheckAsync(NetUtilsLibrary netUtils, string ip, WebSecuritySettings settings, BoundedTtlCache<string> cache, CancellationToken cancellationToken)
    {
        var config = settings.Config.Reputation;
        var zones = await _rules.GetZonesAsync(settings, cancellationToken).ConfigureAwait(false);
        var result = zones.Count == 0
            ? await netUtils.Dnsbl.CheckIpAsync(ip, cancellationToken).ConfigureAwait(false)
            : await netUtils.Dnsbl.CheckIpAsync(ip, Request(zones), cancellationToken).ConfigureAwait(false);

        var ttl = result.ErrorMessage is null ? TimeSpan.FromMinutes(config.CacheMinutes) : TimeSpan.FromMinutes(Math.Max(1, config.CacheMinutes / 10));
        var listedOn = result.IsBlacklisted ? (string.IsNullOrEmpty(result.MatchedService) ? "unknown" : result.MatchedService) : "";
        cache.Set(ip, listedOn, ttl);
        if (result.IsBlacklisted)
            _context.Logger.Info($"Client listed on DNSBL {listedOn}");
        return listedOn;
    }

    private static DnsblCheckRequest Request(IReadOnlyList<WebReputationZone> zones)
    {
        static IReadOnlyList<string> Pick(IEnumerable<WebReputationZone> all, bool ipv6, bool fallback) =>
            [.. all.Where(z => z.Ipv6 == ipv6 && z.Fallback == fallback).Select(z => z.Zone.Trim())];
        return new DnsblCheckRequest
        {
            Ipv4Services = Pick(zones, false, false),
            Ipv4FallbackServices = Pick(zones, false, true),
            Ipv6Services = Pick(zones, true, false),
            Ipv6FallbackServices = Pick(zones, true, true),
            Timeout = TimeSpan.FromSeconds(5)
        };
    }
}
