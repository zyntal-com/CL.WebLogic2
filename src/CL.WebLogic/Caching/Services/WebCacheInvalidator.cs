using Microsoft.AspNetCore.OutputCaching;

namespace CL.WebLogic.Caching.Services;

/// <summary>Evicts from the output cache store and the fragment cache.</summary>
public sealed class WebCacheInvalidator : IWebCacheInvalidator
{
    private readonly IWebFragmentCache _fragments;
    private readonly IOutputCacheStore? _output;

    /// <summary>Creates the invalidator.</summary>
    public WebCacheInvalidator(IWebFragmentCache fragments, IOutputCacheStore? output = null)
    {
        _fragments = fragments ?? throw new ArgumentNullException(nameof(fragments));
        _output = output;
    }

    /// <inheritdoc/>
    public async ValueTask EvictTagsAsync(IEnumerable<string> tags, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tags);
        var list = tags.Where(t => !string.IsNullOrEmpty(t)).Distinct(StringComparer.Ordinal).ToList();
        _fragments.EvictTags(list);
        if (_output is null)
            return;
        foreach (var tag in list)
            await _output.EvictByTagAsync(tag, cancellationToken).ConfigureAwait(false);
    }
}
