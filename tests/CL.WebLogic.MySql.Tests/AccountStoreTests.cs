using CL.WebLogic.Auth.ApiKeys;
using CL.WebLogic.Auth.Users;
using CL.WebLogic.MySql.Stores;
using CL.WebLogic.Security.Sessions;

namespace CL.WebLogic.MySql.Tests;

/// <summary>Users, external logins, sessions and API keys.</summary>
[Collection(MySqlTestGroup.Name)]
public sealed class AccountStoreTests(MySqlFixture fixture)
{
    private static string Id() => Guid.NewGuid().ToString("N")[..12];

    private static WebUser User(string name) => new() { Id = Id(), UserName = name, Email = name + "@Example.test" };

    [MySqlFact]
    public async Task Users_are_unique_by_name_and_email_regardless_of_case_and_found_by_either()
    {
        var store = new MySqlWebUserStore(fixture.Db);
        var name = "Alice" + Id();
        var alice = User(name) with { SuspendedUntil = DateTimeOffset.MaxValue, SuspensionReason = "test", Groups = ["a", "b"] };

        Assert.True((await store.CreateAsync(alice, default)).IsSuccess);
        Assert.False((await store.CreateAsync(User(name.ToUpperInvariant()), default)).IsSuccess);
        Assert.False((await store.CreateAsync(User("other" + Id()) with { Email = alice.Email!.ToLowerInvariant() }, default)).IsSuccess);

        var byName = await store.FindByLoginAsync(name.ToLowerInvariant(), default);
        Assert.Equal(alice.Id, byName?.Id);
        Assert.Equal(DateTimeOffset.MaxValue, byName?.SuspendedUntil);
        Assert.Equal(["a", "b"], byName!.Groups);
        Assert.Equal(alice.Id, (await store.FindByLoginAsync(alice.Email!.ToUpperInvariant(), default))?.Id);
        Assert.Equal(alice.Id, (await store.FindByEmailAsync(alice.Email, default))?.Id);
        Assert.Null(await store.FindByLoginAsync("nobody" + Id(), default));

        await store.UpdateAsync(byName with { IsDisabled = true }, default);
        Assert.True((await store.FindByIdAsync(alice.Id, default))!.IsDisabled);
    }

    [MySqlFact]
    public async Task Concurrent_failed_sign_ins_are_all_counted_and_lock_the_account()
    {
        var store = new MySqlWebUserStore(fixture.Db);
        var user = User("bob" + Id());
        await store.CreateAsync(user, default);
        var until = DateTimeOffset.UtcNow.AddMinutes(15);

        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => store.RecordFailedAccessAsync(user.Id, 5, until, default)));
        Assert.Equal(4, (await store.FindByIdAsync(user.Id, default))!.FailedAccessCount);

        var locked = await store.RecordFailedAccessAsync(user.Id, 5, until, default);
        Assert.Equal(0, locked!.FailedAccessCount);
        Assert.NotNull(locked.LockoutEnd);
        Assert.True(Math.Abs((locked.LockoutEnd!.Value - until).TotalSeconds) < 2);

        await store.ResetFailedAccessAsync(user.Id, default);
        Assert.Null((await store.FindByIdAsync(user.Id, default))!.LockoutEnd);
    }

    [MySqlFact]
    public async Task External_logins_link_to_users()
    {
        var store = new MySqlWebUserStore(fixture.Db);
        var user = User("steam" + Id());
        await store.CreateAsync(user, default);
        var steamId = "7656119" + Id();

        Assert.Null(await store.FindByExternalLoginAsync("Steam", steamId, default));
        await store.AddExternalLoginAsync(user.Id, "Steam", steamId, default);
        Assert.Equal(user.Id, (await store.FindByExternalLoginAsync("steam", steamId, default))?.Id);

        Assert.False(await store.RemoveExternalLoginAsync(user.Id, "discord", default));
        Assert.True(await store.RemoveExternalLoginAsync(user.Id, "STEAM", default));
        Assert.Null(await store.FindByExternalLoginAsync("steam", steamId, default));
        Assert.False(await store.RemoveExternalLoginAsync(user.Id, "steam", default));
    }

    [MySqlFact]
    public async Task Deleting_a_user_removes_the_row_and_its_external_logins()
    {
        var store = new MySqlWebUserStore(fixture.Db);
        var user = User("gone" + Id());
        await store.CreateAsync(user, default);
        var steamId = "7656119" + Id();
        await store.AddExternalLoginAsync(user.Id, "steam", steamId, default);

        Assert.True(await store.DeleteAsync(user.Id, default));
        Assert.Null(await store.FindByIdAsync(user.Id, default));
        Assert.Null(await store.FindByExternalLoginAsync("steam", steamId, default));
        Assert.False(await store.DeleteAsync(user.Id, default));
    }

    [MySqlFact]
    public async Task Sessions_round_trip_list_per_user_and_expire()
    {
        var store = new MySqlWebSessionStore(fixture.Db);
        var userId = Id();
        var now = DateTimeOffset.UtcNow;
        WebSessionRecord Session(DateTimeOffset expires) => new()
        {
            IdHash = Id() + Id(), CreatedAt = now, LastSeenAt = now, AbsoluteExpiresAt = expires, ExpiresAt = expires,
            CsrfSecret = "secret", UserId = userId, UserAgent = "Firefox", Persistent = true
        };
        var live = Session(now.AddHours(1));
        var old = Session(now.AddHours(-1));
        await store.SaveAsync(live, default);
        await store.SaveAsync(old, default);
        await store.SaveAsync(live with { LastSeenAt = now.AddMinutes(1) }, default);

        var loaded = await store.GetAsync(live.IdHash, default);
        Assert.Equal("Firefox", loaded?.UserAgent);
        Assert.True(loaded!.Persistent);
        Assert.Equal(2, (await store.ListForUserAsync(userId, default)).Count);

        Assert.True(await store.DeleteExpiredAsync(now, default) >= 1);
        Assert.Null(await store.GetAsync(old.IdHash, default));
        Assert.Equal(1, await store.DeleteForUserAsync(userId, default));
        Assert.Null(await store.GetAsync(live.IdHash, default));
    }

    [MySqlFact]
    public async Task Api_keys_are_found_by_prefix_and_listed_per_user()
    {
        var store = new MySqlWebApiKeyStore(fixture.Db);
        var userId = Id();
        var key = new WebApiKey { Id = Id(), Prefix = Id(), SecretHash = "hash", Name = "bot", UserId = userId, Scopes = ["read"], CreatedAt = DateTimeOffset.UtcNow };
        await store.CreateAsync(key, default);

        Assert.Equal(["read"], (await store.FindByPrefixAsync(key.Prefix, default))!.Scopes);
        await store.UpdateAsync(key with { RevokedAt = DateTimeOffset.UtcNow }, default);
        Assert.NotNull((await store.FindByIdAsync(key.Id, default))!.RevokedAt);
        await store.UpdateAsync(key with { LastUsedAt = DateTimeOffset.UtcNow }, default); // stale copy from a concurrent touch
        Assert.NotNull((await store.FindByIdAsync(key.Id, default))!.RevokedAt);
        Assert.Single(await store.ListAsync(userId, default));
        Assert.Contains(await store.ListAsync(null, default), k => k.Id == key.Id);
    }
}
