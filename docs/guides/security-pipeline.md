# Security pipeline

Every request passes an ordered list of stages. Request-level stages run before routing; route-level
stages run after routing (so they know the route's rules) and **before** the output cache, so a
cached page can never skip authorization. Stage order is validated at startup.

| Order | Stage (`name`) | Phase | What it does |
|---|---|---|---|
| 100 | `forwarded-headers` | request | Off by default. `X-Forwarded-For/Proto/Host/Prefix` only from `trustedProxies`/`trustedNetworks`, walking right to left, `forwardLimit` hops. A forwarded host must be an allowed host. |
| 150 | `host-filter` | request | `allowedHosts`; optional redirect to the canonical host (from `PublicBaseUrl`, never from `Host`). |
| 200 | `https` | request | Redirect to HTTPS when `https.enforce`. Probe paths are exempt. |
| 250 | `tenant` | request | Resolves the tenant (`IWebTenantResolver`), used by limits, caching, realtime and authorization. |
| 300 | `ip-rules` | request | Allow/deny/exempt CIDRs and countries, from config and app providers. |
| 350 | `reputation` | request | DNS blocklists (configured or app-provided zones), blocking or background mode, cached. |
| 400 | `global-rate-limit` | request | Token bucket per client (IPv6 grouped per /64). `429` with `Retry-After`. |
| 500 | `authentication` | request | Runs the authentication handlers; sets `request.Identity` and `HttpContext.User`. |
| 600 | `request-limits` | route | Body size, form field count, per-route limits. |
| 650 | `route-rate-limit` | route | Named policies, partitioned by IP, user, API key, route or tenant. |
| 700 | `authorization` | route | The route's access rule or policy. Default: signed-in users only. |
| 800 | `antiforgery` | route | CSRF on every unsafe method. |
| — | response hardening | on start | CSP with nonce, HSTS, COOP/CORP, nosniff, referrer and permissions policy, `no-store` for signed-in pages. |
| — | audit | after | Rejections (or all requests) to a background `IWebAuditSink`, IPs HMAC-hashed. |

Stages can be switched off by name (`security.disabledStages`), which is logged at startup and fixed
until restart. Add your own with `web.AddSecurityStage<T>()` (`IWebSecurityStage`: name, phase, order,
`ExecuteAsync` returning continue or short-circuit). Plugins cannot add request-level stages.

## Authorization

The default for every route is **signed-in users only**. Anonymous access is an explicit
`AllowAnonymous()`, and the explorer lists every anonymous route.

```csharp
web.AddAuthorizationPolicy("billing", p => p
    .RequireAnyGroup("finance", "admin")
    .RequirePermissions("invoices.read")
    .RequireSameTenant());

web.AddAuthorizationPolicy("partner-api", p => p
    .RequireScheme(WebAuthenticationSchemes.ApiKey)
    .RequireScopes("orders:read"));

b.MapApi("/api/invoices", ListInvoicesAsync).RequirePolicy("billing");
```

Requirements: `RequireAnyGroup`, `RequireAllGroups`, `RequirePermissions` (wildcards), `RequireScheme`,
`RequireScopes`, `RequireSameTenant`, `RequireRecentSignIn(maxAge, methods)` (step-up, see
[Authentication](authentication.md#step-up-recent-sign-in)), `RequireAssertion(ctx => ...)` and `Require(IWebAuthorizationRequirement)`
for async or resource-based checks. Permissions can come from the identity or from an
`IWebPermissionResolver` (resolved lazily, only when a rule needs them).

### Areas with their own sign-in page

```csharp
var admin = b.MapGroup("/admin").RequireGroups("admin")
    .WithLoginPath("/admin/login")          // instead of Security.LoginPath
    .WithAccessDeniedPath("/admin/denied"); // signed-in users without access, instead of a 403
```

Both are inherited by nested groups and routes, take local paths only, and add a safe `returnUrl`.
APIs keep answering `401`/`403`.

## CSRF

- A synchronizer token tied to the session (or, before sign-in, to a protected `__Host-wl_pre` cookie).
- Masked differently on every render, compared in constant time.
- Required on **every** unsafe method, for anonymous requests and the fallback route too.
- `Sec-Fetch-Site` and `Origin`/`Referer` are checked as well; add other origins in `csrf.trustedOrigins`.
- Not required for non-ambient credentials (API key, bearer) when no session cookie was sent.
- Rotates on sign-in and sign-out. A page that renders a token is never output-cached.

Templates add the field automatically to `{form:...}`; elsewhere use `{csrf}` (hidden field),
`{csrf_token}` or `{csrf_meta}`. The client fetches a token from `/_weblogic/csrf` when it needs one,
so cached pages never embed a token.

## Response headers

The CSP is built from `security.headers` with a per-request nonce and `strict-dynamic`; templates add
the nonce to their own `<script>` and `<style>` tags. Configurable sources: script, style, image,
connect, font, frame, media, worker and manifest sources, extra `form-action` targets and frame
ancestors; `object-src 'none'` and `base-uri 'self'` are fixed. Violation reports go to
`cspReportUri` (`report-uri`) and/or `cspReportTo` (`Reporting-Endpoints` + `report-to`).

A page that embeds third-party content widens the policy for itself only:

```csharp
b.MapPage("/videos/{id}", VideoAsync).AllowAnonymous()
 .WithCsp(c => c.AddFrameSource("https://www.youtube-nocookie.com"));
```

`WithCsp` works on groups too. Only hosts, schemes (`https:`, `data:`, `blob:`) and `'self'` are
accepted; keywords such as `'unsafe-inline'` are refused at registration. A handler that sets a security header has it
replaced (logged at debug); to change one deliberately use `request.Security.OverrideHeader(name, value)`.
`Server` and `X-Powered-By` are not sent.

## Behind a reverse proxy

```json
"security": {
  "forwardedHeaders": { "enabled": true, "trustedProxies": ["10.0.0.5"] },
  "hosts": { "allowedHosts": ["www.example.com"] }
},
"publicBaseUrl": "https://www.example.com"
```

Only the proxy's address may set forwarded headers; requests sent straight to the app cannot spoof
the client IP or scheme. Outside Development, an `https` `PublicBaseUrl` keeps cookies `Secure` and
`__Host-` and keeps HSTS even if the proxy's headers are not trusted.

## IP rules and blocklists

`security.ipRules` holds static rules. Bans and allow-lists an admin edits at runtime come from an
`IWebIpRuleProvider` (for example a database table), merged with the config rules:

```csharp
web.AddIpRuleProvider<BanTableRules>();          // returns WebIpRule records
web.AddReputationListProvider<DnsblZoneTable>(); // returns WebReputationZone records (optional)

public sealed class BanTableRules(BanRepository bans) : IWebIpRuleProvider
{
    public async ValueTask<IReadOnlyList<WebIpRule>> GetRulesAsync(CancellationToken ct) =>
        [.. (await bans.ListAsync(ct)).Select(b => new WebIpRule
        {
            Id = "ban-" + b.Id, Network = b.Cidr, Action = WebIpRuleAction.Deny,
            ExpiresAt = b.ExpiresAt, Reason = b.Note
        })];
}
```

- **Deny** always wins. **Exempt** (network rules only) marks a trusted address: it passes the
  allow-list, country rules and the DNSBL check. **Allow** entries turn on the allow-list: once any
  exist, only matching clients get in. Country rules (`Country = "XX"`) need CL.NetUtils GeoIP.
- Provider results are cached for `ipRules.providerCacheSeconds` (60). Inject
  `IWebSecurityRulesInvalidator` and call `Invalidate()` after an edit to apply it at once. Expired
  rules stop applying on their own. A provider that fails keeps the last good rules (and logs a
  warning); config rules always apply. Invalid rules are skipped with a warning.
- Rejections publish `WebSecurityRejectedEvent` with `RuleId` (your rule id, `config.deny[0]`, or
  `dnsbl:{zone}`), never the client address, so you can keep hit counters.
- When reputation providers return zones, they replace the CL.NetUtils services (primary and
  fallback, IPv4 and IPv6 per zone); `reputation.enabled` still switches the check on.

## Rate limits

```json
"security": {
  "rateLimits": {
    "globalPermitsPerMinute": 1200,
    "policies": [
      { "name": "login", "permits": 10, "windowSeconds": 300, "partition": "ip" },
      { "name": "search", "permits": 60, "windowSeconds": 60, "partition": "user" }
    ]
  }
}
```

The default store is in memory and bounded (`maxTrackedKeys`); when it fills up, idle and fullest
buckets go first so a flood of fresh addresses cannot reset someone's drained limit. For several
nodes supply a shared `IWebRateLimitStore` (`web.UseRateLimitStore<T>()`).

## Audit

`security.audit` records rejected requests (or every request with `recordAllRequests`) and the auth
events (sign-in, failures, lockouts, 2FA, keys) to `IWebAuditSink` (`web.UseAuditSink<T>()`) through a
bounded background queue. Client IPs are HMAC-hashed with `audit.ipHashKey`. The default sink discards
entries; supply one to store them where your app keeps such data. Every rejection is also published as
`WebSecurityRejectedEvent` on the CodeLogic event bus.
