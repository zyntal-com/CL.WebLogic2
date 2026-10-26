# Passkeys

Passkeys (WebAuthn) let users sign in with their device's fingerprint, face or PIN instead of a password.
WebLogic verifies everything in-house: CBOR, COSE keys, authenticator data, signatures and the `none`,
`packed` and `fido-u2f` attestation formats. It uses `System.Security.Cryptography` only, with no
third-party packages.

One passkey covers three uses:

| Use | API | Notes |
|---|---|---|
| Passwordless sign-in | `BeginPasskeySignInAsync` / `PasskeySignInAsync` | Discoverable passkeys with user verification. This counts as multi-factor, so users with TOTP are not asked for a code. |
| Second factor | `BeginPasskeySecondFactorAsync` / `CompleteTwoFactorWithPasskeyAsync` | Instead of the TOTP code after a password. A touch-only security key is accepted only with `AllowUnverifiedSecondFactor`. |
| Step-up | `BeginPasskeyReauthenticationAsync` / `ReauthenticateWithPasskeyAsync` | For `RequireRecentSignIn`. Recorded as method `passkey`: accept it next to a password and code with `RequireRecentSignInAnyOf(age, ["pwd", "otp"], ["passkey"])`. |

## Setup

```csharp
builder.AddCodeLogicWebLogic(web => web
    .UsePasskeyStore<MyPasskeyStore>());     // or UseMySqlStores(...) (CodeLogic.WebLogic.MySql), or UseInMemoryAccounts() in development
```

`IWebPasskeyStore` has six methods. Credential ids are unique across users, and `TryRecordUseAsync`
must be a compare-and-swap on the signature counter. The in-memory, MySQL and Starter (JSON file)
stores show how.

Configuration (`Auth.Passkeys`):

| Setting | Default | |
|---|---|---|
| `Enabled` | `true` | Turns the ceremonies on or off. |
| `RelyingPartyId` | host of `PublicBaseUrl` | The domain passkeys are bound to. A parent domain (`example.com`) also covers its subdomains. Changing it orphans existing passkeys. |
| `RelyingPartyName` | `SiteName` | Shown by the authenticator. |
| `Origins` | origin of `PublicBaseUrl` | Pages allowed to run ceremonies (https, or http only on localhost). |
| `TimeoutSeconds` | 300 | |
| `AllowUnverifiedSecondFactor` | `false` | Accept a security key without a PIN after the password. |
| `RequestAttestation` | `false` | Ask for attestation. `packed` and `fido-u2f` are then verified and other formats refused. |
| `MaxPerUser` | 20 | |

Set `PublicBaseUrl` in production. Without it, the RP id and origin come from the request.

## Routes

The toolkit has no fixed URLs. Map two JSON endpoints per ceremony:

```csharp
var account = b.MapGroup("/account");
account.MapApi("/passkeys/sign-in/begin", async (r, ct) => Json(await r.Auth.BeginPasskeySignInAsync(ct)), "POST").AllowAnonymous().WithRateLimit("login");
account.MapApi("/passkeys/sign-in/finish", async (r, ct) =>
{
    var result = await r.Auth.PasskeySignInAsync((await r.ReadJsonAsync<WebPasskeyRequest>(ct))!, ct);
    return WebResult.Json(result.Succeeded ? new { ok = true, redirect = "/account" } : new { ok = false, message = "That passkey could not be used." });
}, "POST").AllowAnonymous().WithRateLimit("login");

account.MapApi("/passkeys/register/begin", async (r, ct) => Json(await r.Auth.BeginPasskeyRegistrationAsync(ct)), "POST");
account.MapApi("/passkeys/register/finish", async (r, ct) =>
{
    var added = await r.Auth.CompletePasskeyRegistrationAsync((await r.ReadJsonAsync<WebPasskeyRequest>(ct))!, ct);
    return WebResult.Json(new { ok = added.IsSuccess, message = added.IsSuccess ? "Passkey added." : added.Error!.Message, redirect = added.IsSuccess ? "/account" : null });
}, "POST");

static WebResult Json(Result<WebPasskeyChallenge> c) => c.IsSuccess ? WebResult.Json(c.Value!) : WebResult.Problem(400, c.Error!.Message);
```

Begin endpoints return `{ options, state }`. Finish endpoints receive a `WebPasskeyRequest`
(`{ credential, state, name?, remember? }`). Management goes through `request.Auth.ListPasskeysAsync`,
`RenamePasskeyAsync` and `DeletePasskeyAsync`. Adding and removing a passkey e-mails the user
(`PasskeyAdded`, `PasskeyRemoved`). Consider `RequireRecentSignIn` on the registration routes.

## Client

The client runs the browser side without any page script:

```html
<button data-wl-passkey="signin" data-wl-passkey-begin="/account/passkeys/sign-in/begin"
        data-wl-passkey-finish="/account/passkeys/sign-in/finish" data-wl-passkey-remember="#remember">Sign in with a passkey</button>

<!-- Passkey autofill on the username field (autocomplete="username webauthn") -->
<span hidden data-wl-passkey="autofill" data-wl-passkey-begin="..." data-wl-passkey-finish="..."></span>

<input id="passkey-name"><button data-wl-passkey="register" data-wl-passkey-name="#passkey-name"
        data-wl-passkey-begin="/account/passkeys/register/begin" data-wl-passkey-finish="/account/passkeys/register/finish">Add a passkey</button>
```

The finish response is applied like a form result: `message` is shown as a toast, `commands` are run,
and a local `redirect` is followed. A cancelled ceremony stays quiet. Buttons are hidden in browsers
without WebAuthn. From script, use `WebLogic.passkeys.signIn(beginUrl, finishUrl, extra)`, `register`,
`verify`, `startAutofill`, or the low-level `create` and `get`.

## What is checked

| Ceremony | Checks |
|---|---|
| Both | Ceremony state is DataProtection-sealed (purpose, user, RP id, expiry) and its challenge is accepted once (`IWebOneTimeUseStore`), so it works across nodes. `clientDataJSON` type, challenge (constant time) and origin are checked, and cross-origin iframes are refused. The RP id hash must match, user presence is required, and user verification is required when asked for. A backed-up passkey must also be backup-eligible. |
| Registration | The credential id must match the authenticator data. The COSE key must be ES256/384/512 on the matching curve, or RS256/PS256 with at least 2048 bits. EdDSA is not supported. The attestation statement is verified. The passkey limit applies, and credential ids are unique across users. |
| Assertion | The credential must belong to the expected user (second factor, step-up). The user handle must match and is required for passwordless sign-in. The signature is checked against the stored key (ECDSA in DER form). The signature counter must move forward: a counter going backwards refuses the sign-in and publishes `PasskeyCloneSuspectedEvent`. The counter update is a compare-and-swap. |

Account rules still apply after a valid passkey: disabled, other-tenant and suspended accounts are
refused, and a required password change still applies. Two choices are deliberate:

- **The failed-attempt lockout does not block passkeys.** It exists to stop password guessing, which
  a passkey is not. A successful passkey sign-in clears it.
- **Failed passkey attempts never count towards lockout.** Credential ids are not secret, so counting
  them would let anyone lock a user out. Failures are throttled per credential and per client address
  instead.

Attestation certificates are checked for signature, version, `OU=Authenticator Attestation`, the CA
flag and the AAGUID extension. Their chains are not evaluated, because there is no FIDO metadata
service here. `WebPasskey.AttestationType` records what was proven.

## Testing

`CodeLogic.WebLogic.Testing` includes `WebTestAuthenticator`, a software authenticator and browser in
one. It turns your begin responses into the JSON a browser would post, using real keys and signatures:

```csharp
using var authenticator = new WebTestAuthenticator("https://site.test") { Attestation = WebTestAttestation.PackedSelf };
var begin = await PostAsync("/account/passkeys/register/begin");             // { options, state }
var credential = authenticator.Create(begin.GetProperty("options"));
await PostAsync("/account/passkeys/register/finish", new { credential, state = begin.GetProperty("state").GetString() });
```

You can set `Origin`, `UserVerified`, `SignCountStep` or a credential's `SignCount` to exercise the
failure paths. WebLogic's own suite also runs the client in headless Chrome against a DevTools virtual
authenticator.
