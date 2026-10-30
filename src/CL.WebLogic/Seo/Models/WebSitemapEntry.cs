namespace CL.WebLogic.Seo.Models;

/// <summary>
/// One URL in <c>/sitemap.xml</c>, supplied by an <see cref="Services.IWebSitemapProvider"/> for pages the
/// route table cannot list (posts, products: anything with route parameters).
/// </summary>
public sealed record WebSitemapEntry
{
    /// <summary>
    /// Local path, optionally with a query string (<c>/posts/hello-world</c>). The path part is
    /// percent-encoded here; existing <c>%XX</c> escapes are kept. Anything that is not a local path
    /// (<c>https://…</c>, <c>//host</c>) is skipped: the host always comes from <c>PublicBaseUrl</c>.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>When the page last changed, or null.</summary>
    public DateTimeOffset? LastModified { get; init; }

    /// <summary>How often the page changes (a hint to crawlers), or null.</summary>
    public WebSitemapChangeFrequency? ChangeFrequency { get; init; }

    /// <summary>Relative priority between 0.0 and 1.0 (clamped), or null.</summary>
    public double? Priority { get; init; }
}
