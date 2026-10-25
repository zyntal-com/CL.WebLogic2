# Marketing site

Mostly anonymous pages, a contact or newsletter form, maybe a pricing page. The priorities are speed,
search engines and not getting spammed.

## Pieces

- **Routes:** `MapPage(...).AllowAnonymous()` for every public page; put them in one group:
  `var site = b.MapGroup("").AllowAnonymous();`.
- **Output cache:** `web.AddOutputCachePolicy("site", p => p.Tag("site").Expire(TimeSpan.FromHours(1)))`
  and `.WithOutputCache("site")`. A page that renders `{form:...}` contains a CSRF token and is never
  output-cached, so keep the contact form on its own page (the rest of the site stays cached), or post
  from a cached page through the client (`WebLogic.http.post`), which fetches a token when it needs one.
- **SEO:** `WebPageMeta` (title, description, canonical URL, Open Graph, JSON-LD) rendered by
  `{renderhead}`; `security.hosts.redirectToCanonical` to collapse `www`/apex.
- **Forms:** anti-bot is on by default (honeypot plus minimum submit time). Add a rate limit:
  `.WithRateLimit("contact")` with a policy such as 5 per 10 minutes per IP.
- **Assets:** `{asset:...}` fingerprinted URLs are cached as immutable; keep images in the theme's
  `assets` folder or a CDN listed in `security.headers.imageSources`.
- **No accounts?** Register no user store: the default "null" stores mean nobody can sign in. The
  session store defaults to an in-memory one, which the production guard refuses; register a small
  `IWebSessionStore` (a file or your cache server) or, if you are sure the site never stores session
  values, set `security.allowDevelopmentStores` and accept the startup warning.

## Settings

```json
{
  "publicBaseUrl": "https://www.example.com",
  "theme": { "templateChangeCheckSeconds": 0 },
  "security": {
    "hosts": { "allowedHosts": ["www.example.com", "example.com"], "redirectToCanonical": true },
    "https": { "enforce": true, "hstsPreload": true },
    "rateLimits": { "policies": [{ "name": "contact", "permits": 5, "windowSeconds": 600, "partition": "ip" }] },
    "headers": { "imageSources": ["https://cdn.example.com"] }
  },
  "client": { "navigation": true }
}
```

## Content updates

Static pages change on deploy, so a long expiry is fine. When content comes from somewhere else
(a headless CMS, a JSON file), call `IWebCacheInvalidator.EvictTagsAsync(["site"])` from the webhook
that announces the change (`AllowCrossSitePost()` + a signature check on that route).
