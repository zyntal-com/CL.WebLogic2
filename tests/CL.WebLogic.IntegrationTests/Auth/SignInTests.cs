using CL.WebLogic.Auth.Events;
using CL.WebLogic.Auth.Notifications;
using CL.WebLogic.Auth.TwoFactor;
using CL.WebLogic.Auth.Users;
using CL.WebLogic.Hosting.Services;
using CL.WebLogic.Testing.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CL.WebLogic.IntegrationTests.Auth;

public sealed class SignInTests
{
    [Fact]
    public async Task PasswordSignIn_SignsIn_WithPwdMethod()
    {
        await using var host = await AuthTestSite.StartAsync();
        await AuthTestSite.CreateUserAsync(host);

        Assert.Equal("Succeeded", await AuthTestSite.LoginAsync(host.Client));

        var me = (await AuthTestSite.MeAsync(host.Client))!.Value;
        Assert.Equal("session", me.GetProperty("scheme").GetString());
        Assert.Equal("pwd", me.GetProperty("methods").GetString());
    }

    [Fact]
    public async Task SignIn_ByEmail_AndCaseInsensitive()
    {
        await using var host = await AuthTestSite.StartAsync();
        await AuthTestSite.CreateUserAsync(host);

        Assert.Equal("Succeeded", await AuthTestSite.LoginAsync(host.Client, "ALICE@example.test"));
    }

    [Fact]
    public async Task WrongPassword_AndUnknownUser_LookTheSame()
    {
        await using var host = await AuthTestSite.StartAsync();
        await AuthTestSite.CreateUserAsync(host);

        Assert.Equal("InvalidCredentials", await AuthTestSite.LoginAsync(host.Client, "alice", "wrong password!"));
        Assert.Equal("InvalidCredentials", await AuthTestSite.LoginAsync(host.Client, "nobody", "wrong password!"));
        Assert.Null(await AuthTestSite.MeAsync(host.Client));
    }

    [Fact]
    public async Task RepeatedFailures_LockTheAccount_NotifyAndPublish()
    {
        await using var host = await AuthTestSite.StartAsync(c => c.Auth.Lockout.MaxFailedAttempts = 3);
        await AuthTestSite.CreateUserAsync(host);
        UserLockedOutEvent? locked = null;
        host.Context.Events.Subscribe<UserLockedOutEvent>(e => locked = e);

        Assert.Equal("InvalidCredentials", await AuthTestSite.LoginAsync(host.Client, "alice", "nope nope 1"));
        Assert.Equal("InvalidCredentials", await AuthTestSite.LoginAsync(host.Client, "alice", "nope nope 2"));
        Assert.Equal("LockedOut", await AuthTestSite.LoginAsync(host.Client, "alice", "nope nope 3"));
        Assert.Equal("LockedOut", await AuthTestSite.LoginAsync(host.Client)); // even the right password

        Assert.NotNull(locked);
        await AuthTestSite.WaitForMailAsync(host, WebSecurityNotificationKind.AccountLockedOut);
    }

    [Fact]
    public async Task Throttle_BacksOffPerLogin()
    {
        await using var host = await AuthTestSite.StartAsync(c =>
        {
            c.Auth.Lockout.FreeAttempts = 2;
            c.Auth.Lockout.MaxFailedAttempts = 0;
        });
        await AuthTestSite.CreateUserAsync(host);

        await AuthTestSite.LoginAsync(host.Client, "alice", "nope nope 1");
        await AuthTestSite.LoginAsync(host.Client, "alice", "nope nope 2");

        Assert.Equal("Throttled", await AuthTestSite.LoginAsync(host.Client));
    }

    [Fact]
    public async Task DisabledUsers_AndUnconfirmedEmails_AreNotAllowed()
    {
        await using var host = await AuthTestSite.StartAsync(c => c.Auth.RequireConfirmedEmail = true);
        await AuthTestSite.CreateUserAsync(host, "bob", u => u with { IsDisabled = true });
        await AuthTestSite.CreateUserAsync(host, "carol", u => u with { EmailConfirmed = false });

        Assert.Equal("NotAllowed", await AuthTestSite.LoginAsync(host.Client, "bob"));
        Assert.Equal("NotAllowed", await AuthTestSite.LoginAsync(host.Client, "carol"));
        Assert.Null(await AuthTestSite.MeAsync(host.Client));
    }

    [Fact]
    public async Task TwoFactor_RequiresTheCode_AndEachCodeWorksOnce()
    {
        await using var host = await AuthTestSite.StartAsync();
        var (_, secret) = await EnrollAsync(host);

        Assert.Equal("RequiresTwoFactor", await AuthTestSite.LoginAsync(host.Client));
        Assert.Null(await AuthTestSite.MeAsync(host.Client)); // pending is not signed in
        Assert.Equal("InvalidCredentials", await AuthTestSite.PostAsync(host.Client, "/auth/2fa", new { code = "000000" }));

        var code = Code(secret);
        Assert.Equal("Succeeded", await AuthTestSite.PostAsync(host.Client, "/auth/2fa", new { code }));
        Assert.Equal("pwd,otp", (await AuthTestSite.MeAsync(host.Client))!.Value.GetProperty("methods").GetString());

        // Replay of the same code from another browser is refused.
        var attacker = host.CreateClient();
        Assert.Equal("RequiresTwoFactor", await AuthTestSite.LoginAsync(attacker));
        Assert.Equal("InvalidCredentials", await AuthTestSite.PostAsync(attacker, "/auth/2fa", new { code }));
    }

    [Fact]
    public async Task TwoFactor_CannotBeCompletedWithoutThePasswordStep()
    {
        await using var host = await AuthTestSite.StartAsync();
        var (_, secret) = await EnrollAsync(host);

        Assert.Equal("InvalidCredentials", await AuthTestSite.PostAsync(host.Client, "/auth/2fa", new { code = Code(secret) }));
    }

    [Fact]
    public async Task RecoveryCodes_WorkOnce_AndNotify()
    {
        await using var host = await AuthTestSite.StartAsync();
        var (codes, _) = await EnrollAsync(host);

        await AuthTestSite.LoginAsync(host.Client);
        Assert.Equal("Succeeded", await AuthTestSite.PostAsync(host.Client, "/auth/recovery", new { code = codes[0].ToLowerInvariant() }));
        Assert.Equal("pwd,recovery", (await AuthTestSite.MeAsync(host.Client))!.Value.GetProperty("methods").GetString());
        await AuthTestSite.WaitForMailAsync(host, WebSecurityNotificationKind.RecoveryCodeUsed);

        var other = host.CreateClient();
        await AuthTestSite.LoginAsync(other);
        Assert.Equal("InvalidCredentials", await AuthTestSite.PostAsync(other, "/auth/recovery", new { code = codes[0] }));
    }

    [Fact]
    public async Task PendingSecondFactor_Expires()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        await using var host = await AuthTestSite.StartAsync(options: new WebLogicTestHostOptions
        {
            ConfigureServices = s => s.Replace(ServiceDescriptor.Singleton<TimeProvider>(clock))
        });
        var (codes, _) = await EnrollAsync(host);

        await AuthTestSite.LoginAsync(host.Client);
        clock.Advance(TimeSpan.FromMinutes(11));

        Assert.Equal("InvalidCredentials", await AuthTestSite.PostAsync(host.Client, "/auth/recovery", new { code = codes[0] }));
    }

    [Fact]
    public async Task RequiredPasswordChange_IsCompletedWithTheToken()
    {
        await using var host = await AuthTestSite.StartAsync();
        await AuthTestSite.CreateUserAsync(host, change: u => u with { RequiresPasswordChange = true });

        var response = await AuthTestSite.PostRawAsync(host.Client, "/auth/login", new { login = "alice", password = AuthTestSite.Password });
        var json = await System.Net.Http.Json.HttpContentJsonExtensions.ReadFromJsonAsync<System.Text.Json.JsonElement>(response.Content);
        Assert.Equal("RequiresPasswordChange", json.GetProperty("status").GetString());
        Assert.Null(await AuthTestSite.MeAsync(host.Client));

        var token = json.GetProperty("token").GetString();
        Assert.Equal("RequiresPasswordChange", await AuthTestSite.PostAsync(host.Client, "/auth/complete-change", new { token, newPassword = "short" }));
        Assert.Equal("Succeeded", await AuthTestSite.PostAsync(host.Client, "/auth/complete-change", new { token, newPassword = "a brand new passphrase" }));
        Assert.NotNull(await AuthTestSite.MeAsync(host.Client));
        Assert.Equal("Succeeded", await AuthTestSite.LoginAsync(host.CreateClient(), "alice", "a brand new passphrase"));
    }

    [Fact]
    public async Task SignOutEverywhere_EndsOtherSessions()
    {
        await using var host = await AuthTestSite.StartAsync();
        await AuthTestSite.CreateUserAsync(host);
        var phone = host.CreateClient();
        await AuthTestSite.LoginAsync(host.Client);
        await AuthTestSite.LoginAsync(phone);

        await AuthTestSite.PostAsync(host.Client, "/auth/logout?everywhere=1", new { });

        Assert.Null(await AuthTestSite.MeAsync(host.Client));
        Assert.Null(await AuthTestSite.MeAsync(phone));
    }

    [Fact]
    public async Task Refresh_PicksUpNewGroups()
    {
        await using var host = await AuthTestSite.StartAsync();
        var user = await AuthTestSite.CreateUserAsync(host);
        await AuthTestSite.LoginAsync(host.Client);

        await host.Services.GetRequiredService<IWebUserStore>().UpdateAsync(user with { Groups = ["editor"] }, default);
        Assert.Equal("", (await AuthTestSite.MeAsync(host.Client))!.Value.GetProperty("groups").GetString());

        await AuthTestSite.PostAsync(host.Client, "/auth/refresh", new { });
        Assert.Equal("editor", (await AuthTestSite.MeAsync(host.Client))!.Value.GetProperty("groups").GetString());
    }

    [Fact]
    public async Task SecurityStampChange_EndsSessionsAtTheNextCheck()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        await using var host = await AuthTestSite.StartAsync(c => c.Auth.SecurityStampCheckMinutes = 1, options: new WebLogicTestHostOptions
        {
            ConfigureServices = s => s.Replace(ServiceDescriptor.Singleton<TimeProvider>(clock))
        });
        var user = await AuthTestSite.CreateUserAsync(host);
        await AuthTestSite.LoginAsync(host.Client);

        await host.Services.GetRequiredService<IWebUserStore>().UpdateAsync(user with { IsDisabled = true }, default);
        clock.Advance(TimeSpan.FromMinutes(2));

        Assert.Null(await AuthTestSite.MeAsync(host.Client));
    }

    [Fact]
    public async Task Tenancy_UsersOfAnotherTenant_CannotSignIn()
    {
        await using var host = await AuthTestSite.StartAsync(web: w => w.UseHostTenants(new Dictionary<string, string> { ["127.0.0.1"] = "acme" }));
        await AuthTestSite.CreateUserAsync(host, "alice", u => u with { TenantId = "globex" });
        await AuthTestSite.CreateUserAsync(host, "bob", u => u with { TenantId = "acme" });

        Assert.Equal("InvalidCredentials", await AuthTestSite.LoginAsync(host.Client, "alice"));
        Assert.Equal("Succeeded", await AuthTestSite.LoginAsync(host.Client, "bob"));
    }

    private static async Task<(IReadOnlyList<string> Codes, string Secret)> EnrollAsync(WebLogicTestHost host)
    {
        var user = await AuthTestSite.CreateUserAsync(host);
        var twoFactor = host.Services.GetRequiredService<WebTwoFactorService>();
        var enrollment = twoFactor.BeginEnrollment(user);
        Assert.StartsWith("otpauth://totp/", enrollment.ProvisioningUri);
        Assert.StartsWith("data:image/png;base64,", enrollment.QrCodeDataUri);

        Assert.True((await twoFactor.ConfirmEnrollmentAsync(user, enrollment.EnrollmentToken, "123456", default)).IsFailure);
        var confirmed = await twoFactor.ConfirmEnrollmentAsync(user, enrollment.EnrollmentToken, EnrollmentCode(enrollment.Secret), default);
        Assert.True(confirmed.IsSuccess, confirmed.Error?.Message);
        Assert.Equal(10, confirmed.Value!.Count);
        await AuthTestSite.WaitForMailAsync(host, WebSecurityNotificationKind.TwoFactorEnabled);

        return (confirmed.Value, enrollment.Secret);
    }

    // Enrollment used the current time step; sign-ins use the next one (inside the ±1 window).
    private static string Code(string secret) => TotpCodes.At(secret, TotpCodes.CurrentStep + 1);

    private static string EnrollmentCode(string secret) => TotpCodes.At(secret, TotpCodes.CurrentStep);
}
