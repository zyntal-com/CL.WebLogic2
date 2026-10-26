# CL.WebLogic Guidance

## Purpose

`CL.WebLogic` is a toolkit and foundation for building web applications on top of CodeLogic.

It should provide strong runtime primitives and reusable helpers, not force one storage model,
one database, one CMS shape, or one application policy.

## Core Design Rule

Keep the toolkit generic.

That means `CL.WebLogic` should contain:

- startup and ASP.NET handoff/runtime ownership
- request handling and page context
- routing and API registration
- template rendering
- widgets and widget areas
- forms, validation, and mapping helpers
- metadata/head rendering
- realtime/event helpers
- plugin contribution contracts
- dashboard/layout models and helper methods
- client-side toolkit/runtime

That also means `CL.WebLogic` should avoid owning:

- MySQL-specific persistence policy
- app-specific dashboard save strategy
- app-specific auth/user storage assumptions
- app-specific CMS/content storage assumptions
- hardcoded user-bound dashboard behavior

## Persistence Rule

The toolkit may define contracts and helper methods for persistence-backed features, but the
actual persistence implementation should belong to the application or an optional provider layer.

Examples:

- dashboard layout sorting/order:
  keep the models and helper methods in the toolkit
  save/load the layout in the app
- widget settings:
  generic store contract is fine
  app decides file/json/database strategy
- auth identity store:
  toolkit exposes interfaces and auth flow helpers
  app provides the actual store implementation
- audit/request logging:
  toolkit supports the hook
  app decides whether to save anything and where

## Database Rule

Do not tightly couple `CL.WebLogic` core to MySQL or any other single database.

Use this principle:

- toolkit core = generic contracts + helpers
- sample/reference apps = concrete persistence choices
- optional provider layers are acceptable only when they stay clearly outside the core toolkit

`MiniBlog` is allowed to use `CL.MySQL2` heavily because it is a sample application.
`StarterWebsite` should stay lighter and demonstrate app-owned/local persistence where that makes sense.

In v2 the MySQL code lives in `samples/MiniBlog.Data` (`MiniBlog.Shared` is a VB keyword, so CA1716
flags it), and the admin area is `samples/Plugins/MiniBlog.Admin.Plugin`.

## Dashboard Rule

Dashboard support in the toolkit should be about tools, not policy.

Keep in the toolkit:

- dashboard layout models
- sorting/order helpers
- render helpers
- client-side dashboard helpers
- get/save helper methods that use an app-supplied store

Do not assume in the toolkit:

- every dashboard is per-user
- every dashboard is stored in MySQL
- every app needs a dashboard studio

If an app wants per-user dashboard persistence, it should implement that itself.
If an app wants local JSON persistence, that is also valid.

## Plugin Rule

Plugins should be able to contribute web features automatically through generic contracts.

Examples of valid plugin contribution areas:

- routes
- APIs
- widgets
- widget areas
- page scripts
- form option/search providers
- realtime/event contributors

The toolkit should support auto-registration of loaded web-capable plugins, but should not make
app-specific assumptions about what those plugins store or how they persist state.

## File Organization

Prefer small focused files organized by concern.

Toolkit folders should stay grouped by domain, for example:

- `src/Runtime`
- `src/Routing`
- `src/Theming`
- `src/Forms`
- `src/Security`
- `src/Realtime`
- `src/AspNetCore`

Sample apps should keep their own persistence code in app/shared layers, not in toolkit core.

## Decision Reminder

When adding new features, ask:

1. Is this a generic web/runtime tool?
   If yes, it likely belongs in `CL.WebLogic`.
2. Is this a persistence policy or app-specific behavior?
   If yes, it likely belongs in the sample app or app layer.
3. Is the toolkit starting to assume a specific database or product shape?
   If yes, stop and move that decision outward.

---

# CL.WebLogic2 Rules

Everything above still applies. The sections below are specific to the v2 rebuild
(package `CodeLogic.WebLogic`, CodeLogic 4.9 line). The approved roadmap is milestone
based (M0–M9): hosting, templates, security pipeline, auth flows, client, forms,
widgets/realtime, samples, hardening.

## Samples Rule

- Samples reference `src/CL.WebLogic` by default and the package with `-p:WebLogicFromPackage=true`
  (Docker and CI). Keep both working.
- The Docker demo must keep passing `samples/docker/smoke.py`. Add a check whenever a sample gains
  a flow.
- Samples run in Production in Docker. Every security store must be a real app store, or the
  development-store guard stops the container. State goes under `DEMO_DATA_DIR` (`/data`).
- CodeLogic plugins write config, logs and data next to their assembly, so the plugins folder
  must be writable. Hosts copy shipped plugins into the data folder (`PluginInstaller`).

## v2 Rules

- No static mutable state and no service locator. DI is the single source of app-supplied
  contracts (`builder.AddCodeLogicWebLogic(web => web.UseXxx<T>())`).
- Core services depend on `IWebLogicContext`, never on `LibraryContext` directly.
- Never capture config. Read `WebLogicConfigHolder.Current` (or the compiled runtime settings)
  per use; the holder is swapped by `IConfigReloadable` live config changes.
- Every async API takes a `CancellationToken` as its last parameter. No `async void`, no
  `.Result` / `.GetAwaiter().GetResult()`.
- Secure by default. Routes require authentication unless they opt out explicitly, and every
  opt-out is logged. Encode at output. The client never puts untrusted data into `innerHTML`.
- Dev-only defaults are in-memory (`InMemory*`) and refused outside Development unless the app
  opts in explicitly.

## Dependency Rule

- Runtime: CodeLogic and CodeLogic.* packages only, plus the ASP.NET Core shared framework.
  CodeLogic.* packages may bring their own third-party dependencies; WebLogic must not add more.
- No build-time third-party packages either: the template source generator was dropped for 4.9
  rather than take a `Microsoft.CodeAnalysis` dependency (the closure compiler is the fast path).
- Tests only: `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`.
- JavaScript: zero npm dependencies. The client is hand-written ES modules bundled by our own
  C# tool.
- Security code (hashing, tokens, CSRF, rate limiting, sanitizing, sessions, API keys) is
  written in-house on `System.Security.Cryptography` / DataProtection.
- `CodeLogic.MySQL2` is never referenced from the core (`src/CL.WebLogic`). The one exception is
  the optional provider layer `src/CL.WebLogic.MySql` (package `CodeLogic.WebLogic.MySql`), which
  the Database Rule allows because it stays outside the core. The release pre-flight fails if any
  other package depends on it, or if a package depends on anything that is not a `CodeLogic*` package.
- Add-on packages (`CL.WebLogic.MySql`, `CL.WebLogic.ExternalLogins`) follow the same rules as the
  core: in-house security code, `CodeLogic*` dependencies only, same code style. They implement core
  contracts; the core never references them.

## Code Style

Mirror CodeLogic (`CodeLogic/src`) and CodeLogic.Libs (`CL.TwoFactorAuth`, `CL.Mail`):

1. `src/CL.WebLogic/WebLogicLibrary.cs` at the project root; domain folders use `Models/`,
   `Services/`, `Events/` as needed. Folder path = namespace (file-scoped).
2. One public type per file. Aim for 40–250 lines; 400 is the hard ceiling.
3. `sealed` by default, records for data, `_camelCase` fields, `private readonly object _lock = new();`,
   `[]` collection expressions.
4. XML docs on every public member; `<inheritdoc/>` on implementations.
5. The library class uses `// ── Phase N: Name ───` banners (Configure, Initialize, Start,
   Stop, Config reload, Health check) and logs through `context.Logger` in each phase.
6. Config: `[ConfigSection]` classes deriving from `ConfigModelBase`, rich
   `[ConfigField(Label, Description, Group, Order, Min, Max, Collapsed, Secret, RequiresRestart)]`,
   a `Validate()` override, one section per file. Secrets are marked `Secret = true`.
7. Fallible operations return `Result` / `Result<T>` / `Error` (`CodeLogic.Core.Results`) with
   codes like `weblogic.auth.locked_out`. Exceptions are for programmer and startup errors.
8. Events are `record XxxEvent(...) : IEvent` in an `Events/` folder, published on the
   CodeLogic `IEventBus`.
9. Library code uses `ConfigureAwait(false)`. Regexes are `[GeneratedRegex]` or have a timeout.
   Type-keyed caches use `ConditionalWeakTable` so plugin assemblies can unload.

## Pipeline Rule

New request-security behaviour is an `IWebSecurityStage` with an explicit `Phase` and
`Order`, registered through DI, with an integration test. Plugins may not add PreRouting
stages.

## Template Rule

- Execution semantics (value resolution, filters, encoding, conditions, directives) live only in
  `Templates/Rendering/TemplateOps.cs`. Engines (closure compiler, interpreter, a future
  generator) only dispatch to it, so they cannot drift apart.
- Every new directive or value position declares its encoding context in the parser
  (`HtmlContextTracker` / `TemplateContextRules`); unsafe positions are parse errors, not
  render-time surprises.
- Output-changing work updates the golden snapshots deliberately
  (`WEBLOGIC_UPDATE_GOLDEN=1 dotnet test`) and both engines must agree.
- Template hooks into other domains (CSRF, widgets, scripts, authorization, localization) are
  DI contracts with safe no-op defaults; later milestones replace the defaults.

## Auth Rule

- Account persistence (users, API keys, recovery codes, notifications) is an app-supplied
  contract. Core defaults are **null stores** (nothing signs in, nothing is stored) so apps
  without accounts need nothing; `web.UseInMemoryAccounts()` is the explicit development choice
  and is refused outside Development by `DevelopmentStoreGuard`.
- Failures never reveal whether an account exists: one generic status, a dummy hash for unknown
  users, and e-mails sent from a background queue so timing stays the same.
- Anything that changes credentials changes `WebUser.SecurityStamp`; sessions and account
  tokens bound to the old stamp stop working.
- Links in e-mails come from `PublicBaseUrl`, never from the request's Host header.
- Second factors are single-use per time step (`IWebOneTimeUseStore`); account tokens are
  purpose-bound, time-limited and spent on use.
- Every flow that signs a user in on someone else's word (links, external providers) goes through
  the shared account rules (`Evaluate`) and the second factor. `SignInAsync` is only for users the
  app verified itself and is documented as skipping 2FA.
- Only real credential checks refresh `AuthenticatedAt`; re-signing the same user (refresh,
  password change) keeps it, so step-up cannot be satisfied by a refresh.

## Client Rule

- The client is hand-written ES2022 in `src/CL.WebLogic/Client/src`, vanilla, zero npm
  dependencies. Each module stays small and is either DOM-free (testable with
  `node --test "tests/client/*.test.mjs"`) or covered by the browser suite in `Client/src/tests`
  (served at `/_weblogic/tests` in Development, run headless by the integration tests).
- Only the module subset the bundler supports: named `import { … } from './x.js'`,
  `import * as ns`, `export function|class|const` and `export { … }`. No default exports,
  `export let`, re-exports or dynamic `import()` — the bundler fails the build on them.
- Untrusted data only ever becomes text (`el`, `textContent`). `parseHtml` / `swap` are for HTML
  this site's server rendered, and drop executable scripts. Commands are allowlisted; URLs that
  navigate must be local.
- `tools/CL.WebLogic.ClientBundler` is our own build tool; it is never packed or referenced at
  runtime. Built bundles live under `obj/` and are embedded — never commit build output.
- Keep `Client/weblogic.d.ts` in step with the public API.

## Forms Rule

- Validation rules come from DataAnnotations; WebLogic attributes only describe rendering and
  structure. Anything the server does not define is never bound.
- The server is authoritative: client validation, show-when and repeaters are conveniences.
  Hidden fields are neither bound nor validated on either side.
- Uploads are judged by their bytes, never by the claimed content type or file name; stored
  files get generated names. Persistence of uploads goes through `IWebUploadSink`.
- Lookups that leave the process (`WebFormHttpLookupProvider`) use URLs fixed in code and an
  allowlist of hosts — never anything derived from the request.

## Realtime Rule

- Every published message has an audience; pick the narrowest. Never publish client IPs, query
  strings, tokens or config values. Bridged events map their data explicitly.
- Replay and fan-out must use the same audience check (`WebRealtimeConnection.Accepts`).
- Connections are long-lived but not trusted forever: they close on sign-out, password change,
  shutdown and after `Realtime.MaxConnectionMinutes`.

## Caching Rule

- Pages are cached only when nothing personal can be in them: anonymous, no session cookie sent,
  no cookies set, no CSRF token issued, no session loaded or written. Keep `WebLogicOutputCachePolicy` in front of every policy and the
  output cache after endpoint security.
- Fragment cache keys are scoped to the tenant by `request.Cache`; anything user-specific must put
  the user in the key or not be cached.

## Testing Rule

- Every bug fix comes with a regression test.
- Every pipeline stage has an integration test (real Kestrel via `CodeLogic.WebLogic.Testing`).
- Test folders mirror the source folders.

## Cluster Rule

- Any new store or cache that keeps state in the process implements `IWebNodeLocalStore` and is
  added to `ClusterReadinessCheck` (shared = error, advised = warning), so cluster mode can refuse it.
- State that must survive a node switch (tokens, wizard state, link tokens) goes through
  DataProtection, never a per-node dictionary.

## Docs Rule

- `docs/` is the user documentation (DocFX). A change to public behaviour, config or a contract
  updates the matching guide (`docs/guides`) and, where relevant, the scenario pages.
- Code in the docs must match the real API: check names and signatures against `src/` when writing
  them. Documented trade-offs live in `docs/guides/security-model.md` ("Known limits").

## Release Rule

- `main` never publishes. Nothing may merge or push into `release` or `prerelease` automatically.
- A release is a deliberate `git push origin main:prerelease` (preview) or
  `git push origin main:release` (stable); `release.yml` runs only on those branches.
