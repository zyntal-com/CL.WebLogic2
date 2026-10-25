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
| `http` | `get`, `post`, `request`: adds the CSRF header for same-origin requests (fetching a token on demand), aborts by key, timeouts, parses ProblemDetails |
| `forms` | Enhances `data-wl-form`: validates with the server's schema, conditional fields, repeaters, upload progress, double-submit guard, shows server errors, runs result commands |
| `navigation` | Partial navigation: swaps `data-wl-shell`, updates title and meta, restores scroll and focus. Opt out per link with `data-wl-nav="off"` |
| `modules` | `WebLogic.modules.define('chart', (el, wl) => () => cleanup)` mounted on `data-wl-module="chart"`, unmounted when swapped out |
| `widgets` | Widget refresh (`data-wl-refresh`), actions (`data-wl-action="/url"` with `data-wl-payload`, `data-wl-confirm`, `data-wl-target`, `data-wl-swap`) |
| `dashboard` | Layout editing: drag and drop, keyboard moves, add, remove, optimistic-concurrency save |
| `realtime` | SSE/WebSocket connect, reconnect with back-off, `Last-Event-ID`, `subscribe(type, handler)` |
| `commands` | Allowlisted server commands: toast, overlay, redirect/navigate (local URLs only), reload, replace, remove, addClass, removeClass |
| `ui` | Toasts, overlays, field errors, progress bar, screen-reader announcements, through a UI adapter |
| `dom` | Safe builders (`el`, `append`, `swap`, `readJson`); untrusted strings are never assigned to `innerHTML` |

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
