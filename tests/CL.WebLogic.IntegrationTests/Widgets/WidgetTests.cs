using System.Net;
using System.Net.Http.Json;
using System.Text;
using CL.WebLogic.IntegrationTests.Security;
using CL.WebLogic.Routing.Models;
using CL.WebLogic.Routing.Services;
using CL.WebLogic.Testing.Services;
using CL.WebLogic.Widgets.Models;
using CL.WebLogic.Widgets.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CL.WebLogic.IntegrationTests.Widgets;

public sealed class WidgetTests
{
    private static async Task<HttpResponseMessage> PostActionAsync(HttpClient client, string url, string token, string json = "{}")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        if (token.Length > 0)
            request.Headers.Add("X-CSRF-Token", token);
        request.Headers.Add("X-Requested-With", "WebLogic");
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task Templates_render_widgets_areas_and_isolate_failures()
    {
        await using var host = await WidgetTestSite.StartAsync();

        var html = await host.Client.GetStringAsync("/page");

        Assert.Contains("<div class=\"wl-widget\" data-wl-widget=\"greeting\"><p class=\"greeting\">Hello &lt;b&gt;Ada&lt;/b&gt;</p></div>", html);
        Assert.DoesNotContain("top secret", html); // signed-in users only
        Assert.Contains("<!-- widget failed: broken -->", html);
        Assert.Contains("data-wl-widget-area=\"side\"", html);
        Assert.True(html.IndexOf("data-wl-widget-instance=\"side-counter\"", StringComparison.Ordinal) < html.IndexOf("Hello sidebar", StringComparison.Ordinal)); // area order
        Assert.True(host.Context.Log.Contains(CodeLogic.Core.Logging.LogLevel.Error, "Widget 'broken' failed"));
    }

    [Fact]
    public async Task Access_and_area_paths_are_applied()
    {
        await using var host = await WidgetTestSite.StartAsync();
        await host.Client.GetAsync("/signin?user=u1");

        Assert.Contains("top secret", await host.Client.GetStringAsync("/page"));
        Assert.DoesNotContain("side-counter", await host.Client.GetStringAsync("/admin/page"));
    }

    [Fact]
    public async Task Refresh_endpoint_renders_a_fragment_with_access_checks()
    {
        await using var host = await WidgetTestSite.StartAsync();

        var response = await host.Client.GetAsync("/_weblogic/widgets/greeting?name=%3Ci%3EBob");
        Assert.Equal("1", response.Headers.GetValues("X-WebLogic-Fragment").Single());
        Assert.Equal("<p class=\"greeting\">Hello &lt;i&gt;Bob</p>", await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/_weblogic/widgets/secret")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync("/_weblogic/widgets/nope")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.GetAsync("/_weblogic/widgets/counter?instance=bad%20id")).StatusCode);
    }

    [Fact]
    public async Task Actions_save_settings_and_refresh_behind_csrf()
    {
        await using var host = await WidgetTestSite.StartAsync();
        await host.Client.GetAsync("/signin?user=u1");
        var token = await SecurityTestSite.GetCsrfTokenAsync(host.Client);
        const string url = "/_weblogic/widgets/counter/actions/increment?instance=c1";

        Assert.Equal("<span class=\"count\">1</span>", await (await PostActionAsync(host.Client, url, token)).Content.ReadAsStringAsync());
        Assert.Equal("<span class=\"count\">6</span>", await (await PostActionAsync(host.Client, url, token, "{\"by\":5}")).Content.ReadAsStringAsync());
        Assert.Contains(">6<", await host.Client.GetStringAsync("/_weblogic/widgets/counter?instance=c1"));

        Assert.Equal(HttpStatusCode.Forbidden, (await PostActionAsync(host.Client, url, "")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await PostActionAsync(host.Client, "/_weblogic/widgets/counter/actions/nope?instance=c1", token)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostActionAsync(host.Client, url, token, "{not json")).StatusCode);
    }

    [Fact]
    public async Task Widget_settings_belong_to_their_owner_whatever_instance_id_is_sent()
    {
        // Regression (security review H1): settings were keyed by the client-supplied instance id only.
        await using var host = await WidgetTestSite.StartAsync();
        const string url = "/_weblogic/widgets/counter/actions/increment?instance=shared-id";
        await host.Client.GetAsync("/signin?user=victim");
        await PostActionAsync(host.Client, url, await SecurityTestSite.GetCsrfTokenAsync(host.Client), "{\"by\":7}");

        var attacker = host.CreateClient();
        await attacker.GetAsync("/signin?user=attacker");
        Assert.Contains(">0<", await attacker.GetStringAsync("/_weblogic/widgets/counter?instance=shared-id"));
        await PostActionAsync(attacker, url, await SecurityTestSite.GetCsrfTokenAsync(attacker), "{\"by\":100}");

        Assert.Contains(">7<", await host.Client.GetStringAsync("/_weblogic/widgets/counter?instance=shared-id"));

        var anonymous = host.CreateClient();
        Assert.Contains(">0<", await (await PostActionAsync(anonymous, url, await SecurityTestSite.GetCsrfTokenAsync(anonymous))).Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Placement_access_also_applies_at_the_widget_endpoint()
    {
        // Regression (security review): the endpoint only checked the widget's own access rule.
        await using var host = await WidgetTestSite.StartAsync();
        var registrar = host.Services.GetRequiredService<ContributorRegistrar>();
        await using var _ = await registrar.RegisterAsync(new DelegateContributor(b =>
        {
            b.MapWidget("staff-stats", (_, _) => ValueTask.FromResult(WebWidgetResult.FromHtml("<p>stats</p>")), o => o.Dashboard = false);
            b.PlaceWidget("side", "staff-stats", o => o.Access = new CL.WebLogic.Security.Authorization.WebAccessRule { Groups = ["staff"] });
        }), new WebContributorDescriptor { Id = "tests.staff", Name = "Staff" }, CancellationToken.None);

        await host.Client.GetAsync("/signin?user=u1");
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.GetAsync("/_weblogic/widgets/staff-stats")).StatusCode);

        var staff = host.CreateClient();
        await staff.GetAsync("/signin?user=u2&groups=staff");
        Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync("/_weblogic/widgets/staff-stats")).StatusCode);
    }

    [Fact]
    public async Task Regression_Placement_access_also_holds_for_dashboard_widgets()
    {
        // Security review: a widget left at Dashboard = true skipped its placements' rules at the
        // endpoint and could be added to any dashboard.
        await using var host = await WidgetTestSite.StartAsync();
        var registrar = host.Services.GetRequiredService<ContributorRegistrar>();
        await using var _ = await registrar.RegisterAsync(new DelegateContributor(b =>
        {
            b.MapWidget("server-stats", (_, _) => ValueTask.FromResult(WebWidgetResult.FromHtml("<p>stats</p>")));
            b.PlaceWidget("side", "server-stats", o => o.Access = new CL.WebLogic.Security.Authorization.WebAccessRule { Groups = ["admin"] });
        }), new WebContributorDescriptor { Id = "tests.server", Name = "Server" }, CancellationToken.None);

        await host.Client.GetAsync("/signin?user=u1");
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.GetAsync("/_weblogic/widgets/server-stats")).StatusCode);
        var put = new HttpRequestMessage(HttpMethod.Put, "/_weblogic/dashboard/home")
        {
            Content = JsonContent.Create(new { version = 0, items = new[] { new { widgetName = "server-stats", zone = "main" } } })
        };
        put.Headers.Add("X-CSRF-Token", await SecurityTestSite.GetCsrfTokenAsync(host.Client));
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.SendAsync(put)).StatusCode);

        var admin = host.CreateClient();
        await admin.GetAsync("/signin?user=u2&groups=admin");
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/_weblogic/widgets/server-stats")).StatusCode);
    }

    [Fact]
    public async Task Failed_contributions_leave_nothing_behind_and_removal_cleans_up()
    {
        await using var host = await WidgetTestSite.StartAsync();
        var registrar = host.Services.GetRequiredService<ContributorRegistrar>();
        var registry = host.Services.GetRequiredService<WebWidgetRegistry>();

        var duplicate = new DelegateContributor(b =>
        {
            b.MapApi("/plugin/dup", (_, _) => ValueTask.FromResult<Results.WebResult>(Results.WebResult.Text("dup"))).AllowAnonymous();
            b.MapWidget("plugin-ok", (_, _) => ValueTask.FromResult(WebWidgetResult.Empty));
            b.MapWidget("greeting", (_, _) => ValueTask.FromResult(WebWidgetResult.Empty));
        });
        await Assert.ThrowsAsync<InvalidOperationException>(() => registrar.RegisterAsync(duplicate, new WebContributorDescriptor { Id = "plugin.bad", Name = "Bad" }, CancellationToken.None));
        Assert.NotEqual("dup", await host.Client.GetStringAsync("/plugin/dup"));
        Assert.Null(registry.Find("plugin-ok"));
        Assert.False(registrar.IsRegistered("plugin.bad"));

        var plugin = new DelegateContributor(b =>
        {
            b.MapWidget("plugin-w", (_, _) => ValueTask.FromResult(WebWidgetResult.FromHtml("plugin")), o => o.AllowAnonymous());
            b.PlaceWidget("side", "plugin-w");
        });
        await using (var handle = await registrar.RegisterAsync(plugin, new WebContributorDescriptor { Id = "plugin.good", Name = "Good" }, CancellationToken.None))
        {
            Assert.Contains(">plugin<", await host.Client.GetStringAsync("/page"));
        }
        Assert.Null(registry.Find("plugin-w"));
        Assert.DoesNotContain(">plugin<", await host.Client.GetStringAsync("/page"));
    }
}
