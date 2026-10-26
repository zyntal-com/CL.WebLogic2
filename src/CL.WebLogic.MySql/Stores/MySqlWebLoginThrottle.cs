using System.Net;
using CL.WebLogic.Auth.Throttling;
using CL.WebLogic.Configuration.Services;
using CL.WebLogic.MySql.Entities;
using CL.WebLogic.MySql.Services;
using CL.WebLogic.Security.RateLimiting;

namespace CL.WebLogic.MySql.Stores;

/// <summary>
/// The sign-in back-off of the in-memory throttle, shared by every node: failure counters per
/// login and per client address in <c>wl_login_throttle</c>, each kept for an hour after the last
/// failure. Each failure is one atomic upsert: the counter is created, incremented, or restarted at 1
/// when it had expired.
/// </summary>
public sealed class MySqlWebLoginThrottle : IWebLoginThrottle
{
    private static readonly TimeSpan Window = TimeSpan.FromHours(1);
    private readonly WebMySqlDatabase _db;
    private readonly WebLogicConfigHolder _config;
    private readonly TimeProvider _time;

    /// <summary>Creates the throttle.</summary>
    public MySqlWebLoginThrottle(WebMySqlDatabase db, WebLogicConfigHolder config, TimeProvider? time = null)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _time = time ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    public async ValueTask<TimeSpan?> CheckAsync(string login, IPAddress? ip, CancellationToken cancellationToken)
    {
        var config = _config.Current.Auth.Lockout;
        var now = _time.GetUtcNow();
        if (ip is not null && await LiveAsync(IpKey(ip), now, cancellationToken).ConfigureAwait(false) is { } byIp && byIp.Failures >= config.MaxFailuresPerIpPerHour)
            return TimeSpan.FromHours(1);

        if (await LiveAsync(LoginKey(login), now, cancellationToken).ConfigureAwait(false) is not { } state || state.Failures < config.FreeAttempts)
            return null;
        var delay = TimeSpan.FromSeconds(Math.Min(config.MaxBackoffSeconds, Math.Pow(2, state.Failures - config.FreeAttempts)));
        var wait = WebMySqlDatabase.FromUtc(state.LastUtc) + delay - now;
        return wait > TimeSpan.Zero ? wait : null;
    }

    /// <inheritdoc/>
    public async ValueTask RecordFailureAsync(string login, IPAddress? ip, CancellationToken cancellationToken)
    {
        await IncrementAsync(LoginKey(login), cancellationToken).ConfigureAwait(false);
        if (ip is not null)
            await IncrementAsync(IpKey(ip), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask RecordSuccessAsync(string login, IPAddress? ip, CancellationToken cancellationToken)
    {
        var key = LoginKey(login);
        WebMySqlDatabase.Value(await _db.Query<LoginThrottleRow>().Where(t => t.ThrottleKey == key).DeleteAsync(cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Deletes expired counters (called by the sweep).</summary>
    public async Task<int> DeleteExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var nowUtc = now.UtcDateTime;
        return WebMySqlDatabase.Value(await _db.Query<LoginThrottleRow>().Where(t => t.ExpiresUtc <= nowUtc).DeleteAsync(cancellationToken).ConfigureAwait(false));
    }

    private async Task<LoginThrottleRow?> LiveAsync(string key, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var row = WebMySqlDatabase.Value(await _db.Repository<LoginThrottleRow>().GetByIdAsync(key, cancellationToken).ConfigureAwait(false));
        return row is not null && WebMySqlDatabase.FromUtc(row.ExpiresUtc) > now ? row : null;
    }

    private async Task IncrementAsync(string key, CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow();
        var nowUtc = now.UtcDateTime;
        // Failures is assigned first, so it still reads the old expiry.
        WebMySqlDatabase.Value(await _db.Repository<LoginThrottleRow>().UpsertAsync(
            new LoginThrottleRow { ThrottleKey = key, Failures = 1, LastUtc = nowUtc, ExpiresUtc = (now + Window).UtcDateTime },
            (current, incoming) => new LoginThrottleRow
            {
                Failures = current.ExpiresUtc <= nowUtc ? 1 : current.Failures + 1,
                LastUtc = incoming.LastUtc,
                ExpiresUtc = incoming.ExpiresUtc
            }, cancellationToken).ConfigureAwait(false));
    }

    private string IpKey(IPAddress ip) => "ip:" + ClientAddressKey.For(ip, _config.Current.Security.RateLimits.Ipv6PrefixLength);

    private static string LoginKey(string login)
    {
        var key = "login:" + (login ?? string.Empty).Trim().ToUpperInvariant();
        return key.Length <= 300 ? key : key[..300];
    }
}
