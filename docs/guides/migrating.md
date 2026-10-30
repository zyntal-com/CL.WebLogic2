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

## Things that behave differently

- **Unsafe template positions fail at parse time** (unquoted attributes, `on*` handlers, script values
  without `|json`). Fix the template rather than escaping by hand.
- **CSRF on every unsafe request**, anonymous ones and the fallback included. Plain HTML forms need
  `{csrf}` or `{form:…}`; scripts use `WebLogic.http`.
- **Forwarded headers are off** until you list your proxies.
- **Production refuses in-memory security stores.**
- **Realtime does not broadcast requests** unless `realtime.broadcastRequests` is on, and never with IPs.

## Configuration

The config file is `config.weblogic.json` in the CodeLogic library folder, rewritten with new section
names (see [Configuration](configuration.md)). Start from the generated defaults and copy values over;
do not reuse the 0.x file.
