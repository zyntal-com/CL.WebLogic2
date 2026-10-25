using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CL.WebLogic.IntegrationTests.Security;
using CL.WebLogic.IntegrationTests.Widgets;

namespace CL.WebLogic.IntegrationTests.Dashboard;

public sealed class DashboardTests
{
    private static async Task<HttpResponseMessage> PutAsync(HttpClient client, string token, object body, string key = "home")
    {
        var request = new HttpRequestMessage(HttpMethod.Put, "/_weblogic/dashboard/" + key) { Content = JsonContent.Create(body) };
        if (token.Length > 0)
            request.Headers.Add("X-CSRF-Token", token);
        return await client.SendAsync(request);
    }

    private sealed class GreetingDefaults : CL.WebLogic.Dashboard.Services.IWebDashboardDefaults
    {
        public ValueTask<IReadOnlyList<CL.WebLogic.Dashboard.Models.WebDashboardItem>> GetAsync(string dashboardKey, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<CL.WebLogic.Dashboard.Models.WebDashboardItem>>([new() { WidgetName = "greeting", Zone = "main" }]);
    }

    [Fact]
    public async Task Default_items_have_stable_ids_until_and_after_the_first_save()
    {
        // Regression: defaults got a new random id per render, so settings saved before the first
        // layout save (e.g. a note widget) never matched an item.
        await using var host = await WidgetTestSite.StartAsync(web: w => CL.WebLogic.Hosting.Services.WebLogicFeaturesBuilderExtensions.UseDashboardDefaults<GreetingDefaults>(w));
        await host.Client.GetAsync("/signin?user=u1");
        string Id(JsonElement layout) => layout.GetProperty("items")[0].GetProperty("instanceId").GetString()!;

        var first = Id(await host.Client.GetFromJsonAsync<JsonElement>("/_weblogic/dashboard/home"));
        Assert.Equal(first, Id(await host.Client.GetFromJsonAsync<JsonElement>("/_weblogic/dashboard/home")));
        Assert.Matches("^d[0-9a-f]{16}$", first);

        var other = host.CreateClient();
        await other.GetAsync("/signin?user=u2");
        Assert.NotEqual(first, Id(await other.GetFromJsonAsync<JsonElement>("/_weblogic/dashboard/home")));

        var token = await SecurityTestSite.GetCsrfTokenAsync(host.Client);
        var saved = await PutAsync(host.Client, token, new { version = 0, items = new[] { new { instanceId = first, widgetName = "greeting", zone = "side" } } });
        Assert.Equal(first, Id(await saved.Content.ReadFromJsonAsync<JsonElement>()));
    }

    [Fact]
    public async Task Dashboards_need_an_owner()
    {
        await using var host = await WidgetTestSite.StartAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/_weblogic/dashboard/home")).StatusCode);
    }

    [Fact]
    public async Task Save_and_load_with_optimistic_versions()
    {
        await using var host = await WidgetTestSite.StartAsync();
        await host.Client.GetAsync("/signin?user=u1");
        var token = await SecurityTestSite.GetCsrfTokenAsync(host.Client);

        var empty = await host.Client.GetFromJsonAsync<JsonElement>("/_weblogic/dashboard/home");
        Assert.Equal(0, empty.GetProperty("version").GetInt64());
        var available = empty.GetProperty("widgets").EnumerateArray().Select(w => w.GetProperty("name").GetString()).ToList();
        Assert.Contains("greeting", available);
        Assert.Contains("secret", available);
        Assert.DoesNotContain("counter", available); // not a dashboard widget

        var saved = await PutAsync(host.Client, token, new { version = 0, items = new[] { new { widgetName = "greeting", zone = "main", parameters = new { name = "Ada" } } } });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var layout = await saved.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, layout.GetProperty("version").GetInt64());
        Assert.Matches("^w[0-9a-f]{16}$", layout.GetProperty("items")[0].GetProperty("instanceId").GetString());

        Assert.Equal(HttpStatusCode.Conflict, (await PutAsync(host.Client, token, new { version = 0, items = Array.Empty<object>() })).StatusCode);

        var html = await host.Client.GetStringAsync("/_weblogic/dashboard/home?format=html&zone=main&zone=side");
        Assert.Contains("data-wl-dashboard=\"home\"", html);
        Assert.Contains("data-wl-dashboard-version=\"1\"", html);
        Assert.Contains("data-wl-dashboard-zone=\"side\"", html);
        Assert.Contains("Hello Ada", html);
        Assert.Contains("data-wl-dashboard-params=\"{&quot;name&quot;:&quot;Ada&quot;}\"", html);
    }

    [Fact]
    public async Task Owners_come_from_the_server_never_the_request()
    {
        // Regression: the old endpoint took ownerKey from the query string.
        await using var host = await WidgetTestSite.StartAsync();
        await host.Client.GetAsync("/signin?user=u1");
        var token = await SecurityTestSite.GetCsrfTokenAsync(host.Client);
        await PutAsync(host.Client, token, new { version = 0, items = new[] { new { widgetName = "greeting", zone = "main" } } });

        var other = host.CreateClient();
        await other.GetAsync("/signin?user=u2");
        var theirs = await other.GetFromJsonAsync<JsonElement>("/_weblogic/dashboard/home?ownerKey=u1&owner=u1");
        Assert.Equal(0, theirs.GetProperty("items").GetArrayLength());
        Assert.Equal(0, theirs.GetProperty("version").GetInt64());
    }

    [Fact]
    public async Task Invalid_keys_widgets_and_missing_csrf_are_refused()
    {
        await using var host = await WidgetTestSite.StartAsync();
        await host.Client.GetAsync("/signin?user=u1");
        var token = await SecurityTestSite.GetCsrfTokenAsync(host.Client);

        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.GetAsync("/_weblogic/dashboard/a..b")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PutAsync(host.Client, token, new { version = 0, items = new[] { new { widgetName = "nope" } } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PutAsync(host.Client, token, new { version = 0, items = new[] { new { widgetName = "counter" } } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PutAsync(host.Client, token, new { version = 0, items = new[] { new { widgetName = "greeting", zone = "Bad Zone" } } })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await PutAsync(host.Client, "", new { version = 0, items = Array.Empty<object>() })).StatusCode);
    }
}
