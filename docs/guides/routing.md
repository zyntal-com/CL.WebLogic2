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
| `WithLayout(path)` | Default layout for page templates that declare none |
| `WithRateLimit(policy)` | A named rate-limit policy from `security.rateLimits.policies` |
| `WithBodyLimit(bytes)` | Request body limit for this route |
| `WithOutputCache(policy)` | Output cache policy (anonymous GET only; see [Caching](caching.md)) |
| `WithETag()` / `WithoutETag()` | Computed ETag and 304 for non-personal GET responses; overrides `cache.eTags` ([Caching](caching.md)) |
| `WithoutCompression()` | Never compress this route's responses |
| `AllowPersonalCompression()` | Compress even personal responses when `compression.enabled` is on (BREACH: only for content with no reflected input next to a secret) |
| `WithCors(policy)` | An ASP.NET CORS policy |
| `AllowCrossSitePost()` | Webhooks: skip CSRF origin checks (the route should verify a signature) |
| `WithMiddleware(middleware)` | Per-route `IWebMiddleware` |
| `WithName`, `WithDescription`, `WithTags` | Shown in the explorer |

Unknown policy names fail closed (the request is refused), not open.

## The request

`WebRequestContext` gives the handler:

- `Identity` (`WebIdentity`: user, scheme, groups, permissions, scopes, tenant) and `Security`.
- `GetRouteValue`, `GetQuery`, `GetQueryValues`, `GetHeader`, `GetCookie`, `ReadJsonAsync<T>`, `BindJsonAsync<T>`, `ReadBodyAsStringAsync`.
- `Session`: a lazy per-request session; nothing is stored until you write.
- `Auth`: sign-in helpers ([Authentication](authentication.md)).
- `Forms`: bind, prefill and render forms ([Forms](forms.md)).
- `Cache`: the fragment cache ([Caching](caching.md)).
- `GetRequiredService<T>()` for app services.

## Validated JSON bodies

`ReadJsonAsync<T>` only deserializes. `BindJsonAsync<T>` also checks the request and validates the
model with its DataAnnotations, so a JSON API needs no hand-written checks:

```csharp
public sealed class CreateOrder
{
    [Required, StringLength(100)]
    public string? Name { get; set; }

    public List<OrderLine> Items { get; set; } = [];
}

public sealed class OrderLine
{
    [Required]
    public string? Name { get; set; }

    [Range(1, 1000)]
    public int Quantity { get; set; }
}

b.MapApi("/api/orders", async (request, ct) =>
{
    var binding = await request.BindJsonAsync<CreateOrder>(ct);
    if (!binding.IsValid)
        return binding.ToProblem();

    var order = await orders.CreateAsync(binding.Model!, ct);
    return WebResult.Json(order, 201);
}, "POST");
```

| Request | `WebJsonBinding<T>` | `ToProblem()` |
|---|---|---|
| Content-Type is not JSON (`application/json` or `*+json`) | `StatusCode` 415, `weblogic.json.unsupported_media_type` | `415` problem |
| Body over the route's limit (`WithBodyLimit`, default 1 MiB) | 413, `weblogic.json.too_large` | `413` problem |
| Empty body, `null`, or not valid JSON | 400, `weblogic.json.empty` / `weblogic.json.malformed` | `400` problem (no parser details) |
| A value of the wrong type (`"quantity": "many"`) | 400, `Errors["items[0].quantity"]` | `400` validation problem |
| DataAnnotations or `IValidatableObject` fail | 400, `weblogic.json.invalid`, `Errors`, `Model` set | `400` validation problem |
| Valid | `IsValid`, `Model` | — |

- **Error keys are JSON paths** in the route's JSON options (camelCase by default, `[JsonPropertyName]`
  respected): `name`, `address.city`, `items[1].name`, `lines[key].name`; `""` holds model-level
  results of `IValidatableObject`. The validation problem has the `errors` shape the JS client already
  reads (`HttpError.problem.errors`).
- **Nested objects, collections and dictionaries** are validated recursively (up to 32 levels; a cycle
  is walked once). Like DataAnnotations, an object's `IValidatableObject.Validate` and class-level
  attributes run only once its members are valid.
- **Unknown JSON properties are ignored**, and members the JSON contract skips (`[JsonIgnore]`) are
  neither bound nor validated.
- `[Required]` without its own message uses the localizable `forms.required` text, like form fields;
  other attributes use their DataAnnotations message, and `[Display(Name = ...)]` sets the label.
- Put attributes on properties. On a positional record write `[property: Required]`; prefer
  `[Required]` over the C# `required` keyword, whose failure is a plain `400` without field errors.

`WebJsonValidator` (a DI singleton) runs the same validation on any object:
`validator.Validate(model, jsonOptions)` returns the errors by JSON path.

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
