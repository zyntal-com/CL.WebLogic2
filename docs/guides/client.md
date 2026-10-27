# JavaScript client

The client is a set of hand-written ES2022 modules with no dependencies, bundled by WebLogic's own
build tool and served from the assembly as `/_weblogic/client/weblogic.{hash}.js` (immutable cache,
Brotli/gzip). `{weblogic_scripts}` in your layout adds it with its configuration and the CSP nonce.
Type definitions: `weblogic.d.ts` in the package.

The runtime is available as `window.WebLogic` and in the `wl:ready` event:

```js
document.addEventListener('wl:ready', ({ detail: wl }) => {
  wl.ui.toast('Welcome back', 'success');
});
```

## Modules

| Area | What it does |
|---|---|
| `http` | `get`, `post`, `request`: adds the CSRF header for same-origin requests (fetching a token on demand), aborts by key, timeouts, parses ProblemDetails. `post(url, value)` sends `value` as JSON; for form posts and uploads use `request(url, { method: 'POST', form })` (see below) |
| `forms` | Enhances `data-wl-form`: validates with the server's schema, conditional fields, repeaters, upload progress, double-submit guard, shows server errors, runs result commands |
| `navigation` | Partial navigation: swaps `data-wl-shell`, updates title and meta, restores scroll and focus. Opt out per link with `data-wl-nav="off"`. A page with another layout loads in full (see below) |
| `modules` | `WebLogic.modules.define('chart', (el, wl) => () => cleanup)` mounted on `data-wl-module="chart"`, unmounted when swapped out |
| `widgets` | Widget refresh (`data-wl-refresh`), actions (`data-wl-action="/url"` with `data-wl-payload`, `data-wl-confirm`, `data-wl-target`, `data-wl-swap`) |
| `dashboard` | Layout editing: drag and drop, keyboard moves, add, remove, optimistic-concurrency save |
| `realtime` | SSE/WebSocket connect, reconnect with back-off, `Last-Event-ID`, `subscribe(type, handler)` |
| `passkeys` | WebAuthn ceremonies from the server's JSON options: `data-wl-passkey="signin\|register\|verify\|autofill"` buttons, `signIn`, `register`, `verify`, `startAutofill` ([Passkeys](passkeys.md)) |
| `commands` | Allowlisted server commands: toast, overlay, redirect/navigate (local URLs only), reload, replace, remove, addClass, removeClass |
| `ui` | Toasts, overlays, field errors, progress bar, screen-reader announcements, through a UI adapter |
| `dom` | Safe builders (`el`, `append`, `swap`, `readJson`); untrusted strings are never assigned to `innerHTML` |

## Requests

```js
const { data } = await wl.http.get('/api/quota');                                   // JSON parsed when the response is JSON
await wl.http.post('/api/theme', { theme: 'dark' });                                // body: JSON
const form = new FormData(); form.append('file', file); form.append('bucket', 'forum');
await wl.http.request('/api/media', { method: 'POST', form });                       // body: multipart (or URLSearchParams)
await wl.http.request('/search?q=' + encodeURIComponent(q), { key: 'search' });      // a newer call with the same key aborts the older one
```

Every same-origin request carries the CSRF header, so POST routes accept them without a token field. Calls
resolve to `{ data, status, headers, response }` (`data` is parsed JSON or text). A non-2xx response throws
an `HttpError` with `status` and `problem` (ProblemDetails: `title`, `detail`, `errors`); a stale CSRF token
is refreshed and the request retried once.

## Partial navigation and layouts

Partial navigation only swaps the shell when the next page uses the same layout. The layout is the
shell's `data-wl-layout` attribute when the theme sets one; otherwise WebLogic writes the document's
outermost template (its top layout) into the `wl-config` block and the client compares that. A page with
another layout is loaded normally. Set `data-wl-layout` yourself to group layouts that share a shell (two
layouts with the same value swap into each other).

## UI adapters

`client.uiAdapter` selects `plain` (no CSS framework) or `bootstrap5`. Write your own by calling
`WebLogic.ui.setAdapter({ toast, overlay, showErrors, fieldError, clearErrors, ... })`; any method
you leave out falls back to the plain adapter.

## Safety rules the client follows

- HTML from the server is only swapped in from same-origin responses marked as WebLogic fragments.
- Scripts in swapped HTML do not run, except inert data blocks (`application/json`, `application/ld+json`, ...).
- Redirect and navigate commands accept local URLs only.
- The CSRF token is only sent to the same origin.

## Testing

`/_weblogic/tests` (Development, `client.testPageInDevelopment`) runs the client's browser test suite.
CI runs the same page in headless Chrome. Logic without the DOM is covered by `node --test`.
