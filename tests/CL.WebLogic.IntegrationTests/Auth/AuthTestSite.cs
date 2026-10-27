using System.Net.Http.Json;
using System.Text.Json;
using CL.WebLogic.Auth.Accounts;
using CL.WebLogic.Auth.Notifications;
using CL.WebLogic.Auth.Users;
using CL.WebLogic.Configuration.Models;
using CL.WebLogic.Hosting.Services;
using CL.WebLogic.IntegrationTests.Security;
using CL.WebLogic.Results;
using CL.WebLogic.Routing.Models;
using CL.WebLogic.Routing.Services;
using CL.WebLogic.Testing.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CL.WebLogic.IntegrationTests.Auth;

/// <summary>A host with in-memory accounts and JSON endpoints for every auth flow.</summary>
internal static class AuthTestSite
{
    public const string Password = "correct horse battery";

    public static Task<WebLogicTestHost> StartAsync(Action<WebLogicConfig>? configure = null, Action<WebLogicBuilder>? web = null, WebLogicTestHostOptions? options = null) =>
        SecurityTestSite.StartAsync(config =>
        {
            config.PublicBaseUrl = "https://site.test";
            config.Auth.Passwords.Iterations = 100_000;
            config.Auth.Lockout.FreeAttempts = 50;
            configure?.Invoke(config);
        }, builder =>
        {
            builder.UseInMemoryAccounts();
            builder.AddAuthorizationPolicy("orders-read", p => p.RequireScopes("orders:read"));
            builder.AddContributor(new DelegateContributor(Routes), new WebContributorDescriptor { Id = "tests.auth", Name = "Auth" });
            web?.Invoke(builder);
        }, options);

    private sealed record Body(string? Login, string? Password, string? Code, string? Token, string? NewPassword, string? Current, bool Remember = false);

    private static JsonResult Status(object status, string? token = null, string? error = null) =>
        WebResult.Json(new Dictionary<string, string?> { ["status"] = status.ToString(), ["token"] = token, ["error"] = error });

    private static void Routes(WebContributionBuilder b)
    {
        b.MapApi("/auth/login", async (r, ct) =>
        {
            var body = (await r.ReadJsonAsync<Body>(ct))!;
            var result = await r.Auth.PasswordSignInAsync(body.Login ?? "", body.Password ?? "", body.Remember, ct);
            return Status(result.Status, result.PasswordChangeToken);
        }, "POST").AllowAnonymous();
        b.MapApi("/auth/2fa", async (r, ct) => Status((await r.Auth.CompleteTwoFactorAsync((await r.ReadJsonAsync<Body>(ct))!.Code ?? "", ct)).Status), "POST").AllowAnonymous();
        b.MapApi("/auth/recovery", async (r, ct) => Status((await r.Auth.RedeemRecoveryCodeAsync((await r.ReadJsonAsync<Body>(ct))!.Code ?? "", ct)).Status), "POST").AllowAnonymous();
        b.MapApi("/auth/magic", async (r, ct) => Status((await r.Auth.SignInWithMagicLinkAsync((await r.ReadJsonAsync<Body>(ct))!.Token ?? "", cancellationToken: ct)).Status), "POST").AllowAnonymous();
        b.MapApi("/auth/complete-change", async (r, ct) =>
        {
            var body = (await r.ReadJsonAsync<Body>(ct))!;
            var result = await r.Auth.CompletePasswordChangeAsync(body.Token ?? "", body.NewPassword ?? "", ct);
            return Status(result.Status, error: result.Error?.Message);
        }, "POST").AllowAnonymous();
        b.MapApi("/auth/logout", async (r, ct) =>
        {
            await r.Auth.SignOutAsync(r.GetQuery("everywhere") == "1", ct);
            return Status("SignedOut");
        }, "POST").AllowAnonymous();
        b.MapApi("/auth/change-password", async (r, ct) =>
        {
            var body = (await r.ReadJsonAsync<Body>(ct))!;
            var result = await r.Auth.ChangePasswordAsync(body.Current ?? "", body.NewPassword ?? "", ct);
            return Status(result.IsSuccess ? "Changed" : result.Error!.Code);
        }, "POST");
        b.MapApi("/auth/refresh", async (r, ct) => Status(await r.Auth.RefreshAsync(ct)), "POST");
        // Signs the current user in again without saying whether to remember them (keeps the session's choice).
        b.MapApi("/auth/resign", async (r, ct) =>
        {
            var user = (await r.GetRequiredService<IWebUserStore>().FindByIdAsync(r.Identity.UserId!, ct))!;
            await r.Auth.SignInAsync(user, ["custom"], cancellationToken: ct);
            return Status("Resigned");
        }, "POST");
        // Test stand-in for an external provider callback: the "provider" vouched for body.Login.
        b.MapApi("/auth/external", async (r, ct) =>
        {
            var body = (await r.ReadJsonAsync<Body>(ct))!;
            var user = await r.GetRequiredService<IWebUserStore>().FindByLoginAsync(body.Login ?? "", ct);
            return user is null ? Status("NoUser") : Status((await r.Auth.SignInExternalAsync(user, "steam", body.Remember, ct)).Status);
        }, "POST").AllowAnonymous();
        b.MapApi("/auth/reauth", async (r, ct) =>
        {
            var body = (await r.ReadJsonAsync<Body>(ct))!;
            return Status((await r.Auth.ReauthenticateAsync(body.Password ?? "", body.Code, ct)).Status);
        }, "POST");
        b.MapApi("/auth/sessions", async (r, ct) => WebResult.Json(await r.Auth.ListSessionsAsync(ct)));
        b.MapApi("/auth/sessions/revoke", async (r, ct) =>
            Status(await r.Auth.RevokeSessionAsync((await r.ReadJsonAsync<Body>(ct))!.Token ?? "", ct)), "POST");
        b.MapApi("/me", (r, _) => ValueTask.FromResult<WebResult>(WebResult.Json(new Dictionary<string, object?>
        {
            ["user"] = r.Identity.UserId,
            ["scheme"] = r.Identity.Scheme,
            ["methods"] = string.Join(',', r.Identity.AuthenticationMethods),
            ["authenticatedAt"] = r.Identity.AuthenticatedAt?.ToUnixTimeSeconds(),
            ["groups"] = string.Join(',', r.Identity.Groups),
            ["scopes"] = string.Join(',', r.Identity.Scopes)
        })));
        b.MapApi("/scoped", (r, _) => ValueTask.FromResult<WebResult>(WebResult.Text("scoped"))).RequirePolicy("orders-read");
        b.MapApi("/write", (r, _) => ValueTask.FromResult<WebResult>(WebResult.Text(r.Identity.UserId ?? "")), "POST");
    }

    /// <summary>Creates a user with <see cref="Password"/>.</summary>
    public static async Task<WebUser> CreateUserAsync(WebLogicTestHost host, string userName = "alice", Func<WebUser, WebUser>? change = null, string? email = null)
    {
        var accounts = host.Services.GetRequiredService<WebAccountService>();
        var created = await accounts.RegisterAsync(new WebRegistration
        {
            UserName = userName,
            Email = email ?? userName + "@example.test",
            Password = Password,
            EmailConfirmed = true
        }, default);
        Assert.True(created.IsSuccess, created.Error?.Message);
        var user = created.Value!;
        if (change is not null)
        {
            user = change(user);
            await host.Services.GetRequiredService<IWebUserStore>().UpdateAsync(user, default);
        }
        return user;
    }

    /// <summary>Turns on two-factor authentication for <paramref name="user"/>; returns the secret (sign in with <see cref="NextCode"/>).</summary>
    public static async Task<string> EnableTwoFactorAsync(WebLogicTestHost host, WebUser user)
    {
        var twoFactor = host.Services.GetRequiredService<CL.WebLogic.Auth.TwoFactor.WebTwoFactorService>();
        var enrollment = twoFactor.BeginEnrollment(user);
        var confirmed = await twoFactor.ConfirmEnrollmentAsync(user, enrollment.EnrollmentToken, TotpCodes.At(enrollment.Secret, TotpCodes.CurrentStep), default);
        Assert.True(confirmed.IsSuccess, confirmed.Error?.Message);
        return enrollment.Secret;
    }

    /// <summary>A code for the next time step (enrollment used the current one; each step works once).</summary>
    public static string NextCode(string secret, int ahead = 1) => TotpCodes.At(secret, TotpCodes.CurrentStep + ahead);

    /// <summary>POSTs JSON with a fresh CSRF token; returns the "status" field.</summary>
    public static async Task<string> PostAsync(HttpClient client, string path, object body)
    {
        var response = await PostRawAsync(client, path, body);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("status").GetString()!;
    }

    /// <summary>POSTs JSON with a fresh CSRF token.</summary>
    public static async Task<HttpResponseMessage> PostRawAsync(HttpClient client, string path, object body)
    {
        var token = await SecurityTestSite.GetCsrfTokenAsync(client);
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-Token", token);
        return await client.SendAsync(request);
    }

    /// <summary>Signs in with a password; returns the status.</summary>
    public static Task<string> LoginAsync(HttpClient client, string login = "alice", string password = Password) =>
        PostAsync(client, "/auth/login", new { login, password });

    /// <summary>GET /me (or null when not authenticated).</summary>
    public static async Task<JsonElement?> MeAsync(HttpClient client)
    {
        var response = await client.GetAsync("/me");
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<JsonElement>() : null;
    }

    /// <summary>Waits for an outbox message of <paramref name="kind"/>.</summary>
    public static async Task<WebSecurityNotification> WaitForMailAsync(WebLogicTestHost host, WebSecurityNotificationKind kind, string? to = null)
    {
        var outbox = host.Services.GetRequiredService<OutboxSecurityNotifier>();
        for (var i = 0; i < 100; i++)
        {
            var message = outbox.Messages.LastOrDefault(m => m.Kind == kind && (to is null || m.ToAddress == to));
            if (message is not null)
                return message;
            await Task.Delay(50);
        }
        throw new Xunit.Sdk.XunitException($"No {kind} e-mail arrived.");
    }

    /// <summary>The token from an e-mailed link.</summary>
    public static string TokenFrom(WebSecurityNotification message) =>
        Uri.UnescapeDataString(message.Link!.Split("token=")[1]);
}
