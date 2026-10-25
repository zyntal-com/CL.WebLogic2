using System.Net;
using CL.WebLogic.Configuration.Models;

namespace CL.WebLogic.IntegrationTests.Security;

public sealed class HeadersAndNetworkTests
{
    [Fact]
    public async Task SecurityHeaders_AreSentWithANoncedCsp()
    {
        await using var host = await SecurityTestSite.StartAsync();

        var response = await host.Client.GetAsync("/public");

        var csp = response.Headers.GetValues("Content-Security-Policy").Single();
        Assert.Matches("script-src 'self' 'nonce-[A-Za-z0-9_-]{22}' 'strict-dynamic'", csp);
        Assert.Contains("frame-ancestors 'none'", csp);
        Assert.Contains("object-src 'none'", csp);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.False(response.Headers.Contains("Server")); // no "Server: Kestrel"
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("same-origin", response.Headers.GetValues("Cross-Origin-Opener-Policy").Single());
        Assert.False(response.Headers.Contains("Strict-Transport-Security")); // plain HTTP
    }

    [Fact]
    public async Task Regression_HandlerCannotReplaceTheCsp()
    {
        await using var host = await SecurityTestSite.StartAsync();

        var response = await host.Client.GetAsync("/handler-csp");

        Assert.DoesNotContain("default-src *", response.Headers.GetValues("Content-Security-Policy").Single());
    }

    [Fact]
    public async Task ExplicitOverride_IsHonoured()
    {
        await using var host = await SecurityTestSite.StartAsync();

        var response = await host.Client.GetAsync("/framable");

        Assert.Equal("SAMEORIGIN", response.Headers.GetValues("X-Frame-Options").Single());
    }

    [Fact]
    public async Task AuthenticatedResponses_AreNoStore()
    {
        await using var host = await SecurityTestSite.StartAsync();
        await host.Client.GetAsync("/signin?user=alice");

        var response = await host.Client.GetAsync("/secure");

        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task Regression_ForwardedHeaders_AreIgnoredWithoutTrustedProxies()
    {
        await using var host = await SecurityTestSite.StartAsync(c => c.Security.ForwardedHeaders.Enabled = true);

        var body = await Send(host.Client, "/whoami", ("X-Forwarded-For", "203.0.113.9"), ("X-Forwarded-Proto", "https"));

        Assert.StartsWith("127.0.0.1|http|", body);
    }

    [Fact]
    public async Task ForwardedHeaders_FromTrustedProxy_AreApplied()
    {
        await using var host = await SecurityTestSite.StartAsync(c =>
        {
            c.Security.ForwardedHeaders.Enabled = true;
            c.Security.ForwardedHeaders.TrustedProxies.Add("127.0.0.1");
            c.Security.ForwardedHeaders.HonorPrefix = true;
            c.Security.Hosts.AllowedHosts.AddRange(["127.0.0.1", "app.example.test"]);
        });

        var body = await Send(host.Client, "/whoami",
            ("X-Forwarded-For", "198.51.100.1, 203.0.113.9"), ("X-Forwarded-Proto", "https"),
            ("X-Forwarded-Host", "app.example.test"), ("X-Forwarded-Prefix", "/shop"));

        Assert.Equal("203.0.113.9|https|app.example.test|/shop", body); // ForwardLimit = 1: one hop only
    }

    [Theory]
    [InlineData("203.0.113.9:51234", "203.0.113.9")]
    [InlineData("[2001:db8::7]:443", "2001:db8::7")]
    public async Task ForwardedFor_WithAPort_IsUnderstood(string header, string expected)
    {
        // Security review: "ip:port" values fell back to the proxy address, so every client shared one limit.
        await using var host = await SecurityTestSite.StartAsync(c =>
        {
            c.Security.ForwardedHeaders.Enabled = true;
            c.Security.ForwardedHeaders.TrustedProxies.Add("127.0.0.1");
        });

        Assert.StartsWith(expected + "|", await Send(host.Client, "/whoami", ("X-Forwarded-For", header)));
    }

    [Fact]
    public async Task ForwardedHost_NotInAllowedHosts_IsIgnored()
    {
        await using var host = await SecurityTestSite.StartAsync(c =>
        {
            c.Security.ForwardedHeaders.Enabled = true;
            c.Security.ForwardedHeaders.TrustedProxies.Add("127.0.0.1");
            c.Security.Hosts.AllowedHosts.Add("127.0.0.1");
        });

        var body = await Send(host.Client, "/whoami", ("X-Forwarded-Host", "evil.test"));

        Assert.DoesNotContain("evil.test", body);
    }

    [Fact]
    public async Task UnknownHost_IsRejected_ProbesAreExempt()
    {
        await using var host = await SecurityTestSite.StartAsync(c => c.Security.Hosts.AllowedHosts.Add("app.example.test"));

        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.GetAsync("/public")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/_weblogic/health/live")).StatusCode);
    }

    [Fact]
    public async Task Https_RedirectsToTheCanonicalHost_NotTheHostHeader()
    {
        await using var host = await SecurityTestSite.StartAsync(c =>
        {
            c.PublicBaseUrl = "https://www.example.test";
            c.Security.Https.Enforce = true;
        });

        var request = new HttpRequestMessage(HttpMethod.Get, "/public?x=1");
        request.Headers.Host = "evil.test";
        var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.PermanentRedirect, response.StatusCode);
        Assert.Equal("https://www.example.test/public?x=1", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task IpRules_DenyAndAllow()
    {
        await using (var denied = await SecurityTestSite.StartAsync(c => c.Security.IpRules.Deny.Add("127.0.0.0/8")))
            Assert.Equal(HttpStatusCode.Forbidden, (await denied.Client.GetAsync("/public")).StatusCode);

        await using (var notAllowed = await SecurityTestSite.StartAsync(c => c.Security.IpRules.Allow.Add("10.0.0.0/8")))
            Assert.Equal(HttpStatusCode.Forbidden, (await notAllowed.Client.GetAsync("/public")).StatusCode);

        await using var allowed = await SecurityTestSite.StartAsync(c => c.Security.IpRules.Allow.Add("127.0.0.1"));
        Assert.Equal(HttpStatusCode.OK, (await allowed.Client.GetAsync("/public")).StatusCode);
    }

    [Fact]
    public async Task GlobalRateLimit_Returns429_WithRetryAfter()
    {
        await using var host = await SecurityTestSite.StartAsync(c =>
        {
            c.Security.RateLimits.GlobalPermitsPerMinute = 1;
            c.Security.RateLimits.GlobalBurst = 2;
        });

        var statuses = new List<HttpStatusCode>();
        HttpResponseMessage? last = null;
        for (var i = 0; i < 5; i++)
        {
            last = await host.Client.GetAsync("/public");
            statuses.Add(last.StatusCode);
        }

        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK], statuses.Take(3));
        Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode);
        Assert.True(last.Headers.RetryAfter is not null);
    }

    [Fact]
    public async Task RoutePolicy_Limits_AndUnknownPolicyFailsClosed()
    {
        await using var host = await SecurityTestSite.StartAsync(c =>
            c.Security.RateLimits.Policies.Add(new RateLimitPolicyConfig { Name = "tiny", Permits = 2, WindowSeconds = 3600 }));

        var first = await host.Client.GetAsync("/limited");
        Assert.Equal("1", first.Headers.GetValues("RateLimit-Remaining").Single());
        await host.Client.GetAsync("/limited");
        Assert.Equal(HttpStatusCode.TooManyRequests, (await host.Client.GetAsync("/limited")).StatusCode);
        Assert.Equal(HttpStatusCode.InternalServerError, (await host.Client.GetAsync("/unknown-limit")).StatusCode);
    }

    private static async Task<string> Send(HttpClient client, string path, params (string Name, string Value)[] headers)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        foreach (var (name, value) in headers)
            request.Headers.TryAddWithoutValidation(name, value);
        var response = await client.SendAsync(request);
        return await response.Content.ReadAsStringAsync();
    }
}
