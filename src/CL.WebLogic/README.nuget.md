# CodeLogic.WebLogic

Web application toolkit for [CodeLogic](https://github.com/zyntal-com/CodeLogic) 4.9.

- Real ASP.NET Core endpoints for pages, APIs, fragments and SSE — mixes freely with minimal APIs
- An ordered, extensible security pipeline (proxy trust, IP rules, reputation, rate limits,
  authentication handlers, policy authorization, CSRF, response hardening)
- Auth flow helpers: sign-in, 2FA with recovery codes, API keys, reset / verify tokens
- Template engine with context-aware encoding, CSP nonces and compiled templates
- Forms with validation, conditional fields, repeaters, wizards and uploads
- Widgets, dashboards and realtime (SSE / WebSocket)
- A vanilla JS client runtime (no jQuery)

WebLogic is a toolkit: it ships contracts and helpers, and your application owns persistence.
No third-party dependencies beyond CodeLogic.*.
