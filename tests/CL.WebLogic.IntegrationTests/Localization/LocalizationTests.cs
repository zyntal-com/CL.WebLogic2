using System.Globalization;
using System.Net;
using CL.WebLogic.Configuration.Models;
using CL.WebLogic.Hosting.Services;
using CL.WebLogic.Results;
using CL.WebLogic.Routing.Services;
using CL.WebLogic.Testing.Services;

namespace CL.WebLogic.IntegrationTests.Localization;

public sealed class LocalizationTests
{
    private sealed class Counter
    {
        private int _value;

        public int Next() => Interlocked.Increment(ref _value);
    }

    private static async Task<WebLogicTestHost> StartAsync(Counter counter, bool enabled = true, TemplateEngineKind engine = TemplateEngineKind.Closure)
    {
        var host = await WebLogicTestHost.StartAsync(new WebLogicTestHostOptions
        {
            Config = new WebLogicConfig
            {
                Theme = new ThemeConfig { TemplateEngine = engine },
                Localization = new LocalizationConfig { Enabled = enabled, DefaultCulture = "en", SupportedCultures = ["sv-SE", "da"] }
            },
            ConfigureWebLogic = web => web.AddContributor(new DelegateContributor(b =>
            {
                b.MapPage("/hello", (_, _) => ValueTask.FromResult<WebResult>(WebResult.Page("templates/hello"))).AllowAnonymous();
                b.MapPage("/cached", (_, _) => ValueTask.FromResult<WebResult>(WebResult.Text(CultureInfo.CurrentUICulture.Name + ":n=" + counter.Next())))
                    .AllowAnonymous().WithOutputCache("default");
                b.MapPage("/fragment", async (r, ct) =>
                    WebResult.Text(await r.Cache.GetOrAddAsync("menu", _ => ValueTask.FromResult(CultureInfo.CurrentUICulture.Name + ":n=" + counter.Next()), cancellationToken: ct)))
                    .AllowAnonymous();
                b.MapPage("/flow", async (r, ct) =>
                {
                    await Task.Delay(5, ct).ConfigureAwait(false);
                    await Task.Run(() => { }, ct).ConfigureAwait(false);
                    return WebResult.Text(CultureInfo.CurrentCulture.Name + "|" + CultureInfo.CurrentUICulture.Name + "|" + (r.Culture?.Source ?? "none"));
                }).AllowAnonymous();
            }), new Routing.Models.WebContributorDescriptor { Id = "tests.localization", Name = "Localization" })
        });

        host.WriteContentFile("theme/templates/hello.html", "<p title=\"{t:hello}\">{t:hello}</p><i>{model:culture|default:none}</i>");
        host.WriteContentFile("theme/locales/en.json", """{ "hello": "Hello <you> & \"them\"" }""");
        host.WriteContentFile("theme/locales/sv.json", """{ "hello": "Hej" }""");
        host.WriteContentFile("theme/locales/da.json", """{ "hello": "Hej med dig" }""");
        host.WriteContentFile("theme/assets/site.css", "body{}");
        return host;
    }

    private static Task<HttpResponseMessage> GetAsync(HttpClient client, string path, string? acceptLanguage = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (acceptLanguage is not null)
            request.Headers.TryAddWithoutValidation("Accept-Language", acceptLanguage);
        return client.SendAsync(request);
    }

    [Theory]
    [InlineData(TemplateEngineKind.Closure)]
    [InlineData(TemplateEngineKind.Interpreter)]
    public async Task Templates_render_the_request_culture_with_encoding(TemplateEngineKind engine)
    {
        await using var host = await StartAsync(new Counter(), engine: engine);

        var english = await GetAsync(host.CreateClient(), "/hello");
        Assert.Equal("<p title=\"Hello &lt;you&gt; &amp; &quot;them&quot;\">Hello &lt;you&gt; &amp; &quot;them&quot;</p><i>en</i>",
            await english.Content.ReadAsStringAsync());
        Assert.Equal("en", english.Content.Headers.ContentLanguage.Single());
        Assert.Contains("Accept-Language", english.Headers.Vary);

        var swedish = await GetAsync(host.CreateClient(), "/hello", "sv-SE,sv;q=0.9,en;q=0.5");
        Assert.Equal("<p title=\"Hej\">Hej</p><i>sv-SE</i>", await swedish.Content.ReadAsStringAsync()); // sv-SE → sv.json
        Assert.Equal("sv-SE", swedish.Content.Headers.ContentLanguage.Single());

        Assert.Contains("Hej med dig", await host.CreateClient().GetStringAsync("/hello?culture=da"));
        Assert.Contains("Hello", await host.CreateClient().GetStringAsync("/hello?culture=../../etc"));
    }

    [Fact]
    public async Task The_culture_flows_through_awaits_and_static_assets_get_no_language_headers()
    {
        await using var host = await StartAsync(new Counter());

        Assert.Equal("da|da|query", await host.CreateClient().GetStringAsync("/flow?culture=da"));
        Assert.Equal("sv-SE|sv-SE|header", await (await GetAsync(host.CreateClient(), "/flow", "sv")).Content.ReadAsStringAsync());

        var asset = await GetAsync(host.CreateClient(), "/assets/site.css", "da");
        Assert.Equal(HttpStatusCode.OK, asset.StatusCode);
        Assert.Empty(asset.Content.Headers.ContentLanguage);
        Assert.DoesNotContain("Accept-Language", asset.Headers.Vary);
    }

    [Fact]
    public async Task The_switch_endpoint_sets_a_lax_cookie_and_redirects_locally()
    {
        await using var host = await StartAsync(new Counter());
        var client = host.CreateClient();

        var response = await client.GetAsync("/_weblogic/culture?culture=da&returnUrl=%2Fhello%3Fx%3D1");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/hello?x=1", response.Headers.Location!.OriginalString);
        var cookie = response.Headers.GetValues("Set-Cookie").Single();
        Assert.StartsWith("wl_culture=da;", cookie, StringComparison.Ordinal);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("Hej med dig", await client.GetStringAsync("/hello"));
        Assert.Equal("da|da|cookie", await client.GetStringAsync("/flow"));

        var offsite = await client.GetAsync("/_weblogic/culture?culture=sv-SE&returnUrl=%2F%2Fevil.example");
        Assert.Equal("/", offsite.Headers.Location!.OriginalString);

        var unsupported = await client.GetAsync("/_weblogic/culture?culture=fr&returnUrl=%2F");
        Assert.Equal(HttpStatusCode.BadRequest, unsupported.StatusCode);
        Assert.False(unsupported.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Output_and_fragment_caches_vary_by_culture()
    {
        var counter = new Counter();
        await using var host = await StartAsync(counter);

        Assert.Equal("sv-SE:n=1", await (await GetAsync(host.CreateClient(), "/cached", "sv-SE")).Content.ReadAsStringAsync());
        Assert.Equal("sv-SE:n=1", await (await GetAsync(host.CreateClient(), "/cached", "sv")).Content.ReadAsStringAsync());
        var danish = await GetAsync(host.CreateClient(), "/cached", "da");
        Assert.Equal("da:n=2", await danish.Content.ReadAsStringAsync());
        Assert.Equal("da", danish.Content.Headers.ContentLanguage.Single());
        var cachedDanish = await GetAsync(host.CreateClient(), "/cached", "da");
        Assert.Equal("da:n=2", await cachedDanish.Content.ReadAsStringAsync());
        Assert.Equal("da", cachedDanish.Content.Headers.ContentLanguage.Single());

        Assert.Equal("sv-SE:n=3", await (await GetAsync(host.CreateClient(), "/fragment", "sv-SE")).Content.ReadAsStringAsync());
        Assert.Equal("da:n=4", await (await GetAsync(host.CreateClient(), "/fragment", "da")).Content.ReadAsStringAsync());
        Assert.Equal("sv-SE:n=3", await (await GetAsync(host.CreateClient(), "/fragment", "sv-SE")).Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Disabled_localization_changes_nothing()
    {
        var counter = new Counter();
        await using var host = await StartAsync(counter, enabled: false);

        var response = await GetAsync(host.CreateClient(), "/hello?culture=da", "sv-SE");
        Assert.Equal("<p title=\"hello\">hello</p><i>none</i>", await response.Content.ReadAsStringAsync());
        Assert.Empty(response.Content.Headers.ContentLanguage);
        Assert.DoesNotContain("Accept-Language", response.Headers.Vary);
        Assert.Contains("|none", await host.CreateClient().GetStringAsync("/flow?culture=da"));

        Assert.EndsWith(":n=1", await (await GetAsync(host.CreateClient(), "/cached", "sv-SE")).Content.ReadAsStringAsync());
        Assert.EndsWith(":n=1", await (await GetAsync(host.CreateClient(), "/cached", "da")).Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.NotFound, (await host.CreateClient().GetAsync("/_weblogic/culture?culture=da")).StatusCode);
    }
}
