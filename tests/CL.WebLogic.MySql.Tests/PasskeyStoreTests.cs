using CL.WebLogic.Auth.Passkeys;
using CL.WebLogic.MySql.Stores;

namespace CL.WebLogic.MySql.Tests;

/// <summary>The MySQL passkey store: ownership, unique credential ids and the atomic counter.</summary>
[Collection(MySqlTestGroup.Name)]
public sealed class PasskeyStoreTests(MySqlFixture fixture)
{
    private static WebPasskey Passkey(string userId, string? id = null) => new()
    {
        CredentialId = id ?? "cred-" + Guid.NewGuid().ToString("N") + new string('x', 1200), // long ids (up to 1023 bytes) must fit
        UserId = userId,
        UserHandle = "handle-" + userId,
        PublicKey = [0xA5, 0x01, 0x02],
        Algorithm = WebCoseAlgorithm.ES256,
        Name = "Laptop",
        Transports = ["internal", "hybrid"],
        Aaguid = Guid.NewGuid(),
        BackupEligible = true,
        AttestationFormat = "packed",
        AttestationType = WebAttestationType.Self,
        CreatedAt = DateTimeOffset.UtcNow
    };

    [MySqlFact]
    public async Task Passkeys_round_trip_and_belong_to_one_user()
    {
        var store = new MySqlWebPasskeyStore(fixture.Db);
        var user = Guid.NewGuid().ToString("N")[..12];
        var passkey = Passkey(user);

        Assert.True(await store.AddAsync(passkey, default));
        Assert.False(await store.AddAsync(passkey with { UserId = "someone-else" }, default)); // one owner per credential

        var found = (await store.FindAsync(passkey.CredentialId, default))!;
        Assert.Equal((passkey.UserHandle, passkey.Algorithm, "Laptop", WebAttestationType.Self), (found.UserHandle, found.Algorithm, found.Name, found.AttestationType));
        Assert.Equal(passkey.PublicKey, found.PublicKey);
        Assert.Equal(["internal", "hybrid"], found.Transports);
        Assert.Single(await store.ListAsync(user, default));
        Assert.Null(await store.FindAsync("missing", default));

        Assert.False(await store.RenameAsync("someone-else", passkey.CredentialId, "Mine now", default));
        Assert.True(await store.RenameAsync(user, passkey.CredentialId, "Phone", default));
        Assert.Equal("Phone", (await store.FindAsync(passkey.CredentialId, default))!.Name);
        Assert.False(await store.DeleteAsync("someone-else", passkey.CredentialId, default));
        Assert.True(await store.DeleteAsync(user, passkey.CredentialId, default));
        Assert.Empty(await store.ListAsync(user, default));
    }

    [MySqlFact]
    public async Task A_counter_value_is_accepted_by_exactly_one_caller()
    {
        var store = new MySqlWebPasskeyStore(fixture.Db);
        var passkey = Passkey(Guid.NewGuid().ToString("N")[..12]);
        await store.AddAsync(passkey, default);
        var used = DateTimeOffset.UtcNow;

        var wins = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => store.TryRecordUseAsync(passkey.CredentialId, 0, 5, true, used, default)));
        Assert.Single(wins, w => w);
        var stored = (await store.FindAsync(passkey.CredentialId, default))!;
        Assert.Equal((5L, true), (stored.SignCount, stored.BackedUp));
        Assert.InRange((stored.LastUsedAt!.Value - used).TotalSeconds, -1, 1); // DATETIME keeps whole seconds (rounded)
        Assert.False(await store.TryRecordUseAsync(passkey.CredentialId, 0, 6, true, used, default)); // stale expectation
        Assert.True(await store.TryRecordUseAsync(passkey.CredentialId, 5, 6, false, used, default));
    }
}
