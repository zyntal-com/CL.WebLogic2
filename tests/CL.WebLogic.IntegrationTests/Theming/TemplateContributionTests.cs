using CL.WebLogic.IntegrationTests.Forms;
using CL.WebLogic.Results;
using CL.WebLogic.Routing.Models;
using CL.WebLogic.Routing.Services;
using CL.WebLogic.Templates.Rendering;
using CL.WebLogic.Testing.Services;
using CL.WebLogic.Theming.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CL.WebLogic.IntegrationTests.Theming;

public sealed class TemplateContributionTests
{
    private static DelegateContributor HelloPlugin() => new(b =>
    {
        b.MapTemplates(typeof(TemplateContributionTests).Assembly, "plugin-theme/");
        b.MapPage("/hello", (_, _) => ValueTask.FromResult<WebResult>(WebResult.Page("plugin/hello", new { Name = "<world>" }))).AllowAnonymous();
    });

    private static WebContributorDescriptor Descriptor(string id = "plugin.hello") => new() { Id = id, Name = id };

    [Fact]
    public async Task Plugins_ship_templates_until_removed()
    {
        await using var host = await FormsTestSite.StartAsync();
        var registrar = host.Services.GetRequiredService<ContributorRegistrar>();
        var loader = host.Services.GetRequiredService<ITemplateLoader>();

        await using (await registrar.RegisterAsync(HelloPlugin(), Descriptor(), CancellationToken.None))
        {
            var html = await host.Client.GetStringAsync("/hello");
            Assert.Contains("<p class=\"from-plugin\">Hello &lt;world&gt;</p>", html);
            Assert.Contains("<small>plugin footer</small>", html);
            Assert.Contains("<!doctype html>", html);
            Assert.True(loader.Exists("plugin/hello.html"));
        }

        Assert.False(loader.Exists("plugin/hello.html"));
        Assert.Empty(host.Services.GetRequiredService<WebContributedTemplates>().Paths);
    }

    [Fact]
    public async Task The_theme_overrides_a_contributed_template()
    {
        await using var host = await FormsTestSite.StartAsync();
        host.WriteContentFile("theme/plugin/footer.html", "<small>site footer</small>");
        var registrar = host.Services.GetRequiredService<ContributorRegistrar>();

        await using var _ = await registrar.RegisterAsync(HelloPlugin(), Descriptor(), CancellationToken.None);
        var html = await host.Client.GetStringAsync("/hello");
        Assert.Contains("<small>site footer</small>", html);
        Assert.DoesNotContain("plugin footer", html);
    }

    [Fact]
    public async Task Two_contributors_cannot_ship_the_same_template()
    {
        await using var host = await FormsTestSite.StartAsync();
        var registrar = host.Services.GetRequiredService<ContributorRegistrar>();
        await using var first = await registrar.RegisterAsync(new DelegateContributor(b => b.MapTemplate("shared/box", "first")), Descriptor("a"), CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            registrar.RegisterAsync(new DelegateContributor(b => b.MapTemplate("shared/box", "second")), Descriptor("b"), CancellationToken.None));
        Assert.True(host.Services.GetRequiredService<WebContributedTemplates>().TryGet("shared/box.html", out var source, out _));
        Assert.Equal("first", source);
    }

    [Fact]
    public async Task Missing_embedded_templates_fail_with_a_hint()
    {
        await using var host = await FormsTestSite.StartAsync();
        var registrar = host.Services.GetRequiredService<ContributorRegistrar>();
        var plugin = new DelegateContributor(b => b.MapTemplates(typeof(TemplateContributionTests).Assembly, "nothing-here/"));

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => registrar.RegisterAsync(plugin, Descriptor(), CancellationToken.None));
        Assert.Contains("LogicalName", failure.Message);
    }
}
