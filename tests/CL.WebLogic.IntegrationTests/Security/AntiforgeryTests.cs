using System.Net;

namespace CL.WebLogic.IntegrationTests.Security;

public sealed class AntiforgeryTests
{
    [Fact]
    public async Task Regression_AnonymousPostWithoutToken_IsRejected()
    {
        await using var host = await SecurityTestSite.StartAsync();

        var response = await host.Client.PostAsync("/submit", new StringContent("{}"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Regression_FallbackPostWithoutToken_IsRejected()
    {
        await using var host = await SecurityTestSite.StartAsync();

        var response = await host.Client.PostAsync("/anything/here", new StringContent("x"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PostWithToken_FromPreSessionCookie_IsAccepted()
    {
        await using var host = await SecurityTestSite.StartAsync();
        var token = await SecurityTestSite.GetCsrfTokenAsync(host.Client);

        var response = await SecurityTestSite.PostWithTokenAsync(host.Client, "/submit", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("submitted", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task FormFieldToken_IsAccepted()
    {
        await using var host = await SecurityTestSite.StartAsync();
        var token = await SecurityTestSite.GetCsrfTokenAsync(host.Client);

        var response = await host.Client.PostAsync("/submit", new FormUrlEncodedContent([new("__wl_csrf", token), new("name", "x")]));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Tokens_AreMaskedPerIssue_ButAllValidate()
    {
        await using var host = await SecurityTestSite.StartAsync();
        var first = await SecurityTestSite.GetCsrfTokenAsync(host.Client);
        var second = await SecurityTestSite.GetCsrfTokenAsync(host.Client);

        Assert.NotEqual(first, second);
        Assert.Equal(HttpStatusCode.OK, (await SecurityTestSite.PostWithTokenAsync(host.Client, "/submit", first)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SecurityTestSite.PostWithTokenAsync(host.Client, "/submit", second)).StatusCode);
    }

    [Fact]
    public async Task TokenOfAnotherBrowser_IsRejected()
    {
        await using var host = await SecurityTestSite.StartAsync();
        var attackerToken = await SecurityTestSite.GetCsrfTokenAsync(host.CreateClient());
        await SecurityTestSite.GetCsrfTokenAsync(host.Client); // victim has their own pre-session cookie

        var response = await SecurityTestSite.PostWithTokenAsync(host.Client, "/submit", attackerToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TokensIssuedBeforeSignIn_StopWorkingAfterIt()
    {
        await using var host = await SecurityTestSite.StartAsync();
        var anonymousToken = await SecurityTestSite.GetCsrfTokenAsync(host.Client);

        await host.Client.GetAsync("/signin?user=alice");

        Assert.Equal(HttpStatusCode.Forbidden, (await SecurityTestSite.PostWithTokenAsync(host.Client, "/submit", anonymousToken)).StatusCode);
        var freshToken = await SecurityTestSite.GetCsrfTokenAsync(host.Client);
        Assert.Equal(HttpStatusCode.OK, (await SecurityTestSite.PostWithTokenAsync(host.Client, "/submit", freshToken)).StatusCode);
    }

    [Theory]
    [InlineData("Origin", "https://evil.test")]
    [InlineData("Referer", "https://evil.test/page")]
    [InlineData("Sec-Fetch-Site", "cross-site")]
    public async Task CrossSiteRequests_AreRejected_EvenWithAValidToken(string header, string value)
    {
        await using var host = await SecurityTestSite.StartAsync();
        var token = await SecurityTestSite.GetCsrfTokenAsync(host.Client);

        var request = new HttpRequestMessage(HttpMethod.Post, "/submit") { Content = new StringContent("{}") };
        request.Headers.Add("X-CSRF-Token", token);
        request.Headers.Add(header, value);

        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.SendAsync(request)).StatusCode);
    }

    [Fact]
    public async Task TrustedOrigin_IsAccepted()
    {
        await using var host = await SecurityTestSite.StartAsync(c => c.Security.Csrf.TrustedOrigins.Add("https://admin.example.test"));
        var token = await SecurityTestSite.GetCsrfTokenAsync(host.Client);

        var request = new HttpRequestMessage(HttpMethod.Post, "/submit") { Content = new StringContent("{}") };
        request.Headers.Add("X-CSRF-Token", token);
        request.Headers.Add("Origin", "https://admin.example.test");

        Assert.Equal(HttpStatusCode.OK, (await host.Client.SendAsync(request)).StatusCode);
    }

    [Fact]
    public async Task AllowCrossSitePost_IsExempt() =>
        Assert.Equal(HttpStatusCode.OK, (await (await SecurityTestSite.StartAsync()).Client.PostAsync("/hook", new StringContent("{}"))).StatusCode);

    [Fact]
    public async Task CsrfEndpoint_IsNotCacheable()
    {
        await using var host = await SecurityTestSite.StartAsync();

        var response = await host.Client.GetAsync("/_weblogic/csrf");

        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
    }
}
