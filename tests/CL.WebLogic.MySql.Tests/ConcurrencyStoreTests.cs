using CL.WebLogic.Configuration.Models;
using CL.WebLogic.Dashboard.Models;
using CL.WebLogic.MySql.Stores;
using CL.WebLogic.Security.RateLimiting;
using CL.WebLogic.Testing.Services;

namespace CL.WebLogic.MySql.Tests;

/// <summary>The stores whose whole point is that only one caller wins, on any number of nodes.</summary>
[Collection(MySqlTestGroup.Name)]
public sealed class ConcurrencyStoreTests(MySqlFixture fixture)
{
    private static string Id() => Guid.NewGuid().ToString("N")[..12];

    [MySqlFact]
    public async Task A_recovery_code_is_consumed_exactly_once()
    {
        var store = new MySqlWebRecoveryCodeStore(fixture.Db);
        var userId = Id();
        await store.ReplaceAsync(userId, ["h1", "h2", "h3"], default);
        Assert.Equal(3, await store.CountAsync(userId, default));

        var wins = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => store.TryConsumeAsync(userId, "h2", default)));
        Assert.Single(wins, w => w);
        Assert.Equal(2, await store.CountAsync(userId, default));
        Assert.False(await store.TryConsumeAsync(Id(), "h1", default)); // another user's code

        await store.ReplaceAsync(userId, ["n1"], default);
        Assert.Equal(1, await store.CountAsync(userId, default));
    }

    [MySqlFact]
    public async Task A_one_time_key_is_used_once_until_it_expires()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var store = new MySqlWebOneTimeUseStore(fixture.Db, clock);
        var key = "totp:" + Id() + ":123456";

        var wins = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => store.TryUseAsync(key, clock.GetUtcNow().AddMinutes(2), default).AsTask()));
        Assert.Single(wins, w => w);
        Assert.False(await store.TryUseAsync(key, clock.GetUtcNow().AddMinutes(2), default));
        Assert.False(await store.TryUseAsync(Id(), clock.GetUtcNow().AddSeconds(-1), default)); // already expired

        clock.Advance(TimeSpan.FromMinutes(3));
        Assert.True(await store.TryUseAsync(key, clock.GetUtcNow().AddMinutes(2), default));
    }

    [MySqlFact]
    public async Task A_token_bucket_allows_exactly_its_capacity_under_concurrency_and_refills()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var store = new MySqlWebRateLimitStore(fixture.Db, clock);
        var rule = new RateLimitRule("test", 5, 1, RateLimitPartition.Ip);
        var key = "ip:" + Id();

        var decisions = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => store.TryAcquireAsync(key, rule, default).AsTask()));
        Assert.Equal(5, decisions.Count(d => d.Allowed));
        Assert.All(decisions.Where(d => !d.Allowed), d => Assert.True(d.RetryAfter > TimeSpan.Zero));
        Assert.Contains(decisions, d => d.Allowed && d.Remaining == 0);

        clock.Advance(TimeSpan.FromSeconds(2));
        var refilled = await store.TryAcquireAsync(key, rule, default);
        Assert.True(refilled.Allowed);
        Assert.Equal(1, refilled.Remaining);
        Assert.True((await store.TryAcquireAsync("ip:" + Id(), rule, default)).Allowed); // another client
    }

    [MySqlFact]
    public async Task Only_one_of_two_editors_saving_the_same_version_wins()
    {
        var store = new MySqlWebDashboardLayoutStore(fixture.Db);
        var layout = new WebDashboardLayout { OwnerKey = "user:" + Id(), DashboardKey = "home", Items = [new WebDashboardItem { InstanceId = "a", WidgetName = "clock" }] };

        var created = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => store.SaveAsync(layout, 0, default).AsTask()));
        Assert.Single(created, r => r.IsSuccess);
        Assert.Equal(1, (await store.GetAsync(layout.OwnerKey, "home", default))!.Version);

        var updates = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => store.SaveAsync(layout, 1, default).AsTask()));
        Assert.Single(updates, r => r.IsSuccess);
        Assert.Equal(2, (await store.GetAsync(layout.OwnerKey, "home", default))!.Version);
        Assert.False((await store.SaveAsync(layout, 1, default)).IsSuccess); // stale
    }
}
