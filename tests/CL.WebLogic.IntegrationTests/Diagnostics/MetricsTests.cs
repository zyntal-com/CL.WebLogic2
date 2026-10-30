using System.Diagnostics;
using System.Net;
using CL.WebLogic.Configuration.Models;
using CL.WebLogic.Diagnostics.Models;
using CL.WebLogic.IntegrationTests.Auth;
using CL.WebLogic.IntegrationTests.Security;
using CL.WebLogic.Templates.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace CL.WebLogic.IntegrationTests.Diagnostics;

public sealed class MetricsTests
{
    [Fact]
    public async Task RouteRequests_AreCounted_ByPattern_NotByPath()
    {
        await using var host = await SecurityTestSite.StartAsync();
        using var capture = new MetricsCapture(host);

        await host.Client.GetAsync("/public?email=alice@example.test&token=secret123");
        await host.Client.GetAsync("/no-such-page/abc123");

        var requests = capture.Of(WebLogicInstruments.RouteRequests);
        var hit = Assert.Single(requests, m => m[WebLogicInstruments.RouteTag] == "/public");
        Assert.Equal("GET", hit[WebLogicInstruments.MethodTag]);
        Assert.Equal("2xx", hit[WebLogicInstruments.StatusClassTag]);
        Assert.Contains(requests, m => m[WebLogicInstruments.RouteTag] != "/public"); // the fallback pattern, not the path
        Assert.Contains(capture.Of(WebLogicInstruments.RouteDuration), m => m[WebLogicInstruments.RouteTag] == "/public" && m.Value > 0);
        AssertNothingPersonal(capture, "secret123", "alice", "abc123", "127.0.0.1", "?");
    }

    [Fact]
    public async Task SecurityRejections_CarryStage_AndReasonCode()
    {
        await using var host = await SecurityTestSite.StartAsync(c =>
            c.Security.RateLimits.Policies.Add(new RateLimitPolicyConfig { Name = "tiny", Permits = 1, WindowSeconds = 3600 }));
        using var capture = new MetricsCapture(host);

        await host.Client.PostAsync("/submit?q=private", new StringContent("{}"));
        await host.Client.GetAsync("/limited");
        Assert.Equal(HttpStatusCode.TooManyRequests, (await host.Client.GetAsync("/limited")).StatusCode);
        await host.Client.GetAsync("/admin"); // anonymous page: redirected to the login

        var rejections = capture.Of(WebLogicInstruments.SecurityRejections);
        Assert.Contains(rejections, m => m[WebLogicInstruments.StageTag] == "antiforgery"
            && m[WebLogicInstruments.ReasonTag] == "csrf-token-missing" && m[WebLogicInstruments.StatusCodeTag] == "403");
        // "route-rate-limit:tiny" keeps only its code.
        Assert.Contains(rejections, m => m[WebLogicInstruments.StageTag] == "route-rate-limit" && m[WebLogicInstruments.ReasonTag] == "route-rate-limit");
        Assert.Contains(rejections, m => m[WebLogicInstruments.StageTag] == "authorization" && m[WebLogicInstruments.ReasonTag] == "login-required");
        var limited = Assert.Single(capture.Of(WebLogicInstruments.RateLimitRejections));
        Assert.Equal("route", limited[WebLogicInstruments.RateLimitScopeTag]);
        AssertNothingPersonal(capture, "private", "tiny", "127.0.0.1", "/submit");
    }

    [Fact]
    public async Task SignIns_AreCounted_ByOutcome_AndMethod_WithoutUserIds()
    {
        await using var host = await AuthTestSite.StartAsync();
        var alice = await AuthTestSite.CreateUserAsync(host);
        var bob = await AuthTestSite.CreateUserAsync(host, "bob");
        await AuthTestSite.EnableTwoFactorAsync(host, bob);
        using var capture = new MetricsCapture(host);

        Assert.Equal("Succeeded", await AuthTestSite.LoginAsync(host.Client));
        Assert.Equal("InvalidCredentials", await AuthTestSite.LoginAsync(host.CreateClient(), "alice", "wrong password!"));
        Assert.Equal("RequiresTwoFactor", await AuthTestSite.LoginAsync(host.CreateClient(), "bob"));
        await AuthTestSite.PostAsync(host.Client, "/auth/refresh", new { }); // a refresh is not a sign-in

        var signIns = capture.Of(WebLogicInstruments.SignIns);
        var succeeded = Assert.Single(signIns, m => m[WebLogicInstruments.SignInOutcomeTag] == "succeeded");
        Assert.Equal("pwd", succeeded[WebLogicInstruments.SignInMethodTag]);
        Assert.Contains(signIns, m => m[WebLogicInstruments.SignInOutcomeTag] == "failed" && m[WebLogicInstruments.SignInReasonTag] == "invalid_credentials");
        Assert.Contains(signIns, m => m[WebLogicInstruments.SignInOutcomeTag] == "two_factor_required" && m[WebLogicInstruments.SignInMethodTag] == "pwd");
        AssertNothingPersonal(capture, alice.Id, bob.Id, "alice", "bob", "127.0.0.1");
    }

    [Fact]
    public async Task LockedAccounts_AndThrottledSignIns_AreCounted()
    {
        await using var host = await AuthTestSite.StartAsync(c => c.Auth.Lockout.MaxFailedAttempts = 2);
        await AuthTestSite.CreateUserAsync(host);
        using var capture = new MetricsCapture(host);

        await AuthTestSite.LoginAsync(host.Client, "alice", "nope nope 1");
        await AuthTestSite.LoginAsync(host.Client, "alice", "nope nope 2");

        Assert.Contains(capture.Of(WebLogicInstruments.SignIns), m => m[WebLogicInstruments.SignInOutcomeTag] == "locked");
    }

    [Fact]
    public async Task ObservableInstruments_ReportConnections_AndQueues()
    {
        await using var host = await SecurityTestSite.StartAsync();
        using var capture = new MetricsCapture(host);

        capture.Observe();

        var names = capture.Measurements.Select(m => m.Instrument).ToHashSet();
        Assert.Contains(WebLogicInstruments.RealtimeConnections, names);
        Assert.Contains(WebLogicInstruments.NotificationQueueLength, names);
        Assert.Contains(WebLogicInstruments.AuditQueueLength, names);
        Assert.Contains(WebLogicInstruments.AuditDropped, names);
        Assert.Equal(0, capture.Of(WebLogicInstruments.RealtimeConnections).Single().Value);
    }

    [Fact]
    public async Task SecurityPhases_AreTraced_WithTheRejectingStage()
    {
        await using var host = await SecurityTestSite.StartAsync();
        using var capture = new MetricsCapture(host);
        var traceId = ActivityTraceId.CreateRandom();
        var request = new HttpRequestMessage(HttpMethod.Post, "/submit") { Content = new StringContent("{}") };
        request.Headers.Add("traceparent", $"00-{traceId}-{ActivitySpanId.CreateRandom()}-01");

        await host.Client.SendAsync(request);

        var spans = capture.Activities.Where(a => a.TraceId == traceId && a.Source.Name == WebLogicInstruments.ActivitySourceName).ToList();
        var preRouting = Assert.Single(spans, a => a.OperationName == WebLogicInstruments.SecurityPreRoutingSpan);
        Assert.Equal("passed", preRouting.GetTagItem(WebLogicInstruments.SecurityOutcomeTag));
        var endpoint = Assert.Single(spans, a => a.OperationName == WebLogicInstruments.SecurityEndpointSpan);
        Assert.Equal("rejected", endpoint.GetTagItem(WebLogicInstruments.SecurityOutcomeTag));
        Assert.Equal("antiforgery", endpoint.GetTagItem(WebLogicInstruments.StageTag));
        Assert.Equal("csrf-token-missing", endpoint.GetTagItem(WebLogicInstruments.ReasonTag));
    }

    [Fact]
    public async Task TemplateRenders_AreTraced_ByTemplatePath()
    {
        await using var host = await SecurityTestSite.StartAsync();
        using var capture = new MetricsCapture(host);
        var name = "probe" + Guid.NewGuid().ToString("N")[..8];
        host.WriteContentFile($"theme/templates/{name}.html", "hello");

        var html = await host.Services.GetRequiredService<IWebTemplateRenderer>().RenderAsync(
            new TemplateRenderRequest { TemplatePath = "templates/" + name, IgnoreLayout = true }, default);

        Assert.Equal("hello", html);
        Assert.Contains(capture.Activities, a => a.OperationName == WebLogicInstruments.TemplateRenderSpan
            && ((string?)a.GetTagItem(WebLogicInstruments.TemplateTag))?.Contains(name, StringComparison.Ordinal) == true);
    }

    private static void AssertNothingPersonal(MetricsCapture capture, params string[] forbidden)
    {
        foreach (var measurement in capture.Measurements)
        {
            foreach (var (key, value) in measurement.Tags)
            {
                foreach (var word in forbidden)
                {
                    if (key == WebLogicInstruments.RouteTag && word.StartsWith('/'))
                        continue;
                    Assert.False(value?.Contains(word, StringComparison.Ordinal) == true,
                        $"{measurement.Instrument} tag {key}='{value}' contains '{word}'.");
                }
            }
        }
    }
}
