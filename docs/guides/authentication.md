# Authentication and accounts

WebLogic implements the flows: password sign-in with lockout and throttling, TOTP two-factor with
replay protection, recovery codes, password reset, e-mail verification and change, magic links,
API keys, bearer tokens, external providers and tenants. **Storage is yours**: you implement the
store contracts and WebLogic calls them.

## Stores

| Contract | Holds | Register with |
|---|---|---|
| `IWebUserStore` | Users (`WebUser`): name, e-mail, password hash, security stamp, groups, permissions, tenant, 2FA secret, lockout | `web.UseUserStore<T>()` |
| `IWebUserLockoutStore`, `IWebUserEmailStore`, `IWebUserExternalLoginStore` | Optional capabilities on the same class: atomic failure counting, lookup by e-mail, linked external logins | (implement on the user store) |
| `IWebSessionStore` | Session records keyed by the SHA-256 of the session token | `web.UseSessionStore<T>()` |
| `IWebApiKeyStore` | API keys (hash of the secret, scopes, expiry, networks) | `web.UseApiKeyStore<T>()` |
| `IWebRecoveryCodeStore` | Hashed recovery codes; `TryConsumeAsync` must be atomic | `web.UseRecoveryCodeStore<T>()` |
| `IWebOneTimeUseStore` | Spent TOTP steps and single-use links (in memory by default) | `web.UseOneTimeUseStore<T>()` |
| `IWebLoginThrottle` | Back-off per login, cap per client (in memory by default) | `web.UseLoginThrottle<T>()` |
| `IWebSecurityNotifier` | Sends security e-mails | `web.UseMailNotifications()` (CL.Mail) or your own |

`web.UseInMemoryAccounts()` registers in-memory users, sessions, keys and codes for development and
tests. Outside Development the app refuses to start while any of them is still in memory. The
samples have complete stores: JSON files in `samples/StarterWebsite/Infrastructure`, MySQL in
`samples/MiniBlog.Data/Stores`.

## Signing in

```csharp
var result = await request.Auth.PasswordSignInAsync(model.Login, model.Password, ct);
switch (result.Status)
{
    case WebSignInStatus.Succeeded: return FormResult.Success(redirect: WebRequestAuth.GetSafeReturnUrl(model.ReturnUrl));
    case WebSignInStatus.RequiresTwoFactor: return FormResult.Success(redirect: "/account/two-factor");
    case WebSignInStatus.RequiresPasswordChange: /* result.PasswordChangeToken → CompletePasswordChangeAsync */ break;
    case WebSignInStatus.LockedOut or WebSignInStatus.Throttled: /* result.RetryAfter */ break;
    default: /* InvalidCredentials, NotAllowed */ break;
}
```

- Unknown users still cost a full hash, so timing does not reveal which logins exist; a locked
  account is only reported to someone who typed the right password.
- One attempt per login (and per pending second factor) runs at a time on a node, so parallel bursts
  cannot get around the throttle.
- Sign-in rotates the session id and the CSRF secret.
- `GetSafeReturnUrl` accepts local paths only (no `//`, `/\`, schemes or control characters).

Other `request.Auth` members: `CompleteTwoFactorAsync(code)`, `RedeemRecoveryCodeAsync(code)`,
`SignInWithMagicLinkAsync(token)`, `CompletePasswordChangeAsync(token, password)`,
`SignInAsync(user)` (after your own checks), `SignOutAsync(everywhere)`, `RefreshAsync()`,
`ChangePasswordAsync(current, new)` and `GetCurrentUserAsync()`.

## Passwords

`Pbkdf2PasswordHasher`: PBKDF2-SHA256, 600,000 iterations, per-hash salt, optional pepper
(`auth.passwords.pepper`, keep it with your database backups), versioned format
(`$wl$pbkdf2-sha256$v=1$...`). Hashes are upgraded on sign-in when the settings change. Plug in other
algorithms with `web.AddPasswordAlgorithm<T>()`. The default `IWebPasswordPolicy` checks length and
refuses passwords that contain the user name or e-mail.

## Two-factor and recovery codes

```csharp
var enrollment = twoFactor.BeginEnrollment(user);        // secret, otpauth URI, QR code, enrollment token
// ... user scans the QR code and types the first code ...
var codes = await twoFactor.ConfirmEnrollmentAsync(user, enrollment.EnrollmentToken, code, ct);
await request.Auth.RefreshAsync(ct);                       // ends the user's other sessions
```

TOTP codes come from CL.TwoFactorAuth; each time step is accepted once (`IWebOneTimeUseStore`).
Secrets are stored DataProtection-protected, so persist your key ring. Recovery codes are shown once,
stored hashed and consumed atomically.

## Links by e-mail

`WebAccountService` covers registration, `ConfirmEmailAsync`, `RequestPasswordResetAsync`/`ResetPasswordAsync`,
`RequestEmailChangeAsync`/`ConfirmEmailChangeAsync` and `RequestMagicLinkAsync`. Tokens are
DataProtection-protected, purpose-scoped, time-limited, bound to the user's security stamp and
single-use. Request methods answer the same whether or not an account exists, mail is sent in the
background, and each address gets at most a few link e-mails per 15 minutes. A magic link that
confirms a never-confirmed address also drops any password set at registration (against account
pre-hijacking). Changing password, e-mail or 2FA changes the security stamp, which ends the user's
other sessions at their next check (`auth.securityStampCheckMinutes`).

## API keys

```csharp
var created = await apiKeys.CreateAsync(new WebApiKeyRequest
{
    Name = "CI deploy", UserId = user.Id, Scopes = ["deploy"], Lifetime = TimeSpan.FromDays(90)
}, ct);
// created.PlainTextKey ("wl_<prefix>_<secret>") is shown once.
```

Sent as `X-Api-Key`, `Authorization: ApiKey ...` or `Authorization: Bearer wl_...`. Only the SHA-256
of the secret is stored; comparison is constant-time. Keys can be limited by scope, expiry, networks
(CIDRs), tenant and their own rate-limit policy, and stop working when revoked or when their owner is
disabled or deleted. API-key requests are not "ambient", so they need no CSRF token.

## Bearer tokens, ASP.NET schemes, trusted headers

- `web.AddBearerTokens<TValidator>()`: you validate the token (JWT or opaque) in `IWebBearerTokenValidator`
  and return a `WebIdentity`.
- `web.AddAspNetAuthenticationScheme("Negotiate", ambient: true)`: reuse an ASP.NET handler (Windows
  auth, JwtBearer, ...).
- `auth.trustedHeader`: identity from a fronting proxy such as oauth2-proxy, accepted only from a
  trusted proxy address **and** with the shared secret header.

## External providers

Implement `IWebExternalLoginProvider` (`GetChallengeUrlAsync`, `HandleCallbackAsync`) and register it
with `web.AddExternalLoginProvider<T>()`. `WebExternalLoginFlow` handles state and nonce (in a
protected `__Host-` cookie), the safe return URL and linking. The starter sample has a complete Steam
OpenID 2.0 provider.

## Tenants

`web.UseHostTenants(new Dictionary<string, string> { ["a.example.com"] = "a", ["b.example.com"] = "b" })`
or your own `IWebTenantResolver`. The tenant flows into the identity, rate-limit partitions, output
cache keys, realtime delivery, API-key checks and `RequireSameTenant()`. Users with a tenant cannot
sign in on another tenant.
