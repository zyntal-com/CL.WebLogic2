# Security model

What WebLogic protects, with what, and where your application has to take over.

## Defaults

| Threat | Default protection |
|---|---|
| Forgotten authorization | Every route requires sign-in unless `AllowAnonymous()`; unknown policy names deny |
| CSRF | Synchronizer token on every unsafe request, plus Fetch Metadata and Origin checks |
| XSS in templates | Context-aware encoding decided at parse time; dangerous contexts rejected; URL scheme policy |
| XSS in user HTML / Markdown | Own allowlist sanitizer and Markdown renderer; raw HTML encoded |
| XSS in the client | No untrusted `innerHTML`; swapped scripts do not run; strict CSP with nonce |
| Session fixation / theft | `__Host-` cookies, HttpOnly, SameSite=Lax, Secure; id rotated on sign-in/out; tokens stored hashed |
| Credential stuffing | PBKDF2 600k, per-login back-off, lockout, per-IP cap, one attempt at a time per login, generic errors |
| Account enumeration | Constant work for unknown users; reset/magic-link requests answer the same |
| 2FA bypass / replay | Pending second factor is a separate session stage; each TOTP step used once; attempts throttled |
| Phishing, passkey replay or cloning | Passkey assertions are bound to the origin and RP id; each challenge is sealed and single-use; signatures are verified against the stored key; a counter that goes backwards refuses the sign-in |
| Lockout abuse via passkeys | Failed passkey attempts are throttled per credential and IP but never count towards account lockout (credential ids are not secret) |
| Link token abuse | DataProtection, purpose, expiry, bound to the security stamp, single-use, mail rate-limited per address |
| Account pre-hijacking | Magic-link confirmation of a never-confirmed address drops the pre-set password |
| Stale sessions | Security stamp checks end other sessions after password/2FA/e-mail changes |
| Open redirects | `GetSafeReturnUrl`, local-only redirect results and client commands |
| Host header / proxy spoofing | Allowed hosts; forwarded headers only from configured proxies; canonical URL from config |
| Clickjacking, sniffing, leaks | `frame-ancestors`, COOP/CORP, nosniff, referrer and permissions policy, `no-store` when signed in |
| Cache leaks | Output cache refuses signed-in visitors, session cookies, set cookies and CSRF use; varies by tenant |
| Resource exhaustion | Body and field limits, bounded rate-limit and throttle stores, bounded Markdown scanning, regex timeouts, realtime limits |
| Cross-user data in widgets/dashboards | Owner resolved on the server; settings keyed by owner and instance; placement access enforced |
| SSRF in form lookups | Absolute allowlisted hosts only |
| Upload abuse | Size/count/extension limits, magic-byte detection, image header checks, random stored names |
| Insecure production setup | Startup refuses in-memory security stores, the outbox notifier, and (cluster mode) node-local stores |

## Your responsibilities

- **Resource authorization.** Policies decide who may call a route; whether user A may edit order 42 is
  your handler's decision (`Require(IWebAuthorizationRequirement)` helps).
- **Store correctness.** Atomic failure counting, atomic recovery-code consumption, session deletion
  per user, optimistic concurrency for dashboards.
- **Secrets and keys.** DataProtection key ring, pepper, IP hash key, SMTP credentials.
- **Data you publish.** Realtime payloads, bridged events and widget output reach whoever the audience
  says; send only what they may see.
- **Input you accept.** Widget parameters, realtime messages from clients and query strings are untrusted.

## Known limits

These are documented trade-offs, not open bugs:

- **Endpoints outside WebLogic.** Minimal APIs or controllers you map yourself pass the request-level
  stages and get hardened responses, but WebLogic's authorization and CSRF stages only know WebLogic
  routes. Protect them with ASP.NET authorization (the `WebLogic` scheme carries the identity) or map
  them through a contributor.
- **Realtime tickets** are valid for `realtime.ticketSeconds` (60 s) and can be reused inside that
  window. Keep the window short and do not log tickets.
- **`|js`** only encodes for a quoted JavaScript string; outside quotes the template is rejected. For
  data, prefer `<script type="application/json">{model:data|json}</script>` and read it with
  `WebLogic.dom.readJson`.
- **Legacy password hashes.** Unknown users cost one hash with the current settings. Users whose stored
  hash uses older (cheaper) settings verify faster until they sign in once and are upgraded; timing can
  therefore tell such accounts apart. Force-rehash or reset old accounts if this matters.
- **In-memory throttles are per node and bounded** (100,000 logins / addresses). A flood of distinct
  logins can evict an entry and reset its back-off; account lockout (in your user store) still applies.
  Use a shared `IWebLoginThrottle` for clusters or stricter guarantees.
- **Compression and ETags** skip personal responses. `AllowPersonalCompression()` is an explicit BREACH
  trade-off for that route. ETags hash the uncompressed body and are weak (`W/"…"`), so one tag
  honestly covers the Brotli, gzip and identity forms of a response.
- **CSRF and `SameSite=Lax`.** The token is the protection; Lax cookies are defence in depth. Routes
  marked `AllowCrossSitePost()` (webhooks) must authenticate the sender themselves, for example with a
  signature (`WebWebhookVerifier`, see [Webhooks](security-pipeline.md#webhooks)). Its timestamp window
  limits replays but does not make delivery exactly-once; de-duplicate event ids where that matters.

## Reporting a vulnerability

Please report security issues privately via GitHub security advisories on the repository rather than
in public issues.
