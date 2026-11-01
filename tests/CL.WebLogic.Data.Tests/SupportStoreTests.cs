using System.Net;
using CL.WebLogic.Configuration.Models;
using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Hosting.Services;
using CL.WebLogic.Data.Entities;
using CL.WebLogic.Data.Services;
using CL.WebLogic.Data.Stores;
using CL.WebLogic.Security.Audit;
using CL.WebLogic.Security.KeyRing;
using CL.WebLogic.Testing.Services;
using CL.WebLogic.Widgets.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.DependencyInjection;

namespace CL.WebLogic.Data.Tests;

/// <summary>Login throttle, audit, widget settings and the DataProtection key ring.</summary>
[Collection(DataTestGroup.Name)]
public sealed class SupportStoreTests(DataFixture fixture)
{
    private static string Id() => Guid.NewGuid().ToString("N")[..12];

    [Theory, MemberData(nameof(DataFixture.Providers), MemberType = typeof(DataFixture))]
    public async Task The_login_throttle_backs_off_and_a_success_clears_it(string provider)
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var config = new WebLogicConfig();
        config.Auth.Lockout.FreeAttempts = 2;
        var throttle = new DataWebLoginThrottle(fixture.Db(provider), new WebLogicConfigHolder(config), clock);
        var login = "carol" + Id();
        var ip = IPAddress.Parse("203.0.113." + Random.Shared.Next(1, 250));

        await throttle.RecordFailureAsync(login, ip, default);
        Assert.Null(await throttle.CheckAsync(login, ip, default)); // within the free attempts
        await throttle.RecordFailureAsync(login.ToUpperInvariant(), ip, default); // same login, any case
        Assert.NotNull(await throttle.CheckAsync(login, ip, default));

        clock.Advance(TimeSpan.FromSeconds(5));
        Assert.Null(await throttle.CheckAsync(login, ip, default));

        await throttle.RecordSuccessAsync(login, ip, default);
        await throttle.RecordFailureAsync(login, ip, default);
        Assert.Null(await throttle.CheckAsync(login, ip, default));
    }

    [Theory, MemberData(nameof(DataFixture.Providers), MemberType = typeof(DataFixture))]
    public async Task Concurrent_throttle_failures_are_all_counted_and_an_expired_counter_restarts(string provider)
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var throttle = new DataWebLoginThrottle(fixture.Db(provider), new WebLogicConfigHolder(new WebLogicConfig()), clock);
        var login = "dave" + Id();
        var key = "login:" + login.ToUpperInvariant();
        async Task<int> FailuresAsync() =>
            (await fixture.Db(provider).FindAsync<LoginThrottleRow>(t => t.ThrottleKey == key, default))!.Failures;

        await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => throttle.RecordFailureAsync(login, null, default).AsTask()));
        Assert.Equal(10, await FailuresAsync());

        clock.Advance(TimeSpan.FromHours(2)); // past the window
        await throttle.RecordFailureAsync(login, null, default);
        Assert.Equal(1, await FailuresAsync());
    }

    [Theory, MemberData(nameof(DataFixture.Providers), MemberType = typeof(DataFixture))]
    public async Task Audit_rows_are_written_and_expire(string provider)
    {
        var sink = new DataWebAuditSink(fixture.Db(provider));
        var category = "test." + Id();
        await sink.WriteAsync([
            new WebAuditEntry { Timestamp = DateTimeOffset.UtcNow.AddDays(-100), Category = category, Outcome = "old", Path = new string('x', 2000) },
            new WebAuditEntry { Timestamp = DateTimeOffset.UtcNow, Category = category, Outcome = "new", Data = new Dictionary<string, string> { ["k"] = "v" } }
        ], default);

        Assert.True(await sink.DeleteOlderThanAsync(DateTimeOffset.UtcNow.AddDays(-90), default) >= 1);
        var left = WebDataDatabase.Value(await fixture.Db(provider).Query<AuditRow>().Where(a => a.Category == category).CountAsync(default));
        Assert.Equal(1, left);
    }

    [Theory, MemberData(nameof(DataFixture.Providers), MemberType = typeof(DataFixture))]
    public async Task Widget_settings_round_trip(string provider)
    {
        var store = new DataWebWidgetSettingsStore(fixture.Db(provider));
        var owner = "user:" + Id();
        await store.SaveAsync(new WebWidgetSettings(owner, "w1", "clock", new Dictionary<string, string> { ["tz"] = "UTC" }, DateTimeOffset.UtcNow), default);
        await store.SaveAsync(new WebWidgetSettings(owner, "w1", "clock", new Dictionary<string, string> { ["tz"] = "CET" }, DateTimeOffset.UtcNow), default);
        Assert.Equal("CET", (await store.GetAsync(owner, "w1", default))!.Values["tz"]);
        Assert.Null(await store.GetAsync(owner, "w2", default));
    }

    [Theory, MemberData(nameof(DataFixture.Providers), MemberType = typeof(DataFixture))]
    public async Task Two_nodes_share_the_key_ring_and_keys_are_encrypted_at_rest(string provider)
    {
        IDataProtectionProvider Node()
        {
            var services = new ServiceCollection();
            services.AddDataProtection().SetApplicationName("data-tests-" + provider + "-" + Environment.ProcessId)
                .ProtectKeysWithSecret(new WebKeyRingSecrets("a-key-ring-secret-that-is-long-enough-0123456789"));
            services.Configure<KeyManagementOptions>(o => o.XmlRepository = new DataXmlRepository(() => fixture.Db(provider)));
            return services.BuildServiceProvider().GetRequiredService<IDataProtectionProvider>();
        }

        await fixture.ClearAsync<DataProtectionKeyRow>(provider);
        var token = Node().CreateProtector("t").Protect("hello cluster");
        Assert.Equal("hello cluster", Node().CreateProtector("t").Unprotect(token));
        var xml = (await fixture.Db(provider).FindAsync<DataProtectionKeyRow>(k => k.Name != "", default))!.Xml;
        Assert.Contains("webLogicEncryptedKey", xml);
    }
}
