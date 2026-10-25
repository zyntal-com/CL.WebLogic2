using CL.WebLogic.Testing.Services;

namespace CL.WebLogic.IntegrationTests.Security;

public sealed class TlsProxyCookieTests
{
    [Fact]
    public async Task Behind_an_untrusted_tls_proxy_cookies_stay_secure_when_the_public_url_is_https()
    {
        // Regression (security review): requests via a TLS-terminating proxy look like HTTP when its
        // headers are not trusted, and cookies silently lost Secure / __Host- (and HSTS was dropped).
        await using var host = await SecurityTestSite.StartAsync(c => c.PublicBaseUrl = "https://site.test",
            options: new WebLogicTestHostOptions { EnvironmentName = "Production", AllowDevelopmentStores = true });

        var response = await host.Client.GetAsync("/session/set?v=1");
        var cookies = string.Join("\n", response.Headers.GetValues("Set-Cookie"));

        Assert.Contains("__Host-wl_sid=", cookies);
        Assert.Contains("secure", cookies, StringComparison.OrdinalIgnoreCase);
        Assert.True(response.Headers.Contains("Strict-Transport-Security"));
    }

    [Fact]
    public async Task Development_keeps_plain_http_cookies_for_local_runs()
    {
        await using var host = await SecurityTestSite.StartAsync(c => c.PublicBaseUrl = "https://site.test");

        var response = await host.Client.GetAsync("/session/set?v=1");
        var cookies = string.Join("\n", response.Headers.GetValues("Set-Cookie"));

        Assert.Contains("wl_sid=", cookies);
        Assert.DoesNotContain("__Host-wl_sid", cookies);
    }
}
