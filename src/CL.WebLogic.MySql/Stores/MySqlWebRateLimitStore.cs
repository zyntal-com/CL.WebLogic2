using CL.WebLogic.MySql.Services;
using CL.WebLogic.Security.RateLimiting;

namespace CL.WebLogic.MySql.Stores;

/// <summary>
/// Token buckets in <c>wl_rate_limits</c>, shared by every node (N nodes still allow the configured
/// rate, not N times it). Each limited request is one round trip: the bucket is created if missing,
/// refilled and charged in one <c>UPDATE</c>, and the outcome comes back through
/// <c>LAST_INSERT_ID(expr)</c>, which is per connection, so concurrent requests cannot see each
/// other's result.
/// </summary>
public sealed class MySqlWebRateLimitStore : IWebRateLimitStore
{
    // allowed is assigned first (from the old tokens), then tokens uses it, then updated_ms carries
    // the encoded result: allowed * 1e9 + whole tokens left.
    private const string AcquireSql = """
        INSERT IGNORE INTO wl_rate_limits (bucket_key, tokens, allowed, updated_ms) VALUES (@key, @cap, 0, @now);
        UPDATE wl_rate_limits SET
          allowed = IF(LEAST(@cap, tokens + GREATEST(0, @now - updated_ms) / 1000 * @refill) >= 1, 1, 0),
          tokens = LEAST(@cap, tokens + GREATEST(0, @now - updated_ms) / 1000 * @refill) - allowed,
          updated_ms = @now + 0 * LAST_INSERT_ID(allowed * 1000000000 + LEAST(999999999, FLOOR(GREATEST(0, tokens))))
        WHERE bucket_key = @key;
        SELECT LAST_INSERT_ID();
        """;

    private readonly WebMySqlDatabase _db;
    private readonly TimeProvider _time;

    /// <summary>Creates the store.</summary>
    public MySqlWebRateLimitStore(WebMySqlDatabase db, TimeProvider? time = null)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _time = time ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    public async ValueTask<RateLimitDecision> TryAcquireAsync(string key, RateLimitRule rule, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(rule);
        var bucketKey = rule.Name + "|" + key;
        if (bucketKey.Length > 300)
            bucketKey = rule.Name + "|#" + Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key)));

        var encoded = await _db.ScalarAsync<long>(AcquireSql, new Dictionary<string, object?>
        {
            ["@key"] = bucketKey,
            ["@cap"] = (double)rule.Capacity,
            ["@refill"] = rule.RefillPerSecond,
            ["@now"] = _time.GetUtcNow().ToUnixTimeMilliseconds()
        }, cancellationToken).ConfigureAwait(false);

        var allowed = encoded >= 1_000_000_000;
        var remaining = (int)Math.Min(rule.Capacity, encoded % 1_000_000_000);
        var tokens = (double)remaining;
        var retry = allowed ? TimeSpan.Zero : TimeSpan.FromSeconds(Math.Max(0, 1 - tokens) / rule.RefillPerSecond);
        var reset = TimeSpan.FromSeconds((rule.Capacity - tokens) / rule.RefillPerSecond);
        return new RateLimitDecision(allowed, rule.Capacity, remaining, retry, reset);
    }

    /// <summary>Deletes buckets idle for longer than <paramref name="idle"/> (called by the sweep; they would be full again).</summary>
    public Task<int> DeleteIdleAsync(DateTimeOffset now, TimeSpan idle, CancellationToken cancellationToken) =>
        _db.ExecuteAsync("DELETE FROM wl_rate_limits WHERE updated_ms < @cutoff",
            new Dictionary<string, object?> { ["@cutoff"] = (now - idle).ToUnixTimeMilliseconds() }, cancellationToken);
}
