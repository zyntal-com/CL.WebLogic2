# CMS / blog

Public, cacheable content written by a few editors. `samples/MiniBlog` is this scenario on MySQL:
Markdown posts, tag-evicted page cache, RSS and sitemap, and an admin area shipped as a plugin.

## Content storage is yours

WebLogic has no content model. Define your own (`Post`, `Page`, `Category`) and store it wherever you
like. MiniBlog uses CL.MySQL2 in `samples/MiniBlog.Data`; a file-based CMS could read Markdown files
from the theme or a CL.Storage bucket.

## Rendering content

```csharp
b.MapPage("/blog/{slug}", async (r, ct) =>
{
    var post = await posts.FindPublishedAsync(r.GetRouteValue("slug")!, ct);
    if (post is null) return WebResult.Status(404);
    return WebResult.Page("templates/post", post, new WebPageMeta
    {
        Title = post.Title, Description = post.Summary, CanonicalUrl = "/blog/" + post.Slug
    });
}).AllowAnonymous().WithOutputCache("posts");
```

```html
{layout:layouts/main}
<article>
  <h1>{model:title}</h1>
  <time datetime="{model:publishedAt|date:yyyy-MM-dd}">{model:publishedAt|timeago}</time>
  {model:body|markdown}
</article>
```

`|markdown` encodes raw HTML and checks link schemes; for HTML from a rich-text editor store it as
written and render it with `|sanitize` (allowlist).

## Publishing and the cache

Cache public pages with a tag, then evict on change:

```csharp
web.AddOutputCachePolicy("posts", p => p.Tag("posts").Expire(TimeSpan.FromHours(6)));

// after saving a post in the admin
await cache.EvictTagsAsync(["posts"], ct);
```

Editors are signed in, so they always see fresh pages; visitors get cached ones.

## Feeds and sitemaps

Return them as byte results with their content type from anonymous, cached routes:

```csharp
b.MapPage("/feed.xml", async (r, ct) =>
    WebResult.Bytes(Encoding.UTF8.GetBytes(await feeds.RssAsync(ct)), "application/rss+xml; charset=utf-8"))
 .AllowAnonymous().WithOutputCache("posts");
```

## Editors

- Groups or permissions on the admin group: `b.MapGroup("/admin").RequirePermissions("posts.*")`.
- Forms for the editor, with `[WebField(WebFieldType.RichText)]` sanitized on bind, or Markdown in a
  `TextArea`.
- Put the admin in a plugin ([Plugin-heavy platform](plugin-platform.md)) to deploy it separately, as
  MiniBlog does.
- Audit sink for "who changed what" if you need it ([Security pipeline](../guides/security-pipeline.md#audit)).
