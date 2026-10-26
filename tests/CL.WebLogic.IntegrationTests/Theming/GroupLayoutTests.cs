using CL.WebLogic.IntegrationTests.Forms;
using CL.WebLogic.Results;
using CL.WebLogic.Routing.Models;
using CL.WebLogic.Routing.Services;
using CL.WebLogic.Testing.Services;
using CL.WebLogic.Theming.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CL.WebLogic.IntegrationTests.Theming;

/// <summary>A route group's default layout (<c>WithLayout</c>).</summary>
public sealed class GroupLayoutTests
{
    [Fact]
    public async Task Pages_without_a_layout_use_the_groups_layout()
    {
        await using var host = await FormsTestSite.StartAsync();
        var registrar = host.Services.GetRequiredService<ContributorRegistrar>();
        await using var _ = await registrar.RegisterAsync(new DelegateContributor(b =>
        {
            b.MapTemplate("admin/layout", "<main class=\"admin-shell\">{renderbody}</main>");
            b.MapTemplate("admin/dashboard", "<h1>Dashboard</h1>");
            b.MapTemplate("admin/own", "{layout:/admin/other}<h1>Own</h1>");
            b.MapTemplate("admin/other", "<div class=\"other-shell\">{renderbody}</div>");

            var admin = b.MapGroup("/backoffice").AllowAnonymous().WithLayout("admin/layout");
            admin.MapPage("/", (_, _) => ValueTask.FromResult<WebResult>(WebResult.Page("admin/dashboard", new { })));
            admin.MapPage("/own", (_, _) => ValueTask.FromResult<WebResult>(WebResult.Page("admin/own", new { })));
            admin.MapPage("/fragment", (_, _) => ValueTask.FromResult<WebResult>(WebResult.Fragment("admin/dashboard", new { })));
            b.MapPage("/plain", (_, _) => ValueTask.FromResult<WebResult>(WebResult.Page("admin/dashboard", new { }))).AllowAnonymous();
        }), new WebContributorDescriptor { Id = "tests.layout", Name = "Layout" }, CancellationToken.None);

        Assert.Contains("<main class=\"admin-shell\"><h1>Dashboard</h1></main>", await host.Client.GetStringAsync("/backoffice"));

        var own = await host.Client.GetStringAsync("/backoffice/own");
        Assert.Contains("<div class=\"other-shell\"><h1>Own</h1></div>", own);
        Assert.DoesNotContain("admin-shell", own);

        Assert.DoesNotContain("admin-shell", await host.Client.GetStringAsync("/backoffice/fragment"));
        Assert.DoesNotContain("admin-shell", await host.Client.GetStringAsync("/plain"));
    }

    [Theory]
    [InlineData("../secret")]
    [InlineData("c:/layout")]
    [InlineData(" ")]
    public void Layout_paths_are_validated(string path) =>
        Assert.ThrowsAny<ArgumentException>(() => new SettingsOnly().WithLayout(path));

    private sealed class SettingsOnly : IWebRouteSettingsBuilder
    {
        public WebRouteSettings Settings { get; } = new();
    }
}
