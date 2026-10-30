# Localization

Off by default. With `localization.enabled` false nothing is resolved, no header is added,
`{t:key}` writes the key and form messages use the built-in English texts, exactly as before.

When enabled, WebLogic:

- resolves one culture per request, only ever from your list of supported cultures,
- runs the request with `CultureInfo.CurrentUICulture` (and `CurrentCulture`) set to it,
- looks `{t:key}` and form messages up in per-culture texts, with a fallback chain,
- keeps the output cache and the fragment cache apart per culture,
- adds `Content-Language` (and `Vary: Accept-Language`) to responses.

## Configuration

```json
{
  "localization": {
    "enabled": true,
    "defaultCulture": "en",
    "supportedCultures": ["sv-SE", "da"],
    "sources": ["query", "cookie", "header"]
  }
}
```

| Setting | Default | Meaning |
|---|---|---|
| `enabled` | `false` | Turns everything on this page on. Requires a restart (the switch endpoint is mapped at startup) |
| `defaultCulture` | `en` | Used when no source names a supported culture; always supported; the last text fallback |
| `supportedCultures` | `[]` | The only cultures a request can select, besides the default |
| `sources` | `query, cookie, header` | Checked in order. `header` is `Accept-Language` (by quality) |
| `queryParameter` | `culture` | `?culture=sv-SE` |
| `cookieName` | `__Host-wl_culture` | Cookie of the `cookie` source (no `__Host-` prefix over plain HTTP) |
| `cookieDays` | `365` | Lifetime of the culture cookie |
| `switchPath` | `/_weblogic/culture` | The cookie switch endpoint |
| `resourcesFolder` | `locales` | Theme folder with the built-in `{culture}.json` texts |
| `setCurrentCulture` | `true` | Also set `CurrentCulture` (number and date formatting); the UI culture is always set |

Culture names are checked at startup: unknown names (`xx-YY`) make the config invalid.

## How a culture is chosen

Each source value is matched against the supported cultures (the default included), never turned
into a `CultureInfo` itself:

1. exactly, case-insensitively (`sv_SE` counts as `sv-SE`),
2. by its parents: `da-DK` matches `da`,
3. by language: `sv` or `sv-FI` matches the first supported `sv-*` (`sv-SE`).

Anything else (unsupported cultures, characters other than letters, digits, `-` and `_`, values
over 64 characters) is ignored and the next source is tried. `Accept-Language` is read up to 1 KB and
16 entries. If no source matches, the default culture is used.

The result is available everywhere in the request:

```csharp
b.MapPage("/about", (request, ct) =>
{
    var culture = request.Culture;          // WebRequestCulture, or null when localization is off
    var name = culture?.Name;               // "sv-SE"
    var source = culture?.Source;           // "query", "cookie", "header" or "default"
    return ValueTask.FromResult<WebResult>(WebResult.Page("templates/about"));
}).AllowAnonymous();

// Outside WebLogic routes (minimal APIs, middleware):
var culture = WebRequestCulture.Get(httpContext);
```

The cultures are set on the async execution context, so they flow through `await` (also with
`ConfigureAwait(false)`) and are restored when the request ends. The culture middleware runs after
routing and before endpoint security, so access-denied pages are localized too; requests rejected
earlier in the pipeline use the server culture.

## Texts

The default `IWebTextLocalizer` asks `IWebTextResources` for each culture in the fallback chain and
returns the first hit, or the key:

```text
sv-SE → sv → en (defaultCulture) → the key itself
```

`WebCultureFallback.Chain("sv-SE", "en-US")` returns `sv-SE, sv, en-US, en`.

### Theme files (built in)

Put one JSON file per culture in the theme:

```text
theme/
  locales/
    en.json
    sv.json
    sv-SE.json      (only what differs from sv)
```

```json
{
  "nav": { "home": "Hem", "about": "Om oss" },
  "forms": {
    "required": "{0} måste fyllas i.",
    "summary": "Rätta de markerade fälten."
  }
}
```

Nested objects are flattened with dots (`nav.home`, `forms.required`); non-string values are ignored.
Files are cached and re-checked at most every `theme.templateChangeCheckSeconds`, so theme updates
(including Git and storage themes) are picked up without a restart. A file that does not parse is
logged and the last good texts stay in use. Files over 4 MiB and links are refused.

### Your own store

Load texts from a database or anywhere else by replacing the resources. The localizer walks the
fallback chain, so answer only for the exact culture asked:

```csharp
public sealed class DbTextResources(TextCache cache) : IWebTextResources
{
    public string? GetText(string culture, string key) => cache.Find(culture, key);
}

builder.AddCodeLogicWebLogic(web => web.UseTextResources<DbTextResources>());
```

`GetText` is called while rendering: keep the texts in memory. To replace the whole lookup instead,
register your own `IWebTextLocalizer` with `web.UseTextLocalizer<T>()`.

## Templates

```html
<html lang="{model:culture}">
<a href="/" title="{t:nav.home}">{t:nav.home}</a>
```

`{t:key}` is encoded for the context it is in, like any other value (text, attribute, ...), so texts
are plain text, not HTML. The `culture` global holds the resolved culture name (it is not set when
localization is off). `{t:key}` uses the request culture even on error pages.

Form validation messages use the keys `forms.required`, `forms.maxLength`, ... (see
[Forms](forms.md)); a key your texts do not have keeps the built-in English message.

## Switching culture

Link to the switch endpoint. It sets the culture cookie and redirects to a local return URL
(anything else redirects to `/`); an unsupported culture answers 400 and sets nothing:

```html
<a href="/_weblogic/culture?culture=sv-SE&returnUrl=/about">Svenska</a>
<a href="/_weblogic/culture?culture=da&returnUrl=/about">Dansk</a>
```

The cookie is HttpOnly, `SameSite=Lax`, path `/` and `Secure` when the site is reached over HTTPS.
To switch from your own handler (a profile setting, a form post), use `WebCultureCookie`:

```csharp
var cookie = request.GetRequiredService<WebCultureCookie>();
if (!cookie.TrySet(request.HttpContext, form.Language))
    return WebResult.Status(400);
return WebResult.Redirect("/profile");
```

Put `cookie` before `header` in `sources` so a chosen language wins over the browser's.

## Caching

- **Output cache:** entries vary by the resolved culture, so a Swedish page is never served to a
  Danish visitor. The culture cookie does not make a page uncacheable.
- **Fragment cache:** `request.Cache` keys include the culture.
- **HTTP caches and CDNs:** responses carry `Content-Language`, and `Vary: Accept-Language` when the
  `header` source is configured. The cookie source is invisible to a shared cache that ignores
  cookies; when a CDN caches your pages, prefer the `query` source (or culture-specific URLs).

Static theme assets get neither header.

## What the application supplies

- The list of supported cultures and the texts (theme files or an `IWebTextResources`).
- A language switcher in the theme.
- Culture-specific URLs (`/sv/about`) if you want them: map the routes and pass the culture to
  `WebCultureCookie` or read it from the route; the toolkit does not rewrite paths.
