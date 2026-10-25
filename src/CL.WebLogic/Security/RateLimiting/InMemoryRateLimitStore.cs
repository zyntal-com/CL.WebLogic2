using System.Collections.Concurrent;
using CL.WebLogic.Configuration.Services;

namespace CL.WebLogic.Security.RateLimiting;

/// <summary>
/// In-memory token buckets with a hard bound on tracked keys (<c>RateLimits.MaxTrackedKeys</c>).
/// When the bound is exceeded, buckets that have refilled completely (judged by each bucket's own
/// rule) are dropped first — they carry no state a fresh bucket would not — then the fullest ones,
/// so memory stays bounded when an attacker sprays addresses while drained buckets (the ones that
/// are actually limiting someone) are kept longest.
/// </summary>
public sealed class InMemoryRateLimitStore : IWebRateLimitStore, Services.IWebNodeLocalStore
{
    private readonly ConcurrentDictionary<string, Bucket> _buckets = new(StringComparer.Ordinal);
    private readonly WebLogicConfigHolder _config;
    private readonly TimeProvider _time;
    private int _trimming;

    /// <summary>Creates the store.</summary>
    public InMemoryRateLimitStore(WebLogicConfigHolder config, TimeProvider? time = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _time = time ?? TimeProvider.System;
    }

    /// <summary>Tracked buckets (for diagnostics and tests).</summary>
    public int Count => _buckets.Count;

    /// <inheritdoc/>
    public ValueTask<RateLimitDecision> TryAcquireAsync(string key, RateLimitRule rule, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rule);
        var now = _time.GetTimestamp();
        var bucket = _buckets.GetOrAdd(key, static (_, state) => new Bucket(state.Capacity, state.Refill, state.Now), (rule.Capacity, Refill: rule.RefillPerSecond, Now: now));

        RateLimitDecision decision;
        lock (bucket)
        {
            Refill(bucket, rule, now);
            var allowed = bucket.Tokens >= 1;
            if (allowed)
                bucket.Tokens -= 1;
            var retry = allowed ? TimeSpan.Zero : TimeSpan.FromSeconds((1 - bucket.Tokens) / rule.RefillPerSecond);
            var reset = TimeSpan.FromSeconds((rule.Capacity - bucket.Tokens) / rule.RefillPerSecond);
            decision = new RateLimitDecision(allowed, rule.Capacity, (int)Math.Floor(bucket.Tokens), retry, reset);
        }

        if (_buckets.Count > _config.Current.Security.RateLimits.MaxTrackedKeys)
            Trim(now);
        return ValueTask.FromResult(decision);
    }

    private void Refill(Bucket bucket, RateLimitRule rule, long now)
    {
        var elapsed = _time.GetElapsedTime(bucket.LastTimestamp, now).TotalSeconds;
        if (elapsed > 0)
        {
            bucket.Tokens = Math.Min(rule.Capacity, bucket.Tokens + elapsed * rule.RefillPerSecond);
            bucket.LastTimestamp = now;
        }
    }

    private void Trim(long now)
    {
        if (Interlocked.Exchange(ref _trimming, 1) == 1)
            return;
        try
        {
            var max = _config.Current.Security.RateLimits.MaxTrackedKeys;
            foreach (var (key, bucket) in _buckets)
            {
                lock (bucket)
                {
                    // Full after refill = indistinguishable from a new bucket.
                    if (Fill(bucket, now) >= 1)
                        _buckets.TryRemove(key, out _);
                }
            }

            var target = max * 9 / 10;
            if (_buckets.Count <= target)
                return;
            var fullestFirst = _buckets.Select(x => (x.Key, Fill: Fill(x.Value, now))).OrderByDescending(x => x.Fill).ToList();
            foreach (var (key, _) in fullestFirst)
            {
                if (_buckets.Count <= target)
                    break;
                _buckets.TryRemove(key, out _);
            }
        }
        finally
        {
            Volatile.Write(ref _trimming, 0);
        }
    }

    /// <summary>How full the bucket would be now (0–1), by its own refill rate.</summary>
    private double Fill(Bucket bucket, long now)
    {
        lock (bucket)
        {
            var elapsed = _time.GetElapsedTime(bucket.LastTimestamp, now).TotalSeconds;
            return Math.Min(1, (bucket.Tokens + elapsed * bucket.RefillPerSecond) / bucket.Capacity);
        }
    }

    private sealed class Bucket(int capacity, double refillPerSecond, long timestamp)
    {
        public int Capacity { get; } = capacity;

        public double RefillPerSecond { get; } = refillPerSecond;

        public double Tokens { get; set; } = capacity;

        public long LastTimestamp { get; set; } = timestamp;
    }
}
