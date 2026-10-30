using CL.WebLogic.Auth.Accounts;
using CL.WebLogic.Auth.Passkeys;
using CL.WebLogic.Auth.TwoFactor;
using CL.WebLogic.Auth.Users;
using Microsoft.Extensions.DependencyInjection;

namespace CL.WebLogic.IntegrationTests.Auth;

/// <summary><see cref="WebAccountService.DeleteAccountAsync"/> removes the user and revokes everything WebLogic keeps.</summary>
public sealed class AccountDeletionTests
{
    [Fact]
    public async Task Deleting_an_account_signs_it_out_and_removes_its_credentials()
    {
        await using var host = await AuthTestSite.StartAsync();
        var services = host.Services;
        var user = await AuthTestSite.CreateUserAsync(host);
        await ((IWebUserExternalLoginStore)services.GetRequiredService<IWebUserStore>()).AddExternalLoginAsync(user.Id, "steam", "76561190000000001", default);
        await services.GetRequiredService<IWebPasskeyStore>().AddAsync(new WebPasskey
        {
            CredentialId = "cred-1", UserId = user.Id, UserHandle = "handle-1", PublicKey = [1, 2, 3]
        }, default);
        var secret = await AuthTestSite.EnableTwoFactorAsync(host, user);
        var codes = services.GetRequiredService<IWebRecoveryCodeStore>();
        Assert.True(await codes.CountAsync(user.Id, default) > 0);
        await AuthTestSite.LoginAsync(host.Client);
        Assert.Equal("Succeeded", await AuthTestSite.PostAsync(host.Client, "/auth/2fa", new { code = AuthTestSite.NextCode(secret) }));
        Assert.NotNull(await AuthTestSite.MeAsync(host.Client));

        var result = await services.GetRequiredService<WebAccountService>().DeleteAccountAsync(user.Id, "admin-1", default);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var users = services.GetRequiredService<IWebUserStore>();
        Assert.Null(await users.FindByIdAsync(user.Id, default));
        Assert.Null(await ((IWebUserExternalLoginStore)services.GetRequiredService<IWebUserStore>()).FindByExternalLoginAsync("steam", "76561190000000001", default));
        Assert.Empty(await services.GetRequiredService<IWebPasskeyStore>().ListAsync(user.Id, default));
        Assert.Equal(0, await codes.CountAsync(user.Id, default));
        Assert.Null(await AuthTestSite.MeAsync(host.Client));
    }

    [Fact]
    public async Task Unknown_accounts_are_not_found()
    {
        await using var host = await AuthTestSite.StartAsync();
        var result = await host.Services.GetRequiredService<WebAccountService>().DeleteAccountAsync("nobody", null, default);
        Assert.Equal("weblogic.auth.user_not_found", result.Error?.Code);
    }
}
