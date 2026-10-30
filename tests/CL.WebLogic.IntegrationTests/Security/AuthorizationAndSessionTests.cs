using System.Net;
using CL.WebLogic.Security.Sessions;
using Microsoft.Extensions.DependencyInjection;

namespace CL.WebLogic.IntegrationTests.Security;

public sealed class AuthorizationAndSessionTests
{
    [Fact]
    public async Task Routes_AreSecureByDefault_PagesRedirectToLogin_ApisChallenge()
    {
        await using var host = await SecurityTestSite.StartAsync();

        var page = await host.Client.GetAsync("/secure?tab=1");
        Assert.Equal(HttpStatusCode.Redirect, page.StatusCode);
        Assert.Equal("/login?returnUrl=%2Fsecure%3Ftab%3D1", page.Headers.Location?.OriginalString);

        var api = await host.Client.GetAsync("/api/secure");
        Assert.Equal(HttpStatusCode.Unauthorized, api.StatusCode);
        Assert.Equal("WebLogic", api.Headers.WwwAuthenticate.ToString());
    }

    [Fact]
    public async Task SignedInUser_PassesAuthentication_ButNeedsGroupsPermissionsAndPolicies()
    {
        await using var host = await SecurityTestSite.StartAsync();
        await host.Client.GetAsync("/signin?user=alice&groups=editor&perms=posts.*");

        Assert.Equal("secure:alice", await host.Client.GetStringAsync("/secure"));
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.GetAsync("/admin")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/perm")).StatusCode);       // posts.* grants posts.edit
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/policy")).StatusCode);     // editors policy
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.GetAsync("/bad-policy")).StatusCode); // unknown policy fails closed
    }

    [Fact]
    public async Task Regression_RequirePolicy_InAnAnonymousGroup_StillRequiresThePolicy()
    {
        await using var host = await SecurityTestSite.StartAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/open/policy")).StatusCode);
        await host.Client.GetAsync("/signin?user=bob");
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.GetAsync("/open/policy")).StatusCode);
    }

    [Fact]
    public async Task Regression_AnonymousSessions_AreNeverShared()
    {
        await using var host = await SecurityTestSite.StartAsync();
        var alice = host.CreateClient();
        var bob = host.CreateClient();

        await alice.GetAsync("/session/set?v=alice-secret");

        Assert.Equal("alice-secret", await alice.GetStringAsync("/session/get"));
        Assert.Equal("none", await bob.GetStringAsync("/session/get"));
        Assert.Equal("none", await host.CreateClient().GetStringAsync("/session/get"));
    }

    [Fact]
    public async Task SessionCookie_IsOnlyIssuedWhenSomethingIsWritten()
    {
        await using var host = await SecurityTestSite.StartAsync();

        var read = await host.Client.GetAsync("/session/get");
        Assert.False(read.Headers.Contains("Set-Cookie"));

        var write = await host.Client.GetAsync("/session/set?v=1");
        var cookie = Assert.Single(write.Headers.GetValues("Set-Cookie"), c => c.StartsWith("wl_sid=", StringComparison.Ordinal));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Regression_SignIn_RotatesTheSessionId_AndKeepsValues()
    {
        await using var host = await SecurityTestSite.StartAsync();
        var set = await host.Client.GetAsync("/session/set?v=cart");
        var before = SessionCookie(set);

        var signIn = await host.Client.GetAsync("/signin?user=alice");
        var after = SessionCookie(signIn);

        Assert.NotEqual(before, after);
        Assert.Equal("cart", await host.Client.GetStringAsync("/session/get"));
        var store = (InMemoryWebSessionStore)host.Services.GetRequiredService<IWebSessionStore>();
        Assert.Equal(1, store.Count); // the pre-sign-in session was deleted
    }

    [Fact]
    public async Task SignOut_EndsTheSession()
    {
        await using var host = await SecurityTestSite.StartAsync();
        await host.Client.GetAsync("/signin?user=alice");
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/api/secure")).StatusCode);

        await host.Client.GetAsync("/signout");

        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/api/secure")).StatusCode);
    }

    [Fact]
    public async Task SignOutEverywhere_EndsAllSessionsOfTheUser()
    {
        await using var host = await SecurityTestSite.StartAsync();
        var laptop = host.CreateClient();
        var phone = host.CreateClient();
        await laptop.GetAsync("/signin?user=alice");
        await phone.GetAsync("/signin?user=alice");

        await laptop.GetAsync("/signout?everywhere=1");

        Assert.Equal(HttpStatusCode.Unauthorized, (await phone.GetAsync("/api/secure")).StatusCode);
    }

    [Fact]
    public async Task TamperedSessionCookie_IsIgnored()
    {
        await using var host = await SecurityTestSite.StartAsync();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/secure");
        request.Headers.Add("Cookie", "wl_sid=" + new string('A', 43));

        Assert.Equal(HttpStatusCode.Unauthorized, (await host.CreateClient().SendAsync(request)).StatusCode);
    }

    private static string SessionCookie(HttpResponseMessage response) =>
        response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("wl_sid=", StringComparison.Ordinal)).Split(';')[0];
}
