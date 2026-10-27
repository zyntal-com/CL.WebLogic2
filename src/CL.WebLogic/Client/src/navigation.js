/**
 * Partial page navigation: same-origin link clicks inside a page with [data-wl-shell] fetch the
 * next page and swap only the shell's content, the title and the managed head nodes
 * ([data-wl-head]). Inline scripts are never re-run — modules are re-mounted instead. Anything
 * unusual (other origin, non-HTML, another layout, network error) falls back to a normal page
 * load. The layout is the shell's data-wl-layout, or else the server's "layout" in wl-config.
 */
import { parseConfig, readConfig } from './core.js';
import { isExecutableScript } from './dom.js';

/**
 * True when a click on link should be handled by partial navigation.
 * @param {HTMLAnchorElement | null} link
 * @param {MouseEvent} event
 * @param {{ origin: string, pathname: string, search: string }} location
 */
export function shouldIntercept(link, event, location) {
  if (!link || event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return false;
  if (link.hasAttribute('download') || (link.target && link.target !== '_self')) return false;
  if (link.closest('[data-wl-nav="off"]') || /(^|\s)external(\s|$)/.test(link.getAttribute('rel') || '')) return false;
  const href = link.getAttribute('href');
  if (!href || href.startsWith('#')) return false;
  let url;
  try {
    url = new URL(link.href, location.origin);
  } catch {
    return false;
  }
  if (url.origin !== location.origin || (url.protocol !== 'http:' && url.protocol !== 'https:')) return false;
  return !(url.hash && url.pathname === location.pathname && url.search === location.search);
}

/**
 * True when two shells belong to the same layout: their data-wl-layout attributes, or else the
 * "layout" (outermost template) the server wrote into each document's wl-config.
 * @param {Element} shell
 * @param {Element} incoming
 * @param {{ layout?: string | null }} currentConfig
 * @param {{ layout?: string | null }} nextConfig
 */
export function sameLayout(shell, incoming, currentConfig, nextConfig) {
  const current = shell.getAttribute('data-wl-layout') ?? (currentConfig && currentConfig.layout) ?? '';
  const next = incoming.getAttribute('data-wl-layout') ?? (nextConfig && nextConfig.layout) ?? '';
  return current === next;
}

/**
 * @param {{ bus: any, ui: any, config: any, beforeSwap: (shell: Element) => void, afterSwap: (shell: Element) => void,
 *           onConfig: (config: object) => void }} services
 */
export function createNavigation({ bus, ui, config, beforeSwap, afterSwap, onConfig }) {
  let enabled = false;
  let controller = null;

  /** Enables navigation when the page has a shell. */
  function start() {
    if (enabled || config.navigation === false || !document.querySelector('[data-wl-shell]')) return;
    enabled = true;
    document.addEventListener('click', (event) => {
      const link = event.target instanceof Element ? event.target.closest('a[href]') : null;
      if (!shouldIntercept(link, event, globalThis.location)) return;
      event.preventDefault();
      navigate(link.href);
    });
    globalThis.addEventListener('popstate', (event) => {
      if (event.state && event.state.wl) navigate(globalThis.location.href, { history: 'none', scroll: false });
    });
    history.replaceState({ ...(history.state || {}), wl: true }, '');
  }

  /**
   * Navigates to url (partially when possible). Resolves true when the shell was swapped.
   * @param {string} url
   * @param {{ replace?: boolean, history?: 'push' | 'none', scroll?: boolean }} [options]
   */
  async function navigate(url, options = {}) {
    const target = new URL(url, globalThis.location.href);
    if (!enabled || target.origin !== globalThis.location.origin) {
      globalThis.location.assign(target.href);
      return false;
    }

    controller?.abort();
    controller = new AbortController();
    bus.emit('navigate:start', { url: target.href });
    ui.progress.start();
    let failed = false;
    try {
      const response = await fetch(target.href, {
        headers: { 'X-Requested-With': 'WebLogic', 'X-WebLogic-Navigate': '1', Accept: 'text/html' },
        credentials: 'same-origin',
        signal: controller.signal
      });
      const finalUrl = new URL(response.url || target.href);
      if (!(response.headers.get('Content-Type') || '').includes('text/html') || finalUrl.origin !== globalThis.location.origin) {
        globalThis.location.assign(finalUrl.href);
        return false;
      }

      const next = new DOMParser().parseFromString(await response.text(), 'text/html');
      const shell = document.querySelector('[data-wl-shell]');
      const incoming = next.querySelector('[data-wl-shell]');
      if (!shell || !incoming || !sameLayout(shell, incoming, config, readConfig(next))) {
        globalThis.location.assign(finalUrl.href);
        return false;
      }

      beforeSwap(shell);
      updateHead(next);
      for (const script of incoming.querySelectorAll('script')) {
        if (isExecutableScript(script)) script.remove();
      }
      shell.replaceChildren(...[...incoming.childNodes].map((node) => document.importNode(node, true)));

      const address = finalUrl.pathname + finalUrl.search + (finalUrl.hash || target.hash);
      if (options.history !== 'none') history[options.replace ? 'replaceState' : 'pushState']({ wl: true }, '', address);
      afterSwap(shell);
      settle(shell, target.hash, options.scroll !== false);
      bus.emit('navigate:end', { url: address, shell, status: response.status });
      return true;
    } catch (error) {
      if (error && error.name === 'AbortError') return false;
      failed = true;
      bus.emit('navigate:error', { url: target.href, error });
      globalThis.location.assign(target.href);
      return false;
    } finally {
      ui.progress.done(failed);
    }
  }

  function updateHead(next) {
    document.title = next.title;
    for (const node of document.head.querySelectorAll('[data-wl-head]')) node.remove();
    for (const node of next.head.querySelectorAll('[data-wl-head]')) {
      if (node.tagName === 'SCRIPT' && isExecutableScript(node)) continue;
      document.head.append(document.importNode(node, true));
    }
    const nextConfig = next.getElementById('wl-config');
    const currentConfig = document.getElementById('wl-config');
    if (nextConfig && currentConfig) {
      currentConfig.textContent = nextConfig.textContent;
      onConfig(parseConfig(nextConfig.textContent));
    }
  }

  function settle(shell, hash, scroll) {
    if (scroll) {
      const anchor = hash ? document.getElementById(decodeURIComponent(hash.slice(1))) : null;
      if (anchor) anchor.scrollIntoView();
      else globalThis.scrollTo(0, 0);
    }
    const heading = shell.querySelector('h1');
    const focusTarget = heading || shell;
    if (!focusTarget.hasAttribute('tabindex')) focusTarget.setAttribute('tabindex', '-1');
    focusTarget.focus({ preventScroll: true });
    ui.announce(document.title);
  }

  return {
    start,
    navigate,
    get enabled() {
      return enabled;
    }
  };
}
