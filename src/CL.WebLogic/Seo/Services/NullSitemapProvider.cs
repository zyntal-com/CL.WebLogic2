using CL.WebLogic.Seo.Models;

namespace CL.WebLogic.Seo.Services;

/// <summary>The default <see cref="IWebSitemapProvider"/>: no dynamic entries, only the listed routes.</summary>
public sealed class NullSitemapProvider : IWebSitemapProvider
{
    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<WebSitemapEntry>> GetEntriesAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<WebSitemapEntry>>([]);
}
