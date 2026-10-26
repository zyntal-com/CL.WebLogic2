using System.Net;
using System.Net.Http.Headers;
using CL.WebLogic.Auth.ApiKeys;
using CL.WebLogic.Auth.Users;
using CL.WebLogic.Hosting.Services;
using CL.WebLogic.Results;
using CL.WebLogic.Routing.Models;
using CL.WebLogic.Routing.Services;
using CL.WebLogic.Testing.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CL.WebLogic.IntegrationTests.Auth;

/// <summary>Suspended (banned) accounts: sign-in, existing sessions, API keys and the themed page.</summary>
public sealed class SuspensionTests
{
    [Fact]
    public async Task A_suspension_is_only_revealed_after_the_password_is_proven()
    {
        await using var host = await AuthTestSite.StartAsync(web: Pages);
        await AuthTestSite.CreateUserAsync(host, change: u => u with { SuspendedUntil = DateTimeOffset.UtcNow.AddDays(3), SuspensionReason = "Cheating" });

        Assert.Equal("InvalidCredentials", await AuthTestSite.LoginAsync(host.Client, password: "wrong password!!"));
        Assert.Equal("Suspended", await AuthTestSite.LoginAsync(host.Client));
        Assert.Null(await AuthTestSite.MeAsync(host.Client));
        Assert.Equal("Suspended", await AuthTestSite.PostAsync(host.Client, "/auth/external", new { login = "alice" }));
    }

    [Fact]
    public async Task The_suspension_reason_reaches_the_caller_for_every_sign_in_method()
    {
        // Regression: the sign-in pipeline used to rebuild failed results and drop the suspension.
        await using var host = await AuthTestSite.StartAsync(web: web =>
        {
            Pages(web);
            web.AddContributor(new DelegateContributor(b =>
            {
                b.MapApi("/why/password", async (r, ct) => Reason(await r.Auth.PasswordSignInAsync("alice", AuthTestSite.Password, cancellationToken: ct)), "POST").AllowAnonymous();
                b.MapApi("/why/external", async (r, ct) =>
                {
                    var user = (await r.GetRequiredService<IWebUserStore>().FindByLoginAsync("alice", ct))!;
                    return Reason(await r.Auth.SignInExternalAsync(user, "steam", cancellationToken: ct));
                }, "POST").AllowAnonymous();
            }), new WebContributorDescriptor { Id = "tests.why", Name = "Why" });
        });
        await AuthTestSite.CreateUserAsync(host, change: u => u with { SuspendedUntil = DateTimeOffset.UtcNow.AddDays(3), SuspensionReason = "Cheating" });

        foreach (var path in (string[])["/why/password", "/why/external"])
        {
            var response = await AuthTestSite.PostRawAsync(host.Client, path, new { });
            Assert.Equal("Suspended:Cheating", await response.Content.ReadAsStringAsync());
        }
    }

    private static TextResult Reason(CL.WebLogic.Auth.SignIn.WebSignInResult result) =>
        WebResult.Text(result.Status + ":" + result.Suspension?.Reason);

    [Fact]
    public async Task An_ended_suspension_no_longer_applies()
    {
        await using var host = await AuthTestSite.StartAsync();
        await AuthTestSite.CreateUserAsync(host, change: u => u with { SuspendedUntil = DateTimeOffset.UtcNow.AddMinutes(-1), SuspensionReason = "Old" });
        Assert.Equal("Succeeded", await AuthTestSite.LoginAsync(host.Client));
    }

    [Fact]
    public async Task Suspending_a_signed_in_user_ends_the_session_and_their_api_keys_stop_working()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        await using var host = await AuthTestSite.StartAsync(c => c.Auth.SecurityStampCheckMinutes = 1,
            options: new WebLogicTestHostOptions { ConfigureServices = s => s.Replace(ServiceDescriptor.Singleton<TimeProvider>(clock)) });
        var user = await AuthTestSite.CreateUserAsync(host);
        Assert.Equal("Succeeded", await AuthTestSite.LoginAsync(host.Client));
        var key = (await host.Services.GetRequiredService<WebApiKeyService>().CreateAsync(new WebApiKeyRequest { Name = "bot", UserId = user.Id }, default)).PlainTextKey;
        var api = host.CreateClient();
        api.DefaultRequestHeaders.Add("X-Api-Key", key);
        Assert.Equal(HttpStatusCode.OK, (await api.GetAsync("/me")).StatusCode);

        var users = host.Services.GetRequiredService<IWebUserStore>();
        await users.UpdateAsync((await users.FindByIdAsync(user.Id, default))! with { SuspendedUntil = DateTimeOffset.MaxValue }, default);
        clock.Advance(TimeSpan.FromMinutes(2));

        Assert.Null(await AuthTestSite.MeAsync(host.Client));
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.GetAsync("/me")).StatusCode);
    }

    [Fact]
    public async Task The_suspended_page_encodes_the_reason_and_api_clients_get_a_problem()
    {
        await using var host = await AuthTestSite.StartAsync(web: Pages);

        var page = await host.Client.GetAsync("/suspended");
        Assert.Equal(HttpStatusCode.Forbidden, page.StatusCode);
        var html = await page.Content.ReadAsStringAsync();
        Assert.Contains("&lt;b&gt;spam&lt;/b&gt;", html);
        Assert.Contains("2030-01-02", html);
        Assert.Equal("no-store", page.Headers.CacheControl?.ToString());

        var request = new HttpRequestMessage(HttpMethod.Get, "/suspended");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/problem+json"));
        var problem = await host.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, problem.StatusCode);
        Assert.Contains("suspendedUntil", await problem.Content.ReadAsStringAsync());
    }

    private static void Pages(WebLogicBuilder web) => web.AddContributor(new DelegateContributor(b =>
        b.MapPage("/suspended", (_, _) => ValueTask.FromResult<WebResult>(
            WebResult.AccountSuspended(new WebSuspension(new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero), "<b>spam</b>")))).AllowAnonymous()),
        new WebContributorDescriptor { Id = "tests.suspended", Name = "Suspended" });
}
