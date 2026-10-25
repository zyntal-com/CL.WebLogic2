using CL.WebLogic.Caching.Models;

namespace CL.WebLogic.Caching.Services;

/// <summary>Caches computed values (rendered fragments, query results) with tags. Concurrent misses for one key run the factory once.</summary>
public interface IWebFragmentCache
{
    /// <summary>The cached value, or the factory's result (cached unless it throws).</summary>
    ValueTask<T> GetOrAddAsync<T>(string key, Func<CancellationToken, ValueTask<T>> factory, WebFragmentCacheOptions? options, CancellationToken cancellationToken);

    /// <summary>Removes one key.</summary>
    void Remove(string key);

    /// <summary>Removes every entry carrying one of <paramref name="tags"/>.</summary>
    int EvictTags(IEnumerable<string> tags);
}
