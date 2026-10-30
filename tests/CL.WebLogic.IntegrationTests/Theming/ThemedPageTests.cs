using System.Net;
using CL.WebLogic.Configuration.Models;
using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Results;
using CL.WebLogic.Routing.Services;
using CL.WebLogic.Runtime.Models;
using CL.WebLogic.Testing.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CL.WebLogic.IntegrationTests.Theming;

public sealed class ThemedPageTests
{
    private static async Task<WebLogicTestHost> StartAsync(TemplateEngineKind engine = TemplateEngineKind.Closure, string assetsBaseUrl = "")
    {
        var host = await WebLogicTestHost.StartAsync(new WebLogicTestHostOptions
        {
            Config = new WebLogicConfig { SiteName = "Demo", Theme = new ThemeConfig { TemplateEngine = engine, AssetsBaseUrl = assetsBaseUrl } },
            ConfigureWebLogic = web => web.AddContributor(new DelegateContributor(b =>
            {
                b.MapPage("/", (request, _) => ValueTask.FromResult<WebResult>(WebResult.Page(
                    "templates/home",
                    new Dictionary<string, object?> { ["name"] = request.GetQuery("name") },
                    new WebPageMeta { Title = "Home", Description = "Welcome" }))).AllowAnonymous();
                b.MapFragment("/fragment", (_, _) => ValueTask.FromResult<WebResult>(WebResult.Fragment("templates/home"))).AllowAnonymous();
                b.MapPage("/broken", (_, _) => ValueTask.FromResult<WebResult>(WebResult.Page("templates/missing"))).AllowAnonymous();
            }))
        });

        host.WriteContentFile("theme/layouts/main.html",
            "<!doctype html><html><head>{renderhead}<link rel=\"stylesheet\" href=\"{asset:site.css}\"><script src=\"{asset:app.js}\"></script></head><body>{renderbody}</body></html>");
        host.WriteContentFile("theme/templates/home.html", "{layout:layouts/main}<h1>{model:site_name}: hello {model:name|default:guest}</h1>");
        host.WriteContentFile("theme/errors/404.html", "{layout:layouts/main}<h1>Lost ({model:status_code})</h1>");
        host.WriteContentFile("theme/assets/site.css", "body{}");
        host.WriteContentFile("theme/assets/app.js", "1");
        return host;
    }

    [Theory]
    [InlineData(TemplateEngineKind.Closure)]
    [InlineData(TemplateEngineKind.Interpreter)]
    public async Task Page_RendersLayoutHeadAssetsAndEncodedModel(TemplateEngineKind engine)
    {
        await using var host = await StartAsync(engine);

        var html = await host.Client.GetStringAsync("/?name=%3Cscript%3E");

        Assert.Contains("<title>Home</title>", html);
        Assert.Contains("<meta data-wl-head name=\"description\" content=\"Welcome\">", html);
        Assert.Matches("href=\"/assets/site.css\\?v=[0-9a-f]+\"", html);
        Assert.Matches("<script nonce=\"[A-Za-z0-9_-]+\" src=\"/assets/app.js\\?v=[0-9a-f]+\">", html);
        Assert.Contains("<h1>Demo: hello &lt;script&gt;</h1>", html);
    }

    [Fact]
    public async Task AssetsBaseUrl_PointsAssetsAtTheCopy_AndAllowsItsOrigin()
    {
        await using var host = await StartAsync(assetsBaseUrl: "https://cdn.example.test/theme/assets/");

        var response = await host.Client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Matches("href=\"https://cdn.example.test/theme/assets/site.css\\?v=[0-9a-f]+\"", html);
        Assert.Matches("src=\"https://cdn.example.test/theme/assets/app.js\\?v=[0-9a-f]+\"", html);
        var csp = string.Join(' ', response.Headers.GetValues("Content-Security-Policy"));
        foreach (var directive in new[] { "script-src", "style-src", "font-src", "img-src" })
            Assert.Matches(directive + " [^;]*https://cdn.example.test(?:[ ;]|$)", csp);
        Assert.DoesNotContain("connect-src 'self' https://cdn.example.test", csp);
        // The site still serves the folder (hard-coded links, fallback).
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/assets/site.css")).StatusCode);
    }

    [Fact]
    public async Task AssetsBaseUrl_FollowsLiveConfigChanges()
    {
        await using var host = await StartAsync();
        Assert.Matches("href=\"/assets/site.css\\?v=", await host.Client.GetStringAsync("/"));

        var holder = host.Services.GetRequiredService<WebLogicConfigHolder>();
        holder.Swap(new WebLogicConfig { SiteName = "Demo", Theme = new ThemeConfig { AssetsBaseUrl = "https://cdn.example.test/a" } });

        var response = await host.Client.GetAsync("/");
        Assert.Contains("href=\"https://cdn.example.test/a/site.css?v=", await response.Content.ReadAsStringAsync());
        Assert.Contains("https://cdn.example.test", string.Join(' ', response.Headers.GetValues("Content-Security-Policy")));
    }

    [Fact]
    public async Task Fragment_SkipsLayout_AndIsMarked()
    {
        await using var host = await StartAsync();

        var response = await host.Client.GetAsync("/fragment");

        Assert.Equal("<h1>Demo: hello guest</h1>", await response.Content.ReadAsStringAsync());
        Assert.Equal("1", response.Headers.GetValues("X-WebLogic-Fragment").Single());
    }

    [Fact]
    public async Task NotFound_UsesThemedErrorPage()
    {
        await using var host = await StartAsync();

        var response = await host.Client.GetAsync("/nope");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("<h1>Lost (404)</h1>", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task MissingTemplate_Returns500_WithoutLeakingSourcePaths()
    {
        await using var host = await StartAsync();

        var response = await host.Client.GetAsync("/broken");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain(host.ContentRootPath, await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/assets/../templates/home.html")]
    [InlineData("/templates/home.html")]
    [InlineData("/layouts/main.html")]
    public async Task TemplateSources_AreNeverServed(string path)
    {
        await using var host = await StartAsync();

        var response = await host.Client.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("{layout:", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task TemplateEdits_ArePickedUp()
    {
        await using var host = await StartAsync();
        Assert.Contains("hello guest", await host.Client.GetStringAsync("/"));

        host.WriteContentFile("theme/templates/home.html", "{layout:layouts/main}<h1>changed</h1>");
        File.SetLastWriteTimeUtc(Path.Combine(host.ContentRootPath, "theme", "templates", "home.html"), DateTime.UtcNow.AddMinutes(1));
        await Task.Delay(TimeSpan.FromSeconds(2.2));

        Assert.Contains("<h1>changed</h1>", await host.Client.GetStringAsync("/"));
    }
}
