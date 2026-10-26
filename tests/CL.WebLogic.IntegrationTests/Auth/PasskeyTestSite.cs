using System.Net.Http.Json;
using System.Text.Json;
using CL.WebLogic.Auth.Passkeys;
using CL.WebLogic.Configuration.Models;
using CL.WebLogic.Hosting.Services;
using CL.WebLogic.Results;
using CL.WebLogic.Routing.Models;
using CL.WebLogic.Routing.Services;
using CL.WebLogic.Runtime.Models;
using CL.WebLogic.Testing.Services;
using CodeLogic.Core.Results;

namespace CL.WebLogic.IntegrationTests.Auth;

/// <summary>The auth test site plus JSON endpoints for every passkey ceremony, and a client that drives them.</summary>
internal static class PasskeyTestSite
{
    public const string Origin = "https://site.test";

    /// <summary>Starts the site; <paramref name="page"/> also maps <c>/passkeys</c> to <c>templates/passkeys</c> (browser tests).</summary>
    public static Task<WebLogicTestHost> StartAsync(Action<WebLogicConfig>? configure = null, bool page = false) =>
        AuthTestSite.StartAsync(configure, web => web.AddContributor(new DelegateContributor(b =>
        {
            Routes(b);
            if (page)
                b.MapPage("/passkeys", (_, _) => ValueTask.FromResult<WebResult>(WebResult.Page("templates/passkeys"))).AllowAnonymous();
        }), new WebContributorDescriptor { Id = "tests.passkeys", Name = "Passkeys" }));

    private static void Routes(WebContributionBuilder b)
    {
        b.MapApi("/pk/register/begin", async (r, ct) => Challenge(await r.Auth.BeginPasskeyRegistrationAsync(ct)), "POST");
        b.MapApi("/pk/register/finish", async (r, ct) =>
        {
            var result = await r.Auth.CompletePasskeyRegistrationAsync(await Body(r, ct), ct);
            return Status(result.IsSuccess ? "Registered:" + result.Value!.Name : "Failed:" + result.Error!.Message);
        }, "POST");
        b.MapApi("/pk/signin/begin", async (r, ct) => Challenge(await r.Auth.BeginPasskeySignInAsync(ct)), "POST").AllowAnonymous();
        b.MapApi("/pk/signin/finish", async (r, ct) =>
        {
            var result = await r.Auth.PasskeySignInAsync(await Body(r, ct), ct);
            return Status(result.Status + (result.Suspension is { } s ? ":" + s.Reason : ""));
        }, "POST").AllowAnonymous();
        b.MapApi("/pk/2fa/begin", async (r, ct) => Challenge(await r.Auth.BeginPasskeySecondFactorAsync(ct)), "POST").AllowAnonymous();
        b.MapApi("/pk/2fa/finish", async (r, ct) => Status((await r.Auth.CompleteTwoFactorWithPasskeyAsync(await Body(r, ct), ct)).Status.ToString()), "POST").AllowAnonymous();
        b.MapApi("/pk/reauth/begin", async (r, ct) => Challenge(await r.Auth.BeginPasskeyReauthenticationAsync(ct)), "POST");
        b.MapApi("/pk/reauth/finish", async (r, ct) => Status((await r.Auth.ReauthenticateWithPasskeyAsync(await Body(r, ct), ct)).Status.ToString()), "POST");
        b.MapApi("/pk/list", async (r, ct) => WebResult.Json((await r.Auth.ListPasskeysAsync(ct)).Select(p => new { p.CredentialId, p.Name, p.SignCount, p.BackedUp })));
        b.MapApi("/pk/rename", async (r, ct) =>
        {
            var body = await Body(r, ct);
            return Status((await r.Auth.RenamePasskeyAsync(body.State, body.Name ?? "", ct)).ToString());
        }, "POST");
        b.MapApi("/pk/delete", async (r, ct) => Status((await r.Auth.DeletePasskeyAsync((await Body(r, ct)).State, ct)).ToString()), "POST");
        b.MapApi("/pk/admin", (_, _) => ValueTask.FromResult<WebResult>(WebResult.Text("admin")))
            .RequireRecentSignInAnyOf(TimeSpan.FromMinutes(10), ["pwd", "otp"], ["passkey"]);
    }

    private static async Task<WebPasskeyRequest> Body(WebRequestContext request, CancellationToken cancellationToken) =>
        await request.ReadJsonAsync<WebPasskeyRequest>(cancellationToken) ?? new WebPasskeyRequest();

    private static WebResult Challenge(Result<WebPasskeyChallenge> challenge) =>
        challenge.IsSuccess ? WebResult.Json(challenge.Value!) : WebResult.Problem(400, challenge.Error!.Message);

    private static JsonResult Status(string status) => WebResult.Json(new Dictionary<string, string> { ["status"] = status });

    /// <summary>Begins a ceremony; returns (options, state).</summary>
    public static async Task<(JsonElement Options, string State)> BeginAsync(HttpClient client, string ceremony)
    {
        var response = await AuthTestSite.PostRawAsync(client, $"/pk/{ceremony}/begin", new { });
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (json.GetProperty("options"), json.GetProperty("state").GetString()!);
    }

    /// <summary>Finishes a ceremony; returns the status.</summary>
    public static Task<string> FinishAsync(HttpClient client, string ceremony, JsonElement credential, string state, string? name = null, bool remember = false) =>
        AuthTestSite.PostAsync(client, $"/pk/{ceremony}/finish", new { credential, state, name, remember });

    /// <summary>Registers a passkey on <paramref name="authenticator"/> for the signed-in client.</summary>
    public static async Task<string> RegisterAsync(HttpClient client, WebTestAuthenticator authenticator, string? name = null)
    {
        var (options, state) = await BeginAsync(client, "register");
        return await FinishAsync(client, "register", authenticator.Create(options), state, name);
    }

    /// <summary>A full assertion ceremony for <paramref name="ceremony"/> (signin, 2fa, reauth).</summary>
    public static async Task<string> AssertAsync(HttpClient client, WebTestAuthenticator authenticator, string ceremony = "signin", bool remember = false)
    {
        var (options, state) = await BeginAsync(client, ceremony);
        return await FinishAsync(client, ceremony, authenticator.Get(options), state, remember: remember);
    }

    /// <summary>Creates alice, signs her in with her password and registers a passkey; signs out again when <paramref name="signOut"/>.</summary>
    public static async Task<WebTestAuthenticator> EnrolAsync(WebLogicTestHost host, bool signOut = true)
    {
        await AuthTestSite.CreateUserAsync(host);
        var authenticator = new WebTestAuthenticator(Origin);
        Assert.Equal("Succeeded", await AuthTestSite.LoginAsync(host.Client));
        Assert.Equal("Registered:Laptop", await RegisterAsync(host.Client, authenticator, "Laptop"));
        if (signOut)
            Assert.Equal("SignedOut", await AuthTestSite.PostAsync(host.Client, "/auth/logout", new { }));
        return authenticator;
    }

    /// <summary>A copy of <paramref name="element"/> with <paramref name="change"/> applied to its JSON object.</summary>
    public static JsonElement With(JsonElement element, Action<System.Text.Json.Nodes.JsonObject> change)
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse(element.GetRawText())!.AsObject();
        change(node);
        return JsonDocument.Parse(node.ToJsonString()).RootElement.Clone();
    }
}
