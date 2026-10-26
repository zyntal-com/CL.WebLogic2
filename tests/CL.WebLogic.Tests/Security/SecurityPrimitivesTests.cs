using System.Net;
using CL.WebLogic.Configuration.Models;
using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Runtime.Services;
using CL.WebLogic.Security.Identity;
using CL.WebLogic.Security.Models;
using CL.WebLogic.Security.Pipeline;
using CL.WebLogic.Security.RateLimiting;
using CL.WebLogic.Security.Services;
using CL.WebLogic.Testing.Services;

namespace CL.WebLogic.Tests.Security;

public sealed class SecurityPrimitivesTests
{
    [Theory]
    [InlineData("/dashboard?tab=1", "/dashboard?tab=1")]
    [InlineData("/", "/")]
    [InlineData("https://evil.test/", "/")]
    [InlineData("//evil.test/", "/")]
    [InlineData("/\\evil.test", "/")]
    [InlineData("\\\\evil.test", "/")]
    [InlineData("javascript:alert(1)", "/")]
    [InlineData("/ok\r\nSet-Cookie: x=1", "/")]
    [InlineData("", "/")]
    [InlineData(null, "/")]
    public void SafeReturnUrl_AcceptsOnlyLocalPaths(string? input, string expected) =>
        Assert.Equal(expected, WebUrl.SafeReturnUrl(input));

    [Theory]
    [InlineData(new[] { "posts.*" }, "posts.edit", true)]
    [InlineData(new[] { "posts.*" }, "postsx.edit", false)]
    [InlineData(new[] { "*" }, "anything", true)]
    [InlineData(new[] { "Posts.Edit" }, "posts.edit", true)]
    [InlineData(new[] { "posts.read" }, "posts.edit", false)]
    [InlineData(new string[0], "posts.edit", false)]
    public void PermissionMatcher(string[] granted, string required, bool expected) =>
        Assert.Equal(expected, WebPermissionMatcher.Grants(granted, required));

    [Theory]
    [InlineData("10.0.0.0/8", "10.200.3.4", true)]
    [InlineData("10.0.0.0/8", "11.0.0.1", false)]
    [InlineData("10.0.0.5", "10.0.0.5", true)]
    [InlineData("10.0.0.5", "::ffff:10.0.0.5", true)]
    [InlineData("2001:db8::/32", "2001:db8::1", true)]
    public void IpNetworks(string network, string address, bool expected)
    {
        Assert.True(IpNetworkParser.TryParse(network, out var parsed));
        Assert.Equal(expected, IpNetworkParser.ContainsAny([parsed], IPAddress.Parse(address)));
    }

    [Theory]
    [InlineData("not-an-ip")]
    [InlineData("10.0.0.0/99")]
    [InlineData("")]
    public void IpNetworks_RejectGarbage(string value) => Assert.False(IpNetworkParser.TryParse(value, out _));

    [Fact]
    public void HostMatcher_SupportsWildcards()
    {
        var matcher = new HostMatcher(["example.test", "*.example.test"]);
        Assert.True(matcher.IsAllowed("example.test"));
        Assert.True(matcher.IsAllowed("WWW.example.test"));
        Assert.False(matcher.IsAllowed("evilexample.test"));
        Assert.False(matcher.IsAllowed(".example.test"));
        Assert.True(new HostMatcher([]).IsAllowed("anything"));
    }

    [Fact]
    public void Csp_AddsSources_AndCannotBeInjected()
    {
        var csp = CspBuilder.Build(new ResponseHeadersConfig
        {
            ScriptSources = ["https://cdn.test"],
            ImageSources = ["https://img.test; script-src *"],
            AllowInlineStyleAttributes = true
        })!;

        Assert.Contains("script-src 'self' 'nonce-{nonce}' 'strict-dynamic' https://cdn.test", csp);
        Assert.Contains("style-src-attr 'unsafe-inline'", csp);
        Assert.Contains("img-src 'self' data: https://img.test script-src *", csp); // the ';' was stripped
        Assert.Null(CspBuilder.Build(new ResponseHeadersConfig { CspEnabled = false }));
    }

    [Fact]
    public void Csp_HasMediaWorkerManifestFormActionAndReportTo()
    {
        var headers = new ResponseHeadersConfig
        {
            MediaSources = ["https://media.test"],
            FormActions = ["https://pay.test"],
            CspReportTo = "https://reports.test/csp"
        };
        var csp = CspBuilder.Build(headers)!;

        Assert.Contains("media-src 'self' https://media.test", csp);
        Assert.DoesNotContain("worker-src", csp);   // nothing added: default-src 'self' applies
        Assert.Contains("form-action 'self' https://pay.test", csp);
        Assert.Contains("report-to csp-endpoint", csp);
        Assert.Equal("csp-endpoint=\"https://reports.test/csp\"", CspBuilder.ReportingEndpoints(headers));
        Assert.Null(CspBuilder.ReportingEndpoints(new ResponseHeadersConfig()));
    }

    [Fact]
    public void Csp_RouteAdditionsExtendOnlyThatPolicy()
    {
        var additions = new WebCspAdditions().AddFrameSource("https://www.youtube-nocookie.com").AddWorkerSource("blob:");
        var csp = CspBuilder.Build(new ResponseHeadersConfig(), additions.Sources)!;

        Assert.Contains("frame-src https://www.youtube-nocookie.com", csp);
        Assert.DoesNotContain("frame-src 'none'", csp);
        Assert.Contains("worker-src 'self' blob:", csp);
        Assert.Contains("frame-src 'none'", CspBuilder.Build(new ResponseHeadersConfig())!);
    }

    [Theory]
    [InlineData("https://ok.test", true)]
    [InlineData("https:", true)]
    [InlineData("'self'", true)]
    [InlineData("'unsafe-inline'", false)]
    [InlineData("'unsafe-eval'", false)]
    [InlineData("https://a.test; script-src *", false)]
    [InlineData("https://a.test https://b.test", false)]
    [InlineData("", false)]
    public void CspAdditions_OnlyAcceptPlainSources(string source, bool valid)
    {
        Assert.Equal(valid, WebCspAdditions.IsValidSource(source));
        if (!valid)
            Assert.Throws<ArgumentException>(() => new WebCspAdditions().AddFrameSource(source));
    }

    [Fact]
    public void StageOrderValidator_FindsDuplicatesWrongOrderAndWrongPhase()
    {
        Assert.Empty(StageOrderValidator.Validate([Stage("authentication", WebSecurityPhase.PreRouting, 500), Stage("authorization", WebSecurityPhase.Endpoint, 700)]));
        Assert.NotEmpty(StageOrderValidator.Validate([Stage("x", WebSecurityPhase.PreRouting, 1), Stage("X", WebSecurityPhase.Endpoint, 2)]));
        Assert.NotEmpty(StageOrderValidator.Validate([Stage("authorization", WebSecurityPhase.Endpoint, 900), Stage("antiforgery", WebSecurityPhase.Endpoint, 800)]));
        Assert.NotEmpty(StageOrderValidator.Validate([Stage("authorization", WebSecurityPhase.PreRouting, 700)]));
    }

    [Fact]
    public async Task RateLimitStore_RefillsOverTime()
    {
        var clock = new ManualTimeProvider();
        var store = new InMemoryRateLimitStore(new WebLogicConfigHolder(new WebLogicConfig()), clock);
        var rule = new RateLimitRule("t", 2, 1, RateLimitPartition.Ip);

        Assert.True((await store.TryAcquireAsync("k", rule, default)).Allowed);
        Assert.True((await store.TryAcquireAsync("k", rule, default)).Allowed);
        var denied = await store.TryAcquireAsync("k", rule, default);
        Assert.False(denied.Allowed);
        Assert.Equal(TimeSpan.FromSeconds(1), denied.RetryAfter);

        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.True((await store.TryAcquireAsync("k", rule, default)).Allowed);
    }

    [Fact]
    public async Task Regression_RateLimitStore_StaysBounded_Under100kAddresses()
    {
        var config = new WebLogicConfig();
        config.Security.RateLimits.MaxTrackedKeys = 10_000;
        var store = new InMemoryRateLimitStore(new WebLogicConfigHolder(config), new ManualTimeProvider());
        var rule = new RateLimitRule("t", 10, 1, RateLimitPartition.Ip);

        for (var i = 0; i < 100_000; i++)
            await store.TryAcquireAsync("ip:" + i, rule, default);

        Assert.True(store.Count <= 10_001, $"tracked {store.Count}");
    }

    [Fact]
    public async Task Regression_RateLimitStore_KeepsDrainedBuckets_WhenSprayed()
    {
        // Security review: spraying fresh keys used to evict a drained "login" bucket, resetting the limit.
        var config = new WebLogicConfig();
        config.Security.RateLimits.MaxTrackedKeys = 1_000;
        var store = new InMemoryRateLimitStore(new WebLogicConfigHolder(config), new ManualTimeProvider());
        var login = new RateLimitRule("login", 10, 10 / 300d, RateLimitPartition.Ip);
        var global = new RateLimitRule("global", 1400, 20, RateLimitPartition.Ip);
        for (var i = 0; i < 10; i++)
            await store.TryAcquireAsync("login:attacker", login, default);

        for (var i = 0; i < 5_000; i++)
            await store.TryAcquireAsync("global:spray-" + i, global, default);

        Assert.False((await store.TryAcquireAsync("login:attacker", login, default)).Allowed);
    }

    [Theory]
    [InlineData("203.0.113.7", "203.0.113.7")]
    [InlineData("::ffff:203.0.113.7", "203.0.113.7")]
    [InlineData("2001:db8:1:2:aaaa:bbbb:cccc:dddd", "2001:db8:1:2::/64")]
    [InlineData("2001:db8:1:2::1", "2001:db8:1:2::/64")]
    public void ClientAddressKey_GroupsIpv6ByPrefix(string address, string expected) =>
        Assert.Equal(expected, ClientAddressKey.For(System.Net.IPAddress.Parse(address)));

    [Fact]
    public void BoundedTtlCache_ExpiresAndStaysBounded()
    {
        var clock = new ManualTimeProvider();
        var cache = new BoundedTtlCache<int>(100, clock);
        cache.Set("a", 1, TimeSpan.FromMinutes(1));
        Assert.True(cache.TryGet("a", out var value) && value == 1);

        clock.Advance(TimeSpan.FromMinutes(2));
        Assert.False(cache.TryGet("a", out _));

        for (var i = 0; i < 1000; i++)
            cache.Set("k" + i, i, TimeSpan.FromHours(1));
        Assert.True(cache.Count <= 101);
    }

    [Fact]
    public void WebIdentity_BuildsPrincipal()
    {
        var identity = new WebIdentity { UserId = "u1", UserName = "Alice", Scheme = WebAuthenticationSchemes.Session, Groups = ["admin"], Permissions = ["posts.edit"] };

        Assert.True(identity.Principal.Identity!.IsAuthenticated);
        Assert.Equal("Alice", identity.Principal.Identity.Name);
        Assert.True(identity.Principal.IsInRole("admin"));
        Assert.False(WebIdentity.Anonymous.Principal.Identity!.IsAuthenticated);
    }

    private static FakeStage Stage(string name, WebSecurityPhase phase, int order) => new(name, phase, order);

    private sealed class FakeStage(string name, WebSecurityPhase phase, int order) : IWebSecurityStage
    {
        public string Name => name;

        public WebSecurityPhase Phase => phase;

        public int Order => order;

        public ValueTask<WebSecurityDecision> ExecuteAsync(WebSecurityContext context, CancellationToken cancellationToken) =>
            ValueTask.FromResult(WebSecurityDecision.Continue);
    }
}
