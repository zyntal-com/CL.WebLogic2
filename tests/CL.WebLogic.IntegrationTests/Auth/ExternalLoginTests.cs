using System.Net;
using CL.WebLogic.Auth.Events;
using CL.WebLogic.Auth.External;
using CL.WebLogic.Auth.Users;
using CL.WebLogic.Hosting.Services;
using CL.WebLogic.Results;
using CL.WebLogic.Routing.Models;
using CL.WebLogic.Routing.Services;
using CL.WebLogic.Runtime.Models;
using CL.WebLogic.Testing.Services;
using CodeLogic.Core.Results;
using Microsoft.Extensions.DependencyInjection;

namespace CL.WebLogic.IntegrationTests.Auth;

public sealed class ExternalLoginTests
{
    private static Task<WebLogicTestHost> StartAsync() => AuthTestSite.StartAsync(web: w =>
    {
        w.AddExternalLoginProvider<FakeProvider>();
        w.AddContributor(new DelegateContributor(b =>
        {
            b.MapPage("/ext/{provider}", (r, ct) => new ValueTask<WebResult>(r.GetRequiredService<WebExternalLoginFlow>()
                .ChallengeAsync(r, r.GetRouteValue("provider")!, "/ext/" + r.GetRouteValue("provider") + "/callback", r.GetQuery("returnUrl"), ct))).AllowAnonymous();
            b.MapPage("/ext/{provider}/callback", async (r, ct) =>
            {
                var flow = r.GetRequiredService<WebExternalLoginFlow>();
                var outcome = await flow.HandleCallbackAsync(r, r.GetRouteValue("provider")!, ct);
                if (outcome.IsFailure)
                    return WebResult.Text("failed:" + outcome.Error!.Code);
                var user = outcome.Value!.User;
                if (user is null)
                {
                    user = new WebUser { Id = "steam-user", UserName = outcome.Value.Info.UserName! };
                    await r.GetRequiredService<IWebUserStore>().CreateAsync(user, ct);
                    await flow.LinkAsync(user, outcome.Value.Info, ct);
                }
                await r.Auth.SignInAsync(user, ["external"], cancellationToken: ct);
                return WebResult.Text("ok:" + user.Id + ":" + outcome.Value.ReturnUrl);
            }).AllowAnonymous();
        }), new WebContributorDescriptor { Id = "tests.external", Name = "External" });
    });

    [Fact]
    public async Task Challenge_SetsState_AndCallbackVerifiesIt()
    {
        await using var host = await StartAsync();

        var challenge = await host.Client.GetAsync("/ext/fake?returnUrl=%2Fdashboard");
        Assert.Equal(HttpStatusCode.Redirect, challenge.StatusCode);
        var location = challenge.Headers.Location!.ToString();
        Assert.StartsWith("https://idp.test/auth?redirect_uri=https%3A%2F%2Fsite.test%2Fext%2Ffake%2Fcallback&state=", location);
        var state = location.Split("state=")[1];

        Assert.Equal("ok:steam-user:/dashboard", await host.Client.GetStringAsync("/ext/fake/callback?state=" + state + "&id=7656"));
        Assert.Equal("external", (await AuthTestSite.MeAsync(host.Client))!.Value.GetProperty("methods").GetString());
        Assert.Equal(1, (await host.Services.GetRequiredService<IWebUserStore>().FindByIdAsync("steam-user", default)) is null ? 0 : 1);
    }

    [Fact]
    public async Task Callback_WithWrongState_OrFromAnotherBrowser_Fails()
    {
        await using var host = await StartAsync();
        var location = (await host.Client.GetAsync("/ext/fake")).Headers.Location!.ToString();
        var state = location.Split("state=")[1];

        Assert.Equal("failed:weblogic.auth.external_state", await host.Client.GetStringAsync("/ext/fake/callback?state=wrong&id=1"));
        Assert.Equal("failed:weblogic.auth.external_state", await host.CreateClient().GetStringAsync("/ext/fake/callback?state=" + state + "&id=1"));
    }

    [Fact]
    public async Task UnsafeReturnUrls_AreReplaced()
    {
        await using var host = await StartAsync();
        var location = (await host.Client.GetAsync("/ext/fake?returnUrl=https%3A%2F%2Fevil.test")).Headers.Location!.ToString();

        Assert.EndsWith(":/", await host.Client.GetStringAsync("/ext/fake/callback?state=" + location.Split("state=")[1] + "&id=1"));
    }

    [Fact]
    public async Task LinkedIdentity_FindsTheSameUser()
    {
        await using var host = await StartAsync();
        var state = (await host.Client.GetAsync("/ext/fake")).Headers.Location!.ToString().Split("state=")[1];
        await host.Client.GetStringAsync("/ext/fake/callback?state=" + state + "&id=42");

        var other = host.CreateClient();
        state = (await other.GetAsync("/ext/fake")).Headers.Location!.ToString().Split("state=")[1];
        Assert.StartsWith("ok:steam-user:", await other.GetStringAsync("/ext/fake/callback?state=" + state + "&id=42"));
    }

    [Fact]
    public async Task Unlinking_removes_the_identity_once()
    {
        await using var host = await StartAsync();
        var changes = new List<ExternalLoginChangedEvent>();
        host.Context.Events.Subscribe<ExternalLoginChangedEvent>(changes.Add);
        var state = (await host.Client.GetAsync("/ext/fake")).Headers.Location!.ToString().Split("state=")[1];
        await host.Client.GetStringAsync("/ext/fake/callback?state=" + state + "&id=42");
        var flow = host.Services.GetRequiredService<WebExternalLoginFlow>();
        var links = (IWebUserExternalLoginStore)host.Services.GetRequiredService<IWebUserStore>();
        var user = (await links.FindByExternalLoginAsync("fake", "42", default))!;

        Assert.True((await flow.UnlinkAsync(user, "fake", default)).IsSuccess);
        Assert.Null(await links.FindByExternalLoginAsync("fake", "42", default));
        Assert.Equal("weblogic.auth.external_login_not_found", (await flow.UnlinkAsync(user, "fake", default)).Error!.Code);
        Assert.Equal([true, false], changes.Select(c => c.Linked));
    }

    private sealed class FakeProvider : IWebExternalLoginProvider
    {
        public string Name => "fake";

        public string DisplayName => "Fake";

        public ValueTask<string> GetChallengeUrlAsync(WebExternalChallenge challenge, CancellationToken cancellationToken) =>
            ValueTask.FromResult("https://idp.test/auth?redirect_uri=" + Uri.EscapeDataString(challenge.CallbackUrl) + "&state=" + challenge.State);

        public ValueTask<Result<WebExternalLoginInfo>> HandleCallbackAsync(WebRequestContext request, WebExternalChallenge challenge, CancellationToken cancellationToken) =>
            ValueTask.FromResult(request.GetQuery("id") is { } id
                ? Result<WebExternalLoginInfo>.Success(new WebExternalLoginInfo { Provider = Name, ProviderKey = id, UserName = "gamer" + id })
                : Result<WebExternalLoginInfo>.Failure(Error.Validation("fake.no_id", "no id")));
    }
}
