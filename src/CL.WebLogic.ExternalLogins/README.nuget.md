# CodeLogic.WebLogic.ExternalLogins

External sign-in providers for [CodeLogic.WebLogic](https://www.nuget.org/packages/CodeLogic.WebLogic):

```csharp
builder.AddCodeLogicWebLogic(web => web
    .AddSteamLogin()                                   // OpenID 2.0, no keys
    .AddGoogleLogin(googleId, googleSecret)            // OpenID Connect
    .AddMicrosoftLogin(msId, msSecret, tenant: "common")
    .AddGitHubLogin(githubId, githubSecret)            // OAuth 2.0 + primary verified e-mail
    .AddDiscordLogin(discordId, discordSecret)
    .AddOAuthLogin(new WebOAuthOptions { Name = "corp", ClientId = "...", ClientSecret = "...", Authority = "https://login.corp.example" }));
```

Then map two routes with the toolkit's `WebExternalLoginFlow` (challenge and callback) and finish
with `request.Auth.SignInExternalAsync(user, provider)`, which applies the account rules and asks
two-factor users for their code.

- Authorization code flow with PKCE (S256); `state`, `nonce` and the code verifier live in the
  flow's protected cookie, so it works on any number of nodes.
- OpenID Connect ID tokens are validated in-house: signature from the issuer's JWKS (RS/PS/ES 256-512
  only; `none` and HMAC are refused, keys under 2048-bit RSA are ignored), `iss` (Microsoft's
  multi-tenant template included), `aud`/`azp`, `exp`/`nbf`/`iat` with clock skew, and `nonce`.
  Unknown key ids refresh the key set, so rotation needs no restart.
- E-mail addresses are only reported as verified when the provider says so (never for Microsoft).
- No third-party packages: System.Security.Cryptography and the shared framework only.

Docs: https://zyntal-com.github.io/CL.WebLogic2/
