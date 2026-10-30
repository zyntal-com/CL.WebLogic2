using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using CL.WebLogic.Configuration.Models;
using CL.WebLogic.Hosting.Services;
using CL.WebLogic.IntegrationTests.Security;
using CL.WebLogic.Results;
using CL.WebLogic.Routing.Models;
using CL.WebLogic.Routing.Services;
using CL.WebLogic.Security.Antiforgery;
using CL.WebLogic.Testing.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CL.WebLogic.IntegrationTests.Caching;

public sealed class ETagTests
{
    private sealed class Version
    {
        public int Value;
    }

    private static Task<WebLogicTestHost> StartAsync(Version version, Action<WebLogicConfig>? configure = null) =>
        SecurityTestSite.StartAsync(configure, web =>
            web.AddContributor(new DelegateContributor(b =>
            {
                b.MapPage("/tagged", (_, _) => ValueTask.FromResult<WebResult>(WebResult.Html("<p>v" + version.Value + "</p>"))).AllowAnonymous().WithETag();
                b.MapApi("/api/tagged", (_, _) => ValueTask.FromResult<WebResult>(WebResult.Json(new { version = version.Value }))).AllowAnonymous().WithETag();
                b.MapPage("/cached", (_, _) => ValueTask.FromResult<WebResult>(WebResult.Html("<p>c" + version.Value + "</p>"))).AllowAnonymous().WithETag().WithOutputCache("default");
                b.MapPage("/plain", (_, _) => ValueTask.FromResult<WebResult>(WebResult.Html("<p>plain</p>"))).AllowAnonymous();
                b.MapPage("/opted-out", (_, _) => ValueTask.FromResult<WebResult>(WebResult.Html("<p>out</p>"))).AllowAnonymous().WithoutETag();
                b.MapPage("/with-form", (r, _) =>
                {
                    r.GetRequiredService<IWebCsrfTokens>().GetToken(r.HttpContext);
                    return ValueTask.FromResult<WebResult>(WebResult.Html("<p>form</p>"));
                }).AllowAnonymous().WithETag();
                b.MapPage("/missing", (_, _) => ValueTask.FromResult<WebResult>(WebResult.Html("<p>gone</p>", 404))).AllowAnonymous().WithETag();
            }), new WebContributorDescriptor { Id = "tests.etag", Name = "ETag" }));

    private static Task<HttpResponseMessage> GetAsync(HttpClient client, string path, EntityTagHeaderValue? ifNoneMatch = null, string? encoding = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (ifNoneMatch is not null)
            request.Headers.IfNoneMatch.Add(ifNoneMatch);
        if (encoding is not null)
            request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue(encoding));
        return client.SendAsync(request);
    }

    [Fact]
    public async Task Pages_get_an_etag_and_a_304_for_a_matching_if_none_match()
    {
        await using var host = await StartAsync(new Version());

        var first = await GetAsync(host.Client, "/tagged");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var tag = first.Headers.ETag;
        Assert.NotNull(tag);
        Assert.True(tag.IsWeak); // one tag for every content encoding
        Assert.Equal("no-cache", first.Headers.CacheControl!.ToString());
        Assert.Equal("<p>v0</p>", await first.Content.ReadAsStringAsync());

        var second = await GetAsync(host.Client, "/tagged", tag);
        Assert.Equal(HttpStatusCode.NotModified, second.StatusCode);
        Assert.Empty(await second.Content.ReadAsByteArrayAsync());
        Assert.Equal(tag, second.Headers.ETag);

        var strong = await GetAsync(host.Client, "/tagged", new EntityTagHeaderValue(tag.Tag)); // weak comparison
        Assert.Equal(HttpStatusCode.NotModified, strong.StatusCode);
    }

    [Fact]
    public async Task A_changed_body_gets_a_new_etag()
    {
        var version = new Version();
        await using var host = await StartAsync(version);
        var first = (await GetAsync(host.Client, "/api/tagged")).Headers.ETag!;

        version.Value = 1;
        var changed = await GetAsync(host.Client, "/api/tagged", first);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.NotEqual(first, changed.Headers.ETag);
        Assert.Contains("\"version\":1", await changed.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task No_etag_unless_the_route_or_config_opts_in()
    {
        await using (var off = await StartAsync(new Version()))
        {
            var plain = await GetAsync(off.Client, "/plain");
            Assert.Null(plain.Headers.ETag);
            Assert.Null(plain.Headers.CacheControl);
            Assert.Null((await GetAsync(off.Client, "/missing")).Headers.ETag);
        }

        await using var on = await StartAsync(new Version(), c => c.Cache.ETags = true);
        Assert.NotNull((await GetAsync(on.Client, "/plain")).Headers.ETag);
        Assert.Null((await GetAsync(on.Client, "/opted-out")).Headers.ETag);
    }

    [Fact]
    public async Task Personal_responses_never_get_an_etag()
    {
        await using var host = await StartAsync(new Version());

        var form = await GetAsync(host.Client, "/with-form");
        Assert.Equal(HttpStatusCode.OK, form.StatusCode);
        Assert.Null(form.Headers.ETag);

        var anonymousTag = (await GetAsync(host.CreateClient(), "/tagged")).Headers.ETag!;
        var user = host.CreateClient();
        await user.GetAsync("/signin?user=u1");
        var signedIn = await GetAsync(user, "/tagged", anonymousTag);
        Assert.Equal(HttpStatusCode.OK, signedIn.StatusCode);
        Assert.Null(signedIn.Headers.ETag);
        Assert.Equal("<p>v0</p>", await signedIn.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Etags_work_with_compression_and_the_output_cache()
    {
        await using var host = await StartAsync(new Version(), c => c.Compression.Enabled = true);

        var first = await GetAsync(host.Client, "/tagged", encoding: "br");
        Assert.Equal("br", first.Content.Headers.ContentEncoding.Single());
        await using (var decoder = new BrotliStream(await first.Content.ReadAsStreamAsync(), CompressionMode.Decompress))
        using (var reader = new StreamReader(decoder))
            Assert.Equal("<p>v0</p>", await reader.ReadToEndAsync());

        var second = await GetAsync(host.Client, "/tagged", first.Headers.ETag, "br");
        Assert.Equal(HttpStatusCode.NotModified, second.StatusCode);
        Assert.Empty(await second.Content.ReadAsByteArrayAsync());

        var cached = (await GetAsync(host.Client, "/cached")).Headers.ETag!;
        var hit = await GetAsync(host.CreateClient(), "/cached", cached); // served by the output cache
        Assert.Equal(HttpStatusCode.NotModified, hit.StatusCode);
    }
}
