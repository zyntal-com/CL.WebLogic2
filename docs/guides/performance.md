# Performance

## Measured

The StarterWebsite in Production mode, Release build, Kestrel on loopback, 32 concurrent connections,
measured with `tools/CL.WebLogic.LoadTest` on a developer workstation (numbers vary a lot with the
machine; compare only within one run):

| Request | Requests/s | p50 | What it exercises |
|---|---|---|---|
| `/_weblogic/health/live` | ~130,000 | 0.13 ms | Full pipeline, trivial endpoint |
| `/assets/site.css` | ~190,000 | 0.11 ms | Asset resolution and in-memory small files |
| `/nope` (themed 404) | ~150,000 | 0.13 ms | Fallback, themed error page |
| `/cache` (output-cached) | ~220,000 | — | Output cache hit, security stages still run |
| `/account/login` (with cookies) | ~50,000 | 0.19 ms | Template render, form render, CSRF token |
| `/forms` (with cookies) | ~18,000 | 0.36 ms | A large form: 15 field types, options, repeater, schema JSON |

First visits without cookies are slower (a pre-session CSRF cookie is created and DataProtection-protected
per request); with the global rate limit on, the load tool's single client is limited long before these
numbers, which is the point of the limit.

## What is cached

- Compiled templates (re-checked at most every `theme.templateChangeCheckSeconds`), including whether
  optional templates (field overrides, error pages) exist.
- Asset resolution (2 s) and file content for assets up to 256 KB.
- Form definitions per model type, the client bundle (precompressed), JSON serializer options.
- Security settings compiled once per configuration (CIDR sets, CSP template, policies).
- Permissions are resolved only when a rule needs them; sessions are only loaded when used.

## Tuning

- `theme.templateChangeCheckSeconds: 0` for immutable themes.
- Use the output cache for anonymous pages and the fragment cache for expensive parts of personalized
  pages.
- Keep `security.audit.recordAllRequests` off unless you need it; the sink runs in the background but
  every request still allocates an entry.
- Turn `security.reputation` to background mode (default) so DNS lookups never block requests.

## Load tool

```bash
dotnet build tools/CL.WebLogic.LoadTest -c Release
dotnet tools/CL.WebLogic.LoadTest/bin/Release/net10.0/CL.WebLogic.LoadTest.dll \
    --duration 10 --concurrency 32 --header "Cookie: wl_pre=..." http://127.0.0.1:5000/ http://127.0.0.1:5000/forms
```

It reports requests per second, p50/p95/p99 latency and the status codes per URL. Disable
`security.rateLimits.globalEnabled` on the instance under test, or the global limit will answer `429`.
