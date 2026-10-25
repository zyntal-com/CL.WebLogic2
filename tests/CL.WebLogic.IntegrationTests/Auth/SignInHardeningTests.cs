using CL.WebLogic.Auth.Throttling;
using CL.WebLogic.Auth.Users;
using CL.WebLogic.Results;
using CL.WebLogic.Routing.Models;
using CL.WebLogic.Routing.Services;
using CL.WebLogic.Testing.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CL.WebLogic.IntegrationTests.Auth;

/// <summary>Regression tests from the 4.9 security review.</summary>
public sealed class SignInHardeningTests
{
    [Fact]
    public async Task A_session_write_after_sign_out_does_not_restore_the_sign_in()
    {
        await using var host = await AuthTestSite.StartAsync(web: w => w.AddContributor(new DelegateContributor(b =>
            b.MapApi("/auth/logout-with-flash", async (r, ct) =>
            {
                await r.Auth.SignOutAsync(false, ct);
                r.Session.SetString("flash", "Signed out");
                return WebResult.Text("bye");
            }, "POST").AllowAnonymous()), new WebContributorDescriptor { Id = "tests.flash", Name = "Flash" }));
        await AuthTestSite.CreateUserAsync(host);
        Assert.Equal("Succeeded", await AuthTestSite.LoginAsync(host.Client));

        (await AuthTestSite.PostRawAsync(host.Client, "/auth/logout-with-flash", new { })).EnsureSuccessStatusCode();

        Assert.Null(await AuthTestSite.MeAsync(host.Client));
    }

    [Fact]
    public async Task A_locked_account_is_only_revealed_with_the_right_password()
    {
        await using var host = await AuthTestSite.StartAsync();
        await AuthTestSite.CreateUserAsync(host, change: u => u with { LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(10) });

        Assert.Equal("InvalidCredentials", await AuthTestSite.LoginAsync(host.Client, password: "wrong password!"));
        Assert.Equal("LockedOut", await AuthTestSite.LoginAsync(host.Client));
    }

    [Fact]
    public async Task A_concurrent_attempt_for_the_same_login_is_throttled()
    {
        await using var host = await AuthTestSite.StartAsync();
        await AuthTestSite.CreateUserAsync(host);
        var gate = host.Services.GetRequiredService<WebAttemptGate>();

        using (gate.TryEnter("pwd:alice"))
            Assert.Equal("Throttled", await AuthTestSite.LoginAsync(host.Client));
        Assert.Equal("Succeeded", await AuthTestSite.LoginAsync(host.Client));
    }

    [Fact]
    public async Task Refreshing_after_a_security_change_ends_the_other_sessions()
    {
        // Like enabling two-factor: the stamp changes and the same request refreshes the session.
        await using var host = await AuthTestSite.StartAsync(web: w => w.AddContributor(new DelegateContributor(b =>
            b.MapApi("/auth/new-stamp", async (r, ct) =>
            {
                var users = r.Services.GetRequiredService<IWebUserStore>();
                var current = (await users.FindByIdAsync(r.Identity.UserId!, ct))!;
                await users.UpdateAsync(current with { SecurityStamp = WebUser.NewStamp() }, ct);
                return WebResult.Text((await r.Auth.RefreshAsync(ct)).ToString());
            }, "POST")), new WebContributorDescriptor { Id = "tests.stamp", Name = "Stamp" }));
        await AuthTestSite.CreateUserAsync(host);
        var other = host.CreateClient();
        Assert.Equal("Succeeded", await AuthTestSite.LoginAsync(host.Client));
        Assert.Equal("Succeeded", await AuthTestSite.LoginAsync(other));

        var refreshed = await AuthTestSite.PostRawAsync(host.Client, "/auth/new-stamp", new { });
        Assert.Equal("True", await refreshed.Content.ReadAsStringAsync());

        Assert.NotNull(await AuthTestSite.MeAsync(host.Client));
        Assert.Null(await AuthTestSite.MeAsync(other));
    }

    [Fact]
    public void The_attempt_gate_admits_one_holder_per_key()
    {
        var gate = new WebAttemptGate();
        var first = gate.TryEnter("2fa:u1");
        Assert.NotNull(first);
        Assert.Null(gate.TryEnter("2FA:U1"));
        Assert.NotNull(gate.TryEnter("2fa:u2"));
        first!.Dispose();
        Assert.NotNull(gate.TryEnter("2fa:u1"));
    }
}
