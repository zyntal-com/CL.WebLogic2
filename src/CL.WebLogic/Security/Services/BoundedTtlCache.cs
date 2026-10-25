using System.Collections.Concurrent;

namespace CL.WebLogic.Security.Services;

/// <summary>
/// A small thread-safe cache with per-entry expiry and a hard size bound: expired entries go
/// first, then arbitrary entries, so memory stays bounded under address-spraying attacks.
/// </summary>
public sealed class BoundedTtlCache<TValue>
{
    private readonly ConcurrentDictionary<string, (TValue Value, DateTimeOffset Expires)> _entries = new(StringComparer.Ordinal);
    private readonly TimeProvider _time;
    private int _trimming;

    /// <summary>Creates the cache.</summary>
    public BoundedTtlCache(int maxEntries, TimeProvider? time = null)
    {
        MaxEntries = Math.Max(1, maxEntries);
        _time = time ?? TimeProvider.System;
    }

    /// <summary>Upper bound on entries.</summary>
    public int MaxEntries { get; }

    /// <summary>Current entry count.</summary>
    public int Count => _entries.Count;

    /// <summary>Reads a live entry.</summary>
    public bool TryGet(string key, out TValue value)
    {
        if (_entries.TryGetValue(key, out var entry) && entry.Expires > _time.GetUtcNow())
        {
            value = entry.Value;
            return true;
        }
        value = default!;
        return false;
    }

    /// <summary>Stores an entry for <paramref name="ttl"/>.</summary>
    public void Set(string key, TValue value, TimeSpan ttl)
    {
        _entries[key] = (value, _time.GetUtcNow() + ttl);
        if (_entries.Count > MaxEntries)
            Trim();
    }

    private void Trim()
    {
        if (Interlocked.Exchange(ref _trimming, 1) == 1)
            return;
        try
        {
            var now = _time.GetUtcNow();
            foreach (var (key, entry) in _entries)
            {
                if (entry.Expires <= now)
                    _entries.TryRemove(key, out _);
            }

            var target = MaxEntries * 9 / 10;
            foreach (var key in _entries.Keys)
            {
                if (_entries.Count <= target)
                    break;
                _entries.TryRemove(key, out _);
            }
        }
        finally
        {
            Volatile.Write(ref _trimming, 0);
        }
    }
}
