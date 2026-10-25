namespace CL.WebLogic.Caching.Models;

/// <summary>How long a fragment lives and which tags evict it.</summary>
public sealed record WebFragmentCacheOptions
{
    /// <summary>Lifetime (default: <c>Cache.DefaultFragmentSeconds</c>).</summary>
    public TimeSpan? Duration { get; init; }

    /// <summary>Tags (evict with <c>IWebCacheInvalidator.EvictTagsAsync</c>).</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];
}
