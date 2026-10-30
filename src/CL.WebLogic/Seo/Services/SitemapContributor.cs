using System.Globalization;
using System.Text;
using CL.WebLogic.Results;
using CL.WebLogic.Routing;
using CL.WebLogic.Routing.Models;
using CL.WebLogic.Routing.Services;
using CL.WebLogic.Runtime.Models;
using CL.WebLogic.Seo.Models;

namespace CL.WebLogic.Seo.Services;

/// <summary>
/// Serves <c>/sitemap.xml</c> and <c>/robots.txt</c> (anonymous GET). Registered only by
/// <c>web.AddSitemap(...)</c>; apps that do not call it get neither route.
/// </summary>
public sealed class SitemapContributor : IWebContributor
{
    /// <summary>Contributor id.</summary>
    public const string ContributorId = "weblogic.sitemap";

    private readonly WebSitemapService _sitemap;
    private readonly WebSitemapOptions _options;

    /// <summary>Creates the contributor.</summary>
    public SitemapContributor(WebSitemapService sitemap, WebSitemapOptions options)
    {
        _sitemap = sitemap ?? throw new ArgumentNullException(nameof(sitemap));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>Descriptor.</summary>
    public static WebContributorDescriptor Descriptor { get; } = new()
    {
        Id = ContributorId,
        Name = "WebLogic sitemap",
        Kind = "System",
        Description = "Serves /sitemap.xml and /robots.txt."
    };

    /// <inheritdoc/>
    public Task ContributeAsync(WebContributionBuilder builder, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(builder);
        _options.EnsureValid();

        if (_options.ServeSitemap)
        {
            builder.MapApi("/sitemap.xml", SitemapAsync).AllowAnonymous()
                .WithName("weblogic.sitemap").WithDescription("XML sitemap");
        }

        if (_options.ServeRobots)
        {
            builder.MapApi("/robots.txt", (_, _) => ValueTask.FromResult(Cached(WebResult.Text(_sitemap.BuildRobots()))))
                .AllowAnonymous().WithName("weblogic.robots").WithDescription("robots.txt");
        }

        return Task.CompletedTask;
    }

    private async ValueTask<WebResult> SitemapAsync(WebRequestContext request, CancellationToken cancellationToken)
    {
        var xml = await _sitemap.BuildSitemapAsync(cancellationToken).ConfigureAwait(false);
        return xml is null
            ? WebResult.Status(404)
            : Cached(WebResult.Bytes(Encoding.UTF8.GetBytes(xml), "application/xml; charset=utf-8"));
    }

    private WebResult Cached(WebResult result) =>
        result.WithHeader("Cache-Control", _options.CacheSeconds == 0
            ? "no-cache"
            : "public, max-age=" + _options.CacheSeconds.ToString(CultureInfo.InvariantCulture));
}
