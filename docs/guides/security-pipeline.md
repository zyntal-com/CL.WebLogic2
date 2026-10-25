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
| 300 | `ip-rules` | request | Allow/deny CIDRs and countries. |
| 350 | `reputation` | request | DNS blocklists, blocking (bounded wait) or background mode, cached. |
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
`RequireScopes`, `RequireSameTenant`, `RequireAssertion(ctx => ...)` and `Require(IWebAuthorizationRequirement)`
for async or resource-based checks. Permissions can come from the identity or from an
`IWebPermissionResolver` (resolved lazily, only when a rule needs them).

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
the nonce to their own `<script>` and `<style>` tags. A handler that sets a security header has it
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
