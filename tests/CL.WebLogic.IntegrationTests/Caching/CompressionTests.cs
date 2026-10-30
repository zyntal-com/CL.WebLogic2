using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using CL.WebLogic.Client.Services;
using CL.WebLogic.Hosting.Services;
using CL.WebLogic.IntegrationTests.Security;
using CL.WebLogic.Results;
using CL.WebLogic.Routing.Models;
using CL.WebLogic.Routing.Services;
using CL.WebLogic.Security.Antiforgery;
using CL.WebLogic.Testing.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CL.WebLogic.IntegrationTests.Caching;

public sealed class CompressionTests
{
    private static readonly string Body = string.Concat(Enumerable.Repeat("<p>compressible content</p>", 64));

    private static Task<WebLogicTestHost> StartAsync(bool enabled) =>
        SecurityTestSite.StartAsync(c => c.Compression.Enabled = enabled, web =>
            web.AddContributor(new DelegateContributor(b =>
            {
                b.MapPage("/page", (_, _) => ValueTask.FromResult<WebResult>(WebResult.Html(Body))).AllowAnonymous();
                b.MapPage("/with-form", (r, _) =>
                {
                    r.GetRequiredService<IWebCsrfTokens>().GetToken(r.HttpContext);
                    return ValueTask.FromResult<WebResult>(WebResult.Html(Body));
                }).AllowAnonymous();
                b.MapPage("/personal-ok", (_, _) => ValueTask.FromResult<WebResult>(WebResult.Html(Body))).AllowAnonymous().AllowPersonalCompression();
                b.MapPage("/never", (_, _) => ValueTask.FromResult<WebResult>(WebResult.Html(Body))).AllowAnonymous().WithoutCompression();
            }), new WebContributorDescriptor { Id = "tests.compression", Name = "Compression" }));

    private static Task<HttpResponseMessage> GetAsync(HttpClient client, string path, string encoding = "br")
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue(encoding));
        return client.SendAsync(request);
    }

    private static async Task<string> DecompressAsync(HttpResponseMessage response)
    {
        await using var raw = await response.Content.ReadAsStreamAsync();
        await using Stream decoder = response.Content.Headers.ContentEncoding.Single() == "br"
            ? new BrotliStream(raw, CompressionMode.Decompress)
            : new GZipStream(raw, CompressionMode.Decompress);
        using var reader = new StreamReader(decoder);
        return await reader.ReadToEndAsync();
    }

    [Fact]
    public async Task Anonymous_responses_are_compressed_when_enabled()
    {
        await using var host = await StartAsync(enabled: true);

        var brotli = await GetAsync(host.Client, "/page");
        Assert.Equal("br", brotli.Content.Headers.ContentEncoding.Single());
        Assert.Contains("Accept-Encoding", brotli.Headers.Vary);
        Assert.Equal(Body, await DecompressAsync(brotli));

        var gzip = await GetAsync(host.CreateClient(), "/page", "gzip");
        Assert.Equal("gzip", gzip.Content.Headers.ContentEncoding.Single());
        Assert.Equal(Body, await DecompressAsync(gzip));
    }

    [Fact]
    public async Task Nothing_is_compressed_by_default()
    {
        await using var host = await StartAsync(enabled: false);

        var response = await GetAsync(host.Client, "/page");
        Assert.Empty(response.Content.Headers.ContentEncoding);
        Assert.Equal(Body, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Pages_with_a_csrf_token_are_not_compressed()
    {
        // BREACH: a compressed page holding a secret next to reflected input leaks it through its size.
        await using var host = await StartAsync(enabled: true);

        var response = await GetAsync(host.Client, "/with-form");
        Assert.Empty(response.Content.Headers.ContentEncoding);
        Assert.Equal(Body, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Signed_in_users_get_uncompressed_responses_unless_the_route_opts_in()
    {
        await using var host = await StartAsync(enabled: true);
        await host.Client.GetAsync("/signin?user=u1");

        var page = await GetAsync(host.Client, "/page");
        Assert.Empty(page.Content.Headers.ContentEncoding);
        Assert.Equal(Body, await page.Content.ReadAsStringAsync());

        var optedIn = await GetAsync(host.Client, "/personal-ok");
        Assert.Equal("br", optedIn.Content.Headers.ContentEncoding.Single());
    }

    [Fact]
    public async Task Anonymous_session_visitors_and_opted_out_routes_are_not_compressed()
    {
        await using var host = await StartAsync(enabled: true);
        Assert.Empty((await GetAsync(host.Client, "/never")).Content.Headers.ContentEncoding);

        var shopper = host.CreateClient();
        await shopper.GetAsync("/session/set?v=cart");
        Assert.Empty((await GetAsync(shopper, "/page")).Content.Headers.ContentEncoding);
    }

    [Fact]
    public async Task Theme_assets_are_compressed_for_signed_in_users_too()
    {
        await using var host = await StartAsync(enabled: true);
        var css = string.Concat(Enumerable.Repeat("body { color: teal; }\n", 32));
        host.WriteContentFile("theme/assets/site.css", css);
        await host.Client.GetAsync("/signin?user=u1");

        var response = await GetAsync(host.Client, "/assets/site.css");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("br", response.Content.Headers.ContentEncoding.Single());
        Assert.Equal(css, await DecompressAsync(response));
    }

    [Fact]
    public async Task The_precompressed_client_bundle_is_not_compressed_twice()
    {
        await using var host = await StartAsync(enabled: true);
        var asset = host.Services.GetRequiredService<ClientAssetCatalog>().Get("weblogic.esm.js")!;

        var response = await GetAsync(host.Client, ClientAssetCatalog.PathPrefix + asset.HashedName);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("br", response.Content.Headers.ContentEncoding.Single());
        Assert.Equal(asset.Brotli!.Length, (await response.Content.ReadAsByteArrayAsync()).Length);
    }
}
