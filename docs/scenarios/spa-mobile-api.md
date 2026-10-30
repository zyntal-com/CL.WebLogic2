# API for SPA and mobile

A JSON API consumed by a single-page app, a mobile app or partners. The choices: which credential each
client uses, and whether CSRF applies.

## Pick the credential per client

| Client | Credential | CSRF |
|---|---|---|
| SPA on the **same site** | The session cookie (sign in through your API or a page) | Yes: send the token |
| SPA on **another origin** | Bearer token you issue, or the session cookie with CORS and credentials | Bearer: no; cookie: yes |
| Mobile app | Bearer token (`web.AddBearerTokens<T>()`) | No |
| Scripts, CI, partners | API key (`wl_...`) with scopes | No |

CSRF is skipped only for non-ambient credentials (API key, bearer) **and** when no session cookie was
sent, so a browser that happens to hold a session cookie cannot be tricked into a cross-site write.

## Endpoints

```csharp
var api = b.MapGroup("/api/v1").WithRateLimit("api").WithTags("v1");

api.MapApi("/orders", ListOrdersAsync).RequirePolicy("orders-read");
api.MapApi("/orders", CreateOrderAsync, "POST").RequirePolicy("orders-write");

static async ValueTask<WebResult> CreateOrderAsync(WebRequestContext r, CancellationToken ct)
{
    // 415 / 413 / 400 problems for wrong content type, oversized, malformed or invalid bodies;
    // CreateOrder's DataAnnotations (e.g. [Range(1, 100)] Quantity) are the rules.
    var input = await r.BindJsonAsync<CreateOrder>(ct);
    if (!input.IsValid)
        return input.ToProblem();
    // ... input.Model
    return WebResult.Json(order, 201);
}
```

Unauthenticated API calls get `401` ProblemDetails with `WWW-Authenticate`, never a redirect.
Errors are RFC 7807 everywhere (including validation and rate limits, with `Retry-After`).

```csharp
web.AddAuthorizationPolicy("orders-read", p => p.RequireScopes("orders:read"));
web.AddAuthorizationPolicy("orders-write", p => p.RequireScopes("orders:write"));
```

Scopes come from API keys and from the identity your bearer validator returns. Session users have no
scopes; if the same endpoint serves both, use `RequireAssertion` to accept either a scope or a group.

## Same-site SPA

```js
const wl = window.WebLogic;
const { data } = await wl.http.post('/api/v1/orders', { productId: 7, quantity: 1 });
```

`WebLogic.http` adds the CSRF header automatically (fetching `/_weblogic/csrf` when needed). Without the
WebLogic client, `GET /_weblogic/csrf` returns a token to send in `X-CSRF-Token`.

## Cross-origin

```csharp
builder.Services.AddCors(o => o.AddPolicy("app", p => p
    .WithOrigins("https://app.example.com").AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

api.WithCors("app");
```

```json
"security": { "csrf": { "trustedOrigins": ["https://app.example.com"] } }
```

Realtime from another origin: get a ticket from `/_weblogic/realtime/ticket`, then connect with it;
add the origin to `realtime.allowedOrigins`.

## Bearer tokens

```csharp
web.AddBearerTokens<MyTokenValidator>();

public sealed class MyTokenValidator(TokenService tokens) : IWebBearerTokenValidator
{
    public async ValueTask<WebIdentity?> ValidateAsync(string token, WebSecurityContext context, CancellationToken ct) =>
        await tokens.ValidateAsync(token, ct) is { } t
            ? new WebIdentity { UserId = t.UserId, UserName = t.UserName, Scheme = WebAuthenticationSchemes.Bearer, Scopes = t.Scopes }
            : null;
}
```

How you issue tokens (your own signed tokens, an identity provider's JWTs validated by an ASP.NET
handler via `AddAspNetAuthenticationScheme`) is up to you; tokens beginning with `wl_` are treated as
API keys.

## Discovering the API

`/_weblogic/explorer.json` lists every route with methods, policy, rate limit and tags; use it to
generate client docs or check coverage in tests.
