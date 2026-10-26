using System.Security.Cryptography;
using System.Text;
using CL.MySQL2.Models;
using CL.WebLogic.MySql.Entities;
using CL.WebLogic.MySql.Services;
using CL.WebLogic.Security.RateLimiting;

namespace CL.WebLogic.MySql.Stores;

/// <summary>
/// Token buckets in <c>wl_rate_limits</c>, shared by every node (N nodes still allow the configured
/// rate, not N times it). Each request refills and charges its bucket in one atomic update that
/// returns the result (<c>UpdateAndGetAsync</c>: the row is locked, so concurrent requests on any
/// node queue behind each other instead of racing). A missing bucket is created full, minus the
/// request that created it.
/// </summary>
public sealed class MySqlWebRateLimitStore : IWebRateLimitStore
{
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
            bucketKey = rule.Name + "|#" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)));

        // Twice at most: a bucket created by another request between our update and insert.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            if (await ChargeAsync(bucketKey, rule, cancellationToken).ConfigureAwait(false) is { } bucket)
                return Decision(rule, bucket.Allowed == 1, bucket.Tokens);

            var created = WebMySqlDatabase.Value(await _db.Repository<RateLimitRow>().InsertIgnoreAsync(new RateLimitRow
            {
                BucketKey = bucketKey,
                Tokens = rule.Capacity - 1,
                Allowed = 1,
                UpdatedMs = _time.GetUtcNow().ToUnixTimeMilliseconds()
            }, cancellationToken).ConfigureAwait(false));
            if (created == InsertOutcome.Inserted)
                return Decision(rule, allowed: true, rule.Capacity - 1);
        }
        throw new InvalidOperationException("MySQL store: the rate-limit bucket could not be read or created.");
    }

    /// <summary>Deletes buckets idle for longer than <paramref name="idle"/> (called by the sweep; they would be full again).</summary>
    public async Task<int> DeleteIdleAsync(DateTimeOffset now, TimeSpan idle, CancellationToken cancellationToken)
    {
        var cutoff = (now - idle).ToUnixTimeMilliseconds();
        return WebMySqlDatabase.Value(await _db.Query<RateLimitRow>().Where(b => b.UpdatedMs < cutoff).DeleteAsync(cancellationToken).ConfigureAwait(false));
    }

    // Refill by the elapsed time (capped at the capacity), then take one token if there is one.
    // MySQL assigns left to right: Allowed reads the old tokens, Tokens reads the new Allowed, and
    // UpdatedMs is assigned last so both still see the old timestamp.
    private async Task<RateLimitRow?> ChargeAsync(string bucketKey, RateLimitRule rule, CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow().ToUnixTimeMilliseconds();
        double capacity = rule.Capacity;
        var perMs = rule.RefillPerSecond / 1000;
        var rows = WebMySqlDatabase.Value(await _db.Query<RateLimitRow>().Where(b => b.BucketKey == bucketKey).UpdateAndGetAsync(b => new RateLimitRow
        {
            Allowed = Math.Min(capacity, b.Tokens + Math.Max(0, now - b.UpdatedMs) * perMs) >= 1 ? 1 : 0,
            Tokens = Math.Min(capacity, b.Tokens + Math.Max(0, now - b.UpdatedMs) * perMs) - b.Allowed,
            UpdatedMs = Math.Max(now, b.UpdatedMs)
        }, cancellationToken).ConfigureAwait(false));
        return rows.Count == 0 ? null : rows[0];
    }

    private static RateLimitDecision Decision(RateLimitRule rule, bool allowed, double tokens)
    {
        var remaining = (int)Math.Clamp(Math.Floor(tokens), 0, rule.Capacity);
        var retry = allowed ? TimeSpan.Zero : TimeSpan.FromSeconds(Math.Max(0, 1 - tokens) / rule.RefillPerSecond);
        var reset = TimeSpan.FromSeconds((rule.Capacity - Math.Max(0, tokens)) / rule.RefillPerSecond);
        return new RateLimitDecision(allowed, rule.Capacity, remaining, retry, reset);
    }
}
