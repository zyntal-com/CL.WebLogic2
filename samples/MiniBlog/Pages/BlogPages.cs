using System.Globalization;
using System.Text;
using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Results;
using CL.WebLogic.Routing;
using CL.WebLogic.Routing.Services;
using CL.WebLogic.Runtime.Models;
using MiniBlog.Configuration;
using MiniBlog.Data.Posts;

namespace MiniBlog.Pages;

/// <summary>
/// The public blog. Every page is served from the output cache (tag <see cref="BlogCache.Tag"/>)
/// for anonymous readers until the admin plugin changes a post and evicts the tag.
/// </summary>
public sealed class BlogPages : IWebContributor
{
    private readonly BlogPostService _posts;
    private readonly MiniBlogConfig _config;
    private readonly WebLogicConfigHolder _web;

    /// <summary>Creates the contributor.</summary>
    public BlogPages(BlogPostService posts, MiniBlogConfig config, WebLogicConfigHolder web)
    {
        _posts = posts;
        _config = config;
        _web = web;
    }

    /// <inheritdoc/>
    public Task ContributeAsync(WebContributionBuilder builder, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.MapPage("/", (r, ct) => ListAsync(r, null, ct)).AllowAnonymous().WithOutputCache(BlogCache.Policy).WithName("home");
        builder.MapPage("/tags/{tag}", (r, ct) => ListAsync(r, r.GetRouteValue("tag"), ct)).AllowAnonymous().WithOutputCache(BlogCache.Policy);
        builder.MapPage("/posts/{slug}", PostAsync).AllowAnonymous().WithOutputCache(BlogCache.Policy);
        builder.MapApi("/feed.xml", FeedAsync).AllowAnonymous().WithOutputCache(BlogCache.Policy);
        builder.MapApi("/sitemap.xml", SitemapAsync).AllowAnonymous().WithOutputCache(BlogCache.Policy);
        return Task.CompletedTask;
    }

    private async ValueTask<WebResult> ListAsync(WebRequestContext request, string? tag, CancellationToken cancellationToken)
    {
        var page = int.TryParse(request.GetQuery("page"), NumberStyles.None, CultureInfo.InvariantCulture, out var p) ? Math.Clamp(p, 1, 10_000) : 1;
        var result = await _posts.PublishedAsync(page, _config.PageSize, tag, cancellationToken).ConfigureAwait(false);
        if (tag is not null && result.TotalItems == 0)
            return WebResult.Status(404);
        var basePath = tag is null ? "/" : "/tags/" + Uri.EscapeDataString(tag);
        return WebResult.Page(tag is null ? "templates/home" : "templates/tag", new
        {
            _config.Tagline,
            Tag = tag,
            Posts = result.Items.Select(Card).ToList(),
            Page = result.PageNumber,
            result.TotalPages,
            PreviousUrl = result.HasPreviousPage ? $"{basePath}?page={result.PageNumber - 1}" : null,
            NextUrl = result.HasNextPage ? $"{basePath}?page={result.PageNumber + 1}" : null
        }, new WebPageMeta { Title = tag is null ? "Home" : "#" + tag, Description = _config.Tagline });
    }

    private async ValueTask<WebResult> PostAsync(WebRequestContext request, CancellationToken cancellationToken)
    {
        var post = await _posts.FindPublishedAsync(request.GetRouteValue("slug") ?? string.Empty, cancellationToken).ConfigureAwait(false);
        if (post is null)
            return WebResult.Status(404);
        return WebResult.Page("templates/post", new
        {
            post.Title,
            post.Body,
            post.Author,
            post.PublishedAt,
            Tags = post.Tags.Select(t => new { Name = t, Url = "/tags/" + Uri.EscapeDataString(t) }).ToList()
        }, new WebPageMeta { Title = post.Title, Description = post.Summary, CanonicalUrl = Absolute(post.Url) });
    }

    private async ValueTask<WebResult> FeedAsync(WebRequestContext request, CancellationToken cancellationToken)
    {
        var posts = await _posts.PublishedAsync(1, 20, null, cancellationToken).ConfigureAwait(false);
        var xml = new StringBuilder("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<rss version=\"2.0\"><channel>");
        xml.Append("<title>").Append(Xml(_web.Current.SiteName)).Append("</title><link>").Append(Xml(Absolute("/"))).Append("</link>")
           .Append("<description>").Append(Xml(_config.Tagline)).Append("</description>");
        foreach (var post in posts.Items)
        {
            xml.Append("<item><title>").Append(Xml(post.Title)).Append("</title><link>").Append(Xml(Absolute(post.Url))).Append("</link>")
               .Append("<guid isPermaLink=\"true\">").Append(Xml(Absolute(post.Url))).Append("</guid>")
               .Append("<pubDate>").Append((post.PublishedAt ?? post.UpdatedAt).ToString("R", CultureInfo.InvariantCulture)).Append("</pubDate>")
               .Append("<description>").Append(Xml(post.Summary)).Append("</description></item>");
        }
        xml.Append("</channel></rss>");
        return WebResult.Bytes(Encoding.UTF8.GetBytes(xml.ToString()), "application/rss+xml; charset=utf-8");
    }

    private async ValueTask<WebResult> SitemapAsync(WebRequestContext request, CancellationToken cancellationToken)
    {
        var posts = await _posts.PublishedAsync(1, 1000, null, cancellationToken).ConfigureAwait(false);
        var xml = new StringBuilder("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");
        xml.Append("<url><loc>").Append(Xml(Absolute("/"))).Append("</loc></url>");
        foreach (var post in posts.Items)
            xml.Append("<url><loc>").Append(Xml(Absolute(post.Url))).Append("</loc><lastmod>").Append(post.UpdatedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append("</lastmod></url>");
        xml.Append("</urlset>");
        return WebResult.Bytes(Encoding.UTF8.GetBytes(xml.ToString()), "application/xml; charset=utf-8");
    }

    private static object Card(BlogPost post) => new
    {
        post.Title,
        post.Url,
        post.Summary,
        post.Author,
        post.PublishedAt,
        Tags = post.Tags.Select(t => new { Name = t, Url = "/tags/" + Uri.EscapeDataString(t) }).ToList()
    };

    /// <summary>Absolute URLs come from the configured public base URL, never from the Host header.</summary>
    private string Absolute(string path) => _web.Current.PublicBaseUrl.TrimEnd('/') + path;

    private static string Xml(string value) => System.Security.SecurityElement.Escape(value);
}
