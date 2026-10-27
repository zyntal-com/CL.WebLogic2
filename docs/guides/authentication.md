# Authentication and accounts

WebLogic implements the flows: password sign-in with lockout and throttling, passkeys
([guide](passkeys.md)), TOTP two-factor with replay protection, recovery codes, password reset, e-mail verification and change, magic links,
API keys, bearer tokens, external providers and tenants. **Storage is yours**: you implement the
store contracts and WebLogic calls them.

## Stores

| Contract | Holds | Register with |
|---|---|---|
| `IWebUserStore` | Users (`WebUser`): name, e-mail, password hash, security stamp, groups, permissions, tenant, 2FA secret, lockout | `web.UseUserStore<T>()` |
| `IWebUserLockoutStore`, `IWebUserEmailStore`, `IWebUserExternalLoginStore` | Optional capabilities on the same class: atomic failure counting, lookup by e-mail, linked external logins | (implement on the user store) |
| `IWebSessionStore` | Session records keyed by the SHA-256 of the session token | `web.UseSessionStore<T>()` |
| `IWebApiKeyStore` | API keys (hash of the secret, scopes, expiry, networks) | `web.UseApiKeyStore<T>()` |
| `IWebPasskeyStore` | Passkeys (public key, user handle, counter); see [Passkeys](passkeys.md) | `web.UsePasskeyStore<T>()` |
| `IWebRecoveryCodeStore` | Hashed recovery codes; `TryConsumeAsync` must be atomic | `web.UseRecoveryCodeStore<T>()` |
| `IWebOneTimeUseStore` | Spent TOTP steps and single-use links (in memory by default) | `web.UseOneTimeUseStore<T>()` |
| `IWebLoginThrottle` | Back-off per login, cap per client (in memory by default) | `web.UseLoginThrottle<T>()` |
| `IWebSecurityNotifier` | Sends security e-mails | `web.UseMailNotifications()` (CL.Mail) or your own |

Each `Use…<T>()` registers `T` as one singleton for its own type and the contract, so the app can
inject its store by type (for its extra methods) and gets the instance WebLogic uses.

`web.UseInMemoryAccounts()` registers in-memory users, sessions, keys and codes for development and
tests. Outside Development the app refuses to start while any of them is still in memory. The
samples have complete stores: JSON files in `samples/StarterWebsite/Infrastructure`; MiniBlog uses
the [`CodeLogic.WebLogic.MySql`](mysql-stores.md) add-on.

## Signing in

```csharp
var result = await request.Auth.PasswordSignInAsync(model.Login, model.Password, persistent: model.RememberMe, ct);
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
`SignInWithMagicLinkAsync(token)`, `SignInExternalAsync(user, provider)`,
`CompletePasswordChangeAsync(token, password)` (a refused password keeps the token and sets
`result.Error` to the policy message, safe to show), `ReauthenticateAsync(password, code)`,
`SignInAsync(user, methods, persistent)` (after your own checks; skips the second factor; `persistent: null`
keeps "remember me" when the same user is signed in again), `SignOutAsync(everywhere)`,
`RefreshAsync()`, `ChangePasswordAsync(current, new)`, `ListSessionsAsync()`,
`RevokeSessionAsync(idHash)` and `GetCurrentUserAsync()`.

## Remember me

Every sign-in method takes `persistent` ("remember me"). It carries through the second factor and a
required password change. Remembered sessions use `Session.PersistentIdleTimeoutDays` (30) and
`Session.PersistentAbsoluteTimeoutDays` (90) and a cookie that survives a browser restart; other
sessions end with the browser session (or the normal idle/absolute timeouts). Security-stamp checks,
rotation and "sign out everywhere" apply to both.

## Step-up (recent sign-in)

Sensitive pages can demand that the user proved their credentials recently, optionally with specific
methods:

```csharp
account.MapPage("/delete", DeleteAccountAsync).RequireRecentSignIn(TimeSpan.FromMinutes(10));
b.MapGroup("/billing").RequireRecentSignIn(TimeSpan.FromMinutes(15), "pwd", "otp");
web.AddAuthorizationPolicy("payouts", p => p.RequireAnyGroup("finance").RequireRecentSignIn(TimeSpan.FromMinutes(5), "otp"));
```

When it fails, pages redirect to the login path with `reauth=1&returnUrl=…` and APIs answer `401`
with `WWW-Authenticate: WebLogic error="reauthentication_required"`. Users prove themselves with
`request.Auth.ReauthenticateAsync(password, code)` (the code is required when they use two-factor
authentication), `ReauthenticateWithPasskeyAsync` ([Passkeys](passkeys.md)) or by simply signing in
again; refreshing the session does not count. Users without a password sign in again through their
magic link, provider or passkey.

The listed methods must **all** have been used in the latest sign-in. The method names are `pwd`,
`otp`, `recovery`, `passkey`, `link` and `ext:{provider}`. A passkey sign-in records only `passkey`,
so `"pwd", "otp"` above sends passkey users to prove themselves again with a password and code.

To accept several ways of proving it, give alternatives; each is a set that must be complete:

```csharp
b.MapGroup("/admin").RequireRecentSignInAnyOf(TimeSpan.FromHours(8), ["pwd", "otp"], ["passkey"]);
web.AddAuthorizationPolicy("payouts", p => p.RequireRecentSignInAnyOf(TimeSpan.FromMinutes(5), ["otp"], ["passkey"]));
```

## Sessions as devices

```csharp
var sessions = await request.Auth.ListSessionsAsync(ct);     // browser, created, last seen, remembered, IsCurrent
await request.Auth.RevokeSessionAsync(sessions[1].IdHash, ct); // "sign out that device"
```

Session records keep the browser's user agent, the authentication time and "remember me". Listing
needs a session store that implements the optional `IWebSessionListStore`
(`ListForUserAsync(userId)`); both samples do. The same capability enables:

- `Session.MaxPerUser`: the oldest sessions end when a new sign-in exceeds the cap.
- `Auth.Notifications.NotifyNewDevice`: an e-mail when someone signs in from a browser none of the
  user's other sessions use. Browsers are compared by browser and system family ("Chrome on Windows",
  `WebUserAgent`), so a browser update is not a new device; the e-mail and `WebSessionInfo.Device`
  carry that name. An unrecognised user agent is named "an unknown browser": the raw header, which
  the client controls, never goes into the e-mail (the `device` template value).
- Ending a session (`RevokeAsync`, the per-user cap, a password reset) also closes its realtime
  connections: at once on the same node, and within `Realtime.SessionCheckSeconds` on the others.

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

## Suspending accounts

Set `WebUser.SuspendedUntil` (and `SuspensionReason`) to ban a user for a while, or with
`DateTimeOffset.MaxValue` until lifted. Unlike `IsDisabled`, the user is told why:

- Sign-in methods return `WebSignInStatus.Suspended` with `result.Suspension` (until, reason), but
  only after the password, link or provider proved the user, so it tells a password guesser nothing.
- Existing sessions end at their next security-stamp check (`auth.securityStampCheckMinutes`; change
  the stamp as well to end them at once), and the user's API keys stop working.
- `WebResult.AccountSuspended(result.Suspension)` answers 403 with the theme's `errors/suspended`
  template (model: `reason`, `until`, `indefinite`), a plain built-in page without one, and
  ProblemDetails (`suspendedUntil`) for API clients.

Your user store persists the two fields like any other (the samples show JSON and MySQL).

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
protected `__Host-` cookie), the safe return URL and linking. Finish with
`request.Auth.SignInExternalAsync(user, provider)`: it refuses disabled, locked and other-tenant
accounts and asks users with two-factor authentication for their code, exactly like a password
sign-in. (`SignInAsync` would skip the second factor.)

Ready-made providers (Steam, Google, Microsoft, GitHub, Discord and any OpenID Connect issuer, with
PKCE and in-house ID token validation) are in the
[`CodeLogic.WebLogic.ExternalLogins`](external-logins.md) add-on; the starter sample uses it.

## Tenants

`web.UseHostTenants(new Dictionary<string, string> { ["a.example.com"] = "a", ["b.example.com"] = "b" })`
or your own `IWebTenantResolver`. The tenant flows into the identity, rate-limit partitions, output
cache keys, realtime delivery, API-key checks and `RequireSameTenant()`. Users with a tenant cannot
sign in on another tenant.
