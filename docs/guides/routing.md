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
| `WithCors(policy)` | An ASP.NET CORS policy |
| `AllowCrossSitePost()` | Webhooks: skip CSRF origin checks (verify the sender with [`WebWebhookVerifier`](security-pipeline.md#webhooks)) |
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

## Pagination

`WebPagination` does the page arithmetic and clamps what came from the query string; `WebPageLinks`
builds the links.

```csharp
b.MapPage("/posts", async (request, ct) =>
{
    var total = await posts.CountAsync(ct);
    var pagination = WebPagination.Create(WebPagination.ParsePage(request), pageSize: 20, total); // max page size 100
    var items = await posts.ListAsync(pagination.Skip, pagination.PageSize, ct);
    return WebResult.Page("templates/posts", new { items, pager = WebPageLinks.Build(request, pagination) });
}).AllowAnonymous();
```

- `Create(page, pageSize, totalItems, maxPageSize = 100)` clamps the page size to 1..max and the page
  to 1..`TotalPages` (an empty list is page 1 of 1). It exposes `HasPrevious`, `HasNext`, `Skip`,
  `FirstItem`, `LastItem`. `ParsePage` accepts digits only; anything else is page 1.
- `pagination.Apply(list)` pages an in-memory sequence into a `WebPagedResult<T>` (`Items`, `Pagination`).
- `WebPageLinks.Build(request, pagination, window = 2, parameter = "page")` returns `WebPagerLinks`:
  `PreviousUrl`, `NextUrl`, `FirstUrl`, `LastUrl` (null when there is no such page) and `Pages`
  (`Number`, `Url`, `IsCurrent`) around the current page. Every URL is the request's own local path
  (with path base) and keeps the other query values (filters, search, sort); keys and values are
  encoded and page 1 drops the parameter. `WebPageLinks.PageUrl(...)` builds a single link; the
  overloads that take a path refuse anything but a local path.

There is no pager directive; render it with a partial, for example `partials/pager.html`:

```html
<nav class="pager" aria-label="Pages">
  {if:model:pager.previousUrl}<a href="{model:pager.previousUrl}" rel="prev">Previous</a>{/if}
  {foreach:model:pager.pages as pg}
    {if:pg:isCurrent}<span aria-current="page">{pg:number}</span>{else}<a href="{pg:url}">{pg:number}</a>{/if}
  {/foreach}
  {if:model:pager.nextUrl}<a href="{model:pager.nextUrl}" rel="next">Next</a>{/if}
</nav>
```

## Sitemap and robots.txt

Off unless registered:

```csharp
builder.AddCodeLogicWebLogic(web => web
    .AddSitemap(o =>
    {
        o.ExcludePaths.Add("/login");                 // globs: /account/**
        o.RobotsText = "User-agent: *\nDisallow: /admin/\n";
    })
    .UseSitemapProvider<PostSitemap>());              // optional: dynamic pages

public sealed class PostSitemap(PostStore posts) : IWebSitemapProvider
{
    public async ValueTask<IReadOnlyList<WebSitemapEntry>> GetEntriesAsync(CancellationToken ct) =>
        [.. (await posts.PublishedAsync(ct)).Select(p => new WebSitemapEntry { Path = "/posts/" + p.Slug, LastModified = p.UpdatedAt })];
}
```

- `/sitemap.xml` lists every page route that is anonymous, answers GET, has no route parameters and no
  group, permission or policy requirement, then the provider's entries (default `NullSitemapProvider`:
  none). `/_weblogic/*` routes, routes tagged `.WithTags(WebSitemapOptions.ExcludeTag)` and
  `ExcludePaths` are left out; duplicates are written once, at most `MaxEntries` (50,000).
- URLs are absolute from `PublicBaseUrl`, never the request's Host header. Provider paths must be
  local (`/posts/x`); anything else is skipped. Paths are percent-encoded and everything is XML-escaped.
  Without `PublicBaseUrl` the sitemap answers 404 and logs a warning.
- `/robots.txt` is `RobotsText` (default: allow everything) plus `Sitemap: {PublicBaseUrl}/sitemap.xml`.
- Both send `Cache-Control: public, max-age=3600` (`CacheSeconds`). Turn either off with `ServeSitemap` /
  `ServeRobots`. Do not also map your own `/sitemap.xml` or `/robots.txt`.

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
