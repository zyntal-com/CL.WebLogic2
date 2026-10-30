using System.Net;
using System.Text;
using CL.WebLogic.Auth.External;
using CL.WebLogic.Configuration.Models;
using CL.WebLogic.ExternalLogins.Hosting;
using CL.WebLogic.Hosting.Services;
using CL.WebLogic.Results;
using CL.WebLogic.Routing.Models;
using CL.WebLogic.Routing.Services;
using CL.WebLogic.Testing.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CL.WebLogic.ExternalLogins.Tests;

/// <summary>Steam OpenID 2.0 against a fake <c>check_authentication</c> endpoint.</summary>
public sealed class SteamLoginTests
{
    private const string SteamId = "76561197960287930";

    private static Task<WebLogicTestHost> StartAsync(FakeSteam steam) =>
        WebLogicTestHost.StartAsync(new WebLogicTestHostOptions
        {
            Config = new WebLogicConfig { PublicBaseUrl = "https://site.test" },
            ConfigureWebLogic = web =>
            {
                web.UseInMemoryAccounts().AddSteamLogin();
                web.AddContributor(new DelegateContributor(b =>
                {
                    b.MapPage("/ext/steam", (r, ct) => new ValueTask<WebResult>(r.GetRequiredService<WebExternalLoginFlow>()
                        .ChallengeAsync(r, "steam", "/ext/steam/callback", null, ct))).AllowAnonymous();
                    b.MapPage("/ext/steam/callback", async (r, ct) =>
                    {
                        var outcome = await r.GetRequiredService<WebExternalLoginFlow>().HandleCallbackAsync(r, "steam", ct);
                        return WebResult.Text(outcome.IsFailure ? "failed:" + outcome.Error!.Message : "ok:" + outcome.Value!.Info.ProviderKey);
                    }).AllowAnonymous();
                }), new WebContributorDescriptor { Id = "tests.steam", Name = "Steam" });
            },
            ConfigureServices = services => services.AddHttpClient(WebExternalLoginHttp.ClientName).ConfigurePrimaryHttpMessageHandler(() => steam)
        });

    private static async Task<(string ReturnTo, string Callback)> ChallengeAsync(WebLogicTestHost host, string claimedId = "https://steamcommunity.com/openid/id/" + SteamId)
    {
        var location = (await host.Client.GetAsync("/ext/steam")).Headers.Location!;
        Assert.StartsWith("https://steamcommunity.com/openid/login?", location.ToString());
        var returnTo = FakeIdentityProvider.Query(location)["openid.return_to"];
        var query = string.Join('&', new[]
        {
            ("openid.ns", "http://specs.openid.net/auth/2.0"), ("openid.mode", "id_res"),
            ("openid.op_endpoint", "https://steamcommunity.com/openid/login"), ("openid.claimed_id", claimedId),
            ("openid.identity", claimedId), ("openid.return_to", returnTo), ("openid.sig", "c2ln")
        }.Select(p => p.Item1 + "=" + Uri.EscapeDataString(p.Item2)));
        return (returnTo, new Uri(returnTo).PathAndQuery + "&" + query);
    }

    [Fact]
    public async Task Steam_is_an_allowed_form_target()
    {
        using var steam = new FakeSteam(valid: true);
        await using var host = await StartAsync(steam);
        Assert.Equal(["https://steamcommunity.com"], host.Services.GetServices<IWebExternalLoginProvider>().Single().RedirectOrigins);
        var csp = (await host.Client.GetAsync("/ext/steam")).Headers.GetValues("Content-Security-Policy").Single();
        Assert.Contains("form-action 'self' https://steamcommunity.com", csp);
    }

    [Fact]
    public async Task A_confirmed_assertion_signs_in_with_the_SteamID()
    {
        using var steam = new FakeSteam(valid: true);
        await using var host = await StartAsync(steam);
        var (returnTo, callback) = await ChallengeAsync(host);

        Assert.StartsWith("https://site.test/ext/steam/callback?state=", returnTo);
        Assert.Equal("ok:" + SteamId, await host.Client.GetStringAsync(callback));
        Assert.Contains("openid.mode=check_authentication", steam.LastBody);
    }

    [Fact]
    public async Task Unconfirmed_or_foreign_identities_are_refused()
    {
        using var steam = new FakeSteam(valid: false);
        await using var host = await StartAsync(steam);
        Assert.Equal("failed:Steam did not confirm the sign-in.", await host.Client.GetStringAsync((await ChallengeAsync(host)).Callback));

        using var trusting = new FakeSteam(valid: true);
        await using var other = await StartAsync(trusting);
        Assert.Equal("failed:Steam returned an unexpected identity.",
            await other.Client.GetStringAsync((await ChallengeAsync(other, "https://evil.test/openid/id/" + SteamId)).Callback));
        Assert.Null(trusting.LastBody); // refused before asking Steam
    }

    private sealed class FakeSteam(bool valid) : HttpMessageHandler
    {
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastBody = Uri.UnescapeDataString(await request.Content!.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("ns:http://specs.openid.net/auth/2.0\nis_valid:" + (valid ? "true" : "false") + "\n", Encoding.UTF8, "text/plain")
            };
        }
    }
}
