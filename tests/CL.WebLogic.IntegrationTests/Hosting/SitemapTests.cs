using System.Net;
using System.Xml.Linq;
using CL.WebLogic.Configuration.Models;
using CL.WebLogic.Hosting.Services;
using CL.WebLogic.Results;
using CL.WebLogic.Routing.Services;
using CL.WebLogic.Seo.Models;
using CL.WebLogic.Seo.Services;
using CL.WebLogic.Testing.Services;

namespace CL.WebLogic.IntegrationTests.Hosting;

public sealed class SitemapTests
{
    private static readonly XNamespace Ns = "http://www.sitemaps.org/schemas/sitemap/0.9";

    [Fact]
    public async Task NotRegistered_ServesNeitherFile()
    {
        await using var host = await StartAsync(sitemap: false);

        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync("/sitemap.xml")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync("/robots.txt")).StatusCode);
    }

    [Fact]
    public async Task Sitemap_ListsAnonymousStaticPages_AndProviderEntries()
    {
        await using var host = await StartAsync();

        var response = await host.Client.GetAsync("/sitemap.xml");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/xml", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("public, max-age=3600", response.Headers.CacheControl?.ToString());

        var doc = XDocument.Parse(await response.Content.ReadAsStringAsync());
        var locs = doc.Root!.Elements(Ns + "url").Select(u => (string)u.Element(Ns + "loc")!).ToList();
        Assert.Equal(
        [
            "https://example.test/",
            "https://example.test/about",
            "https://example.test/about/team",
            "https://example.test/posts/a&b",
            "https://example.test/posts/%3Cscript%3E%20x"
        ], locs);

        var post = doc.Root.Elements(Ns + "url").Single(u => ((string)u.Element(Ns + "loc")!).EndsWith("a&b", StringComparison.Ordinal));
        Assert.Equal("2026-01-02T03:04:05Z", (string)post.Element(Ns + "lastmod")!);
        Assert.Equal("weekly", (string)post.Element(Ns + "changefreq")!);
        Assert.Equal("1.0", (string)post.Element(Ns + "priority")!);
    }

    [Fact]
    public async Task Sitemap_UsesPublicBaseUrl_NeverTheHostHeader()
    {
        await using var host = await StartAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/sitemap.xml");
        request.Headers.Host = "evil.example";

        var response = await host.Client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("https://example.test/about", body, StringComparison.Ordinal);
        Assert.DoesNotContain("evil.example", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Robots_HasAppTextAndSitemapLine()
    {
        await using var host = await StartAsync();

        var response = await host.Client.GetAsync("/robots.txt");

        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("User-agent: *\nDisallow: /admin/\n\nSitemap: https://example.test/sitemap.xml\n", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task WithoutPublicBaseUrl_SitemapIs404_AndRobotsHasNoSitemapLine()
    {
        await using var host = await StartAsync(baseUrl: "");

        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync("/sitemap.xml")).StatusCode);
        Assert.Equal("User-agent: *\nDisallow: /admin/\n", await host.Client.GetStringAsync("/robots.txt"));
    }

    private static Task<WebLogicTestHost> StartAsync(bool sitemap = true, string baseUrl = "https://example.test/") =>
        WebLogicTestHost.StartAsync(new WebLogicTestHostOptions
        {
            Config = new WebLogicConfig { PublicBaseUrl = baseUrl },
            ConfigureWebLogic = web =>
            {
                web.AddContributor(new DelegateContributor(b =>
                {
                    static ValueTask<WebResult> Ok(Runtime.Models.WebRequestContext r, CancellationToken c) => ValueTask.FromResult<WebResult>(WebResult.Html("ok"));
                    b.MapPage("/", Ok).AllowAnonymous();
                    b.MapPage("/about", Ok).AllowAnonymous();
                    b.MapGroup("/about").AllowAnonymous().MapPage("team", Ok);
                    b.MapPage("/posts/{slug}", Ok).AllowAnonymous();
                    b.MapPage("/account", Ok);
                    b.MapPage("/members", Ok).AllowAnonymous().RequireGroups("members");
                    b.MapPage("/login", Ok).AllowAnonymous().WithTags(WebSitemapOptions.ExcludeTag);
                    b.MapPage("/drafts/one", Ok).AllowAnonymous();
                    b.MapPage("/contact", Ok, "POST").AllowAnonymous();
                    b.MapApi("/api/status", Ok).AllowAnonymous();
                }));
                if (!sitemap)
                    return;
                web.AddSitemap(o =>
                {
                    o.ExcludePaths.Add("/drafts/**");
                    o.RobotsText = "User-agent: *\r\nDisallow: /admin/";
                }).UseSitemapProvider<PostsProvider>();
            }
        });

    private sealed class PostsProvider : IWebSitemapProvider
    {
        public ValueTask<IReadOnlyList<WebSitemapEntry>> GetEntriesAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<WebSitemapEntry>>(
            [
                new WebSitemapEntry
                {
                    Path = "/posts/a&b",
                    LastModified = new DateTimeOffset(2026, 1, 2, 4, 4, 5, TimeSpan.FromHours(1)),
                    ChangeFrequency = WebSitemapChangeFrequency.Weekly,
                    Priority = 7
                },
                new WebSitemapEntry { Path = "/posts/<script> x" },
                new WebSitemapEntry { Path = "https://evil.example/x" },
                new WebSitemapEntry { Path = "//evil.example/x" },
                new WebSitemapEntry { Path = "/drafts/two" },
                new WebSitemapEntry { Path = "/about" }
            ]);
    }
}
