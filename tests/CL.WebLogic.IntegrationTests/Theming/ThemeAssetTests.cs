using System.Net;
using CL.WebLogic.Testing.Services;

namespace CL.WebLogic.IntegrationTests.Theming;

public sealed class ThemeAssetTests
{
    private static async Task<WebLogicTestHost> StartWithThemeAsync()
    {
        var host = await WebLogicTestHost.StartAsync();
        host.WriteContentFile("theme/assets/site.css", "body{}");
        host.WriteContentFile("theme/assets/.secret.css", "hidden");
        host.WriteContentFile("theme/assets/notes.cshtml", "not allowlisted");
        host.WriteContentFile("theme/templates/home.html", "<h1>template source</h1>");
        return host;
    }

    [Fact]
    public async Task Asset_IsServed_WithValidators()
    {
        await using var host = await StartWithThemeAsync();

        var response = await host.Client.GetAsync("/assets/site.css");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/css", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("body{}", await response.Content.ReadAsStringAsync());
        Assert.NotNull(response.Headers.ETag);
        Assert.Contains("max-age=3600", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task Asset_ConditionalRequest_Returns304()
    {
        await using var host = await StartWithThemeAsync();
        var first = await host.Client.GetAsync("/assets/site.css");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/assets/site.css");
        request.Headers.IfNoneMatch.Add(first.Headers.ETag!);
        var second = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotModified, second.StatusCode);
    }

    [Fact]
    public async Task FingerprintedAsset_IsImmutable()
    {
        await using var host = await StartWithThemeAsync();

        var response = await host.Client.GetAsync("/assets/site.css?v=abc123");

        Assert.Contains("immutable", response.Headers.CacheControl?.ToString());
    }

    [Theory]
    [InlineData("/assets/%2e%2e/templates/home.html")]
    [InlineData("/assets/..%2ftemplates/home.html")]
    [InlineData("/assets/..%5ctemplates%5chome.html")]
    [InlineData("/assets/.secret.css")]
    [InlineData("/assets/notes.cshtml")]
    [InlineData("/templates/home.html")]
    [InlineData("/assets/missing.css")]
    public async Task UnsafeOrUnknownPaths_Return404(string path)
    {
        await using var host = await StartWithThemeAsync();

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(host.BaseAddress, new Uri(path, UriKind.Relative)));
        var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("template source", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task MissingAsset_KeepsBareStatus()
    {
        await using var host = await StartWithThemeAsync();

        var response = await host.Client.GetAsync("/assets/missing.css");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync());
    }
}
