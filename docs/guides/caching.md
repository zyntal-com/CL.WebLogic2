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

## ETags for pages and JSON

Off by default. Turn it on per route, or for every page, fragment, API and fallback route with
`cache.eTags: true` (a route can then opt out with `WithoutETag()`):

```csharp
b.MapPage("/blog/{slug}", PostAsync).AllowAnonymous().WithETag();
b.MapApi("/api/menu", MenuAsync).AllowAnonymous().WithETag();
```

The body of GET/HEAD requests is buffered (up to `cache.eTagMaxBytes`, 1 MB; larger bodies stream
without an ETag). When the answer is a 200 without its own ETag, WebLogic adds a strong ETag (the first
128 bits of the body's SHA-256) and `Cache-Control: no-cache` if the handler set none, so browsers
revalidate every time. A matching `If-None-Match` gets a `304` without a body. The handler still runs
(or the output cache answers): ETags save bandwidth, not server work.

Personal responses never get an ETag, by the same rules as the output cache: signed-in or `Authorization`
requests, a session cookie, a waiting flash message, cookies set, a CSRF token issued, the session
loaded or written, or `Cache-Control: private`/`no-store`. The check is in `WebResponsePrivacy`, shared
by the output cache, ETags and compression. Do not use `WithETag()` on routes that stream (flush
partial output); SSE and asset routes are never buffered, and assets keep their own ETags.

## Compression

Off by default. `compression.enabled: true` turns on Brotli and gzip (ASP.NET Core's response
compression, level `compression.level`, `fastest` by default) for HTML, CSS, JavaScript, JSON, XML,
SVG and `compression.mimeTypes`.

Compressing a secret next to attacker-controlled input leaks the secret through the response size
(BREACH). WebLogic therefore never compresses a personal response (the rules above: signed in, session,
CSRF token, cookies). Anonymous pages and public JSON are compressed, signed-in pages are not. Theme and
plugin asset routes are static files and are compressed for everyone. A route whose personal responses hold no secret next to reflected input may opt in with
`AllowPersonalCompression()`; `WithoutCompression()` turns compression off for a route. Compression
runs outside the output cache, so cached entries are stored uncompressed and compressed per request.

The client bundle is already precompressed and keeps its own `Content-Encoding`; it is never compressed
twice. If a reverse proxy or CDN compresses for you, leave this off.

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
