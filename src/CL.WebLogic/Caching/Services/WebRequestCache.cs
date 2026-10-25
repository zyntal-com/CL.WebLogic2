using CL.WebLogic.Caching.Models;
using CL.WebLogic.Runtime.Models;

namespace CL.WebLogic.Caching.Services;

/// <summary>The fragment cache for the current request (<c>request.Cache</c>); keys are scoped to the tenant.</summary>
public sealed class WebRequestCache
{
    private readonly WebRequestContext _request;
    private readonly IWebFragmentCache _cache;
    private readonly IWebCacheInvalidator _invalidator;

    internal WebRequestCache(WebRequestContext request, IWebFragmentCache cache, IWebCacheInvalidator invalidator)
    {
        _request = request;
        _cache = cache;
        _invalidator = invalidator;
    }

    /// <summary>The cached value for <paramref name="key"/>, or the factory's result.</summary>
    public ValueTask<T> GetOrAddAsync<T>(string key, Func<CancellationToken, ValueTask<T>> factory, TimeSpan? duration = null, IReadOnlyList<string>? tags = null, CancellationToken cancellationToken = default) =>
        _cache.GetOrAddAsync(Scoped(key), factory, new WebFragmentCacheOptions { Duration = duration, Tags = tags ?? [] }, Token(cancellationToken));

    /// <summary>Removes one key.</summary>
    public void Remove(string key) => _cache.Remove(Scoped(key));

    /// <summary>Evicts pages and fragments tagged with any of <paramref name="tags"/>.</summary>
    public ValueTask EvictTagsAsync(params string[] tags) => _invalidator.EvictTagsAsync(tags, _request.HttpContext.RequestAborted);

    private string Scoped(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        return "t:" + (_request.Security.TenantId ?? "") + "|" + key;
    }

    private CancellationToken Token(CancellationToken cancellationToken) =>
        cancellationToken.CanBeCanceled ? cancellationToken : _request.HttpContext.RequestAborted;
}
