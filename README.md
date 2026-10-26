# CL.WebLogic2

`CodeLogic.WebLogic` — the web application toolkit for [CodeLogic](https://github.com/zyntal-com/CodeLogic) 4.9,
rebuilt from the ground up.

WebLogic gives a CodeLogic application the runtime pieces of a web stack — routing on real
ASP.NET Core endpoints, an ordered security pipeline, auth flow helpers, templates, forms,
widgets and realtime — while the application keeps ownership of persistence and policy.

> **Status:** preview (4.9.x). The rebuild is milestone based; see `CHANGELOG.md` for what has
> landed. The previous `CL.WebLogic` 0.x line is not compatible.

## Principles

- **A toolkit, not a product.** Contracts and helpers live here; your app (or a sample) decides
  how users, sessions, dashboards and content are stored.
- **Secure by default.** Routes require authentication unless they opt out, CSRF is enforced on
  every unsafe request, responses are hardened, and dev-only in-memory stores refuse to run in
  production.
- **No third-party dependencies beyond CodeLogic.\*.** Security code is written in-house on the
  .NET cryptography primitives; the JS client has zero npm dependencies.
- **CodeLogic style.** Small single-purpose files, folder-per-namespace, lifecycle-managed
  library with live config reload (`IConfigReloadable`).

## Documentation

The `docs/` folder is a DocFX site (published to GitHub Pages by `docs.yml`):

- [Getting started](docs/getting-started.md)
- [Guides](docs/guides/index.md): configuration, routing, the security pipeline, authentication,
  forms, templates, the JS client, widgets and dashboards, realtime, caching, plugins, testing,
  deployment, the security model and performance.
- [Scenarios](docs/scenarios/index.md): marketing site, CMS, admin back office, multi-tenant SaaS,
  SPA/mobile API, intranet behind a proxy, gaming community, plugin platform, cluster.

Build it locally with `dotnet tool install -g docfx` and `docfx docs/docfx.json --serve`.

## Repository layout

| Path | Contents |
| --- | --- |
| `src/CL.WebLogic` | The toolkit (`CodeLogic.WebLogic`) |
| `src/CL.WebLogic.Testing` | Test host and fakes (`CodeLogic.WebLogic.Testing`) |
| `src/CL.WebLogic.MySql` | Optional MySQL stores and key ring on CL.MySQL2 (`CodeLogic.WebLogic.MySql`) |
| `src/CL.WebLogic.ExternalLogins` | Optional Steam and OAuth 2.0 / OpenID Connect sign-in (`CodeLogic.WebLogic.ExternalLogins`) |
| `tests/` | Unit and integration tests |
| `samples/` | StarterWebsite (JSON-file persistence) and MiniBlog (MySQL, admin plugin) |
| `samples/docker` | The deployable demo: `docker compose up -d --build` (see its README) |
| `tools/` | Client bundler (build-time) and `CL.WebLogic.LoadTest` |
| `docs/` | DocFX documentation site |

## Building

```bash
dotnet build CL.WebLogic2.slnx -c Release
dotnet test CL.WebLogic2.slnx -c Release
```

Requires the .NET 10 SDK. All dependencies restore from nuget.org — no sibling repositories
are needed.

## Versioning

`major.minor` comes from `version.txt` and follows the CodeLogic line (`4.9`); the patch is the
CI run number. `release` publishes `4.9.<run>`, `prerelease` publishes `4.9.<run>-preview`.

Pushing to `main` never publishes. Releasing is always a deliberate push of `main` to a release branch:

```bash
git push origin main:prerelease   # 4.9.<run>-preview
git push origin main:release      # 4.9.<run> (stable)
```

## License

MIT © Zyntal.com
