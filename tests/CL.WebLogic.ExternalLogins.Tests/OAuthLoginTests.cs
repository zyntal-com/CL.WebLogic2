using System.Net;
using CL.WebLogic.Auth.External;
using CL.WebLogic.Configuration.Models;
using CL.WebLogic.ExternalLogins.Hosting;
using CL.WebLogic.ExternalLogins.OAuth;
using CL.WebLogic.Hosting.Services;
using CL.WebLogic.Results;
using CL.WebLogic.Routing.Models;
using CL.WebLogic.Routing.Services;
using CL.WebLogic.Testing.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CL.WebLogic.ExternalLogins.Tests;

/// <summary>The OAuth / OpenID Connect provider end to end, against <see cref="FakeIdentityProvider"/>.</summary>
public sealed class OAuthLoginTests : IDisposable
{
    private readonly FakeIdentityProvider _idp = new();

    private readonly ManualTimeProvider _clock = new(DateTimeOffset.UtcNow);

    private Task<WebLogicTestHost> StartAsync(Action<WebLogicBuilder> providers) =>
        WebLogicTestHost.StartAsync(new WebLogicTestHostOptions
        {
            Config = new WebLogicConfig { PublicBaseUrl = "https://site.test" },
            ConfigureWebLogic = web =>
            {
                web.UseInMemoryAccounts();
                providers(web);
                web.AddContributor(new DelegateContributor(b =>
                {
                    b.MapPage("/ext/{provider}", (r, ct) => new ValueTask<WebResult>(r.GetRequiredService<WebExternalLoginFlow>()
                        .ChallengeAsync(r, r.GetRouteValue("provider")!, "/ext/" + r.GetRouteValue("provider") + "/callback", null, ct))).AllowAnonymous();
                    b.MapPage("/ext/{provider}/callback", async (r, ct) =>
                    {
                        var outcome = await r.GetRequiredService<WebExternalLoginFlow>().HandleCallbackAsync(r, r.GetRouteValue("provider")!, ct);
                        if (outcome.IsFailure)
                            return WebResult.Text("failed:" + outcome.Error!.Message);
                        var info = outcome.Value!.Info;
                        return WebResult.Text($"ok:{info.Provider}|{info.ProviderKey}|{info.UserName}|{info.Email}|{info.EmailVerified}");
                    }).AllowAnonymous();
                }), new WebContributorDescriptor { Id = "tests.oauth", Name = "OAuth" });
            },
            ConfigureServices = services =>
            {
                services.Replace(ServiceDescriptor.Singleton<TimeProvider>(_clock));
                services.AddHttpClient(WebExternalLoginHttp.ClientName).ConfigurePrimaryHttpMessageHandler(() => _idp)
                    .SetHandlerLifetime(Timeout.InfiniteTimeSpan);
            }
        });

    /// <summary>Starts the sign-in and returns the callback query the provider would send back.</summary>
    private async Task<string> ChallengeAsync(WebLogicTestHost host, string provider)
    {
        var response = await host.Client.GetAsync("/ext/" + provider);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        _idp.Authorization = response.Headers.Location!;
        return "state=" + FakeIdentityProvider.Query(_idp.Authorization)["state"];
    }

    private async Task<string> SignInAsync(WebLogicTestHost host, string provider, string extra = "")
    {
        var state = await ChallengeAsync(host, provider);
        return await host.Client.GetStringAsync($"/ext/{provider}/callback?{state}&code={FakeIdentityProvider.Code}{extra}");
    }

    [Fact]
    public async Task OpenID_Connect_signs_in_with_PKCE_and_a_validated_ID_token()
    {
        await using var host = await StartAsync(web => web.AddGoogleLogin("google-client", "google-secret"));

        Assert.Equal("ok:google|oidc-user-1|Ada|ada@example.test|True", await SignInAsync(host, "google"));

        var query = FakeIdentityProvider.Query(_idp.Authorization!);
        Assert.StartsWith("https://accounts.google.com/authorize?", _idp.Authorization!.ToString());
        Assert.Equal(("code", "google-client", "https://site.test/ext/google/callback", "S256", "openid email profile"),
            (query["response_type"], query["client_id"], query["redirect_uri"], query["code_challenge_method"], query["scope"]));
        Assert.Equal(32, query["nonce"].Length);
        Assert.Equal(43, _idp.LastTokenForm["code_verifier"].Length);
        Assert.DoesNotContain(_idp.LastTokenForm["code_verifier"], _idp.Authorization.ToString()); // the verifier never reaches the browser
        Assert.Equal("google-secret", _idp.LastTokenForm["client_secret"]);
    }

    [Theory]
    [InlineData("nonce")]
    [InlineData("aud")]
    [InlineData("iss")]
    [InlineData("exp")]
    public async Task A_bad_ID_token_is_refused(string claim)
    {
        _idp.Tamper = claims => claims[claim] = claim == "exp" ? DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds() : "other";
        await using var host = await StartAsync(web => web.AddGoogleLogin("google-client", "google-secret"));
        Assert.Equal("failed:The provider's identity token was not valid.", await SignInAsync(host, "google"));
    }

    [Fact]
    public async Task A_token_signed_by_a_stranger_is_refused_and_rotated_keys_are_picked_up()
    {
        await using var host = await StartAsync(web => web.AddGoogleLogin("google-client", "google-secret"));
        Assert.StartsWith("ok:", await SignInAsync(host, "google"));

        // The issuer rotates to a new key id: the cached key set is refreshed once.
        _clock.Advance(TimeSpan.FromSeconds(31));
        _idp.Signer = new TestSigner("rsa-2");
        Assert.StartsWith("ok:", await SignInAsync(host, "google"));
        Assert.Equal(2, _idp.Requests.Count(r => r.EndsWith("/jwks", StringComparison.Ordinal)));

        // A forger using the published key id with their own key: refused.
        using var forger = new TestSigner("rsa-2");
        _idp.Forger = forger;
        Assert.Equal("failed:The provider's identity token was not valid.", await SignInAsync(host, "google"));

        // An unknown key id refetches at most every 30 seconds (no refetch storm).
        using var stranger = new TestSigner("rsa-3");
        _idp.Forger = stranger;
        Assert.StartsWith("failed:", await SignInAsync(host, "google"));
        Assert.StartsWith("failed:", await SignInAsync(host, "google"));
        Assert.Equal(2, _idp.Requests.Count(r => r.EndsWith("/jwks", StringComparison.Ordinal)));
        _clock.Advance(TimeSpan.FromSeconds(31));
        Assert.StartsWith("failed:", await SignInAsync(host, "google"));
        Assert.Equal(3, _idp.Requests.Count(r => r.EndsWith("/jwks", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Plain_OAuth_reads_the_profile_and_the_primary_verified_email()
    {
        await using var host = await StartAsync(web => web.AddGitHubLogin("gh-client", "gh-secret"));

        Assert.Equal("ok:github|583231|octocat|octo@example.test|True", await SignInAsync(host, "github"));
        Assert.False(FakeIdentityProvider.Query(_idp.Authorization!).ContainsKey("nonce"));
        Assert.Contains(_idp.Requests, r => r == "GET https://api.github.com/user/emails");
    }

    [Fact]
    public async Task An_unverified_email_is_never_reported_as_verified()
    {
        _idp.UserInfo = new { id = "80351110224678912", username = "nelly", email = "nelly@example.test", verified = false };
        await using var host = await StartAsync(web => web.AddDiscordLogin("d-client", "d-secret"));
        Assert.Equal("ok:discord|80351110224678912|nelly|nelly@example.test|False", await SignInAsync(host, "discord"));
    }

    [Fact]
    public async Task Provider_errors_and_mix_ups_fail_cleanly()
    {
        await using var host = await StartAsync(web => web.AddGoogleLogin("google-client", "google-secret"));

        var state = await ChallengeAsync(host, "google");
        Assert.Equal("failed:The sign-in was cancelled.", await host.Client.GetStringAsync($"/ext/google/callback?{state}&error=access_denied"));

        state = await ChallengeAsync(host, "google");
        Assert.Equal("failed:The provider sent no authorization code.", await host.Client.GetStringAsync($"/ext/google/callback?{state}"));

        state = await ChallengeAsync(host, "google");
        Assert.Equal("failed:The sign-in response came from another provider.",
            await host.Client.GetStringAsync($"/ext/google/callback?{state}&code={FakeIdentityProvider.Code}&iss=https%3A%2F%2Fevil.test"));

        state = await ChallengeAsync(host, "google");
        Assert.StartsWith("failed:", await host.Client.GetStringAsync($"/ext/google/callback?{state}&code=stolen-code"));
    }

    [Fact]
    public async Task A_discovery_document_for_another_issuer_is_refused()
    {
        _idp.DiscoveryIssuer = "https://evil.test";
        await using var host = await StartAsync(web => web.AddOAuthLogin(new WebOAuthOptions
        {
            Name = "corp", ClientId = "corp-client", ClientSecret = "s", Authority = "https://login.corp.test", Scopes = ["openid"]
        }));
        var response = await host.Client.GetAsync("/ext/corp");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode); // a configuration fault, never a redirect
        Assert.DoesNotContain(_idp.Requests, r => r.Contains("/jwks", StringComparison.Ordinal));
    }

    [Fact]
    public void Options_are_validated()
    {
        Assert.Throws<ArgumentException>(() => new WebOAuthOptions { Name = "Bad Name", ClientId = "c", Authority = "https://x.test" }.Validate());
        Assert.Throws<ArgumentException>(() => new WebOAuthOptions { Name = "a", ClientId = "c", Authority = "http://x.test" }.Validate());
        Assert.Throws<ArgumentException>(() => new WebOAuthOptions { Name = "a", ClientId = "c" }.Validate());
        WebOAuthPresets.Microsoft("c", "s", "organizations").Validate();
    }

    [Fact]
    public async Task Two_providers_with_one_name_are_refused() =>
        await Assert.ThrowsAsync<InvalidOperationException>(() => StartAsync(web => web.AddGoogleLogin("a", "b").AddGoogleLogin("c", "d")));

    public void Dispose() => _idp.Dispose();
}
