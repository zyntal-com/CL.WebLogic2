using System.Net;
using CL.WebLogic.Configuration.Models;
using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Hosting.Services;
using CL.WebLogic.MySql.Entities;
using CL.WebLogic.MySql.Services;
using CL.WebLogic.MySql.Stores;
using CL.WebLogic.Security.Audit;
using CL.WebLogic.Security.KeyRing;
using CL.WebLogic.Testing.Services;
using CL.WebLogic.Widgets.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.DependencyInjection;

namespace CL.WebLogic.MySql.Tests;

/// <summary>Login throttle, audit, widget settings and the DataProtection key ring.</summary>
[Collection(MySqlTestGroup.Name)]
public sealed class SupportStoreTests(MySqlFixture fixture)
{
    private static string Id() => Guid.NewGuid().ToString("N")[..12];

    [MySqlFact]
    public async Task The_login_throttle_backs_off_and_a_success_clears_it()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var config = new WebLogicConfig();
        config.Auth.Lockout.FreeAttempts = 2;
        var throttle = new MySqlWebLoginThrottle(fixture.Db, new WebLogicConfigHolder(config), clock);
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

    [MySqlFact]
    public async Task Concurrent_throttle_failures_are_all_counted_and_an_expired_counter_restarts()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var throttle = new MySqlWebLoginThrottle(fixture.Db, new WebLogicConfigHolder(new WebLogicConfig()), clock);
        var login = "dave" + Id();
        var key = "login:" + login.ToUpperInvariant();
        async Task<int> FailuresAsync() =>
            WebMySqlDatabase.Value(await fixture.Db.Repository<LoginThrottleRow>().GetByIdAsync(key, default))!.Failures;

        await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => throttle.RecordFailureAsync(login, null, default).AsTask()));
        Assert.Equal(10, await FailuresAsync());

        clock.Advance(TimeSpan.FromHours(2)); // past the window
        await throttle.RecordFailureAsync(login, null, default);
        Assert.Equal(1, await FailuresAsync());
    }

    [MySqlFact]
    public async Task Audit_rows_are_written_and_expire()
    {
        var sink = new MySqlWebAuditSink(fixture.Db);
        var category = "test." + Id();
        await sink.WriteAsync([
            new WebAuditEntry { Timestamp = DateTimeOffset.UtcNow.AddDays(-100), Category = category, Outcome = "old", Path = new string('x', 2000) },
            new WebAuditEntry { Timestamp = DateTimeOffset.UtcNow, Category = category, Outcome = "new", Data = new Dictionary<string, string> { ["k"] = "v" } }
        ], default);

        Assert.True(await sink.DeleteOlderThanAsync(DateTimeOffset.UtcNow.AddDays(-90), default) >= 1);
        var left = WebMySqlDatabase.Value(await fixture.Db.Query<AuditRow>().Where(a => a.Category == category).CountAsync(default));
        Assert.Equal(1, left);
    }

    [MySqlFact]
    public async Task Widget_settings_round_trip()
    {
        var store = new MySqlWebWidgetSettingsStore(fixture.Db);
        var owner = "user:" + Id();
        await store.SaveAsync(new WebWidgetSettings(owner, "w1", "clock", new Dictionary<string, string> { ["tz"] = "UTC" }, DateTimeOffset.UtcNow), default);
        await store.SaveAsync(new WebWidgetSettings(owner, "w1", "clock", new Dictionary<string, string> { ["tz"] = "CET" }, DateTimeOffset.UtcNow), default);
        Assert.Equal("CET", (await store.GetAsync(owner, "w1", default))!.Values["tz"]);
        Assert.Null(await store.GetAsync(owner, "w2", default));
    }

    [MySqlFact]
    public void Two_nodes_share_the_key_ring_and_keys_are_encrypted_at_rest()
    {
        IDataProtectionProvider Node()
        {
            var services = new ServiceCollection();
            services.AddDataProtection().SetApplicationName("mysql-tests-" + Environment.ProcessId)
                .ProtectKeysWithSecret(new WebKeyRingSecrets("a-key-ring-secret-that-is-long-enough-0123456789"));
            services.Configure<KeyManagementOptions>(o => o.XmlRepository = new MySqlXmlRepository(() => fixture.Db));
            return services.BuildServiceProvider().GetRequiredService<IDataProtectionProvider>();
        }

        fixture.ClearAsync<DataProtectionKeyRow>().GetAwaiter().GetResult();
        var token = Node().CreateProtector("t").Protect("hello cluster");
        Assert.Equal("hello cluster", Node().CreateProtector("t").Unprotect(token));
        var xml = WebMySqlDatabase.Value(fixture.Db.Query<DataProtectionKeyRow>().FirstOrDefaultAsync(default).GetAwaiter().GetResult())!.Xml;
        Assert.Contains("webLogicEncryptedKey", xml);
    }
}
