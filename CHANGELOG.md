# Changelog

## Unreleased

### Added

- `CodeLogic.WebLogic.Testing` ships a package readme (usage example and helper overview); the
  `CodeLogic.WebLogic` readme gains a quick start and links to the docs.

### Fixed

- CI: golden template snapshots are found under deterministic (`/_/`) build paths, the Markdown
  pathological-input test compares scaling instead of wall-clock time, and parallel Docker builds
  lock the shared NuGet cache.

## 4.9.1-preview (2026-10-25)

First preview of the rebuild on nuget.org.

### Added

- New repository: CL.WebLogic rebuilt from the ground up on CodeLogic 4.9 and published as
  `CodeLogic.WebLogic`. Breaking with the 0.x line — see `docs/guides/migrating.md`.
- `WebLogicLibrary` implements `IConfigReloadable`: `weblogic` config changes are applied live
  through `WebLogicConfigHolder`; restart-only fields are flagged (`WebLogicRestartRequiredEvent`,
  Degraded health) instead of failing the change.
- `IWebLogicContext` decouples toolkit services from the static CodeLogic runtime.
- `CodeLogic.WebLogic.Testing` package with `FakeWebLogicContext` and `RecordingLogger`.
- Hosting: `AddCodeLogicWebLogic()` / `UseCodeLogicWebLogic()` / `MapCodeLogicWebLogic()`. Every
  WebLogic route is a real ASP.NET endpoint (templates and constraints such as `{id:int}`, 405 with
  `Allow`, HEAD for GET routes, per-endpoint CORS) and mixes freely with minimal APIs.
- Routing: `IWebContributor.ContributeAsync(WebContributionBuilder, ct)` with `MapPage`, `MapApi`,
  `MapFragment`, `MapSse`, `MapFallback` and nested `MapGroup` prefixes whose options (anonymous
  access, groups, permissions, policies, CORS, rate limit, body limit, middleware) are inherited.
  Routes are **secure by default**; anonymous routes are listed at registration.
- Contributions are all-or-nothing and removable: `WebContributionHandle`, `OnRemove` cleanups,
  ambiguity detection, a single fallback. CodeLogic plugins implementing `IWebContributor` are
  registered on load and removed on unload.
- Results: `Html`, `Text`, `Json` (per-contributor serializer options), `Bytes`, local-only
  `Redirect` / explicit `ExternalRedirect`, `File` (ranges, ETag), `Problem` / `ValidationProblem`
  (RFC 7807), `NoContent`, `Status`, `Commands` (allowlisted client commands), `Sse`, `FromAspNet`.
- `WebRequestContext` per request (no static accessor) with bounded body readers (413 above the
  route limit), typed route values and cancellation on client disconnect.
- Exception boundary: correlation ids, safe 500 pages (details only in Development), ProblemDetails
  for APIs, bodies for empty 4xx/5xx responses, client disconnects logged at Debug.
- Health probes `/_weblogic/health/live` and `/_weblogic/health/ready`.
- Theme assets served only from the theme's `assets` folder with path containment, dotfile and
  symlink refusal, an extension allowlist, ETag / Last-Modified and immutable caching for `?v=` URLs.
- Templates: the AST template engine, ported and split into small types. Two engines share one set
  of execution semantics (`TemplateOps`): `ClosureTemplateCompiler` (default; compiles a template
  once into delegates) and `TemplateInterpreter` (the reference AST walk), selected with
  `Theme.TemplateEngine`. The legacy regex engine is gone.
- Context-aware output encoding: the parser infers whether each value lands in element text, a
  quoted attribute, a URL attribute (scheme allowlist: http, https, mailto, tel), a `style`
  attribute, `<script>` or `<style>`. Unsafe positions (inside a tag, unquoted attributes, `on*`
  handlers, script values without `|json`/`|js`) are template **parse errors**. New filters
  `|json`, `|js`, `|attr`, `|css`, `|url`, `|markdown`, `|sanitize`, `|join`; `|nl2br` now encodes
  first. `WebHtml` marks trusted markup.
- CSP nonces: `{nonce}`, and static `<script>` / `<style>` tags get a per-request nonce
  automatically. New directives `{asset:path}` (fingerprinted, immutable-cacheable URLs),
  `{t:key}` (`IWebTextLocalizer`), `{weblogic_scripts}`; `{foreach}` gains `{empty}`; `{item}` /
  `{alias}` write the current item.
- v1 theme syntax keeps working: `{{key}}` / `{{page.x}}` / `{{item.x}}` tokens (now encoded for
  their context), `{if:accessgroup:…}`, `|uppercase` / `|lowercase`, `a→b` replacements; unknown
  filters still pass the value through.
- Layouts nest, sections defined closer to the page win, partials take parameters, and recursion /
  depth (32) are guarded. `WebResult.Page(...)` and `WebResult.Fragment(...)` (no layout,
  `X-WebLogic-Fragment`).
- `{renderhead}` from `WebPageMeta`: title, description, robots, keywords, canonical, hreflang
  alternates, Open Graph (incl. article times, image alt), Twitter cards and safely serialized
  JSON-LD.
- Theme templates load from the active theme with containment and link refusal; cached compiled
  templates revalidate by timestamp, then source hash, at most every
  `Theme.TemplateChangeCheckSeconds`.
- Themed error pages: `errors/{status}.html`, then `errors/error.html`, falling back to the
  built-in page; API / JSON clients still get ProblemDetails.
- Theme sources: Local, **Git** (CL.GitHelper; clone + fast-forward into `{data}/themes/{id}`) and
  **Storage** (CL.Storage; atomic directory download). `ThemeSyncService.RefreshAsync()` re-syncs on
  demand; a failed update keeps serving the last good copy. `ThemeSyncedEvent`.
- Content: our own `WebMarkdownRenderer` (CommonMark + GFM subset: headings, emphasis,
  strikethrough, code, fenced/indented code, quotes, nested lists, tables, links, images,
  autolinks; raw HTML is written as text; URLs scheme-checked) and `WebHtmlSanitizer` (allowlist,
  re-serialized from our own `HtmlTokenizer`, always balanced, `rel` on links), both covered by XSS
  corpora.
- Template hooks for later milestones are DI contracts with safe defaults: `ITemplateCsrfProvider`,
  `ITemplateWidgetRenderer`, `ITemplateScriptProvider`, `ITemplateAuthorizer`,
  `ITemplateGlobalsProvider`, `ITemplateFilter` (`web.AddTemplateFilter<T>()`).
- Testing: `TemplateTestRenderer` / `InMemoryTemplateLoader` render templates in unit tests
  without a host; golden snapshots run on both engines.

- Security pipeline: ordered `IWebSecurityStage`s in two phases (pre-routing and endpoint),
  registered through DI, validated at startup (unique names, required order and phase) and
  switchable by name (`Security.DisabledStages`, logged). Rejections are audited and published as
  `WebSecurityRejectedEvent` (no client addresses).
- Stages: forwarded headers (off by default, explicit proxies / CIDRs only, hop limit, safe
  `X-Forwarded-Prefix`), host filter and canonical-host redirect, HTTPS redirect to the
  `PublicBaseUrl` host (never the Host header), tenant (`IWebTenantResolver`), IP and country rules
  (CL.NetUtils GeoIP, cached), DNSBL reputation (CL.NetUtils; blocking with a time budget or
  background), global and per-route token-bucket rate limiting (`429` with `Retry-After` and
  `RateLimit-*`; unknown policies fail closed), authentication, request limits (`413`, form field
  limits), authorization and CSRF.
- Response hardening applied when the response starts: nonce-based CSP with `'strict-dynamic'`
  (configurable sources, report-only), HSTS on HTTPS, nosniff, frame protection, Referrer-,
  Permissions-, COOP and CORP policies, `no-store` for signed-in users and CSRF-bearing responses.
  Handlers cannot weaken them except through `request.Security.OverrideHeader` (logged).
- Sessions: one lazy session per request (nothing stored, no cookie until written), `__Host-`
  cookie over HTTPS, token stored only as a SHA-256 hash, idle and absolute timeouts, optional IP
  binding, rotation on sign-in / sign-out / privilege change, sign out everywhere, a sweeper, and
  the `IWebSessionStore` contract (`web.UseSessionStore<T>()`).
- Identity: `WebIdentity` (scheme, ambient flag, groups, permissions, scopes, tenant, amr),
  `IWebAuthenticationHandler` chain (session cookie built in; API keys / bearer / trusted header
  come with the auth milestone), `WebLogic` ASP.NET scheme so `RequireAuthorization()` works on
  minimal APIs.
- Authorization: routes are secure by default; groups, permissions (wildcards, resolved lazily
  through `IWebPermissionResolver`), named policies (`web.AddAuthorizationPolicy`), custom and
  resource-based requirements, `IWebAuthorizationService` for handler code. Pages redirect to
  `Security.LoginPath` with a safe `returnUrl`; APIs get `401` + `WWW-Authenticate`; unknown
  policies deny.
- CSRF v2: session-bound synchronizer tokens (DataProtection-protected pre-session cookie before
  sign-in), masked per render, enforced for every unsafe request to WebLogic routes including
  anonymous ones and the fallback, plus `Sec-Fetch-Site` and `Origin`/`Referer` checks; tokens
  rotate on sign-in / sign-out; `GET /_weblogic/csrf` for SPAs; `{csrf}` in templates.
- Audit: bounded background queue to an app-supplied `IWebAuditSink`; client IPs are HMAC hashed.
- `DevelopmentStoreGuard` refuses to start outside Development with in-memory security stores
  unless `Security.AllowDevelopmentStores` is set.
- `request.Identity`, `request.Session`, `request.Security`; `WebUrl.SafeReturnUrl`.
- Testing: `ManualTimeProvider`; the test host allows development stores by default.

- Auth flows (`Auth/`): `request.Auth` with password sign-in, TOTP and recovery-code second
  factor, magic links, required password change, sign-out (everywhere), refresh and password
  change; `WebSignInStatus` results that never reveal whether an account exists.
- Users: `WebUser` + `IWebUserStore` with optional lockout, e-mail and external-login
  capabilities; null default store, `web.UseUserStore<T>()`, `web.UseInMemoryAccounts()` for
  development.
- Passwords: own PBKDF2-SHA256 hasher (`$wl$pbkdf2-sha256$v=1$i=…`, 600k iterations, optional
  pepper, automatic rehash, pluggable legacy algorithms), NIST-style `WebPasswordPolicy`,
  per-login exponential back-off and per-IP cap (`IWebLoginThrottle`), account lockout.
- Two-factor: enrollment with QR code via CL.TwoFactorAuth, secrets stored DataProtection-
  protected, every time step accepted once, hashed single-use recovery codes
  (`IWebRecoveryCodeStore`).
- Account lifecycle (`WebAccountService`): registration, e-mail verification, password reset,
  e-mail change (confirmed at the new address, old address told), magic links. Tokens are
  DataProtection-protected, purpose-bound, time-limited, bound to the security stamp and
  single-use (`IWebSecurityTokenService`, `IWebOneTimeUseStore`).
- Security stamps: credential changes end other sessions (checked every
  `Auth.SecurityStampCheckMinutes` through the new `IWebSessionValidator` hook); password resets
  end all sessions at once.
- Notifications: background `WebNotificationDispatcher`; `MailSecurityNotifier` (CL.Mail
  templates `weblogic.{kind}` with built-in fallback texts), development outbox writing `.eml`
  files, null notifier by default.
- API authentication: API keys `wl_{prefix}_{secret}` (SHA-256 at rest, constant-time check,
  scopes, groups, tenant, expiry, revocation, allowed networks, per-key rate policy) via
  `X-Api-Key` / `Authorization`; app-validated bearer tokens (`web.AddBearerTokens<T>()`);
  trusted proxy headers (trusted peer + shared secret only); ASP.NET schemes such as Negotiate
  (`web.AddAspNetAuthenticationScheme`).
- External login: `IWebExternalLoginProvider` + `WebExternalLoginFlow` (protected state/nonce
  cookie, safe return URL, account linking).
- Tenancy: `web.UseHostTenants(map)`; users, sessions and API keys of another tenant are
  rejected.
- Events: `UserSignedInEvent`, `UserSignedOutEvent`, `SignInFailedEvent`, `UserLockedOutEvent`,
  `UserRegisteredEvent`, `PasswordChangedEvent`, `EmailConfirmedEvent`, `TwoFactorChangedEvent`,
  `RecoveryCodeRedeemedEvent`, `ApiKeyChangedEvent`.
- `DevelopmentStoreGuard` now also covers the account stores and the outbox.

- Client runtime v2 (`Client/src`, vanilla ES2022, no npm dependencies, CSP-safe): config from
  `#wl-config`, event bus, `http` (same-origin-only CSRF header with lazy token fetch and one
  retry after rotation, keyed cancellation, timeouts, ProblemDetails → `HttpError`), safe `dom`
  helpers, page `modules` (mount/unmount), allowlisted `commands`, ajax `forms` (schema-driven
  client validation, double-submit guard, XHR upload progress, form results), `widgets`
  (`data-wl-action` / `data-wl-refresh`), `realtime` (SSE or WebSocket, back-off with jitter,
  last-event-id resume), partial `navigation` for `[data-wl-shell]` pages (head/title swap,
  history, focus and announcements, full-load fallback), and a pluggable `ui` with `plain` and
  `bootstrap5` adapters. `Client/weblogic.d.ts` ships in the package.
- Our own bundler (`tools/CL.WebLogic.ClientBundler`): exact JS lexer (strings, templates,
  regex vs division, comments), strict module subset, dependency-ordered linking into ESM and
  classic bundles, comment stripping, Brotli/gzip, content hashes and SRI. Runs incrementally
  from MSBuild; output is embedded in `CL.WebLogic.dll`.
- Serving: `/_weblogic/client/{name}.{hash}.{ext}` (immutable, precompressed, ETag/304);
  `{weblogic_scripts}` writes the config block, stylesheet and scripts with SRI and the CSP
  nonce (no CSRF token, so pages stay cacheable). New `Client` config section (adapter, format,
  navigation, stylesheet, test page).
- Head meta written by `WebPageHeadRenderer` is marked `data-wl-head` so navigation can swap it.
- Tests: bundler unit tests, `node --test` suites for the DOM-free modules, and a browser suite
  at `/_weblogic/tests` (Development) that the integration tests run in headless Chrome. CI gains
  a client job; the release pre-flight checks the client is embedded and the bundler is not a
  dependency.
- Forms v2 (`Forms/`): definitions from DataAnnotations (`Required`, `StringLength`, `Range`,
  `RegularExpression` with a 1 s timeout, `EmailAddress`, `Url`, `Phone`, `Compare`, `DataType`,
  any other `ValidationAttribute`, `IValidatableObject`) plus `[WebForm]`, `[WebField]`,
  `[WebOptions]`, `[WebShowWhen]`, `[WebFile]`, `[WebRepeater]`, `[WebStep]` and `[WebIgnore]`;
  fluent `IWebFormConfigurator<T>`; definitions cached per type in a `ConditionalWeakTable`.
- `request.Forms.BindAsync<T>()`: binds only defined fields (no over-posting), invariant browser
  formats, real checkboxes, multi-value fields, indexed repeaters (`Lines[0].Product`), hidden
  conditional fields skipped, rich text sanitized, fixed and provider choices checked, async
  cross-field `IWebFormValidator<T>`s. Messages go through `IWebTextLocalizer` (`forms.*` keys).
- `FormResult`: the client's JSON result (`ok`, `formId`, `errors`, `summary`, `message`,
  `redirect`, `commands`, `data`, `step`; 422 for errors); plain posts get a 303 on success and the
  fallback page re-rendered with values and errors.
- Rendering: `IWebFormRenderer` with `bootstrap5` (default) and `plain` renderers, native
  validation attributes, ARIA (`aria-invalid`, `aria-describedby`, alert summary), the client
  schema block, CSRF, honeypot and timestamp fields; theme overrides per field type
  (`forms/fields/{type}.html`). `{form:id action=… class=… submit=… renderer=…}` template
  directive (both engines); `request.Forms.Prefill(model)` / `RenderAsync<T>()`.
- Anti-bot without third parties: honeypot plus a protected render timestamp (minimum and maximum
  age); `FormBotRejectedEvent`, `FormSubmittedEvent`.
- Multi-step forms: progress in a DataProtection-protected token (stateless, cluster-safe);
  earlier steps can't be altered by later posts; Back button; works without script.
- Uploads: count and size limits, extension allowlist, content sniffing that must match the
  extension, our own `ImageHeaderReader` (PNG, JPEG, GIF, WebP, BMP dimensions), cleaned display
  names; `IWebUploadSink` with a CL.Storage-backed default that stores under random names.
- Endpoints, always registered: remote field validation
  (`POST /_weblogic/forms/{form}/validate/{field}`, CSRF) and options/search
  (`GET /_weblogic/forms/options/{provider}`, anonymous or policy per provider), rate limited
  by `Forms.EndpointRatePolicy`. `WebFormHttpLookupProvider` takes an absolute https URL template
  fixed in code and calls only hosts in `Forms.AllowedLookupHosts` (the old Host-header SSRF is
  gone), with no redirects, timeouts, size caps and a bounded cache.
- Client: `formextras.js` adds show-when toggling (hidden fields are disabled and not validated),
  repeaters from `<template>`, an accessible autocomplete listbox and remote validation; schema
  messages override the defaults; `formnovalidate` submitters skip client validation.
- Builder: `AddForm<T>()`, `ConfigureForm<T, TConfigurator>()`, `AddFormValidator<T, TValidator>()`,
  `AddFormOptions(…)`, `AddFormRenderer<T>()`, `UseUploadSink<T>()`. New `Forms` config section.
- Widgets (`Widgets/`): `b.MapWidget(name, handler, o => …)` from contributors and plugins (removed
  with the contribution), template or trusted-HTML results, actions, instance settings through the
  app's `IWebWidgetSettingsStore`, access rules per widget (signed-in by default), client refresh
  intervals. `{widget:name attr="…"}` and `{widgetarea:name}` render through `WebWidgetRenderer`;
  placements (`b.PlaceWidget(area, widget, …)`) are ordered, filtered by path globs and access; a
  failing widget is logged and replaced by a comment. Endpoints are always registered:
  `GET /_weblogic/widgets/{name}` (fragment) and `POST /_weblogic/widgets/{name}/actions/{action}`
  (CSRF, 64 KB JSON).
- Contributions can stage non-route registrations with `OnCommit`; a contribution that fails while
  committing is rolled back completely.
- Dashboards (`Dashboard/`): layout models and pure helpers (`WebDashboardLayouts`: keys, normalize,
  move, add, remove), app-supplied `IWebDashboardLayoutStore` with optimistic versions,
  `IWebDashboardOwnerResolver` (default: the signed-in user, per tenant) and
  `IWebDashboardDefaults`. `GET/PUT /_weblogic/dashboard/{key}` (JSON, or rendered with
  `?format=html`). The owner always comes from the server — the old `ownerKey` query parameter and
  `..` keys are gone. Client `dashboard.js`: edit mode, drag and drop, Alt+Arrow moves, add/remove,
  conflict reload.
- Realtime (`Realtime/`), replacing SignalR: `WebRealtimeHub` with audiences (Public, Authenticated,
  Group, Permission, User, Tenant, Internal) and channels, server-sent events
  (`/_weblogic/realtime/sse`) and WebSockets (`/_weblogic/realtime/ws`, Origin-checked, ping/pong,
  optional `IWebRealtimeReceiver`), Last-Event-ID replay filtered by audience (a lost resume point
  sends `weblogic.replay-gap`), bounded per-connection queues, connection limits per node and per
  client, a maximum connection lifetime, connection tickets for cookieless clients
  (`POST /_weblogic/realtime/ticket`), `IWebRealtimeBackplane` (in-process default), event bridges
  (`web.BridgeEvent<T>(…)`, `b.BridgeEvent<T>(…)`) with explicit data mapping, connections closed on
  sign-out and password change, config change notices (property names only) to operators.
  `Realtime.BroadcastRequests` is off by default and never includes IPs or query strings.
- Caching (`Caching/`): routes opt into the shared-framework output cache with
  `.WithOutputCache(policy)`; WebLogic's policy runs first (anonymous GET/HEAD without credentials;
  nothing stored with cookies, issued CSRF tokens, session writes, non-200 or private responses;
  varies by host, tenant and client type) and the cache sits after endpoint security.
  `web.AddOutputCachePolicy(name, …)`, fragment cache `request.Cache.GetOrAddAsync(…)` (tenant-scoped
  keys, tags, single-flight factories, bounded), `IWebCacheInvalidator.EvictTagsAsync` for both.
- Explorer: `/_weblogic/explorer` and `/_weblogic/explorer.json` list routes with their protection,
  the anonymous routes, the security pipeline, widgets and areas, forms, options providers,
  realtime and contributors. On in Development; elsewhere only with `Explorer.InProduction` and the
  `weblogic.explorer` permission. The page is static, nonced and builds everything as text.
- New config sections `Realtime`, `Cache`, `Explorer`; the client config carries the realtime
  settings (`Realtime.ClientTransport`, `Realtime.ClientAutoConnect`).
- Development stores for widget settings and dashboards log a warning outside Development.
- Samples (`samples/`), rebuilt on the v2 toolkit and runnable as a deployable Docker demo
  (`samples/docker`: compose with MySQL 8.4, Mailpit and an optional Caddy HTTPS proxy):
  - `StarterWebsite`: every toolkit area in use, with app-owned JSON-file stores for users,
    sessions, API keys, recovery codes, dashboards and widget settings. It covers registration
    with e-mail verification, password reset, TOTP with recovery codes, personal API keys for
    `/api/v1`, Steam OpenID sign-in, the forms gallery and wizard, dashboards, the live wall
    (SSE), and output and fragment caching.
  - `MiniBlog`, `MiniBlog.Data` and `Plugins/MiniBlog.Admin.Plugin`: a blog on CL.MySQL2 with
    MySQL stores, cached pages tagged `posts`, RSS and sitemap, posts and the editor account
    seeded once, and the admin area as a CodeLogic plugin.
  - The Docker images pack the toolkit and publish the samples against the package. They run in
    Production as a non-root user with a read-only root filesystem and keep all state in `/data`.
  - `samples/docker/smoke.py` is an end-to-end test over HTTP. A new CI job (`samples`) builds
    the images, runs the stack, and runs the smoke tests before and after a restart.
  - The samples are in the solution, so CI compiles them with warnings as errors.
- Contributors and plugins can register forms and options providers: `b.MapForm<T>()` and
  `b.MapFormOptions(provider)`. They are removed with the contribution, and a name that is
  already registered fails the contribution.
- `WebDashboardErrors`: the error codes app stores return (`Conflict()` → 409, `Full()` → 503).

- Plugins can ship templates: `b.MapTemplates(assembly, "theme/")` registers embedded `.html`
  resources (`b.MapTemplate(path, source)` for one), removed with the contribution. A theme file with
  the same path wins, so sites can restyle plugin pages. The MiniBlog admin plugin ships its own
  templates this way.
- Cluster mode (`Cluster.Enabled`): startup fails while state that must be shared (sessions,
  accounts, API keys, recovery codes, one-time tokens, login throttle, rate limits, realtime
  backplane, notifier) is per node or the DataProtection key ring is not shared; warns for widget
  settings, dashboards, fragment and output caches and a missing application name.
  `Cluster.AllowNodeLocal` accepts deliberate exceptions (logged). Per-node stores implement the
  marker `IWebNodeLocalStore`.
- `WebRealtimeHub.PublishToTenantAsync(tenantId, …)`; envelopes carry an optional `TenantId` and
  are only delivered to connections of that tenant.
- `RateLimits.Ipv6PrefixLength` (default 64): IPv6 clients are limited per prefix and IPv4-mapped
  addresses count as IPv4, in the rate limits and the login throttle.
- `tools/CL.WebLogic.LoadTest`: a dependency-free HTTP load generator (requests/s, p50/p95/p99,
  status codes; `--header` for cookies).
- Documentation site (`docs/`, DocFX, `docs.yml` publishes to GitHub Pages): getting started, 16
  guides and 9 scenario guides (marketing site, CMS, admin, multi-tenant SaaS, SPA/mobile API,
  intranet, Steam community, plugin platform, cluster), plus the security model with its documented
  limits.

### Changed (breaking)

- `IWebWidgetSettingsStore` is keyed by owner and instance: `GetAsync(ownerKey, instanceId, ct)`,
  and `WebWidgetSettings` gains `OwnerKey`. The owner comes from `IWebDashboardOwnerResolver`
  (dashboard key `widgets`), never from the request. `WebWidgetContext.SaveSettingsAsync` returns
  `false` when there is no owner (anonymous visitors).
- Remote field validation (`/_weblogic/forms/{form}/validate/{field}`) only answers for fields with
  `[WebField(Remote = true)]` (404 otherwise) and applies the options provider's access rules.
- Templates: `|js` is only accepted inside a quoted JavaScript string; elsewhere in `<script>` it is
  a parse error (use `|json`). A value right after `<` or `</` and the `srcdoc` attribute are
  rejected like event handlers.
- The external-login state cookie is `__Host-wl_ext`.
- Cookies are `Secure` and `__Host-`-prefixed, and HSTS is sent, outside Development whenever
  `PublicBaseUrl` is `https`, even if the request arrived over HTTP from an untrusted TLS proxy.
- A wrong password on a locked account now returns `InvalidCredentials`; the lock is only revealed
  together with the correct password.

### Security

Fixes from the pre-release security review (each with a regression test):

- Sign-out fully resets the session (values, CSRF secret, identity) and rotates its id; re-signing
  the same user keeps the absolute timeout. `RefreshAsync` after a security-stamp change deletes the
  user's other sessions.
- One password attempt per login and one second-factor attempt per user run at a time on a node
  (`WebAttemptGate`), so parallel bursts cannot outrun the throttle.
- Widget endpoints enforce placement access rules; widget settings can no longer be read or written
  across owners by guessing instance ids.
- Markdown inline parsing is bounded (no quadratic scans on crafted input).
- Realtime tenant isolation (see `PublishToTenantAsync`).
- The in-memory rate-limit store evicts by each bucket's own refill rate, fullest first, so a flood
  of new addresses cannot reset drained buckets; IPv6 clients cannot rotate addresses inside a /64.
- `ForwardedHeadersStage` parses `X-Forwarded-For` values that carry a port.
- API keys stop working when their owner is disabled or deleted (`api-key-owner`).
- Magic-link sign-in on a never-confirmed address drops a password set at registration, rotates the
  stamp and ends other sessions (account pre-hijacking); it also honours `RequiresPasswordChange`.
- Password reset, magic-link, verification and e-mail-change mails are limited to 3 per address and
  kind per 15 minutes; alerts are never limited.
- The output cache neither serves nor stores responses for requests carrying a session cookie, or
  when the session was loaded.
- Path exclude patterns fail closed when their regex times out.
- `DisabledStages` is fixed at startup; a config reload cannot switch stages off.
- Client: inert script types are allowlisted when swapping HTML, upload CSRF headers go to the same
  origin only, and dashboard URLs must be local.
- Outside Development a warning is logged when DataProtection keys are not persisted.

### Performance

- Theme assets: resolutions (including misses) are cached for 2 seconds and files up to 256 KB are
  served from memory (about 8x the throughput for small assets).
- The template loader caches whether optional templates exist (field overrides, themed error
  pages), so forms and 404 pages no longer touch the file system on every render.

### Fixed

- Kestrel's `Server: Kestrel` header is no longer sent (`AddServerHeader = false`), and
  `X-Powered-By` is removed.
- Default dashboard items get stable instance ids (derived from the owner, key, position and
  widget). Before, every render made new ones, so widget settings saved before the first layout
  save never matched an item.
- `dotnet pack src/CL.WebLogic` on a clean checkout: the client bundler now runs after the
  project references are built (it ran at `BeforeBuild` and found no bundler).

### Removed

- The build-time template source generator (`CL.WebLogic.TemplateCompiler`) is not part of 4.9:
  the closure compiler already compiles each template once into delegates, and the generator would
  need `Microsoft.CodeAnalysis` at build time, against the no-third-party-packages rule.
