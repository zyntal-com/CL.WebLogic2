namespace CL.WebLogic.Seo.Models;

/// <summary>
/// Options of <c>web.AddSitemap(...)</c>. Set once at startup by the app; the public base URL is
/// read from the live config on every request.
/// </summary>
public sealed class WebSitemapOptions
{
    /// <summary>Route tag that keeps an otherwise eligible page out of the sitemap: <c>.WithTags(WebSitemapOptions.ExcludeTag)</c>.</summary>
    public const string ExcludeTag = "sitemap:exclude";

    /// <summary>The sitemap protocol's limit of URLs per file.</summary>
    public const int ProtocolMaxEntries = 50_000;

    /// <summary>Serve <c>/sitemap.xml</c>.</summary>
    public bool ServeSitemap { get; set; } = true;

    /// <summary>Serve <c>/robots.txt</c>.</summary>
    public bool ServeRobots { get; set; } = true;

    /// <summary>
    /// List anonymous GET page routes without route parameters (<c>/</c>, <c>/about</c>). Routes that
    /// need sign-in, groups, permissions or policies, system routes under <c>/_weblogic</c> and routes tagged
    /// <see cref="ExcludeTag"/> are never listed.
    /// </summary>
    public bool IncludeRoutes { get; set; } = true;

    /// <summary>Path patterns left out of the sitemap (<c>/login</c>, <c>/account/**</c>), for routes and provider entries alike.</summary>
    public IList<string> ExcludePaths { get; } = [];

    /// <summary>Most URLs written (at most <see cref="ProtocolMaxEntries"/>); the rest are dropped with a warning.</summary>
    public int MaxEntries { get; set; } = ProtocolMaxEntries;

    /// <summary>
    /// The body of <c>/robots.txt</c> without the <c>Sitemap:</c> line, which is added from
    /// <c>PublicBaseUrl</c> when the sitemap is served. The default allows everything.
    /// </summary>
    public string RobotsText { get; set; } = "User-agent: *\nDisallow:\n";

    /// <summary>Seconds browsers and proxies may cache both files (<c>Cache-Control: public, max-age</c>); 0 sends <c>no-cache</c>.</summary>
    public int CacheSeconds { get; set; } = 3600;

    /// <summary>Throws when the options are unusable (startup error).</summary>
    internal void EnsureValid()
    {
        if (MaxEntries is < 1 or > ProtocolMaxEntries)
            throw new InvalidOperationException($"WebSitemapOptions.MaxEntries must be 1–{ProtocolMaxEntries}.");
        if (CacheSeconds < 0)
            throw new InvalidOperationException("WebSitemapOptions.CacheSeconds must not be negative.");
        if (RobotsText is null)
            throw new InvalidOperationException("WebSitemapOptions.RobotsText must not be null.");
    }
}
