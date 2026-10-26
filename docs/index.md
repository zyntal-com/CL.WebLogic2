# CodeLogic.WebLogic

WebLogic is the web toolkit for [CodeLogic](https://github.com/zyntal-com/CodeLogic) 4.9. It gives a
CodeLogic application the runtime parts of a web stack and leaves the decisions to the application.

| You get | You decide |
|---|---|
| Routes on real ASP.NET Core endpoints, route groups, results | What your pages and APIs do |
| An ordered security pipeline: hosts, HTTPS, IP rules, rate limits, authentication, authorization, CSRF, hardened responses, audit | Which policies apply where |
| Sign-in flows: passwords, passkeys, TOTP two-factor with recovery codes, magic links, API keys, bearer tokens, external providers | Where users, sessions and keys are stored |
| Templates with context-aware encoding, Markdown, an HTML sanitizer | Your theme |
| Model-driven forms: rendering, client and server validation, uploads, wizards | Your form models |
| Widgets, widget areas, dashboards | Your widgets and dashboard storage |
| Realtime over SSE and WebSocket, output and fragment caching, an API explorer | What to publish and cache |
| A vanilla JavaScript client (no dependencies) | Your page scripts |

Everything with state is a **contract**: `IWebUserStore`, `IWebSessionStore`, `IWebDashboardLayoutStore`
and the rest. In-memory implementations exist for development; in production WebLogic refuses to
start until the security-relevant ones are replaced by your own.

## Where to start

- [Getting started](getting-started.md): a running site in a few minutes.
- [Guides](guides/index.md): one page per area (security, auth, forms, templates, ...).
- [Scenarios](scenarios/index.md): how to put the pieces together for a marketing site, a CMS, a
  SaaS app, an API for a mobile client, an intranet, a cluster and more.
- The samples in the repository: `samples/StarterWebsite` (JSON-file persistence) and
  `samples/MiniBlog` (MySQL, admin plugin), runnable as a Docker demo (`samples/docker`).

## Principles

- **A toolkit, not a product.** No database, CMS shape or user model is assumed.
- **Secure by default.** Routes require sign-in unless they opt out, every unsafe request needs a
  CSRF token, and opting out of a protection is explicit and logged.
- **No third-party packages** beyond CodeLogic.\* and the .NET shared framework. The security code
  is written on the .NET cryptography primitives; the client has no npm dependencies.
