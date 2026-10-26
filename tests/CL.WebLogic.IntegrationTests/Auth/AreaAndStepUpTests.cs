using System.Net;
using CL.WebLogic.Results;
using CL.WebLogic.Routing.Models;
using CL.WebLogic.Routing.Services;
using CL.WebLogic.Testing.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CL.WebLogic.IntegrationTests.Auth;

/// <summary>Per-area login and access-denied pages, and step-up (recent sign-in) requirements.</summary>
public sealed class AreaAndStepUpTests
{
    [Fact]
    public async Task An_area_sends_visitors_to_its_own_login_page()
    {
        await using var host = await StartAsync();
        var browser = host.CreateClient();

        var admin = await browser.GetAsync("/admin/orders?page=2");
        Assert.Equal(HttpStatusCode.Redirect, admin.StatusCode);
        Assert.Equal("/admin/login?returnUrl=%2Fadmin%2Forders%3Fpage%3D2", admin.Headers.Location!.OriginalString);

        var site = await browser.GetAsync("/members");
        Assert.StartsWith("/login?returnUrl=", site.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Signed_in_users_without_access_go_to_the_areas_denied_page()
    {
        await using var host = await StartAsync();
        await AuthTestSite.CreateUserAsync(host);
        Assert.Equal("Succeeded", await AuthTestSite.LoginAsync(host.Client));

        var page = await host.Client.GetAsync("/admin/orders");
        Assert.Equal(HttpStatusCode.Redirect, page.StatusCode);
        Assert.Equal("/admin/denied?returnUrl=%2Fadmin%2Forders", page.Headers.Location!.OriginalString);

        // APIs keep plain status codes.
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.GetAsync("/admin/api/orders")).StatusCode);
    }

    [Fact]
    public async Task Step_up_asks_for_a_new_sign_in_once_the_last_one_is_too_old()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        await using var host = await StartAsync(clock);
        await AuthTestSite.CreateUserAsync(host);
        Assert.Equal("Succeeded", await AuthTestSite.LoginAsync(host.Client));

        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/account/delete")).StatusCode);

        clock.Advance(TimeSpan.FromMinutes(6));
        var page = await host.Client.GetAsync("/account/delete");
        Assert.Equal(HttpStatusCode.Redirect, page.StatusCode);
        Assert.Equal("/login?reauth=1&returnUrl=%2Faccount%2Fdelete", page.Headers.Location!.OriginalString);

        var api = await host.Client.GetAsync("/account/api/delete");
        Assert.Equal(HttpStatusCode.Unauthorized, api.StatusCode);
        Assert.Contains("reauthentication_required", api.Headers.WwwAuthenticate.ToString(), StringComparison.Ordinal);

        // Still signed in: refreshing the session does not count as proving the credentials again.
        Assert.Equal("True", await AuthTestSite.PostAsync(host.Client, "/auth/refresh", new { }));
        Assert.Equal(HttpStatusCode.Redirect, (await host.Client.GetAsync("/account/delete")).StatusCode);

        Assert.Equal("InvalidCredentials", await AuthTestSite.PostAsync(host.Client, "/auth/reauth", new { password = "wrong password!" }));
        Assert.Equal("Succeeded", await AuthTestSite.PostAsync(host.Client, "/auth/reauth", new { password = AuthTestSite.Password }));
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/account/delete")).StatusCode);
    }

    [Fact]
    public async Task Step_up_with_methods_needs_the_second_factor_again()
    {
        await using var host = await StartAsync();
        var user = await AuthTestSite.CreateUserAsync(host);
        Assert.Equal("Succeeded", await AuthTestSite.LoginAsync(host.Client));

        // Signed in with the password only: the billing page wants pwd + otp.
        Assert.Equal(HttpStatusCode.Redirect, (await host.Client.GetAsync("/billing")).StatusCode);

        var secret = await AuthTestSite.EnableTwoFactorAsync(host, user);
        Assert.Equal("RequiresTwoFactor", await AuthTestSite.PostAsync(host.Client, "/auth/reauth", new { password = AuthTestSite.Password }));
        Assert.Equal(HttpStatusCode.Redirect, (await host.Client.GetAsync("/billing")).StatusCode);

        Assert.Equal("Succeeded", await AuthTestSite.PostAsync(host.Client, "/auth/reauth", new { password = AuthTestSite.Password, code = AuthTestSite.NextCode(secret) }));
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/billing")).StatusCode);
        Assert.Equal("pwd,otp", (await AuthTestSite.MeAsync(host.Client))!.Value.GetProperty("methods").GetString());
    }

    [Fact]
    public async Task Step_up_does_not_ask_anonymous_or_unauthorized_users_to_reauthenticate()
    {
        await using var host = await StartAsync();
        Assert.StartsWith("/login?returnUrl=", (await host.CreateClient().GetAsync("/account/delete")).Headers.Location!.OriginalString);

        await AuthTestSite.CreateUserAsync(host);
        Assert.Equal("Succeeded", await AuthTestSite.LoginAsync(host.Client));
        // The admin step-up route needs the admin group first: a plain user gets the denied page, not a re-login.
        Assert.StartsWith("/admin/denied", (await host.Client.GetAsync("/admin/settings")).Headers.Location!.OriginalString);
    }

    [Fact]
    public void Area_paths_must_be_local()
    {
        var group = new SettingsOnly();
        Assert.Throws<ArgumentException>(() => group.WithLoginPath("https://evil.test/login"));
        Assert.Throws<ArgumentException>(() => group.WithAccessDeniedPath("//evil.test"));
    }

    private sealed class SettingsOnly : IWebRouteSettingsBuilder
    {
        public WebRouteSettings Settings { get; } = new();
    }

    private static Task<WebLogicTestHost> StartAsync(ManualTimeProvider? clock = null) =>
        AuthTestSite.StartAsync(web: web => web.AddContributor(new DelegateContributor(b =>
        {
            static ValueTask<WebResult> Ok(Runtime.Models.WebRequestContext r, CancellationToken _) => ValueTask.FromResult<WebResult>(WebResult.Text("ok"));
            b.MapPage("/members", Ok);
            b.MapPage("/account/delete", Ok).RequireRecentSignIn(TimeSpan.FromMinutes(5));
            b.MapApi("/account/api/delete", Ok).RequireRecentSignIn(TimeSpan.FromMinutes(5));
            b.MapPage("/billing", Ok).RequireRecentSignIn(TimeSpan.FromMinutes(5), "pwd", "otp");

            var admin = b.MapGroup("/admin").RequireGroups("admin").WithLoginPath("/admin/login").WithAccessDeniedPath("/admin/denied");
            admin.MapPage("/orders", Ok);
            admin.MapApi("/api/orders", Ok);
            admin.MapPage("/settings", Ok).RequireRecentSignIn(TimeSpan.FromMinutes(5));
        }), new WebContributorDescriptor { Id = "tests.areas", Name = "Areas" }),
        options: clock is null ? null : new WebLogicTestHostOptions { ConfigureServices = s => s.Replace(ServiceDescriptor.Singleton<TimeProvider>(clock)) });
}
