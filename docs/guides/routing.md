# Routing and results

Every WebLogic route is a real ASP.NET Core endpoint, so route templates, constraints, `405` with an
`Allow` header, HEAD, CORS policies and the ASP.NET output cache all work, and your own minimal APIs
or controllers can live next to WebLogic routes.

## Contributors

Routes (and widgets, forms, templates) come from `IWebContributor` implementations. A contributor's
registrations are staged and committed together: if `ContributeAsync` throws, nothing it staged is
registered. A contributor added by a plugin is removed again when the plugin unloads.

```csharp
public sealed class ShopPages(ProductCatalog catalog) : IWebContributor
{
    public Task ContributeAsync(WebContributionBuilder b, CancellationToken ct)
    {
        b.MapPage("/products/{slug}", async (r, ct) =>
        {
            var product = await catalog.FindAsync(r.GetRouteValue("slug")!, ct);
            return product is null
                ? WebResult.Status(404)
                : WebResult.Page("templates/product", product, new WebPageMeta { Title = product.Name });
        }).AllowAnonymous();

        var admin = b.MapGroup("/admin").RequireGroups("admin");
        admin.MapPage("/orders", OrdersAsync);
        admin.MapApi("/orders/{id:int}", OrderJsonAsync);
        return Task.CompletedTask;
    }
}

// Program.cs
web.AddContributor<ShopPages>(new WebContributorDescriptor { Id = "shop", Name = "Shop" });
```

| Method | Kind | Notes |
|---|---|---|
| `MapPage(pattern, handler, methods...)` | HTML page | Unauthenticated → redirect to `Security.LoginPath` with a safe `returnUrl` |
| `MapApi(...)` | JSON API | Unauthenticated → `401` ProblemDetails with `WWW-Authenticate` |
| `MapFragment(...)` | Partial HTML | For `data-wl-shell` swaps and widget refreshes |
| `MapSse(pattern, handler)` | Server-sent events | GET only |
| `MapFallback(handler)` | `{**path}` | Lowest order; still behind the full pipeline (CSRF included) |
| `MapGroup(prefix)` | Group | Settings set on a group apply to every route in it |

A handler is `(WebRequestContext request, CancellationToken ct) => ValueTask<WebResult>`.

## Route settings

On a route or a group:

| Setting | Effect |
|---|---|
| `AllowAnonymous()` | Opt out of the default "signed-in users only" (logged and shown in the explorer) |
| `RequireAuthenticated()`, `RequireGroups(...)`, `RequirePermissions(...)` | Built-in access rules (permissions support wildcards: `posts.*`) |
| `RequirePolicy(name)` | A named policy (`web.AddAuthorizationPolicy`) |
| `RequireRecentSignIn(maxAge, methods...)` | Step-up: credentials proven recently (pages redirect with `reauth=1`, APIs 401) |
| `WithLoginPath(path)`, `WithAccessDeniedPath(path)` | Per-area sign-in and access-denied pages (pages only) |
| `WithCsp(c => ...)` | Extra Content-Security-Policy sources for these routes |
| `WithRateLimit(policy)` | A named rate-limit policy from `security.rateLimits.policies` |
| `WithBodyLimit(bytes)` | Request body limit for this route |
| `WithOutputCache(policy)` | Output cache policy (anonymous GET only; see [Caching](caching.md)) |
| `WithCors(policy)` | An ASP.NET CORS policy |
| `AllowCrossSitePost()` | Webhooks: skip CSRF origin checks (the route should verify a signature) |
| `WithMiddleware(middleware)` | Per-route `IWebMiddleware` |
| `WithName`, `WithDescription`, `WithTags` | Shown in the explorer |

Unknown policy names fail closed (the request is refused), not open.

## The request

`WebRequestContext` gives the handler:

- `Identity` (`WebIdentity`: user, scheme, groups, permissions, scopes, tenant) and `Security`.
- `GetRouteValue`, `GetQuery`, `GetQueryValues`, `GetHeader`, `GetCookie`, `ReadJsonAsync<T>`, `ReadBodyAsStringAsync`.
- `Session`: a lazy per-request session; nothing is stored until you write.
- `Auth`: sign-in helpers ([Authentication](authentication.md)).
- `Forms`: bind, prefill and render forms ([Forms](forms.md)).
- `Cache`: the fragment cache ([Caching](caching.md)).
- `GetRequiredService<T>()` for app services.

## Results

| Result | Use |
|---|---|
| `WebResult.Page(template, model, meta, status)` | Render a theme template with its layout |
| `WebResult.Html`, `Fragment`, `Text`, `Json`, `Bytes`, `File` (ranges, ETag) | Raw responses |
| `WebResult.Redirect(url)` | Local URLs only; `ExternalRedirect` for an explicit absolute URL |
| `WebResult.Status`, `NoContent`, `Problem`, `ValidationProblem` | Status codes and RFC 7807 ProblemDetails |
| `WebResult.Sse(IAsyncEnumerable<...>)` | A server-sent event stream |
| `WebResult.Commands(...)` | Client commands (toast, replace, redirect, ...) |
| `FormResult.Success(message, redirect, data, commands)` | Form posts: JSON for the client, `303` for plain posts |
| `WebResult.FromAspNet(IResult)` | Return any ASP.NET Core `IResult` |

`WebPageMeta` sets the title, description, canonical URL, robots and Open Graph tags that
`{renderhead}` writes.

## The explorer

`/_weblogic/explorer` (and `/_weblogic/explorer.json`) lists every route with its kind, methods,
access rule, rate limit and cache policy, the anonymous routes, the security stages in order,
widgets, forms and loaded plugins. It is on in Development; in production it needs
`explorer.inProduction` and the `explorer.permission` permission.

## Minimal APIs alongside

Endpoints you map yourself still pass the request-level stages (forwarded headers, hosts, HTTPS, IP
rules, global rate limit, authentication; `HttpContext.User` is set) and get hardened responses. The
route-level stages (authorization, CSRF, body limits) only know WebLogic routes, so protect your own
endpoints with ASP.NET's `[Authorize]`/`RequireAuthorization()` (the `WebLogic` authentication scheme
replays the WebLogic identity) or, simpler, map them through a contributor.
