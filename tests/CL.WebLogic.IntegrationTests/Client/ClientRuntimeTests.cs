using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using CL.WebLogic.Client.Services;
using CL.WebLogic.Results;
using CL.WebLogic.Routing.Models;
using CL.WebLogic.Routing.Services;
using CL.WebLogic.Testing.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CL.WebLogic.IntegrationTests.Client;

public sealed partial class ClientRuntimeTests
{
    [GeneratedRegex("<script type=\"module\" src=\"(?<src>[^\"]+)\" integrity=\"(?<sri>sha384-[^\"]+)\" crossorigin=\"anonymous\" nonce=\"(?<nonce>[^\"]+)\"></script>")]
    private static partial Regex ModuleScript();

    private static async Task<WebLogicTestHost> StartAsync(string environment = "Development", Action<CL.WebLogic.Configuration.Models.WebLogicConfig>? configure = null)
    {
        var config = new CL.WebLogic.Configuration.Models.WebLogicConfig();
        configure?.Invoke(config);
        var host = await WebLogicTestHost.StartAsync(new WebLogicTestHostOptions
        {
            EnvironmentName = environment,
            Config = config,
            ConfigureWebLogic = w => w.AddContributor(new DelegateContributor(b =>
                b.MapPage("/", (_, _) => ValueTask.FromResult<WebResult>(WebResult.Page("templates/home"))).AllowAnonymous()),
                new WebContributorDescriptor { Id = "tests.client", Name = "Client" })
        });
        host.WriteContentFile("theme/templates/home.html", "<!doctype html><html><head><title>t</title></head><body><main data-wl-shell>hi</main>{weblogic_scripts}</body></html>");
        return host;
    }

    [Fact]
    public async Task WebLogicScripts_EmitsConfigStylesheetAndNoncedModule()
    {
        await using var host = await StartAsync();
        var response = await host.Client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        var script = ModuleScript().Match(html);
        Assert.True(script.Success, html);
        Assert.Matches(@"^/_weblogic/client/weblogic\.esm\.[0-9a-f]{12}\.js$", script.Groups["src"].Value);
        Assert.Contains("'nonce-" + script.Groups["nonce"].Value + "'", response.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Matches("<link rel=\"stylesheet\" href=\"/_weblogic/client/weblogic\\.[0-9a-f]{12}\\.css\" integrity=\"sha384-", html);

        var configJson = Regex.Match(html, "<script type=\"application/json\" id=\"wl-config\">(?<json>.*?)</script>").Groups["json"].Value;
        var config = JsonDocument.Parse(configJson).RootElement;
        Assert.Equal("X-CSRF-Token", config.GetProperty("csrfHeader").GetString());
        Assert.True(config.GetProperty("debug").GetBoolean());
        Assert.False(config.TryGetProperty("csrfToken", out _)); // pages stay cacheable
    }

    [Fact]
    public async Task Bootstrap5AndClassicFormats_AreSelectable()
    {
        await using var host = await StartAsync(configure: c =>
        {
            c.Client.UiAdapter = "bootstrap5";
            c.Client.Format = "classic";
        });
        var html = await host.Client.GetStringAsync("/");

        Assert.Matches("<script defer src=\"/_weblogic/client/weblogic\\.[0-9a-f]{12}\\.js\"", html);
        Assert.Matches("<script defer src=\"/_weblogic/client/weblogic\\.bootstrap5\\.[0-9a-f]{12}\\.js\"", html);
        Assert.DoesNotContain("type=\"module\"", html);
    }

    [Fact]
    public async Task Disabled_EmitsNothing()
    {
        await using var host = await StartAsync(configure: c => c.Client.Enabled = false);
        Assert.DoesNotContain("wl-config", await host.Client.GetStringAsync("/"));
    }

    [Fact]
    public async Task FingerprintedFiles_AreImmutable_Precompressed_AndMatchTheirIntegrity()
    {
        await using var host = await StartAsync();
        var asset = host.Services.GetRequiredService<ClientAssetCatalog>().Get("weblogic.esm.js")!;

        var request = new HttpRequestMessage(HttpMethod.Get, ClientAssetCatalog.PathPrefix + asset.HashedName);
        request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("br"));
        var brotli = await host.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, brotli.StatusCode);
        Assert.Equal("br", brotli.Content.Headers.ContentEncoding.Single());
        Assert.Contains("immutable", brotli.Headers.CacheControl!.ToString());
        Assert.Equal("text/javascript", brotli.Content.Headers.ContentType!.MediaType);

        var plain = await host.Client.GetByteArrayAsync(ClientAssetCatalog.PathPrefix + asset.HashedName);
        Assert.Equal(asset.Integrity, "sha384-" + Convert.ToBase64String(System.Security.Cryptography.SHA384.HashData(plain)));

        var conditional = new HttpRequestMessage(HttpMethod.Get, ClientAssetCatalog.PathPrefix + asset.HashedName);
        conditional.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(asset.ETag));
        Assert.Equal(HttpStatusCode.NotModified, (await host.Client.SendAsync(conditional)).StatusCode);
    }

    [Fact]
    public async Task UnhashedNames_Revalidate_UnknownFilesAre404()
    {
        await using var host = await StartAsync();

        var unhashed = await host.Client.GetAsync(ClientAssetCatalog.PathPrefix + "weblogic.js");
        Assert.Equal("no-cache", unhashed.Headers.CacheControl!.ToString());
        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync(ClientAssetCatalog.PathPrefix + "nope.js")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync(ClientAssetCatalog.PathPrefix + "client-manifest.json")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync(ClientAssetCatalog.PathPrefix + "..%2Fweblogic.js")).StatusCode);
    }

    [Fact]
    public async Task TestPage_OnlyInDevelopment()
    {
        await using (var production = await StartAsync("Production"))
            Assert.Equal(HttpStatusCode.NotFound, (await production.Client.GetAsync("/_weblogic/tests")).StatusCode);

        await using var development = await StartAsync();
        var html = await development.Client.GetStringAsync("/_weblogic/tests");
        Assert.Contains("weblogic.tests.esm.", html);
        Assert.Contains("id=\"wl-test-results\"", html);
    }

    [Fact]
    public async Task BrowserSuite_PassesInHeadlessChrome()
    {
        var browser = FindBrowser();
        if (browser is null)
            return; // no Chrome/Edge on this machine; CI provides one

        await using var host = await StartAsync();
        var profile = Path.Combine(Path.GetTempPath(), "wl-chrome-" + Guid.NewGuid().ToString("N"));
        try
        {
            var start = new ProcessStartInfo(browser)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            foreach (var argument in (string[])["--headless=new", "--disable-gpu", "--no-sandbox", "--no-first-run", "--user-data-dir=" + profile,
                         "--virtual-time-budget=15000", "--dump-dom", new Uri(host.BaseAddress, "/_weblogic/tests").ToString()])
                start.ArgumentList.Add(argument);

            using var process = Process.Start(start)!;
            var dom = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(60));

            var summary = Regex.Match(dom, "<p id=\"wl-test-summary\"[^>]*>(?<text>[^<]+)</p>").Groups["text"].Value;
            Assert.True(summary.StartsWith("PASS", StringComparison.Ordinal), "Browser suite: " + (summary.Length > 0 ? summary : "no summary") + "\n"
                + string.Join("\n", Regex.Matches(dom, "<li class=\"fail\">(?<t>[^<]+)</li>").Select(m => WebUtility.HtmlDecode(m.Groups["t"].Value))));
        }
        finally
        {
            try
            {
                Directory.Delete(profile, recursive: true);
            }
            catch (IOException)
            {
                // the browser may still hold files for a moment
            }
        }
    }

    /// <summary>Chrome or Edge on this machine (CHROME_PATH first), or null.</summary>
    internal static string? FindBrowser()
    {
        string[] candidates =
        [
            Environment.GetEnvironmentVariable("CHROME_PATH") ?? "",
            @"C:\Program Files\Google\Chrome\Application\chrome.exe",
            @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
            "/usr/bin/google-chrome", "/usr/bin/chromium", "/usr/bin/chromium-browser",
            "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"
        ];
        return candidates.FirstOrDefault(c => c.Length > 0 && File.Exists(c));
    }
}
