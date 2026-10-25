namespace CL.WebLogic.Caching.Services;

/// <summary>Evicts cached pages and fragments by tag (e.g. after a post is edited).</summary>
public interface IWebCacheInvalidator
{
    /// <summary>Evicts output-cache and fragment-cache entries tagged with any of <paramref name="tags"/>.</summary>
    ValueTask EvictTagsAsync(IEnumerable<string> tags, CancellationToken cancellationToken);
}
