using System.Net;
using System.Text.Json;
using CL.WebLogic.Results;
using CL.WebLogic.Routing.Services;
using CL.WebLogic.Testing.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.IntegrationTests.Hosting;

public sealed class RoutingEndpointTests
{
    private static Task<WebLogicTestHost> StartAsync(Action<WebContributionBuilder> contribute, Action<WebApplication>? app = null) =>
        WebLogicTestHost.StartAsync(new WebLogicTestHostOptions
        {
            ConfigureWebLogic = web => web.AddContributor(new DelegateContributor(contribute)),
            ConfigureApp = app
        });

    [Fact]
    public async Task Page_ReturnsHtml()
    {
        await using var host = await StartAsync(b => b.MapPage("/", (_, _) => ValueTask.FromResult<WebResult>(WebResult.Html("<h1>Home</h1>"))).AllowAnonymous());

        var response = await host.Client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("<h1>Home</h1>", await response.Content.ReadAsStringAsync());
        Assert.True(response.Headers.Contains("X-Correlation-Id"));
    }

    [Fact]
    public async Task Api_SerializesCamelCase()
    {
        await using var host = await StartAsync(b => b.MapApi("/api/item", (_, _) =>
            ValueTask.FromResult<WebResult>(WebResult.Json(new { ItemName = "x", Count = 2 }))).AllowAnonymous());

        var json = await host.Client.GetStringAsync("/api/item");

        Assert.Equal("""{"itemName":"x","count":2}""", json);
    }

    [Fact]
    public async Task RouteTemplate_ParsesConstrainedParameter()
    {
        await using var host = await StartAsync(b => b.MapApi("/api/items/{id:int}", (request, _) =>
        {
            request.TryGetRouteValue<int>("id", out var id);
            return ValueTask.FromResult<WebResult>(WebResult.Text($"item {id}"));
        }).AllowAnonymous());

        Assert.Equal("item 42", await host.Client.GetStringAsync("/api/items/42"));
        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync("/api/items/abc")).StatusCode);
    }

    [Fact]
    public async Task WrongMethod_Returns405_WithAllowHeader()
    {
        await using var host = await StartAsync(b => b.MapApi("/api/only-get", (_, _) => ValueTask.FromResult<WebResult>(WebResult.Text("ok"))).AllowAnonymous());

        var response = await host.Client.DeleteAsync("/api/only-get");

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Contains("GET", response.Content.Headers.Allow);
    }

    [Fact]
    public async Task Head_IsAnsweredForGetRoutes_WithoutBody()
    {
        await using var host = await StartAsync(b => b.MapPage("/page", (_, _) => ValueTask.FromResult<WebResult>(WebResult.Html("body"))).AllowAnonymous());

        using var request = new HttpRequestMessage(HttpMethod.Head, "/page");
        var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Fallback_HandlesUnmatchedPaths()
    {
        await using var host = await StartAsync(b => b.MapFallback((request, _) =>
            ValueTask.FromResult<WebResult>(WebResult.Text("fallback " + request.Path))).AllowAnonymous());

        Assert.Equal("fallback /some/deep/path", await host.Client.GetStringAsync("/some/deep/path"));
    }

    [Fact]
    public async Task MinimalApi_CoexistsWithWebLogicRoutes()
    {
        await using var host = await StartAsync(
            b => b.MapFallback((_, _) => ValueTask.FromResult<WebResult>(WebResult.Text("fallback"))).AllowAnonymous(),
            app => app.MapGet("/minimal", () => "minimal"));

        Assert.Equal("minimal", await host.Client.GetStringAsync("/minimal"));
        Assert.Equal("fallback", await host.Client.GetStringAsync("/other"));
    }

    [Fact]
    public async Task GroupPrefix_AndMiddleware_Apply()
    {
        await using var host = await StartAsync(b =>
        {
            var admin = b.MapGroup("/admin").AllowAnonymous().WithMiddleware(new HeaderMiddleware("X-Group", "admin"));
            admin.MapPage("dashboard", (_, _) => ValueTask.FromResult<WebResult>(WebResult.Html("dash")));
        });

        var response = await host.Client.GetAsync("/admin/dashboard");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("admin", response.Headers.GetValues("X-Group").Single());
    }

    [Fact]
    public async Task UnregisteredContributor_RoutesDisappear()
    {
        await using var host = await StartAsync(_ => { });
        var registrar = (ContributorRegistrar)host.Services.GetService(typeof(ContributorRegistrar))!;

        var handle = await registrar.RegisterAsync(
            new DelegateContributor(b => b.MapPage("/temp", (_, _) => ValueTask.FromResult<WebResult>(WebResult.Html("temp"))).AllowAnonymous()),
            new() { Id = "temp", Name = "Temp" }, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/temp")).StatusCode);

        await handle.DisposeAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync("/temp")).StatusCode);
    }

    [Fact]
    public async Task Sse_StreamsEvents()
    {
        await using var host = await StartAsync(b => b.MapSse("/events", (_, _) =>
            ValueTask.FromResult<WebResult>(WebResult.Sse(Events()))).AllowAnonymous());

        var body = await host.Client.GetStringAsync("/events");

        Assert.Contains("event: tick\ndata: 1\n\n", body);
        Assert.Contains("data: 2\n\n", body);

        static async IAsyncEnumerable<WebSseEvent> Events()
        {
            yield return new WebSseEvent("1", "tick");
            await Task.Yield();
            yield return new WebSseEvent("2", "tick");
        }
    }

    [Fact]
    public async Task Commands_ProduceClientPayload()
    {
        await using var host = await StartAsync(b => b.MapApi("/api/cmd", (_, _) =>
            ValueTask.FromResult<WebResult>(WebResult.Commands(WebCommand.Toast("Saved"), WebCommand.Navigate("/next")))).AllowAnonymous());

        using var doc = JsonDocument.Parse(await host.Client.GetStringAsync("/api/cmd"));

        Assert.True(doc.RootElement.GetProperty("ok").GetBoolean());
        var commands = doc.RootElement.GetProperty("commands");
        Assert.Equal("toast", commands[0].GetProperty("type").GetString());
        Assert.Equal("/next", commands[1].GetProperty("url").GetString());
    }

    private sealed class HeaderMiddleware(string name, string value) : CL.WebLogic.Routing.IWebMiddleware
    {
        public async ValueTask<WebResult> InvokeAsync(CL.WebLogic.Runtime.Models.WebRequestContext request, CL.WebLogic.Routing.WebMiddlewareNext continuation, CancellationToken cancellationToken)
        {
            var result = await continuation(request, cancellationToken);
            return result.WithHeader(name, value);
        }
    }
}
