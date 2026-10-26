using System.Net;
using CL.WebLogic.Security.Events;
using CL.WebLogic.Security.Rules;
using CL.WebLogic.Testing.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CL.WebLogic.IntegrationTests.Security;

/// <summary>App-managed IP rules: providers, caching, invalidation, expiry and rule ids.</summary>
public sealed class IpRuleProviderTests
{
    [Fact]
    public async Task A_provider_ban_applies_reports_its_id_and_is_lifted_by_invalidation()
    {
        var bans = new TestRules([new WebIpRule { Id = "ban-42", Network = "127.0.0.1", Reason = "spam" }]);
        await using var host = await StartAsync(bans);
        WebSecurityRejectedEvent? rejected = null;
        host.Context.Events.Subscribe<WebSecurityRejectedEvent>(e => rejected = e);

        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.GetAsync("/public")).StatusCode);
        Assert.Equal("ban-42", rejected?.RuleId);
        Assert.Equal("ip-rules", rejected?.Stage);

        bans.Rules = [];
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.GetAsync("/public")).StatusCode); // still cached
        host.Services.GetRequiredService<IWebSecurityRulesInvalidator>().Invalidate();
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/public")).StatusCode);
        Assert.Equal(1, bans.Loads - 1); // one reload after invalidation, not one per request
    }

    [Fact]
    public async Task Temporary_bans_end_on_their_own()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var bans = new TestRules([
            new WebIpRule { Id = "temp", Network = "127.0.0.0/8", ExpiresAt = clock.GetUtcNow().AddMinutes(5) },
            new WebIpRule { Id = "old", Network = "10.0.0.0/8", ExpiresAt = clock.GetUtcNow().AddMinutes(-1) }
        ]);
        await using var host = await StartAsync(bans, clock, cacheSeconds: 3600);

        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.GetAsync("/public")).StatusCode);
        clock.Advance(TimeSpan.FromMinutes(6));
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/public")).StatusCode);
        Assert.Equal(1, bans.Loads); // the rules were recompiled, not reloaded
    }

    [Fact]
    public async Task Exempt_addresses_pass_the_allow_list_but_not_a_deny_rule()
    {
        var trusted = new TestRules([new WebIpRule { Id = "office", Network = "127.0.0.1", Action = WebIpRuleAction.Exempt }]);
        await using (var host = await StartAsync(trusted, configure: c => c.Security.IpRules.Allow.Add("10.0.0.0/8")))
            Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/public")).StatusCode);

        await using var denied = await StartAsync(trusted, configure: c => c.Security.IpRules.Deny.Add("127.0.0.0/8"));
        WebSecurityRejectedEvent? rejected = null;
        denied.Context.Events.Subscribe<WebSecurityRejectedEvent>(e => rejected = e);
        Assert.Equal(HttpStatusCode.Forbidden, (await denied.Client.GetAsync("/public")).StatusCode);
        Assert.Equal("config.deny[0]", rejected?.RuleId);
    }

    [Fact]
    public async Task A_failing_provider_keeps_the_last_good_rules()
    {
        var bans = new TestRules([new WebIpRule { Id = "ban", Network = "127.0.0.1" }]);
        await using var host = await StartAsync(bans);
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.GetAsync("/public")).StatusCode);

        bans.Fail = true;
        host.Services.GetRequiredService<IWebSecurityRulesInvalidator>().Invalidate();
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.GetAsync("/public")).StatusCode);
    }

    private static Task<WebLogicTestHost> StartAsync(TestRules rules, ManualTimeProvider? clock = null, int cacheSeconds = 60, Action<Configuration.Models.WebLogicConfig>? configure = null) =>
        SecurityTestSite.StartAsync(
            c =>
            {
                c.Security.IpRules.ProviderCacheSeconds = cacheSeconds;
                configure?.Invoke(c);
            },
            web => web.Services.AddSingleton<IWebIpRuleProvider>(rules),
            clock is null ? null : new WebLogicTestHostOptions { ConfigureServices = s => s.Replace(ServiceDescriptor.Singleton<TimeProvider>(clock)) });

    private sealed class TestRules(IReadOnlyList<WebIpRule> rules) : IWebIpRuleProvider
    {
        public IReadOnlyList<WebIpRule> Rules { get; set; } = rules;

        public bool Fail { get; set; }

        public int Loads { get; private set; }

        public ValueTask<IReadOnlyList<WebIpRule>> GetRulesAsync(CancellationToken cancellationToken)
        {
            Loads++;
            return Fail ? throw new InvalidOperationException("database down") : ValueTask.FromResult(Rules);
        }
    }
}
