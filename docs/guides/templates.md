# Templates and themes

A theme is a folder of HTML templates and static assets. Templates use a small brace syntax that is
parsed once, compiled to delegates and cached. Every value is **encoded for the place it is written**
(text, attribute, URL, script string, CSS), and contexts that cannot be made safe are rejected when
the template is parsed, not at runtime.

## Layout

```
theme/
  layouts/main.html        {renderbody}, {renderhead}, {rendersection:name}
  templates/...            pages (WebResult.Page("templates/home"))
  partials/...             {partial:partials/nav}
  widgets/...              widget templates
  forms/fields/{type}.html optional field overrides
  errors/404.html, errors/error.html   themed error pages
  errors/suspended.html    page for suspended accounts (WebResult.AccountSuspended)
  assets/...               served at /assets/... (extension allowlist)
```

Templates, layouts, partials and dotfiles are never served as assets; requests that escape the theme
folder get a 404. `{asset:site.css}` writes a fingerprinted URL served with an immutable cache header;
`{asset:plugin-name:css/admin.css}` points at a plugin's embedded file ([Plugins](plugins.md)).

A page template picks its layout with `{layout:...}`. Pages without one get their route group's
default layout, if it has one: `b.MapGroup("/admin").WithLayout("admin/layout")`.

## Syntax

| Syntax | Meaning |
|---|---|
| `{model:title}` | A value from the page model (property names are case-insensitive; nested: `model:author.name`) |
| `{page:...}`, `{query:q}`, `{route:slug}`, `{user:name}` | Page meta, query string, route values, the signed-in user |
| `{model:title\|upper\|truncate:60}` | Filters, chained left to right |
| `{if:model:items}...{else}...{/if}` | Truthiness; comparisons: `{if:model:count gt 0}` (`eq ne gt gte lt lte`) |
| `{if:auth}`, `{ifnot:...}` | Signed in / negation |
| `{foreach:model:posts as p}{p:title}{/foreach}` | Loops; `{loop:index}`, `{loop:first}`, `{loop:last}`. The alias can't be a directive or scope word (`t`, `section`, `item`, `loop`, `model`…): those already mean something before `:`, so the parser refuses them |
| `{switch:model:status}{case:draft}...{default}...{/switch}` | Switch |
| `{layout:layouts/main}` | First line of a page: render inside a layout |
| `{section:scripts}...{/section}` / `{rendersection:scripts}` | Named sections |
| `{partial:partials/card}` | Include (relative to the current template unless it starts with `/`) |
| `{form:contact}` | A registered form ([Forms](forms.md)) |
| `{widget:name}`, `{widgetarea:sidebar}` | Widgets ([Widgets](widgets-dashboards.md)) |
| `{t:key}` | Localized text (`IWebTextLocalizer`) |
| `{csrf}`, `{csrf_token}`, `{csrf_meta}` | CSRF field, raw token, meta tag |
| `{nonce}` | The CSP nonce (added automatically to static `<script>` and `<style>` tags) |
| `{renderhead}` | Title, meta, canonical, Open Graph, JSON-LD from `WebPageMeta` |
| `{weblogic_scripts}` | The client bundle and its config, with the nonce |
| `{raw:model:html}` | Write without encoding. Prefer `WebHtml` values or `\|sanitize` |
| `{verbatim}...{/verbatim}` | Literal braces |

An unknown scope word stays literal text, so inline CSS like `{color:red}` is left alone.

## Encoding contexts

The parser tracks where each value lands:

| Where | Encoding |
|---|---|
| Text | HTML-encoded |
| Attribute value | Attribute-encoded |
| `href`, `src`, `action`, ... | Attribute-encoded, and URL schemes other than `http`, `https`, `mailto`, `tel` and relative are replaced by `about:blank#blocked` |
| `<img src>` | As above, and base64 raster `data:image/png\|jpeg\|gif\|webp` URLs are kept (e.g. a two-factor QR code from `QrCodeDataUri`); SVG and other `data:` URLs are blocked |
| `style` attribute | CSS-encoded |
| Event handler attributes (`on*`), `srcdoc` | Rejected at parse time |
| Unquoted attribute values, inside a tag name or between attributes | Rejected at parse time |
| `<script>` | Only `\|json`, or `\|js` inside a `'…'` or `"…"` JS string; values inside a `` `…` `` template literal and anything else are parse errors |
| `<style>` | Only `\|css` |

Values of type `WebHtml` (rendered Markdown, sanitized HTML, forms, widgets) are written as-is in text
context. Everything else is a string and gets encoded.

## Filters

Text: `upper`, `lower`, `capitalize`, `trim`, `truncate:n`, `replace:a:b`, `prefix:x`, `suffix:x`,
`default:x`, `slug`, `wordcount`, `length`/`count`, `join:sep`, `reverse`.
Numbers and dates: `number`, `round:n`, `percent`, `abbreviate` (1.2k), `filesize`, `date:format`,
`timeago`, `format:fmt`.
Markup and contexts: `markdown`, `sanitize`, `nl2br` (encodes first), `attr`, `url`/`urlencode`, `js`,
`json`, `css`.

Add your own with `web.AddTemplateFilter<T>()` (`ITemplateFilter`). Return `WebHtml` only when the
filter itself guarantees safe markup.

## Markdown and sanitizing

`|markdown` uses WebLogic's own CommonMark-subset renderer: raw HTML is encoded, link and image URLs
go through the scheme policy, and pathological input is bounded. `|sanitize` runs an allowlist
sanitizer on WebLogic's own HTML tokenizer (`web.UseSanitizerOptions(...)` to change the allowlist).
Use it for rich-text form fields and any stored HTML.

Styles and embeds are off by default. Turn on only what your editor produces:

```csharp
web.UseSanitizerOptions(new WebHtmlSanitizerOptions
{
    AllowedStyleProperties = new Dictionary<string, Func<string, bool>>(StringComparer.OrdinalIgnoreCase)
    {
        ["width"] = WebCssValues.Percentage,                         // image resize
        ["color"] = WebCssValues.Color,
        ["text-align"] = WebCssValues.OneOf("left", "center", "right")
    },
    AllowedFrameSources = [new WebFrameSource("www.youtube-nocookie.com", "/embed/")]
});
```

The `style` attribute is rebuilt from allowed declarations (no `url()`, comments, escapes or
non-colour functions); a kept iframe gets a fixed `sandbox`, `allow` and referrer policy. Allowed frame
hosts also need `security.headers.frameSources`.

## Template globals

`ITemplateGlobalsProvider` adds values every template sees (`web.AddTemplateGlobals<T>()`), for example
the site name, the current year or navigation items.

## Theme sources

`theme.source`:

- `local`: a folder (`theme.localPath`).
- `git`: a repository cloned and pulled through CL.GitHelper (`repositoryUrl`, `branch`, `themeSubPath`).
  Private repositories: `gitTokenEnvironmentVariable` (an HTTPS token read from the environment;
  `gitUserName` defaults to `x-access-token`, GitLab wants `oauth2`) or `gitSshKeyPath` (a deploy key,
  with `gitSshPassphraseEnvironmentVariable` if it has a passphrase). `gitToken` exists as a secret
  config field, but prefer the environment. Credentials apply at startup.
- `storage`: a CL.Storage connection (S3, Azure, ...), synced to a local cache.

Templates are re-checked at most every `theme.templateChangeCheckSeconds` (2 s by default; 0 for
immutable production themes). Plugins can ship templates as embedded resources
([Plugins](plugins.md)); a theme file with the same path wins.

## Checking a theme

`WebThemeChecker.Check("theme")` parses every template (skipping `assets/`) and returns the ones the
engine would refuse, with file, line and column: syntax errors, inline `on*` handlers, unquoted
attribute values, script values without `|json`. Run it before switching a 0.x theme, or keep it in
a test:

```csharp
[Fact] public void Theme_parses() => Assert.Empty(WebThemeChecker.Check("../../src/MySite/theme"));
```

From a clone of this repository: `dotnet run --project tools/CL.WebLogic.ThemeCheck -- path/to/theme`
(exit code 1 when something needs changing).

## Engines

`theme.templateEngine` is `closure` (compiled delegates, default) or `interpreter` (the reference AST
walker). Both produce identical output; the test suite checks this against golden files.
