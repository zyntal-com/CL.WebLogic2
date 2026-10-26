using System.Net;
using System.Text.RegularExpressions;
using CL.WebLogic.IntegrationTests.Forms;
using CL.WebLogic.Results;
using CL.WebLogic.Routing.Models;
using CL.WebLogic.Routing.Services;
using CL.WebLogic.Testing.Services;
using CL.WebLogic.Theming.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CL.WebLogic.IntegrationTests.Theming;

/// <summary>Static files embedded in contributors and plugins (<c>b.MapAssets</c>).</summary>
public sealed partial class AssetContributionTests
{
    private static DelegateContributor AssetPlugin() => new(b =>
    {
        b.MapAssets(typeof(AssetContributionTests).Assembly, "plugin-assets/", "tests.assets");
        b.MapTemplate("plugin/styled", "<link rel=\"stylesheet\" href=\"{asset:tests.assets:css/plugin.css}\"><p>styled</p>");
        b.MapPage("/styled", (_, _) => ValueTask.FromResult<WebResult>(WebResult.Page("plugin/styled", new { }))).AllowAnonymous();
    });

    private static WebContributorDescriptor Descriptor => new() { Id = "plugin.assets", Name = "Assets" };

    [Fact]
    public async Task Plugins_serve_fingerprinted_assets_until_removed()
    {
        await using var host = await FormsTestSite.StartAsync();
        var registrar = host.Services.GetRequiredService<ContributorRegistrar>();
        string url;
        await using (await registrar.RegisterAsync(AssetPlugin(), Descriptor, CancellationToken.None))
        {
            url = Href().Match(await host.Client.GetStringAsync("/styled")).Groups[1].Value;
            Assert.Matches("^/_weblogic/plugins/tests.assets/css/plugin.css\\?v=[0-9a-f]{16}$", url);

            var css = await host.Client.GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, css.StatusCode);
            Assert.Equal("text/css", css.Content.Headers.ContentType?.MediaType);
            Assert.Contains("immutable", css.Headers.CacheControl?.ToString());
            Assert.Contains("rebeccapurple", await css.Content.ReadAsStringAsync());

            var revalidate = new HttpRequestMessage(HttpMethod.Get, "/_weblogic/plugins/tests.assets/css/plugin.css");
            revalidate.Headers.IfNoneMatch.Add(css.Headers.ETag!);
            Assert.Equal(HttpStatusCode.NotModified, (await host.Client.SendAsync(revalidate)).StatusCode);

            Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/_weblogic/plugins/tests.assets/js/app.js")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync("/_weblogic/plugins/tests.assets/script.php")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync("/_weblogic/plugins/tests.assets/css/%2e%2e/script.php")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync("/_weblogic/plugins/other/css/plugin.css")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync(url)).StatusCode);
        Assert.Empty(host.Services.GetRequiredService<WebContributedAssets>().Names);
    }

    [Fact]
    public async Task A_theme_file_overrides_the_plugin_asset()
    {
        await using var host = await FormsTestSite.StartAsync();
        host.WriteContentFile("theme/assets/plugins/tests.assets/css/plugin.css", ".from-site { color: teal; }");
        var registrar = host.Services.GetRequiredService<ContributorRegistrar>();
        await using var _ = await registrar.RegisterAsync(AssetPlugin(), Descriptor, CancellationToken.None);

        var url = Href().Match(await host.Client.GetStringAsync("/styled")).Groups[1].Value;
        Assert.StartsWith("/assets/plugins/tests.assets/css/plugin.css?v=", url);
        Assert.Contains("teal", await host.Client.GetStringAsync(url));
    }

    [Fact]
    public async Task Missing_assets_and_bad_names_fail_at_registration()
    {
        await using var host = await FormsTestSite.StartAsync();
        var registrar = host.Services.GetRequiredService<ContributorRegistrar>();
        var assembly = typeof(AssetContributionTests).Assembly;

        await Assert.ThrowsAsync<InvalidOperationException>(() => registrar.RegisterAsync(new DelegateContributor(b => b.MapAssets(assembly, "nothing/", "x")), Descriptor, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => registrar.RegisterAsync(new DelegateContributor(b => b.MapAssets(assembly, "plugin-assets/", "Bad Name")), Descriptor, CancellationToken.None));
    }

    [GeneratedRegex("href=\"([^\"]+)\"")]
    private static partial Regex Href();
}
