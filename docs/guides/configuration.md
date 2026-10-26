# Configuration

WebLogic is a CodeLogic library. Its settings are one config section, `weblogic`, stored at
`{FrameworkRoot}/Libraries/CL.WebLogic/config.weblogic.json` (camelCase, enums as camelCase strings).
CodeLogic creates the file with every default on first run; you only keep the values you change.

```json
{
  "siteName": "My Site",
  "publicBaseUrl": "https://www.example.com",
  "security": {
    "loginPath": "/account/login",
    "hosts": { "allowedHosts": ["www.example.com"], "redirectToCanonical": true },
    "https": { "enforce": true },
    "forwardedHeaders": { "enabled": true, "trustedProxies": ["10.0.0.5"] }
  },
  "auth": {
    "requireConfirmedEmail": true,
    "passwords": { "pepper": "<long random secret>" },
    "twoFactor": { "issuer": "My Site" }
  },
  "client": { "uiAdapter": "bootstrap5" },
  "realtime": { "clientAutoConnect": true }
}
```

## Sections

| Section | What it controls |
|---|---|
| `siteName`, `publicBaseUrl` | Display name; the canonical URL for e-mail links, redirects and cookies. Never derived from the `Host` header. |
| `theme` | Theme source (`local`, `git`, `storage`), folders, asset prefix and extensions, template engine, change checks |
| `security` | Disabled stages, login path, probe paths and the sub-sections below |
| `security.forwardedHeaders` | Off by default. Trusted proxies/networks, forward limit, `X-Forwarded-Prefix` |
| `security.hosts` | Allowed hosts, redirect to the canonical host |
| `security.https` | Redirect to HTTPS, HSTS |
| `security.ipRules` | Allow/deny CIDRs, allow/deny countries (CL.NetUtils GeoIP) |
| `security.reputation` | DNS blocklists (CL.NetUtils), blocking or background mode |
| `security.rateLimits` | Global per-client limit, named policies, tracked-key bound, IPv6 prefix length |
| `security.limits` | Default body size and form field count |
| `security.session` | Cookie name, idle and absolute timeouts, "remember me" timeouts, sessions per user, touch interval, IP binding |
| `security.csrf` | Header and field names, pre-session cookie, trusted origins |
| `security.headers` | CSP sources (incl. media, worker, manifest, form actions), report-only, report-uri / report-to, frame ancestors, referrer, permissions, COOP/CORP, `no-store` when signed in |
| `security.audit` | Audit on/off, all requests or rejections only, IP hash key, queue size |
| `auth` | Confirmed e-mail requirement, stamp check interval, and the sub-sections below |
| `auth.passwords` | PBKDF2 iterations, pepper, length limits |
| `auth.lockout` | Failures before lockout, lockout minutes, per-login back-off, per-IP cap |
| `auth.twoFactor` | Issuer, recovery code count, minutes allowed for the second step |
| `auth.tokens` | Lifetimes and paths of reset, verification, magic-link and e-mail-change links |
| `auth.apiKeys` | On/off, header name, last-used touch interval |
| `auth.trustedHeader` | Sign-in from a fronting proxy (oauth2-proxy and similar), headers and shared secret |
| `auth.notifications` | From address/name, outbox folder (development), mail template prefix, new-device e-mails |
| `forms` | Renderer, honeypot, submit timing, upload limits and extensions, lookup host allowlist |
| `client` | Client on/off, UI adapter (`plain`, `bootstrap5`), module or classic script, partial navigation |
| `realtime` | On/off, anonymous access, WebSockets, replay, connection limits, allowed origins, tickets |
| `cache` | Output cache on/off, default lifetimes, fragment cache size |
| `explorer` | Explorer in Development / in production (with a permission) |
| `cluster` | Cluster mode validation, contracts allowed to stay node-local |

Every property carries a description in the code (`[ConfigField]`), and CodeLogic tooling shows them.
Secrets (`passwords.pepper`, `audit.ipHashKey`, `trustedHeader.secret`) are marked secret.

## Live reload

WebLogic implements CodeLogic's `IConfigReloadable`. When the file changes, the new configuration is
validated and applied atomically: request-time settings (headers, limits, policies, timeouts) take
effect on the next request. Settings marked *requires restart* (stage list, cookie names, cluster
mode, ...) are reported (`WebLogicRestartRequiredEvent`, readiness health shows `restartPending`) and
take effect after a restart. Disabled security stages are fixed at startup on purpose, so a reload
can never switch off authorization or CSRF protection.

## Settings in code

Anything that is code rather than data is registered on the builder:

```csharp
builder.AddCodeLogicWebLogic(web =>
{
    web.AddAuthorizationPolicy("editors", p => p.RequireAnyGroup("editor", "admin"));
    web.AddOutputCachePolicy("pages", p => p.Tag("pages").Expire(TimeSpan.FromMinutes(5)));
    web.AddTemplateGlobals<SiteGlobals>();   // values every template can read
    web.AddSecurityStage<MyStage>();
});
```

The builder methods are listed in the API reference under `WebLogicBuilder` and the
`WebLogic*BuilderExtensions` classes.
