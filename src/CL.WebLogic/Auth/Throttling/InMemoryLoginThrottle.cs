using System.Net;
using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Security.Services;

namespace CL.WebLogic.Auth.Throttling;

/// <summary>Per-node login throttle on bounded in-memory caches.</summary>
public sealed class InMemoryLoginThrottle : IWebLoginThrottle, Security.Services.IWebNodeLocalStore
{
    private readonly BoundedTtlCache<(int Failures, DateTimeOffset Last)> _logins;
    private readonly BoundedTtlCache<int> _ips;
    private readonly WebLogicConfigHolder _config;
    private readonly TimeProvider _time;
    private readonly object _lock = new();

    /// <summary>Creates the throttle.</summary>
    public InMemoryLoginThrottle(WebLogicConfigHolder config, TimeProvider? time = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _time = time ?? TimeProvider.System;
        _logins = new BoundedTtlCache<(int, DateTimeOffset)>(100_000, _time);
        _ips = new BoundedTtlCache<int>(100_000, _time);
    }

    /// <inheritdoc/>
    public ValueTask<TimeSpan?> CheckAsync(string login, IPAddress? ip, CancellationToken cancellationToken)
    {
        var config = _config.Current.Auth.Lockout;
        if (ip is not null && _ips.TryGet(IpKey(ip), out var ipFailures) && ipFailures >= config.MaxFailuresPerIpPerHour)
            return ValueTask.FromResult<TimeSpan?>(TimeSpan.FromHours(1));

        if (!_logins.TryGet(Key(login), out var state) || state.Failures < config.FreeAttempts)
            return ValueTask.FromResult<TimeSpan?>(null);

        var delay = TimeSpan.FromSeconds(Math.Min(config.MaxBackoffSeconds, Math.Pow(2, state.Failures - config.FreeAttempts)));
        var wait = state.Last + delay - _time.GetUtcNow();
        return ValueTask.FromResult<TimeSpan?>(wait > TimeSpan.Zero ? wait : null);
    }

    /// <inheritdoc/>
    public ValueTask RecordFailureAsync(string login, IPAddress? ip, CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow();
        lock (_lock)
        {
            var failures = _logins.TryGet(Key(login), out var state) ? state.Failures + 1 : 1;
            _logins.Set(Key(login), (failures, now), TimeSpan.FromHours(1));
            if (ip is not null)
                _ips.Set(IpKey(ip), (_ips.TryGet(IpKey(ip), out var count) ? count : 0) + 1, TimeSpan.FromHours(1));
        }
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask RecordSuccessAsync(string login, IPAddress? ip, CancellationToken cancellationToken)
    {
        _logins.Set(Key(login), (0, _time.GetUtcNow()), TimeSpan.FromSeconds(1));
        return ValueTask.CompletedTask;
    }

    private string IpKey(IPAddress ip) =>
        Security.RateLimiting.ClientAddressKey.For(ip, _config.Current.Security.RateLimits.Ipv6PrefixLength);

    private static string Key(string login) => (login ?? string.Empty).Trim().ToUpperInvariant();
}
