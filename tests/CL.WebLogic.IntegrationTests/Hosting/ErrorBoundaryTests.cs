using System.Net;
using System.Text;
using CL.WebLogic.Results;
using CL.WebLogic.Routing.Services;
using CL.WebLogic.Testing.Services;
using CodeLogic.Framework.Libraries;

namespace CL.WebLogic.IntegrationTests.Hosting;

public sealed class ErrorBoundaryTests
{
    private static Task<WebLogicTestHost> StartAsync(Action<WebContributionBuilder> contribute, string environment = "Production") =>
        WebLogicTestHost.StartAsync(new WebLogicTestHostOptions
        {
            EnvironmentName = environment,
            ConfigureWebLogic = web => web.AddContributor(new DelegateContributor(contribute))
        });

    [Fact]
    public async Task UnhandledException_InProduction_HidesDetails()
    {
        await using var host = await StartAsync(b => b.MapPage("/boom", (_, _) => throw new InvalidOperationException("secret-detail")).AllowAnonymous());

        var response = await host.Client.GetAsync("/boom");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("500", body);
        Assert.DoesNotContain("secret-detail", body);
        Assert.Contains(host.Context.Log.Entries, e => e.Message.Contains("secret-detail"));
    }

    [Fact]
    public async Task UnhandledException_OnApi_ReturnsProblemJson()
    {
        await using var host = await StartAsync(b => b.MapApi("/api/boom", (_, _) => throw new InvalidOperationException("nope")).AllowAnonymous());

        var response = await host.Client.GetAsync("/api/boom");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("traceId", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UnhandledException_InDevelopment_ShowsDetails()
    {
        await using var host = await StartAsync(b => b.MapPage("/boom", (_, _) => throw new InvalidOperationException("dev-detail")).AllowAnonymous(), "Development");

        var body = await host.Client.GetStringAsyncAllowingErrors("/boom");

        Assert.Contains("dev-detail", body);
    }

    [Fact]
    public async Task EmptyNotFound_GetsStatusPage()
    {
        await using var host = await StartAsync(b => b.MapPage("/x", (_, _) => ValueTask.FromResult<WebResult>(WebResult.Status(404))).AllowAnonymous());

        var response = await host.Client.GetAsync("/x");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("404", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task BodyOverRouteLimit_Returns413()
    {
        await using var host = await StartAsync(b => b.MapApi("/api/upload", async (request, ct) =>
        {
            var text = await request.ReadBodyAsStringAsync(ct);
            return WebResult.Text(text.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }, "POST").AllowAnonymous().WithBodyLimit(10));

        using var content = new StringContent(new string('x', 100), Encoding.UTF8, "text/plain");
        var response = await host.Client.PostAsync("/api/upload", content);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task HealthProbes_ReflectLibraryHealth()
    {
        await using var host = await StartAsync(_ => { });

        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/_weblogic/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/_weblogic/health/ready")).StatusCode);

        host.Context.Health = HealthStatus.Unhealthy("down");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await host.Client.GetAsync("/_weblogic/health/ready")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/_weblogic/health/live")).StatusCode);
    }
}

internal static class HttpClientTestExtensions
{
    public static async Task<string> GetStringAsyncAllowingErrors(this HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        return await response.Content.ReadAsStringAsync();
    }
}
