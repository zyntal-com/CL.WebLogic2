# Scenarios

WebLogic does not assume what you are building. These pages show how the same toolkit is put together
for different kinds of sites: which pieces to use, which contracts to implement, which settings matter.

| Scenario | Key pieces |
|---|---|
| [Marketing site](marketing-site.md) | Anonymous pages, output cache, forms with anti-bot, strict CSP |
| [CMS / blog](cms.md) | Markdown, sanitizer, tag eviction, RSS/sitemap, editors |
| [Admin back office](admin.md) | Groups and permissions, forms, dashboards, audit |
| [SaaS, multi-tenant](saas-multi-tenant.md) | Tenant resolver, per-tenant data, realtime, rate limits |
| [API for SPA and mobile](spa-mobile-api.md) | API keys, bearer tokens, CORS, ProblemDetails, CSRF endpoint |
| [Intranet behind a proxy](intranet.md) | Forwarded headers, trusted-header or Windows sign-in, IP rules |
| [Gaming community (Steam)](steam-community.md) | External sign-in, live wall, widgets |
| [Plugin-heavy platform](plugin-platform.md) | Plugins contributing routes, forms, widgets, templates |
| [Cluster](cluster.md) | Shared stores, key ring, backplane, startup validation |

The two samples in the repository cover several of these end to end: `StarterWebsite` (accounts, forms,
dashboards, realtime, caching, API keys, Steam) and `MiniBlog` (CMS on MySQL with an admin plugin).
