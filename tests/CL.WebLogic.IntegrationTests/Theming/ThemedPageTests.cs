using System.Net;
using CL.WebLogic.Configuration.Models;
using CL.WebLogic.Results;
using CL.WebLogic.Routing.Services;
using CL.WebLogic.Runtime.Models;
using CL.WebLogic.Testing.Services;

namespace CL.WebLogic.IntegrationTests.Theming;

public sealed class ThemedPageTests
{
    private static async Task<WebLogicTestHost> StartAsync(TemplateEngineKind engine = TemplateEngineKind.Closure)
    {
        var host = await WebLogicTestHost.StartAsync(new WebLogicTestHostOptions
        {
            Config = new WebLogicConfig { SiteName = "Demo", Theme = new ThemeConfig { TemplateEngine = engine } },
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
