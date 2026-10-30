# External logins (add-on)

`CodeLogic.WebLogic.ExternalLogins` adds ready-made `IWebExternalLoginProvider`s: Steam (OpenID 2.0) and
a generic OAuth 2.0 / OpenID Connect provider with presets for Google, Microsoft, GitHub and Discord. It
has no third-party dependencies: tokens are checked with `System.Security.Cryptography`.

```csharp
builder.AddCodeLogicWebLogic(web => web
    .AddSteamLogin()
    .AddGoogleLogin(env("GOOGLE_CLIENT_ID"), env("GOOGLE_CLIENT_SECRET"))
    .AddMicrosoftLogin(env("MS_CLIENT_ID"), env("MS_CLIENT_SECRET"), tenant: "common")
    .AddGitHubLogin(env("GITHUB_CLIENT_ID"), env("GITHUB_CLIENT_SECRET"))
    .AddDiscordLogin(env("DISCORD_CLIENT_ID"), env("DISCORD_CLIENT_SECRET"))
    .AddOAuthLogin(new WebOAuthOptions
    {
        Name = "corp",
        DisplayName = "Corp SSO",
        ClientId = "weblogic",
        ClientSecret = env("CORP_SECRET"),
        Authority = "https://login.corp.example",          // discovery + ID token validation
        Scopes = ["openid", "email", "profile"]
    }));
```

Register `{PublicBaseUrl}/account/external/{name}/callback` (or whatever your callback route is) at the
provider. Set `PublicBaseUrl` in production so the callback never depends on the request's `Host`.

## Routes

The provider only speaks the protocol; the app maps two routes with the core `WebExternalLoginFlow` and
decides what an unknown identity means (register, link, refuse):

```csharp
b.MapPage("/account/external/{provider}", (r, ct) => new ValueTask<WebResult>(flow.ChallengeAsync(
    r, r.GetRouteValue("provider")!, $"/account/external/{r.GetRouteValue("provider")}/callback", r.GetQuery("returnUrl"), ct))).AllowAnonymous();

b.MapPage("/account/external/{provider}/callback", async (r, ct) =>
{
    var outcome = await flow.HandleCallbackAsync(r, r.GetRouteValue("provider")!, ct);
    if (outcome.IsFailure) return WebResult.Page("account/external-failed", new { outcome.Error!.Message });
    var user = outcome.Value!.User ?? await RegisterOrLinkAsync(outcome.Value.Info, ct);
    var result = await r.Auth.SignInExternalAsync(user, outcome.Value.Info.Provider, cancellationToken: ct);
    if (result.Suspension is { } suspension) return WebResult.AccountSuspended(suspension);
    return result.Status switch
    {
        WebSignInStatus.Succeeded => WebResult.Redirect(outcome.Value.ReturnUrl),       // already a safe local URL
        WebSignInStatus.RequiresTwoFactor => WebResult.Redirect("/account/two-factor"),
        _ => WebResult.Page("account/external-failed", new { Message = "This account cannot sign in right now." })
    };
}).AllowAnonymous();
```

`SignInExternalAsync` applies the account rules (disabled, locked, suspended, other tenant) and asks
two-factor users for their code. The starter sample has the complete flow.

The challenge route can be a GET link or a POST form. A POST form (with the CSRF token) stops other sites from
starting a link for a signed-in user; browsers then apply the page's `form-action` to the redirect to the
provider, so WebLogic adds each provider's `RedirectOrigins` to `form-action` on every page
(`headers.externalLoginFormActions`, on by default). A custom provider lists the origins its challenge URL uses.

## Linking and unlinking

`flow.LinkAsync(user, info)` links an identity to a signed-in user and `flow.UnlinkAsync(user, provider)`
removes it (`weblogic.auth.external_login_not_found` when there is none). Both raise
`ExternalLoginChangedEvent` and are audited. Before unlinking, check that the user keeps another way to sign
in (a password, a passkey or another provider). Custom user stores implement
`IWebUserExternalLoginStore.RemoveExternalLoginAsync`; stores without it throw `NotSupportedException`.

## What is checked

| Step | Check |
|---|---|
| Challenge | Random `state`, `nonce` and PKCE verifier in the flow's protected, 10-minute `__Host-` cookie (works on any node). The verifier never reaches the browser; the redirect carries the S256 challenge. |
| Callback | `state` compared in constant time; `error=` handled; RFC 9207 `iss` checked when sent. |
| Token request | `code`, `redirect_uri` and `code_verifier`; client secret in the body or HTTP Basic. Responses are size-capped. |
| ID token (OIDC) | Signature with a key from the issuer's JWKS (`RS256/384/512`, `PS256/384/512`, `ES256/384/512`; `none` and HMAC refused; RSA keys under 2048 bits ignored; a key's `alg` restriction honoured), `iss` (Microsoft's `{tenantid}` template uses the token's `tid`), `aud` and `azp`, `exp`/`nbf`/`iat` with `ClockSkew` (5 minutes), `nonce`, and no unknown `crit` headers. |
| Discovery | The document's `issuer` must be the configured authority; every endpoint must be `https`. Cached for a day. |
| Key rotation | A token with an unknown `kid` refreshes the key set, at most every 30 seconds per issuer. |
| Profile | The subject claim (strings or numbers) becomes `ProviderKey`. An e-mail is reported as verified only when the provider says so — never for Microsoft (`EmailVerifiedClaim = null`); GitHub's primary verified address comes from `/user/emails`. |

Failure reasons are logged as warnings (`External sign-in with 'google' failed: id_token: wrong nonce`);
users only see a generic message.

## Presets

| Preset | Kind | Subject | Notes |
|---|---|---|---|
| `Google` | OIDC (`accounts.google.com`) | `sub` | `email_verified` from Google |
| `Microsoft` | OIDC (`login.microsoftonline.com/{tenant}/v2.0`) | `sub` | `common`, `organizations`, `consumers` or a tenant id |
| `GitHub` | OAuth 2.0 | `id` | name = `login`; primary verified e-mail |
| `Discord` | OAuth 2.0 | `id` | name = `username`; `verified` |
| Steam | OpenID 2.0 | 64-bit SteamID | no keys, no e-mail; assertion confirmed with `check_authentication` |

`WebOAuthOptions` is a record, so a preset can be adjusted with `with`:

```csharp
web.AddOAuthLogin(WebOAuthPresets.Google(id, secret) with
{
    AuthorizationParameters = new Dictionary<string, string> { ["prompt"] = "select_account" }
});
```

## HTTP

All providers use the named `HttpClient` `WebExternalLoginHttp.ClientName` (10-second timeout).
Configure a proxy or handler with `services.AddHttpClient(WebExternalLoginHttp.ClientName)`.
