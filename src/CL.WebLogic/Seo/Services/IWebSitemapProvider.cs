using CL.WebLogic.Seo.Models;

namespace CL.WebLogic.Seo.Services;

/// <summary>
/// Supplies the dynamic URLs of <c>/sitemap.xml</c> (posts, products, …). Register with
/// <c>web.UseSitemapProvider&lt;T&gt;()</c>; the default, <see cref="NullSitemapProvider"/>, adds nothing.
/// Return only public pages: the sitemap is served to anyone.
/// </summary>
public interface IWebSitemapProvider
{
    /// <summary>The entries to list, in order.</summary>
    ValueTask<IReadOnlyList<WebSitemapEntry>> GetEntriesAsync(CancellationToken cancellationToken);
}
