using System.Collections.Concurrent;
using CL.WebLogic.Caching.Models;
using CL.WebLogic.Configuration.Services;
using Microsoft.Extensions.Caching.Memory;

namespace CL.WebLogic.Caching.Services;

/// <summary>In-process fragment cache: bounded entry count, tag index, single-flight factories.</summary>
public sealed class MemoryFragmentCache : IWebFragmentCache, IDisposable, Security.Services.IWebNodeLocalStore
{
    private readonly MemoryCache _cache;
    private readonly WebLogicConfigHolder _config;
    private readonly ConcurrentDictionary<string, Lazy<Task<object?>>> _inflight = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> _tags = new(StringComparer.Ordinal);
    private readonly Lock _tagLock = new();

    /// <summary>Creates the cache.</summary>
    public MemoryFragmentCache(WebLogicConfigHolder config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = config.Current.Cache.FragmentMaxEntries });
    }

    /// <inheritdoc/>
    public async ValueTask<T> GetOrAddAsync<T>(string key, Func<CancellationToken, ValueTask<T>> factory, WebFragmentCacheOptions? options, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(factory);
        if (_cache.TryGetValue(key, out var cached) && cached is Box box)
            return (T)box.Value!;

        // The factory runs detached from the first caller's token so a cancelled caller doesn't fail the others.
        var lazy = _inflight.GetOrAdd(key, _ => new Lazy<Task<object?>>(() => CreateAsync(key, factory, options)));
        try
        {
            return (T)(await lazy.Value.WaitAsync(cancellationToken).ConfigureAwait(false))!;
        }
        finally
        {
            if (lazy.Value.IsCompleted)
                _inflight.TryRemove(new KeyValuePair<string, Lazy<Task<object?>>>(key, lazy));
        }
    }

    /// <inheritdoc/>
    public void Remove(string key) => _cache.Remove(key);

    /// <inheritdoc/>
    public int EvictTags(IEnumerable<string> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        lock (_tagLock)
        {
            foreach (var tag in tags)
            {
                if (_tags.Remove(tag, out var tagged))
                    keys.UnionWith(tagged);
            }
        }
        foreach (var key in keys)
            _cache.Remove(key);
        return keys.Count;
    }

    /// <summary>Entries currently cached.</summary>
    public int Count => _cache.Count;

    /// <inheritdoc/>
    public void Dispose() => _cache.Dispose();

    private async Task<object?> CreateAsync<T>(string key, Func<CancellationToken, ValueTask<T>> factory, WebFragmentCacheOptions? options)
    {
        var value = await factory(CancellationToken.None).ConfigureAwait(false);
        var tags = options?.Tags ?? [];
        var entry = new MemoryCacheEntryOptions
        {
            Size = 1,
            AbsoluteExpirationRelativeToNow = options?.Duration ?? TimeSpan.FromSeconds(_config.Current.Cache.DefaultFragmentSeconds)
        };
        if (tags.Count > 0)
        {
            lock (_tagLock)
            {
                foreach (var tag in tags)
                {
                    if (!_tags.TryGetValue(tag, out var keys))
                        _tags[tag] = keys = new HashSet<string>(StringComparer.Ordinal);
                    keys.Add(key);
                }
            }
            entry.RegisterPostEvictionCallback((evictedKey, _, _, _) => Untag((string)evictedKey, tags));
        }
        _cache.Set(key, new Box(value), entry);
        return value;
    }

    private void Untag(string key, IReadOnlyList<string> tags)
    {
        lock (_tagLock)
        {
            foreach (var tag in tags)
            {
                if (_tags.TryGetValue(tag, out var keys) && keys.Remove(key) && keys.Count == 0)
                    _tags.Remove(tag);
            }
        }
    }

    private sealed record Box(object? Value);
}
