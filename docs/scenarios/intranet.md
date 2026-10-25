# Intranet behind a proxy

An internal site behind a reverse proxy or gateway, where sign-in is handled by the company's
identity system: an authenticating proxy (oauth2-proxy, a cloud IAP), Windows authentication, or an
OIDC provider.

## Proxy first

```json
{
  "publicBaseUrl": "https://intranet.corp.example",
  "security": {
    "forwardedHeaders": { "enabled": true, "trustedNetworks": ["10.20.0.0/24"], "forwardLimit": 1 },
    "hosts": { "allowedHosts": ["intranet.corp.example"] },
    "ipRules": { "allow": ["10.0.0.0/8"] }
  }
}
```

- Only the proxy subnet may set `X-Forwarded-*`; the real client IP feeds IP rules, rate limits and
  audit.
- `ipRules.allow` turns the whole site into "corporate network only", as defence in depth.
- Served under a path (`https://corp.example/intranet/`)? Enable `forwardedHeaders.honorPrefix` and let
  the proxy send `X-Forwarded-Prefix`.

## Option A: an authenticating proxy

The proxy signs users in and forwards who they are:

```json
"auth": {
  "trustedHeader": {
    "enabled": true,
    "userHeader": "X-Forwarded-User",
    "emailHeader": "X-Forwarded-Email",
    "groupsHeader": "X-Forwarded-Groups",
    "secretHeader": "X-Proxy-Secret",
    "secret": "<long random value the proxy also sends>"
  }
}
```

The headers are only believed when the request comes from a trusted proxy **and** carries the shared
secret, so someone reaching the app directly cannot impersonate a user. Groups from the header become
`Identity.Groups` for `RequireGroups`.

## Option B: Windows authentication

```csharp
builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme).AddNegotiate();
builder.AddCodeLogicWebLogic(web => web.AddAspNetAuthenticationScheme(NegotiateDefaults.AuthenticationScheme, ambient: true));
```

`ambient: true` because the browser sends the credential automatically, so CSRF protection stays on.
Map AD groups to WebLogic groups or permissions with an `IWebPermissionResolver`.

## Option C: OIDC sign-in

Implement `IWebExternalLoginProvider` for the company's provider (authorization code flow: build the
challenge URL, exchange the code in `HandleCallbackAsync`), or put an OIDC-capable proxy in front and use
option A. WebLogic's external login flow handles state, nonce and the return URL; your user store links
the external id to a local user.

## No local passwords

With A or B there is no password sign-in: register a user store only if you keep local profiles,
and leave `Security.LoginPath` pointing at a page that explains how to get access. Sessions still need a
real `IWebSessionStore` in production.

## Typical intranet extras

- Dashboards per user or per department (`IWebDashboardOwnerResolver` returning `"dept:" + department`).
- Realtime announcements to `Group("all-staff")`.
- Forms with lookups against internal systems (`WebFormHttpLookupProvider` with
  `forms.allowedLookupHosts`).
