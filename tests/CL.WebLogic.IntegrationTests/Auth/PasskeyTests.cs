using System.Net;
using System.Text.Json;
using CL.WebLogic.Auth.Events;
using CL.WebLogic.Auth.Notifications;
using CL.WebLogic.Auth.Users;
using CL.WebLogic.Testing.Services;
using Microsoft.Extensions.DependencyInjection;
using static CL.WebLogic.IntegrationTests.Auth.PasskeyTestSite;

namespace CL.WebLogic.IntegrationTests.Auth;

/// <summary>Passkey registration and sign-in through the full pipeline, with a software authenticator.</summary>
public sealed class PasskeyTests
{
    [Fact]
    public async Task A_registered_passkey_signs_in_without_a_password()
    {
        await using var host = await StartAsync();
        using var authenticator = await EnrolAsync(host);
        Assert.Null(await AuthTestSite.MeAsync(host.Client));

        Assert.Equal("Succeeded", await AssertAsync(host.Client, authenticator, remember: true));
        var me = (await AuthTestSite.MeAsync(host.Client))!.Value;
        Assert.Equal("passkey", me.GetProperty("methods").GetString());
        Assert.NotEqual(JsonValueKind.Null, me.GetProperty("authenticatedAt").ValueKind);

        var list = JsonDocument.Parse(await host.Client.GetStringAsync("/pk/list")).RootElement;
        Assert.Equal("Laptop", list[0].GetProperty("name").GetString());
        await AuthTestSite.WaitForMailAsync(host, WebSecurityNotificationKind.PasskeyAdded);
    }

    [Fact]
    public async Task A_passkey_is_multi_factor_so_totp_users_are_not_asked_for_a_code()
    {
        await using var host = await StartAsync();
        var user = await AuthTestSite.CreateUserAsync(host);
        var secret = await AuthTestSite.EnableTwoFactorAsync(host, user);
        using var authenticator = new WebTestAuthenticator(Origin);
        Assert.Equal("RequiresTwoFactor", await AuthTestSite.LoginAsync(host.Client));
        Assert.Equal("Succeeded", await AuthTestSite.PostAsync(host.Client, "/auth/2fa", new { code = AuthTestSite.NextCode(secret) }));
        Assert.StartsWith("Registered", await RegisterAsync(host.Client, authenticator));
        await AuthTestSite.PostAsync(host.Client, "/auth/logout", new { });

        Assert.Equal("Succeeded", await AssertAsync(host.Client, authenticator));
        Assert.Equal("passkey", (await AuthTestSite.MeAsync(host.Client))!.Value.GetProperty("methods").GetString());
    }

    [Fact]
    public async Task A_passkey_can_be_the_second_factor_after_a_password()
    {
        await using var host = await StartAsync();
        var user = await AuthTestSite.CreateUserAsync(host);
        var secret = await AuthTestSite.EnableTwoFactorAsync(host, user);
        using var authenticator = new WebTestAuthenticator(Origin) { UserVerified = false };
        await AuthTestSite.LoginAsync(host.Client);
        await AuthTestSite.PostAsync(host.Client, "/auth/2fa", new { code = AuthTestSite.NextCode(secret) });
        authenticator.UserVerified = true; // registration always verifies the user
        await RegisterAsync(host.Client, authenticator);
        await AuthTestSite.PostAsync(host.Client, "/auth/logout", new { });

        Assert.Equal("RequiresTwoFactor", await AuthTestSite.LoginAsync(host.Client));
        Assert.Equal("Succeeded", await AssertAsync(host.Client, authenticator, "2fa"));
        Assert.Equal("pwd,passkey", (await AuthTestSite.MeAsync(host.Client))!.Value.GetProperty("methods").GetString());

        // Without a pending password sign-in there is nothing to complete.
        var anonymous = host.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await AuthTestSite.PostRawAsync(anonymous, "/pk/2fa/begin", new { })).StatusCode);
    }

    [Fact]
    public async Task A_touch_only_security_key_is_a_second_factor_only_when_allowed()
    {
        await using var host = await StartAsync(c => c.Auth.Passkeys.AllowUnverifiedSecondFactor = true);
        var user = await AuthTestSite.CreateUserAsync(host);
        var secret = await AuthTestSite.EnableTwoFactorAsync(host, user);
        using var key = new WebTestAuthenticator(Origin);
        await AuthTestSite.LoginAsync(host.Client);
        await AuthTestSite.PostAsync(host.Client, "/auth/2fa", new { code = AuthTestSite.NextCode(secret) });
        await RegisterAsync(host.Client, key);
        await AuthTestSite.PostAsync(host.Client, "/auth/logout", new { });

        key.UserVerified = false;
        await AuthTestSite.LoginAsync(host.Client);
        var (options, state) = await BeginAsync(host.Client, "2fa");
        Assert.Equal("preferred", options.GetProperty("userVerification").GetString());
        Assert.Equal("Succeeded", await FinishAsync(host.Client, "2fa", key.Get(options), state));

        // Passwordless sign-in still requires verification.
        await AuthTestSite.PostAsync(host.Client, "/auth/logout", new { });
        (options, state) = await BeginAsync(host.Client, "signin");
        var unverified = key.Get(With(options, o => o.Remove("userVerification")));
        Assert.Equal("InvalidCredentials", await FinishAsync(host.Client, "signin", unverified, state));
    }

    [Fact]
    public async Task Ceremonies_cannot_be_replayed_or_moved_to_another_origin_or_site()
    {
        await using var host = await StartAsync();
        using var authenticator = await EnrolAsync(host);

        // Replaying a finished ceremony: the challenge is spent.
        var (options, state) = await BeginAsync(host.Client, "signin");
        var assertion = authenticator.Get(options);
        Assert.Equal("Succeeded", await FinishAsync(host.Client, "signin", assertion, state));
        await AuthTestSite.PostAsync(host.Client, "/auth/logout", new { });
        Assert.Equal("InvalidCredentials", await FinishAsync(host.Client, "signin", assertion, state));

        // A phishing page on another origin.
        authenticator.Origin = "https://site-login.test";
        Assert.Equal("InvalidCredentials", await AssertAsync(host.Client, authenticator));
        authenticator.Origin = Origin;

        // An answer to a different challenge, a tampered signature, a forged state.
        (options, state) = await BeginAsync(host.Client, "signin");
        var (other, _) = await BeginAsync(host.Client, "signin");
        Assert.Equal("InvalidCredentials", await FinishAsync(host.Client, "signin", authenticator.Get(other), state));
        (options, state) = await BeginAsync(host.Client, "signin");
        var tampered = With(authenticator.Get(options), o => o["response"]!["signature"] = "MEUCIQ" + new string('A', 90));
        Assert.Equal("InvalidCredentials", await FinishAsync(host.Client, "signin", tampered, state));
        (options, _) = await BeginAsync(host.Client, "signin");
        Assert.Equal("InvalidCredentials", await FinishAsync(host.Client, "signin", authenticator.Get(options), "forged-state"));

        // A sign-in state cannot finish a step-up, and the other way round.
        Assert.Equal("Succeeded", await AssertAsync(host.Client, authenticator));
        (options, state) = await BeginAsync(host.Client, "reauth");
        Assert.Equal("InvalidCredentials", await FinishAsync(host.Client, "signin", authenticator.Get(options), state));
    }

    [Fact]
    public async Task Registration_checks_the_site_the_user_and_the_answer()
    {
        await using var host = await StartAsync();
        await AuthTestSite.CreateUserAsync(host);
        Assert.Equal(HttpStatusCode.Unauthorized, (await AuthTestSite.PostRawAsync(host.Client, "/pk/register/begin", new { })).StatusCode);
        await AuthTestSite.LoginAsync(host.Client);
        using var authenticator = new WebTestAuthenticator(Origin);

        var (options, state) = await BeginAsync(host.Client, "register");
        Assert.Equal("site.test", options.GetProperty("rp").GetProperty("id").GetString());
        Assert.Equal("required", options.GetProperty("authenticatorSelection").GetProperty("residentKey").GetString());
        var forOtherSite = authenticator.Create(With(options, o => o["rp"]!["id"] = "evil.test"));
        Assert.StartsWith("Failed", await FinishAsync(host.Client, "register", forOtherSite, state));

        // Another user's state.
        var bob = host.CreateClient();
        await AuthTestSite.CreateUserAsync(host, "bob");
        await AuthTestSite.LoginAsync(bob, "bob");
        (options, state) = await BeginAsync(host.Client, "register");
        Assert.Equal("Failed:The request expired; try again.", await FinishAsync(bob, "register", authenticator.Create(options), state));

        // Re-registering the same authenticator is excluded.
        Assert.StartsWith("Registered", await RegisterAsync(host.Client, authenticator));
        (options, _) = await BeginAsync(host.Client, "register");
        Assert.Single(options.GetProperty("excludeCredentials").EnumerateArray());
        Assert.Throws<InvalidOperationException>(() => authenticator.Create(options));
    }

    [Theory]
    [InlineData(WebTestAttestation.PackedSelf)]
    [InlineData(WebTestAttestation.PackedCertificate)]
    [InlineData(WebTestAttestation.FidoU2f)]
    public async Task Attested_registrations_are_verified_and_unverifiable_ones_follow_the_policy(WebTestAttestation attestation)
    {
        await using var host = await StartAsync(c => c.Auth.Passkeys.RequestAttestation = true);
        await AuthTestSite.CreateUserAsync(host);
        await AuthTestSite.LoginAsync(host.Client);
        using var authenticator = new WebTestAuthenticator(Origin) { Attestation = attestation };
        var (options, state) = await BeginAsync(host.Client, "register");
        Assert.Equal("direct", options.GetProperty("attestation").GetString());
        Assert.StartsWith("Registered", await FinishAsync(host.Client, "register", authenticator.Create(options), state));
    }

    [Fact]
    public async Task A_counter_that_goes_backwards_is_refused_as_a_possible_clone()
    {
        await using var host = await StartAsync();
        using var authenticator = await EnrolAsync(host);
        authenticator.SignCountStep = 1;
        var clones = new List<PasskeyCloneSuspectedEvent>();
        host.Context.Events.Subscribe<PasskeyCloneSuspectedEvent>(e => clones.Add(e));

        Assert.Equal("Succeeded", await AssertAsync(host.Client, authenticator));
        Assert.Equal("Succeeded", await AssertAsync(host.Client, authenticator));
        authenticator.Credentials[0].SignCount = 0; // a copy that lags behind
        Assert.Equal("InvalidCredentials", await AssertAsync(host.Client, authenticator));
        Assert.Single(clones);
        Assert.Equal((2L, 1L), (clones[0].StoredSignCount, clones[0].ReceivedSignCount));
    }

    [Fact]
    public async Task Account_rules_apply_but_password_lockout_does_not()
    {
        await using var host = await StartAsync(c => c.Auth.Lockout.MaxFailedAttempts = 3);
        using var authenticator = await EnrolAsync(host);
        var users = host.Services.GetRequiredService<IWebUserStore>();
        var alice = (await users.FindByLoginAsync("alice", default))!;

        await users.UpdateAsync(alice with { LockoutEnd = DateTimeOffset.UtcNow.AddHours(1), FailedAccessCount = 3 }, default);
        Assert.Equal("LockedOut", await AuthTestSite.LoginAsync(host.Client));
        Assert.Equal("Succeeded", await AssertAsync(host.Client, authenticator));
        Assert.Equal(0, (await users.FindByLoginAsync("alice", default))!.FailedAccessCount);
        await AuthTestSite.PostAsync(host.Client, "/auth/logout", new { });

        alice = (await users.FindByLoginAsync("alice", default))!;
        await users.UpdateAsync(alice with { SuspendedUntil = DateTimeOffset.UtcNow.AddDays(1), SuspensionReason = "Spam" }, default);
        Assert.Equal("Suspended:Spam", await AssertAsync(host.Client, authenticator));
        await users.UpdateAsync(alice with { IsDisabled = true }, default);
        Assert.Equal("NotAllowed", await AssertAsync(host.Client, authenticator));
        Assert.Null(await AuthTestSite.MeAsync(host.Client));
    }

    [Fact]
    public async Task Failed_passkey_attempts_never_count_towards_lockout()
    {
        await using var host = await StartAsync(c => c.Auth.Lockout.MaxFailedAttempts = 2);
        using var authenticator = await EnrolAsync(host);
        authenticator.Origin = "https://wrong.test";
        for (var i = 0; i < 4; i++)
            Assert.NotEqual("Succeeded", await AssertAsync(host.Client, authenticator));

        var alice = (await host.Services.GetRequiredService<IWebUserStore>().FindByLoginAsync("alice", default))!;
        Assert.Equal((0, null), (alice.FailedAccessCount, alice.LockoutEnd));
        Assert.Equal("Succeeded", await AuthTestSite.LoginAsync(host.Client));
    }

    [Fact]
    public async Task Step_up_with_a_passkey_refreshes_the_authentication_time()
    {
        await using var host = await StartAsync();
        using var authenticator = await EnrolAsync(host, signOut: false);
        var before = (await AuthTestSite.MeAsync(host.Client))!.Value.GetProperty("authenticatedAt").GetInt64();
        await Task.Delay(1100);

        Assert.Equal("Succeeded", await AssertAsync(host.Client, authenticator, "reauth"));
        var me = (await AuthTestSite.MeAsync(host.Client))!.Value;
        Assert.Equal("passkey", me.GetProperty("methods").GetString());
        Assert.True(me.GetProperty("authenticatedAt").GetInt64() > before);
    }

    [Fact]
    public async Task Step_up_any_of_accepts_a_password_with_a_code_or_a_passkey()
    {
        await using var host = await StartAsync();
        using var authenticator = await EnrolAsync(host, signOut: false);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/pk/admin")).StatusCode); // password only

        var user = (await host.Services.GetRequiredService<IWebUserStore>().FindByLoginAsync("alice", default))!;
        var secret = await AuthTestSite.EnableTwoFactorAsync(host, user);
        Assert.Equal("Succeeded", await AuthTestSite.PostAsync(host.Client, "/auth/reauth", new { password = AuthTestSite.Password, code = AuthTestSite.NextCode(secret) }));
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/pk/admin")).StatusCode); // pwd + otp

        Assert.Equal("SignedOut", await AuthTestSite.PostAsync(host.Client, "/auth/logout", new { }));
        Assert.Equal("Succeeded", await AssertAsync(host.Client, authenticator));
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/pk/admin")).StatusCode); // passkey
    }

    [Fact]
    public async Task One_users_passkey_cannot_prove_another_user()
    {
        await using var host = await StartAsync();
        using var alicesKey = await EnrolAsync(host);
        var bob = await AuthTestSite.CreateUserAsync(host, "bob");
        var secret = await AuthTestSite.EnableTwoFactorAsync(host, bob);
        var bobs = host.CreateClient();

        // Bob without passkeys: nothing to offer as a second factor.
        Assert.Equal("RequiresTwoFactor", await AuthTestSite.LoginAsync(bobs, "bob"));
        Assert.Equal(HttpStatusCode.BadRequest, (await AuthTestSite.PostRawAsync(bobs, "/pk/2fa/begin", new { })).StatusCode);

        // Bob registers his own; Alice's authenticator then answers Bob's challenge.
        await AuthTestSite.PostAsync(bobs, "/auth/2fa", new { code = AuthTestSite.NextCode(secret) });
        using var bobsKey = new WebTestAuthenticator(Origin);
        await RegisterAsync(bobs, bobsKey);
        await AuthTestSite.PostAsync(bobs, "/auth/logout", new { });
        Assert.Equal("RequiresTwoFactor", await AuthTestSite.LoginAsync(bobs, "bob"));
        var (options, state) = await BeginAsync(bobs, "2fa");
        var alicesAnswer = alicesKey.Get(With(options, o => o.Remove("allowCredentials")));
        Assert.Equal("InvalidCredentials", await FinishAsync(bobs, "2fa", alicesAnswer, state));

        // Step-up for alice cannot be answered with bob's passkey either.
        Assert.Equal("Succeeded", await AssertAsync(host.Client, alicesKey));
        (options, state) = await BeginAsync(host.Client, "reauth");
        Assert.Equal("InvalidCredentials", await FinishAsync(host.Client, "reauth", bobsKey.Get(With(options, o => o.Remove("allowCredentials"))), state));
    }

    [Fact]
    public async Task Passkeys_can_be_renamed_and_removed_and_removed_ones_stop_working()
    {
        await using var host = await StartAsync();
        using var authenticator = await EnrolAsync(host, signOut: false);
        var id = JsonDocument.Parse(await host.Client.GetStringAsync("/pk/list")).RootElement[0].GetProperty("credentialId").GetString();

        Assert.Equal("True", await AuthTestSite.PostAsync(host.Client, "/pk/rename", new { state = id, name = "  Phone\u0007 " }));
        Assert.Equal("Phone", JsonDocument.Parse(await host.Client.GetStringAsync("/pk/list")).RootElement[0].GetProperty("name").GetString());
        Assert.Equal("False", await AuthTestSite.PostAsync(host.Client, "/pk/delete", new { state = "not-mine" }));
        var users = host.Services.GetRequiredService<IWebUserStore>();
        var stamp = (await users.FindByLoginAsync("alice", default))!.SecurityStamp;
        Assert.Equal("True", await AuthTestSite.PostAsync(host.Client, "/pk/delete", new { state = id }));
        await AuthTestSite.WaitForMailAsync(host, WebSecurityNotificationKind.PasskeyRemoved);
        // Security review: sessions signed in with a lost passkey survived its removal. The stamp
        // changes (other sessions end at their next check); this session is kept.
        Assert.NotEqual(stamp, (await users.FindByLoginAsync("alice", default))!.SecurityStamp);
        Assert.NotNull(await AuthTestSite.MeAsync(host.Client));

        await AuthTestSite.PostAsync(host.Client, "/auth/logout", new { });
        var (options, state) = await BeginAsync(host.Client, "signin");
        Assert.Equal("InvalidCredentials", await FinishAsync(host.Client, "signin", authenticator.Get(options), state));
    }

    [Fact]
    public async Task Turning_passkeys_off_refuses_new_ceremonies()
    {
        await using var host = await StartAsync(c => c.Auth.Passkeys.Enabled = false);
        Assert.Equal(HttpStatusCode.BadRequest, (await AuthTestSite.PostRawAsync(host.Client, "/pk/signin/begin", new { })).StatusCode);
    }
}
