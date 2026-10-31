using System.Net;
using CL.Data.Query;
using CL.WebLogic.Auth.Throttling;
using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Data.Entities;
using CL.WebLogic.Data.Services;
using CL.WebLogic.Security.RateLimiting;

namespace CL.WebLogic.Data.Stores;

/// <summary>
/// The sign-in back-off of the in-memory throttle, shared by every node: failure counters per
/// login and per client address in <c>wl_login_throttle</c>, each kept for an hour after the last
/// failure. Each failure is one atomic upsert: the counter is created, incremented, or restarted at 1
/// when it had expired.
/// </summary>
public sealed class DataWebLoginThrottle : IWebLoginThrottle
{
    private static readonly TimeSpan Window = TimeSpan.FromHours(1);
    private readonly WebDataDatabase _db;
    private readonly WebLogicConfigHolder _config;
    private readonly TimeProvider _time;

    /// <summary>Creates the throttle.</summary>
    public DataWebLoginThrottle(WebDataDatabase db, WebLogicConfigHolder config, TimeProvider? time = null)
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
        var wait = WebDataDatabase.FromUtc(state.LastUtc) + delay - now;
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
        WebDataDatabase.Value(await _db.Query<LoginThrottleRow>().Where(t => t.ThrottleKey == key).DeleteAsync(ct: cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Deletes expired counters (called by the sweep).</summary>
    public async Task<int> DeleteExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var nowUtc = now.UtcDateTime;
        return WebDataDatabase.Value(await _db.Query<LoginThrottleRow>().Where(t => t.ExpiresUtc <= nowUtc).DeleteAsync(ct: cancellationToken).ConfigureAwait(false));
    }

    private async Task<LoginThrottleRow?> LiveAsync(string key, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var row = WebDataDatabase.Value(await _db.Query<LoginThrottleRow>().Where(r => r.ThrottleKey == key).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false));
        return row is not null && WebDataDatabase.FromUtc(row.ExpiresUtc) > now ? row : null;
    }

    // One atomic UPDATE counts the failure on any node; a missing counter is created with one failure.
    // Failures is assigned before ExpiresUtc, so it reads the old expiry on every database (MySQL
    // assigns left to right; the others always read the old row). If another node creates the row
    // between the update and the insert, the update runs again.
    private async Task IncrementAsync(string key, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var now = _time.GetUtcNow();
            var nowUtc = now.UtcDateTime;
            var expires = (now + Window).UtcDateTime;
            var updated = WebDataDatabase.Value(await _db.Query<LoginThrottleRow>().Where(t => t.ThrottleKey == key).UpdateAsync(t => new LoginThrottleRow
            {
                Failures = t.ExpiresUtc <= nowUtc ? 1 : t.Failures + 1,
                LastUtc = nowUtc,
                ExpiresUtc = expires
            }, cancellationToken).ConfigureAwait(false));
            if (updated > 0)
                return;

            var created = WebDataDatabase.Value(await _db.Repository<LoginThrottleRow>().InsertIgnoreAsync(
                new LoginThrottleRow { ThrottleKey = key, Failures = 1, LastUtc = nowUtc, ExpiresUtc = expires }, cancellationToken).ConfigureAwait(false));
            if (created == InsertOutcome.Inserted)
                return;
        }
        throw new InvalidOperationException("WebLogic data store: the login throttle counter could not be updated.");
    }

    private string IpKey(IPAddress ip) => "ip:" + ClientAddressKey.For(ip, _config.Current.Security.RateLimits.Ipv6PrefixLength);

    private static string LoginKey(string login)
    {
        var key = "login:" + (login ?? string.Empty).Trim().ToUpperInvariant();
        return key.Length <= 300 ? key : key[..300];
    }
}
