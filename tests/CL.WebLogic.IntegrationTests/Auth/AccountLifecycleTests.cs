using CL.WebLogic.Auth.Accounts;
using CL.WebLogic.Auth.Notifications;
using Microsoft.Extensions.DependencyInjection;

namespace CL.WebLogic.IntegrationTests.Auth;

public sealed class AccountLifecycleTests
{
    [Fact]
    public async Task Registration_SendsVerification_AndConfirmationEnablesSignIn()
    {
        await using var host = await AuthTestSite.StartAsync(c => c.Auth.RequireConfirmedEmail = true);
        var accounts = host.Services.GetRequiredService<WebAccountService>();

        var registered = await accounts.RegisterAsync(new WebRegistration { UserName = "dave", Email = "dave@example.test", Password = AuthTestSite.Password }, default);
        Assert.True(registered.IsSuccess);
        Assert.Equal("NotAllowed", await AuthTestSite.LoginAsync(host.Client, "dave"));

        var mail = await AuthTestSite.WaitForMailAsync(host, WebSecurityNotificationKind.EmailVerification);
        Assert.StartsWith("https://site.test/account/verify-email?token=", mail.Link);
        Assert.True((await accounts.ConfirmEmailAsync(AuthTestSite.TokenFrom(mail), default)).IsSuccess);
        Assert.True((await accounts.ConfirmEmailAsync(AuthTestSite.TokenFrom(mail), default)).IsFailure); // single use

        Assert.Equal("Succeeded", await AuthTestSite.LoginAsync(host.Client, "dave"));
    }

    [Fact]
    public async Task Registration_RejectsDuplicates_BadEmails_AndWeakPasswords()
    {
        await using var host = await AuthTestSite.StartAsync();
        var accounts = host.Services.GetRequiredService<WebAccountService>();
        await AuthTestSite.CreateUserAsync(host);

        Assert.Equal("weblogic.auth.duplicate_user", (await accounts.RegisterAsync(new WebRegistration { UserName = "ALICE", Password = AuthTestSite.Password }, default)).Error!.Code);
        // Security review: a user name equal to another account's e-mail (or the reverse) hijacked its logins.
        Assert.Equal("weblogic.auth.duplicate_user", (await accounts.RegisterAsync(new WebRegistration { UserName = "Alice@example.test", Password = AuthTestSite.Password }, default)).Error!.Code);
        Assert.True((await accounts.RegisterAsync(new WebRegistration { UserName = "bob@example.test", Password = AuthTestSite.Password }, default)).IsSuccess);
        Assert.Equal("weblogic.auth.duplicate_user", (await accounts.RegisterAsync(new WebRegistration { UserName = "carol", Email = "bob@example.test", Password = AuthTestSite.Password }, default)).Error!.Code);
        Assert.Equal("weblogic.auth.invalid_email", (await accounts.RegisterAsync(new WebRegistration { UserName = "x", Email = "not-an-email", Password = AuthTestSite.Password }, default)).Error!.Code);
        Assert.Equal("weblogic.auth.password_policy", (await accounts.RegisterAsync(new WebRegistration { UserName = "y", Password = "password123" }, default)).Error!.Code);
        Assert.Equal("weblogic.auth.password_policy", (await accounts.RegisterAsync(new WebRegistration { UserName = "zelda", Password = "zelda-is-my-name" }, default)).Error!.Code);
    }

    [Fact]
    public async Task PasswordReset_EndsAllSessions_AndTheLinkWorksOnce()
    {
        await using var host = await AuthTestSite.StartAsync();
        var accounts = host.Services.GetRequiredService<WebAccountService>();
        await AuthTestSite.CreateUserAsync(host);
        var laptop = host.CreateClient();
        await AuthTestSite.LoginAsync(laptop);

        await accounts.RequestPasswordResetAsync("alice@example.test", default);
        await accounts.RequestPasswordResetAsync("nobody@example.test", default); // silently nothing
        var mail = await AuthTestSite.WaitForMailAsync(host, WebSecurityNotificationKind.PasswordReset);
        Assert.StartsWith("https://site.test/account/reset-password?token=", mail.Link);
        var token = AuthTestSite.TokenFrom(mail);

        Assert.True((await accounts.ResetPasswordAsync(token, "123", default)).IsFailure); // policy, link not spent
        Assert.True((await accounts.ResetPasswordAsync(token, "my new passphrase", default)).IsSuccess);
        Assert.True((await accounts.ResetPasswordAsync(token, "another passphrase", default)).IsFailure);

        Assert.Null(await AuthTestSite.MeAsync(laptop));
        Assert.Equal("InvalidCredentials", await AuthTestSite.LoginAsync(host.Client));
        Assert.Equal("Succeeded", await AuthTestSite.LoginAsync(host.Client, "alice", "my new passphrase"));
        await AuthTestSite.WaitForMailAsync(host, WebSecurityNotificationKind.PasswordChanged);
        Assert.Single(host.Services.GetRequiredService<OutboxSecurityNotifier>().Messages, m => m.Kind == WebSecurityNotificationKind.PasswordReset);
    }

    [Fact]
    public async Task ResetLink_DiesWhenThePasswordChangesOtherwise()
    {
        await using var host = await AuthTestSite.StartAsync();
        var accounts = host.Services.GetRequiredService<WebAccountService>();
        var user = await AuthTestSite.CreateUserAsync(host);
        await accounts.RequestPasswordResetAsync("alice@example.test", default);
        var token = AuthTestSite.TokenFrom(await AuthTestSite.WaitForMailAsync(host, WebSecurityNotificationKind.PasswordReset));

        await accounts.SetPasswordAsync(user, "set by the admin now", viaReset: false, default);

        Assert.True((await accounts.ResetPasswordAsync(token, "my new passphrase", default)).IsFailure);
    }

    [Fact]
    public async Task ChangePassword_KeepsThisSession_EndsOthers()
    {
        await using var host = await AuthTestSite.StartAsync();
        await AuthTestSite.CreateUserAsync(host);
        var phone = host.CreateClient();
        await AuthTestSite.LoginAsync(host.Client);
        await AuthTestSite.LoginAsync(phone);

        Assert.Equal("weblogic.auth.wrong_password", await AuthTestSite.PostAsync(host.Client, "/auth/change-password", new { current = "wrong", newPassword = "my new passphrase" }));
        Assert.Equal("Changed", await AuthTestSite.PostAsync(host.Client, "/auth/change-password", new { current = AuthTestSite.Password, newPassword = "my new passphrase" }));

        Assert.NotNull(await AuthTestSite.MeAsync(host.Client));
        Assert.Null(await AuthTestSite.MeAsync(phone));
    }

    [Fact]
    public async Task Regression_ChangePassword_GuessesCountTowardsTheLockout()
    {
        // Security review: the current password was checked without throttle or lockout.
        await using var host = await AuthTestSite.StartAsync(c =>
        {
            c.Auth.Lockout.MaxFailedAttempts = 3;
            c.Auth.Lockout.FreeAttempts = 10;
        });
        await AuthTestSite.CreateUserAsync(host);
        await AuthTestSite.LoginAsync(host.Client);

        for (var i = 0; i < 3; i++)
            await AuthTestSite.PostAsync(host.Client, "/auth/change-password", new { current = "wrong guess " + i, newPassword = "my new passphrase" });

        Assert.Equal("weblogic.auth.locked_out", await AuthTestSite.PostAsync(host.Client, "/auth/change-password", new { current = AuthTestSite.Password, newPassword = "my new passphrase" }));
    }

    [Fact]
    public async Task MagicLink_SignsIn_Once()
    {
        await using var host = await AuthTestSite.StartAsync();
        await AuthTestSite.CreateUserAsync(host);
        await host.Services.GetRequiredService<WebAccountService>().RequestMagicLinkAsync("alice@example.test", default);
        var token = AuthTestSite.TokenFrom(await AuthTestSite.WaitForMailAsync(host, WebSecurityNotificationKind.MagicLink));

        Assert.Equal("Succeeded", await AuthTestSite.PostAsync(host.Client, "/auth/magic", new { token }));
        Assert.Equal("link", (await AuthTestSite.MeAsync(host.Client))!.Value.GetProperty("methods").GetString());
        Assert.Equal("InvalidCredentials", await AuthTestSite.PostAsync(host.CreateClient(), "/auth/magic", new { token }));
    }

    [Fact]
    public async Task MagicLink_ToAnUnconfirmedAddress_DropsThePasswordSetAtRegistration()
    {
        // Security review (account pre-hijacking): someone registers the victim's address with their
        // own password; when the victim proves the address with a magic link, that password must die.
        await using var host = await AuthTestSite.StartAsync();
        await AuthTestSite.CreateUserAsync(host, change: u => u with { EmailConfirmed = false });
        await host.Services.GetRequiredService<WebAccountService>().RequestMagicLinkAsync("alice@example.test", default);
        var token = AuthTestSite.TokenFrom(await AuthTestSite.WaitForMailAsync(host, WebSecurityNotificationKind.MagicLink));

        Assert.Equal("Succeeded", await AuthTestSite.PostAsync(host.Client, "/auth/magic", new { token }));
        Assert.Equal("InvalidCredentials", await AuthTestSite.LoginAsync(host.CreateClient()));
    }

    [Fact]
    public async Task EmailChange_IsConfirmedAtTheNewAddress_AndTheOldOneIsTold()
    {
        await using var host = await AuthTestSite.StartAsync();
        var accounts = host.Services.GetRequiredService<WebAccountService>();
        var user = await AuthTestSite.CreateUserAsync(host);

        Assert.True((await accounts.RequestEmailChangeAsync(user, "alice@new.test", default)).IsSuccess);
        var mail = await AuthTestSite.WaitForMailAsync(host, WebSecurityNotificationKind.EmailChangeConfirmation, "alice@new.test");

        var changed = await accounts.ConfirmEmailChangeAsync(AuthTestSite.TokenFrom(mail), default);
        Assert.Equal("alice@new.test", changed.Value!.Email);
        await AuthTestSite.WaitForMailAsync(host, WebSecurityNotificationKind.EmailChanged, "alice@example.test");
        Assert.Equal("Succeeded", await AuthTestSite.LoginAsync(host.Client, "alice@new.test"));
    }

    [Fact]
    public async Task Outbox_WritesEmlFiles()
    {
        await using var host = await AuthTestSite.StartAsync();
        await AuthTestSite.CreateUserAsync(host);
        await host.Services.GetRequiredService<WebAccountService>().RequestMagicLinkAsync("alice@example.test", default);
        await AuthTestSite.WaitForMailAsync(host, WebSecurityNotificationKind.MagicLink);

        var file = Directory.GetFiles(Path.Combine(host.Context.DataDirectory, "outbox"), "*MagicLink*.eml").Single();
        var eml = await File.ReadAllTextAsync(file);
        Assert.Contains("To: alice@example.test", eml);
        Assert.Contains("https://site.test/account/magic-link?token=", eml);
    }
}
