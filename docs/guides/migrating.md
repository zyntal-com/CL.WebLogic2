# Migrating from 0.x

WebLogic 4.9 (`CodeLogic.WebLogic`) is a rewrite. Concepts carry over, but the APIs do not, so
treat a migration as a port. The usual order: hosting, stores, routes, templates, forms, then the
client scripts.

## Concept map

| 0.x | 4.9 |
|---|---|
| `services.AddCodeLogicWebLogic()` + one catch-all endpoint | `builder.AddCodeLogicWebLogic(web => …)`, `app.UseCodeLogicWebLogic()`, `app.MapCodeLogicWebLogic()`; every route is a real endpoint |
| `IWebRouteContributor.RegisterRoutesAsync(WebRegistrationContext)` | `IWebContributor.ContributeAsync(WebContributionBuilder, ct)` with `MapPage`/`MapApi`/`MapGroup` |
| `WebLogicRequest` / `WebRequestContext.Current` (static) | The `WebRequestContext` passed to every handler |
| Routes open unless protected | Routes require sign-in unless `AllowAnonymous()` |
| `IWebIdentityStore`, `WebIdentityProfile` | `IWebUserStore` + optional `IWebUserLockoutStore`, `IWebUserEmailStore`, `IWebUserExternalLoginStore`; `WebUser` |
| `IWebSessionStore` (shared static empty session) | `IWebSessionStore` storing hashed tokens; lazy per-request sessions |
| Manual `RotateAfterSignInAsync` | `request.Auth.PasswordSignInAsync` / `SignInAsync` rotate automatically |
| `IWebRequestAuditStore` | `IWebAuditSink` (background queue, hashed IPs) |
| File stores for dashboards and widget settings in the toolkit | App-owned `IWebDashboardLayoutStore` / `IWebWidgetSettingsStore` (JSON versions in the StarterWebsite sample) |
| Dashboard owner from the query string | `IWebDashboardOwnerResolver` on the server |
| `ThemeManager`, regex template engine | `ThemeTemplateLoader` + closure compiler; AST syntax with context-aware encoding |
| `{{key}}` tokens | Still parsed (now encoded for their context); prefer `{model:key}` |
| `WebFormFieldAttribute`, static `WebFormBinder`/`WebFormRenderer` | DataAnnotations + `[WebField]`, `request.Forms.BindAsync<T>()`, `{form:id}` |
| `IWebFormSearchProvider` | `IWebFormOptionsProvider.SearchAsync` |
| Static `WebFormMapper.Map<TSource,TTarget>`, `[WebFormMapFrom]` | Back as the injectable `WebFormMapper` (`request.Forms.Mapper`): `ToEntity` / `ToForm`, `[WebFormMapFrom]`, `[WebFormMapIgnore]`; form → entity writes only the form's fields ([Forms](forms.md#mapping-to-entities)) |
| SignalR realtime (`SignalRWebLogicRealtimeBroadcaster`) | Own SSE/WebSocket hub `WebRealtimeHub`, audiences, `IWebRealtimeBackplane` |
| jQuery-based client | Dependency-free ES modules (`window.WebLogic`), UI adapters |
| SkiaSharp image checks | Own image header reader |
| Debug-flag explorer | `/_weblogic/explorer`, Development by default |
| `IWebPageScript` / `WebPageScriptContext` | A route handler (`MapPage(path, handler)`) |
| `IWebPermissionResolver.GetPermissionsAsync(userId, ct)` | `IWebPermissionResolver.ResolveAsync(WebIdentity, ct)` (same name, new signature: the whole identity, not only the user id) |
| `WebRequestHandledEvent` | `audit.recordAllRequests` (an `IWebAuditSink` entry per request) |
| `WebRequestBlockedEvent` | `WebSecurityRejectedEvent` (stage, reason) |
| `ThemeSynchronizedEvent` | `ThemeSyncedEvent` |
| `UseCompiledTemplates` / `UseAstEngine` (template source generator) | `theme.templateEngine` (closure compiler by default, or the interpreter); the source generator was dropped |

## Things that behave differently

- **Unsafe template positions fail at parse time** (unquoted attributes, `on*` handlers, script values
  without `|json`). Fix the template rather than escaping by hand.
- **CSRF on every unsafe request**, anonymous ones and the fallback included. Plain HTML forms need
  `{csrf}` or `{form:…}`; scripts use `WebLogic.http`.
- **Forwarded headers are off** until you list your proxies.
- **Production refuses in-memory security stores.**
- **Realtime does not broadcast requests** unless `realtime.broadcastRequests` is on, and never with IPs.
- **Compression is opt-in.** 0.x compressed every response (`security.enableCompression`, on by default).
  In 4.9 it is `compression.enabled` (off), and signed-in, session, CSRF and cookie-setting responses
  are never compressed (BREACH). See [Caching](caching.md).

## Configuration

The config file is `config.weblogic.json` in the CodeLogic library folder, rewritten with new section
names (see [Configuration](configuration.md)). Start from the generated defaults and copy values over;
do not reuse the 0.x file. Where the common 0.x settings went:

| 0.x | 4.9 |
|---|---|
| `theme.*` (source, paths, repository, branch) | `theme.*`, mostly the same names; Git credentials and storage sources added |
| `theme.enableCaching` | Always on; `theme.templateChangeCheckSeconds` controls reloads |
| `security.enforceHttps` | `security.https.enforce` |
| `security.trustForwardedHeaders`, `trustedProxies`, `trustedNetworks` | `security.forwardedHeaders.enabled`, `.trustedProxies`, `.trustedNetworks` |
| `security.enableDnsbl` | `security.reputation.enabled` (+ `mode`) |
| `security.ipAllowlistResolver` (a delegate in config) | `security.ipRules` and an app `IWebIpRuleProvider` |
| `security.enableExplorerRoutes` | `explorer.inDevelopment` / `explorer.inProduction` |
| `security.enableCsrf` | Always on; `security.csrf.*` names the header, field and trusted origins |
| `security.enableCompression` | `compression.enabled` (off) |
| `security.allowedMethods` | Gone: each route declares its methods |
| `security.rateLimit.requestsPerWindow` / `windowSeconds` | `security.rateLimits.globalPermitsPerMinute` / `globalBurst`, plus named `policies` |
| `security.headers.enableCsp`, `cspDirectives`, `cspReportOnly` | `security.headers.cspEnabled`, per-directive source lists, `cspReportOnly` (nonce-based, no `unsafe-inline`) |
| `security.headers.enableHsts`, `hstsMaxAgeSeconds`, … | `security.https.hsts`, `hstsMaxAgeDays`, `hstsIncludeSubDomains`, `hstsPreload` |
| `security.headers.frameOptions`, `referrerPolicy`, `permissionsPolicy` | `security.headers.frameAncestors`, `referrerPolicy`, `permissionsPolicy` |
| `session.cookieName`, `idleTimeoutMinutes` | `security.session.cookieName`, `idleTimeoutMinutes` |
| `session.rememberMeDays` | `security.session.persistentIdleTimeoutDays` / `persistentAbsoluteTimeoutDays` |
| `session.maxConcurrentSessions` (3) | `security.session.maxPerUser` (**0 = unlimited** by default) |
| `session.bindToClientIp` (**on**) | `security.session.bindToClientIp` (**off** by default) |
| `session.cookieDomain`, `cookieSecure`, `cookieSameSite` | Gone: the cookie is always `__Host-`, `Secure`, `SameSite=Lax` and host-only, so a session cannot be shared across subdomains |
| `storage.*` (toolkit file stores) | Gone: stores are app-supplied (`web.UseXxx<T>()`); the samples show JSON and MySQL stores |
| `widgets.enablePersistentSettings`, `settingsFileName` | An app `IWebWidgetSettingsStore` |

Two defaults got weaker on purpose and are worth setting if you relied on them:
`security.session.maxPerUser` and `security.session.bindToClientIp`.
