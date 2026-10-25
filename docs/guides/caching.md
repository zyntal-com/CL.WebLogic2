# Caching

Two layers: the **output cache** stores whole responses, the **fragment cache** stores the result of
expensive work inside a handler. Both evict by tag.

## Output cache

WebLogic uses ASP.NET Core's output cache, placed **after** the route-level security stages, so a
cached page is still authorized and rate-limited on every request.

```csharp
web.AddOutputCachePolicy("pages", p => p.Tag("pages").Expire(TimeSpan.FromMinutes(10)));

b.MapPage("/", HomeAsync).AllowAnonymous().WithOutputCache("pages");
```

WebLogic's policy refuses to look up or store a response when:

- the visitor is signed in, or sent a session cookie,
- the handler set a cookie, used the CSRF token or wrote the session,
- the status is not 200, or the method is not GET/HEAD.

Cache keys vary by tenant automatically. So a cached page never contains another user's data or a
CSRF token. A page that renders `{form:...}` includes a token and is therefore not cached; buttons and
scripts on cached pages can still post through the client (`WebLogic.http.post`, `data-wl-action`),
which fetches a token from `/_weblogic/csrf` when it needs one.

Evict with `IWebCacheInvalidator.EvictTagsAsync(["pages"], ct)`, for example after publishing a post.
For several nodes, register a shared `IOutputCacheStore` (ASP.NET) on your cache server.

## Fragment cache

```csharp
var report = await request.Cache.GetOrAddAsync("sales:daily", async ct =>
{
    return await BuildReportAsync(ct);
}, TimeSpan.FromMinutes(5), ["sales"], ct);
```

- Concurrent callers for the same key share one factory run (stampede protection).
- Entries expire by time and are evicted by tag through the same `IWebCacheInvalidator`.
- The default `MemoryFragmentCache` is bounded (`cache.fragmentMaxEntries`). Supply a shared one with
  `web.UseFragmentCache<T>()` (`IWebFragmentCache`) when nodes must agree.
- Fragment keys are yours: include the tenant or user in the key when the content depends on them.

## Static files

Theme assets get an ETag and `Cache-Control: max-age` (`theme.assetCacheSeconds`); fingerprinted
`{asset:}` URLs and the client bundle are immutable. Small files are kept in memory.
