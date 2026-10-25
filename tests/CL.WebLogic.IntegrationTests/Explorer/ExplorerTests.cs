using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CL.WebLogic.IntegrationTests.Widgets;
using CL.WebLogic.Testing.Services;

namespace CL.WebLogic.IntegrationTests.Explorer;

public sealed class ExplorerTests
{
    [Fact]
    public async Task Development_lists_routes_protection_and_features()
    {
        await using var host = await WidgetTestSite.StartAsync();

        var json = await host.Client.GetFromJsonAsync<JsonElement>("/_weblogic/explorer.json");

        var routes = json.GetProperty("routes").EnumerateArray().ToList();
        Assert.Contains(routes, r => r.GetProperty("pattern").GetString() == "/admin" && r.GetProperty("groups")[0].GetString() == "admin");
        Assert.Contains("GET /public", json.GetProperty("anonymousRoutes").EnumerateArray().Select(r => r.GetString()));
        Assert.DoesNotContain("GET /secure", json.GetProperty("anonymousRoutes").EnumerateArray().Select(r => r.GetString()));
        var stages = json.GetProperty("stages").EnumerateArray().Select(s => s.GetProperty("name").GetString()).ToList();
        Assert.True(stages.IndexOf("authentication") < stages.IndexOf("authorization") && stages.IndexOf("authorization") < stages.IndexOf("antiforgery"));
        Assert.Contains(json.GetProperty("widgets").EnumerateArray(), w => w.GetProperty("name").GetString() == "counter" && w.GetProperty("actions")[0].GetString() == "increment");
        Assert.Contains(json.GetProperty("widgetAreas").EnumerateArray(), a => a.GetProperty("area").GetString() == "side");
        Assert.Contains(json.GetProperty("contributors").EnumerateArray(), c => c.GetProperty("id").GetString() == "weblogic.realtime");
        Assert.True(json.GetProperty("realtime").GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public async Task Page_is_static_and_nonced()
    {
        await using var host = await WidgetTestSite.StartAsync();

        var response = await host.Client.GetAsync("/_weblogic/explorer");
        var html = await response.Content.ReadAsStringAsync();
        var csp = response.Headers.GetValues("Content-Security-Policy").Single();
        var nonce = System.Text.RegularExpressions.Regex.Match(csp, "'nonce-([^']+)'").Groups[1].Value;

        Assert.Contains("<script nonce=\"" + nonce + "\">", html);
        Assert.Contains("<style nonce=\"" + nonce + "\">", html);
        Assert.Contains("data-source=\"/_weblogic/explorer.json\"", html);
        Assert.DoesNotContain("innerHTML", html);
    }

    [Fact]
    public async Task Production_is_off_by_default_and_needs_the_permission_when_on()
    {
        await using (var off = await WidgetTestSite.StartAsync(options: new WebLogicTestHostOptions { EnvironmentName = "Production" }))
        {
            Assert.DoesNotContain("\"routes\"", await off.Client.GetStringAsync("/_weblogic/explorer.json"));
        }

        await using var on = await WidgetTestSite.StartAsync(c => c.Explorer.InProduction = true, options: new WebLogicTestHostOptions { EnvironmentName = "Production" });
        Assert.Equal(HttpStatusCode.Unauthorized, (await on.Client.GetAsync("/_weblogic/explorer.json")).StatusCode);
        await on.Client.GetAsync("/signin?user=u1");
        Assert.Equal(HttpStatusCode.Forbidden, (await on.Client.GetAsync("/_weblogic/explorer.json")).StatusCode);

        var admin = on.CreateClient();
        await admin.GetAsync("/signin?user=u2&perms=weblogic.explorer");
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/_weblogic/explorer.json")).StatusCode);
    }
}
