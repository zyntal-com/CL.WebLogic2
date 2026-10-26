# CodeLogic.WebLogic

Web application toolkit for [CodeLogic](https://github.com/zyntal-com/CodeLogic) 4.9.

- Real ASP.NET Core endpoints for pages, APIs, fragments and SSE — mixes freely with minimal APIs
- An ordered, extensible security pipeline (proxy trust, hosts, HTTPS, IP rules, reputation, rate
  limits, authentication, policy authorization, CSRF, response hardening, audit)
- Auth flows: password sign-in with lockout, passkeys (WebAuthn, verified in-house), TOTP 2FA with
  recovery codes, magic links, API keys,
  bearer tokens, external providers, tenants — on stores your app supplies
- Template engine with context-aware encoding, CSP nonces, Markdown and an HTML sanitizer
- Model-driven forms: validation, conditional fields, repeaters, wizards, uploads, anti-bot
- Widgets, dashboards, realtime (SSE / WebSocket), output and fragment caching, API explorer
- Plugins contribute routes, forms, widgets and templates; cluster-mode startup checks
- A vanilla JS client runtime with no dependencies

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddCodeLogicWebLogic(web =>
{
    web.UseUserStore<MyUserStore>().UseSessionStore<MySessionStore>();
    web.AddForm<ContactForm>();
    web.AddContributor<SitePages>();
});

var app = builder.Build();
app.UseCodeLogicWebLogic();
app.MapCodeLogicWebLogic();
await app.RunAsync();
```

WebLogic is a toolkit: it ships contracts and helpers, and your application owns persistence.
Routes require sign-in unless they opt out. No third-party dependencies beyond CodeLogic.*.

- Docs: https://zyntal-com.github.io/CL.WebLogic2/
- Source and samples: https://github.com/zyntal-com/CL.WebLogic2
- Tests: [CodeLogic.WebLogic.Testing](https://www.nuget.org/packages/CodeLogic.WebLogic.Testing)
