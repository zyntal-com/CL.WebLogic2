using System.Globalization;
using System.Security;
using System.Text;
using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Hosting.Services;
using CL.WebLogic.Routing.Models;
using CL.WebLogic.Routing.Services;
using CL.WebLogic.Runtime.Services;
using CL.WebLogic.Seo.Models;
using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Seo.Services;

/// <summary>
/// Builds <c>/sitemap.xml</c> and <c>/robots.txt</c>. Absolute URLs come from <c>PublicBaseUrl</c>
/// (read per call), never from the request's Host header; every value is XML-escaped.
/// </summary>
public sealed class WebSitemapService
{
    private readonly WebRouteRegistry _routes;
    private readonly IWebSitemapProvider _provider;
    private readonly WebSitemapOptions _options;
    private readonly WebLogicConfigHolder _config;
    private readonly IWebLogicContext _context;

    /// <summary>Creates the service.</summary>
    public WebSitemapService(WebRouteRegistry routes, IWebSitemapProvider provider, WebSitemapOptions options, WebLogicConfigHolder config, IWebLogicContext context)
    {
        _routes = routes ?? throw new ArgumentNullException(nameof(routes));
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary><c>PublicBaseUrl</c> without a trailing slash, or null when it is not set.</summary>
    public string? BaseUrl
    {
        get
        {
            var value = _config.Current.PublicBaseUrl.Trim().TrimEnd('/');
            return value.Length == 0 ? null : value;
        }
    }

    /// <summary>The sitemap document, or null when <c>PublicBaseUrl</c> is not set (absolute URLs are required).</summary>
    public async Task<string?> BuildSitemapAsync(CancellationToken cancellationToken)
    {
        if (BaseUrl is not { } baseUrl)
        {
            _context.Logger.Warning("PublicBaseUrl is not set; /sitemap.xml answers 404. Set PublicBaseUrl.");
            return null;
        }

        var entries = new List<WebSitemapEntry>();
        if (_options.IncludeRoutes)
            entries.AddRange(RouteEntries());
        entries.AddRange(await _provider.GetEntriesAsync(cancellationToken).ConfigureAwait(false));

        var xml = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var written = 0;
        foreach (var entry in entries)
        {
            if (entry is null || Normalize(entry.Path) is not { } path || !WebPathPattern.Passes(PathOnly(path), [], [.. _options.ExcludePaths]) || !seen.Add(path))
                continue;
            if (written == _options.MaxEntries)
            {
                _context.Logger.Warning($"The sitemap has more than {_options.MaxEntries} URLs; the rest were left out.");
                break;
            }

            written++;
            xml.Append("  <url><loc>").Append(SecurityElement.Escape(baseUrl + path)).Append("</loc>");
            if (entry.LastModified is { } modified)
                xml.Append("<lastmod>").Append(modified.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)).Append("</lastmod>");
            if (entry.ChangeFrequency is { } frequency)
                xml.Append("<changefreq>").Append(frequency.ToString().ToLowerInvariant()).Append("</changefreq>");
            if (entry.Priority is { } priority && double.IsFinite(priority))
                xml.Append("<priority>").Append(Math.Clamp(priority, 0, 1).ToString("0.0", CultureInfo.InvariantCulture)).Append("</priority>");
            xml.Append("</url>\n");
        }

        return xml.Append("</urlset>\n").ToString();
    }

    /// <summary>The robots.txt body: <see cref="WebSitemapOptions.RobotsText"/> plus a <c>Sitemap:</c> line when the sitemap is served and <c>PublicBaseUrl</c> is set.</summary>
    public string BuildRobots()
    {
        var text = new StringBuilder(_options.RobotsText.Replace("\r\n", "\n", StringComparison.Ordinal));
        if (text.Length > 0 && text[^1] != '\n')
            text.Append('\n');
        if (_options.ServeSitemap && BaseUrl is { } baseUrl)
            text.Append("\nSitemap: ").Append(baseUrl).Append("/sitemap.xml\n");
        return text.ToString();
    }

    private IEnumerable<WebSitemapEntry> RouteEntries() =>
        _routes.Routes
            .Select(r => r.Metadata)
            .Where(IsListed)
            .Select(m => m.Pattern)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.Ordinal)
            .Select(p => new WebSitemapEntry { Path = p });

    private static bool IsListed(WebRouteMetadata route) =>
        route.Kind == WebRouteKind.Page
        && route.AllowAnonymous
        && route.RequiredGroups.Count == 0 && route.RequiredPermissions.Count == 0 && route.Policies.Count == 0 && route.RecentSignIn is null
        && route.Methods.Contains("GET", StringComparer.OrdinalIgnoreCase)
        && !route.Pattern.Contains('{', StringComparison.Ordinal) && !route.Pattern.Contains('*', StringComparison.Ordinal)
        && !route.Pattern.StartsWith("/_weblogic", StringComparison.OrdinalIgnoreCase)
        && !route.Tags.Contains(WebSitemapOptions.ExcludeTag, StringComparer.OrdinalIgnoreCase);

    /// <summary>A local path with its path part percent-encoded, or null when it is not local.</summary>
    private static string? Normalize(string? path)
    {
        if (!WebUrl.IsLocal(path) || path!.Length > 2048)
            return null;
        var hash = path.IndexOf('#', StringComparison.Ordinal);
        if (hash >= 0)
            path = path[..hash];
        var query = path.IndexOf('?', StringComparison.Ordinal);
        var encoded = new PathString(query < 0 ? path : path[..query]).ToUriComponent();
        return query < 0 ? encoded : encoded + path[query..].Replace(" ", "%20", StringComparison.Ordinal);
    }

    private static string PathOnly(string path) =>
        path.IndexOf('?', StringComparison.Ordinal) is var query and >= 0 ? path[..query] : path;
}
