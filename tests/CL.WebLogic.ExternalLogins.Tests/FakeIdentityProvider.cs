using System.Buffers.Text;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CL.WebLogic.ExternalLogins.Tests;

/// <summary>
/// An in-process identity provider behind the providers' HttpClient: discovery, JWKS, a token
/// endpoint that enforces PKCE and the redirect URI, and user info. Any host is answered, so the
/// real presets (accounts.google.com, github.com…) can be exercised.
/// </summary>
internal sealed class FakeIdentityProvider : HttpMessageHandler
{
    public const string Code = "the-code";

    public TestSigner Signer { get; set; } = new();

    /// <summary>Signs ID tokens instead of <see cref="Signer"/> (a forger; the JWKS still serves <see cref="Signer"/>).</summary>
    public TestSigner? Forger { get; set; }

    /// <summary>The authorization URL the browser was sent to (set by the test).</summary>
    public Uri? Authorization { get; set; }

    /// <summary>Changes the ID token claims before signing.</summary>
    public Action<Dictionary<string, object>>? Tamper { get; set; }

    /// <summary>The issuer the discovery document names (null = the authority itself).</summary>
    public string? DiscoveryIssuer { get; set; }

    public object UserInfo { get; set; } = new { id = 583231, login = "octocat", email = (string?)null };

    public object Emails { get; set; } = new[]
    {
        new { email = "old@example.test", primary = false, verified = true },
        new { email = "octo@example.test", primary = true, verified = true }
    };

    public List<string> Requests { get; } = [];

    public Dictionary<string, string> LastTokenForm { get; private set; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var url = request.RequestUri!;
        Requests.Add(request.Method + " " + url.GetLeftPart(UriPartial.Path));
        var authority = url.GetLeftPart(UriPartial.Authority);
        var path = url.AbsolutePath;

        if (path.EndsWith("/.well-known/openid-configuration", StringComparison.Ordinal))
        {
            var issuer = authority + path[..^"/.well-known/openid-configuration".Length];
            return Json(new
            {
                issuer = DiscoveryIssuer ?? issuer,
                authorization_endpoint = issuer + "/authorize",
                token_endpoint = issuer + "/token",
                userinfo_endpoint = issuer + "/userinfo",
                jwks_uri = issuer + "/jwks"
            });
        }
        if (path.EndsWith("/jwks", StringComparison.Ordinal))
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Signer.Jwks(), Encoding.UTF8, "application/json") };
        if (path.EndsWith("/token", StringComparison.Ordinal) || path.EndsWith("/access_token", StringComparison.Ordinal))
            return await TokenAsync(request, authority + path[..path.LastIndexOf('/')], cancellationToken);
        if (request.Headers.Authorization?.Parameter != "access-1")
            return Json(new { message = "Bad credentials" }, HttpStatusCode.Unauthorized);
        if (path.EndsWith("/emails", StringComparison.Ordinal))
            return Json(Emails);
        return Json(UserInfo);
    }

    private async Task<HttpResponseMessage> TokenAsync(HttpRequestMessage request, string issuer, CancellationToken cancellationToken)
    {
        var form = (await request.Content!.ReadAsStringAsync(cancellationToken)).Split('&')
            .Select(p => p.Split('=', 2)).ToDictionary(p => Uri.UnescapeDataString(p[0]), p => Uri.UnescapeDataString(p[1].Replace('+', ' ')));
        LastTokenForm = form;
        var query = Query(Authorization!);

        if (form.GetValueOrDefault("code") != Code || form.GetValueOrDefault("redirect_uri") != query["redirect_uri"])
            return Json(new { error = "invalid_grant" }, HttpStatusCode.BadRequest);
        if (query.TryGetValue("code_challenge", out var challenge)
            && Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(form.GetValueOrDefault("code_verifier") ?? ""))) != challenge)
            return Json(new { error = "invalid_grant", error_description = "PKCE" }, HttpStatusCode.BadRequest);

        if (!query.TryGetValue("nonce", out var nonce))
            return Json(new { access_token = "access-1", token_type = "bearer" });
        var claims = new Dictionary<string, object>
        {
            ["iss"] = issuer, ["aud"] = query["client_id"], ["sub"] = "oidc-user-1", ["nonce"] = nonce,
            ["email"] = "ada@example.test", ["email_verified"] = true, ["name"] = "Ada",
            ["iat"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), ["exp"] = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds()
        };
        Tamper?.Invoke(claims);
        return Json(new { access_token = "access-1", token_type = "bearer", id_token = (Forger ?? Signer).Sign(claims) });
    }

    public static Dictionary<string, string> Query(Uri uri) =>
        uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Split('=', 2))
            .ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1]));

    private static HttpResponseMessage Json(object body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            Signer.Dispose();
        base.Dispose(disposing);
    }
}
