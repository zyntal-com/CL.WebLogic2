using System.Net.Http.Json;
using System.Text.Json;
using CL.WebLogic.Auth.Notifications;
using CL.WebLogic.Auth.Users;
using CL.WebLogic.Testing.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CL.WebLogic.IntegrationTests.Auth;

/// <summary>External sign-in with two-factor, "remember me" and the user's sessions as devices.</summary>
public sealed class SessionFeatureTests
{
    [Fact]
    public async Task Regression_external_sign_in_asks_two_factor_users_for_their_code()
    {
        // Wish list P1: an external provider used to sign two-factor users straight in.
        await using var host = await AuthTestSite.StartAsync();
        var user = await AuthTestSite.CreateUserAsync(host);
        var secret = await AuthTestSite.EnableTwoFactorAsync(host, user);

        Assert.Equal("RequiresTwoFactor", await AuthTestSite.PostAsync(host.Client, "/auth/external", new { login = "alice" }));
        Assert.Null(await AuthTestSite.MeAsync(host.Client));

        Assert.Equal("Succeeded", await AuthTestSite.PostAsync(host.Client, "/auth/2fa", new { code = AuthTestSite.NextCode(secret) }));
        Assert.Equal("ext:steam,otp", (await AuthTestSite.MeAsync(host.Client))!.Value.GetProperty("methods").GetString());
    }

    [Fact]
    public async Task External_sign_in_applies_the_account_rules()
    {
        await using var host = await AuthTestSite.StartAsync();
        await AuthTestSite.CreateUserAsync(host);
        await AuthTestSite.CreateUserAsync(host, "bob", u => u with { IsDisabled = true });

        Assert.Equal("NotAllowed", await AuthTestSite.PostAsync(host.Client, "/auth/external", new { login = "bob" }));
        Assert.Null(await AuthTestSite.MeAsync(host.Client));

        Assert.Equal("Succeeded", await AuthTestSite.PostAsync(host.Client, "/auth/external", new { login = "alice" }));
        var me = (await AuthTestSite.MeAsync(host.Client))!.Value;
        Assert.Equal("ext:steam", me.GetProperty("methods").GetString());
        Assert.True(me.GetProperty("authenticatedAt").GetInt64() > 0);
    }

    [Fact]
    public async Task Remember_me_sets_a_persistent_cookie_and_survives_the_second_factor()
    {
        await using var host = await AuthTestSite.StartAsync();
        var user = await AuthTestSite.CreateUserAsync(host);

        var plain = await AuthTestSite.PostRawAsync(host.CreateClient(), "/auth/login", new { login = "alice", password = AuthTestSite.Password });
        Assert.DoesNotContain("expires=", SessionCookie(plain), StringComparison.OrdinalIgnoreCase);

        var remembered = await AuthTestSite.PostRawAsync(host.CreateClient(), "/auth/login", new { login = "alice", password = AuthTestSite.Password, remember = true });
        Assert.Contains("expires=", SessionCookie(remembered), StringComparison.OrdinalIgnoreCase);

        var secret = await AuthTestSite.EnableTwoFactorAsync(host, user);
        var client = host.CreateClient();
        Assert.Equal("RequiresTwoFactor", await AuthTestSite.PostAsync(client, "/auth/login", new { login = "alice", password = AuthTestSite.Password, remember = true }));
        var completed = await AuthTestSite.PostRawAsync(client, "/auth/2fa", new { code = AuthTestSite.NextCode(secret) });
        Assert.Contains("expires=", SessionCookie(completed), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Signing_the_same_user_in_again_keeps_remember_me()
    {
        await using var host = await AuthTestSite.StartAsync();
        await AuthTestSite.CreateUserAsync(host);

        var remembered = host.CreateClient();
        await AuthTestSite.PostRawAsync(remembered, "/auth/login", new { login = "alice", password = AuthTestSite.Password, remember = true });
        Assert.Contains("expires=", SessionCookie(await AuthTestSite.PostRawAsync(remembered, "/auth/resign", new { })), StringComparison.OrdinalIgnoreCase);

        var plain = host.CreateClient();
        await AuthTestSite.PostRawAsync(plain, "/auth/login", new { login = "alice", password = AuthTestSite.Password });
        Assert.DoesNotContain("expires=", SessionCookie(await AuthTestSite.PostRawAsync(plain, "/auth/resign", new { })), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Users_list_their_sessions_and_end_another_one()
    {
        await using var host = await AuthTestSite.StartAsync();
        await AuthTestSite.CreateUserAsync(host);
        var laptop = Browser(host, "Laptop/1.0");
        var phone = Browser(host, "Phone/2.0");
        Assert.Equal("Succeeded", await AuthTestSite.LoginAsync(laptop));
        Assert.Equal("Succeeded", await AuthTestSite.LoginAsync(phone));

        var sessions = await SessionsAsync(laptop);
        Assert.Equal(2, sessions.Count);
        Assert.Single(sessions, s => s.GetProperty("isCurrent").GetBoolean() && s.GetProperty("userAgent").GetString() == "Laptop/1.0");
        var other = sessions.Single(s => !s.GetProperty("isCurrent").GetBoolean());
        Assert.Equal("Phone/2.0", other.GetProperty("userAgent").GetString());

        Assert.Equal("True", await AuthTestSite.PostAsync(laptop, "/auth/sessions/revoke", new { token = other.GetProperty("idHash").GetString() }));
        Assert.Null(await AuthTestSite.MeAsync(phone));
        Assert.NotNull(await AuthTestSite.MeAsync(laptop));
        Assert.Single(await SessionsAsync(laptop));
    }

    [Fact]
    public async Task A_user_cannot_end_someone_elses_session()
    {
        await using var host = await AuthTestSite.StartAsync();
        await AuthTestSite.CreateUserAsync(host);
        await AuthTestSite.CreateUserAsync(host, "bob");
        var alice = Browser(host, "A/1");
        var bob = Browser(host, "B/1");
        Assert.Equal("Succeeded", await AuthTestSite.LoginAsync(alice));
        Assert.Equal("Succeeded", await AuthTestSite.LoginAsync(bob, "bob"));
        var bobSession = (await SessionsAsync(bob)).Single().GetProperty("idHash").GetString();

        Assert.Equal("False", await AuthTestSite.PostAsync(alice, "/auth/sessions/revoke", new { token = bobSession }));
        Assert.NotNull(await AuthTestSite.MeAsync(bob));
    }

    [Fact]
    public async Task The_per_user_cap_ends_the_oldest_sessions()
    {
        await using var host = await AuthTestSite.StartAsync(c => c.Security.Session.MaxPerUser = 2);
        await AuthTestSite.CreateUserAsync(host);
        var first = Browser(host, "One/1");
        var second = Browser(host, "Two/1");
        var third = Browser(host, "Three/1");
        foreach (var client in new[] { first, second, third })
            Assert.Equal("Succeeded", await AuthTestSite.LoginAsync(client));

        Assert.Null(await AuthTestSite.MeAsync(first));
        Assert.NotNull(await AuthTestSite.MeAsync(second));
        Assert.NotNull(await AuthTestSite.MeAsync(third));
    }

    [Fact]
    public async Task New_device_sign_ins_are_mailed_when_enabled()
    {
        await using var host = await AuthTestSite.StartAsync(c => c.Auth.Notifications.NotifyNewDevice = true);
        await AuthTestSite.CreateUserAsync(host);

        Assert.Equal("Succeeded", await AuthTestSite.LoginAsync(Browser(host, "Laptop/1.0")));
        await AuthTestSite.WaitForMailAsync(host, WebSecurityNotificationKind.NewDeviceSignIn);
        Assert.Equal("Succeeded", await AuthTestSite.LoginAsync(Browser(host, "Laptop/1.0")));
        Assert.Equal("Succeeded", await AuthTestSite.LoginAsync(Browser(host, "Phone/2.0")));

        var outbox = host.Services.GetRequiredService<OutboxSecurityNotifier>();
        for (var i = 0; i < 100 && outbox.Messages.Count(m => m.Kind == WebSecurityNotificationKind.NewDeviceSignIn) < 2; i++)
            await Task.Delay(20);
        var notices = outbox.Messages.Where(m => m.Kind == WebSecurityNotificationKind.NewDeviceSignIn).ToList();
        Assert.Equal(2, notices.Count);
        // An unrecognised user agent is named generically: the raw header (attacker-controlled) never reaches the mail.
        Assert.Equal("an unknown browser", notices[^1].Values["device"]);
    }

    [Fact]
    public async Task A_browser_update_is_not_mailed_as_a_new_device_and_mails_name_the_browser()
    {
        const string chrome120 = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
        const string chrome131 = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.6778.86 Safari/537.36";
        const string firefox = "Mozilla/5.0 (X11; Linux x86_64; rv:121.0) Gecko/20100101 Firefox/121.0";
        await using var host = await AuthTestSite.StartAsync(c => c.Auth.Notifications.NotifyNewDevice = true);
        await AuthTestSite.CreateUserAsync(host);
        var outbox = host.Services.GetRequiredService<OutboxSecurityNotifier>();
        int Notices() => outbox.Messages.Count(m => m.Kind == WebSecurityNotificationKind.NewDeviceSignIn);

        Assert.Equal("Succeeded", await AuthTestSite.LoginAsync(Browser(host, chrome120)));
        await AuthTestSite.WaitForMailAsync(host, WebSecurityNotificationKind.NewDeviceSignIn);
        Assert.Equal("Chrome on Windows", outbox.Messages.Last(m => m.Kind == WebSecurityNotificationKind.NewDeviceSignIn).Values["device"]);

        Assert.Equal("Succeeded", await AuthTestSite.LoginAsync(Browser(host, chrome131))); // the same browser, updated
        Assert.Equal("Succeeded", await AuthTestSite.LoginAsync(Browser(host, firefox)));
        for (var i = 0; i < 100 && Notices() < 2; i++)
            await Task.Delay(20);
        await Task.Delay(100);
        Assert.Equal(2, Notices());
        Assert.Equal("Firefox on Linux", outbox.Messages.Last(m => m.Kind == WebSecurityNotificationKind.NewDeviceSignIn).Values["device"]);
    }

    private static HttpClient Browser(WebLogicTestHost host, string userAgent)
    {
        var client = host.CreateClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        return client;
    }

    private static async Task<List<JsonElement>> SessionsAsync(HttpClient client) =>
        [.. (await client.GetFromJsonAsync<JsonElement>("/auth/sessions")).EnumerateArray()];

    private static string SessionCookie(HttpResponseMessage response) =>
        response.Headers.GetValues("Set-Cookie").Last(c => c.Contains("wl_sid=", StringComparison.Ordinal));
}
