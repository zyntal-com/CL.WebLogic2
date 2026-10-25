/**
 * Server-driven actions and refreshable regions:
 * - [data-wl-action="/url"] (click): POSTs data-wl-payload (JSON) and applies the response —
 *   an HTML fragment into data-wl-target (or the closest [data-wl-widget]), or commands.
 *   Optional data-wl-method, data-wl-confirm, data-wl-swap (inner|outer).
 * - [data-wl-refresh="/url"]: reloads its content from the URL; with
 *   data-wl-refresh-interval="seconds" (min 5) while the page is visible.
 */
import { setBusy } from './dom.js';
import { isLocalUrl, isSameOrigin } from './url.js';

const MIN_INTERVAL_SECONDS = 5;

/**
 * @param {{ http: any, commands: any, ui: any, bus: any, swap: Function, enhance: (root: Element) => void, unmount: (root: Element) => void }} services
 */
export function createWidgets({ http, commands, ui, bus, swap, enhance, unmount }) {
  const timers = new Map();
  let listening = false;

  /** Starts click handling and refresh timers in root. */
  function bind(root) {
    if (!listening) {
      listening = true;
      document.addEventListener('click', onClick);
    }
    const elements = root.matches && root.matches('[data-wl-refresh-interval]') ? [root] : [];
    elements.push(...root.querySelectorAll('[data-wl-refresh-interval]'));
    for (const element of elements) schedule(element);
  }

  function onClick(event) {
    const element = event.target instanceof Element ? event.target.closest('[data-wl-action]') : null;
    if (!element || event.defaultPrevented) return;
    event.preventDefault();
    action(element);
  }

  /**
   * Runs the action of element.
   * @param {HTMLElement} element
   */
  async function action(element) {
    const url = element.getAttribute('data-wl-action');
    if (!isLocalUrl(url)) {
      console.warn('WebLogic: data-wl-action must be a local URL', url);
      return null;
    }
    const confirmText = element.getAttribute('data-wl-confirm');
    if (confirmText && !globalThis.confirm(confirmText)) return null;

    let payload = {};
    const raw = element.getAttribute('data-wl-payload');
    if (raw) {
      try {
        payload = JSON.parse(raw);
      } catch {
        console.warn('WebLogic: data-wl-payload is not valid JSON');
        return null;
      }
    }

    setBusy(element, true);
    try {
      const method = (element.getAttribute('data-wl-method') || 'POST').toUpperCase();
      const result = await http.request(url, {
        method,
        json: method === 'GET' ? undefined : payload,
        key: 'action:' + url,
        headers: { Accept: 'application/json, text/html' }
      });
      apply(element, result, target(element), element.getAttribute('data-wl-swap') || 'inner');
      bus.emit('action:done', { element, url, data: result.data });
      return result.data;
    } catch (error) {
      ui.toast(error && error.problem ? error.problem.detail || error.problem.title : 'The action failed.', 'danger');
      bus.emit('action:error', { element, url, error });
      return null;
    } finally {
      setBusy(element, false);
    }
  }

  /**
   * Reloads a [data-wl-refresh] element.
   * @param {HTMLElement} element
   */
  async function refresh(element) {
    const url = element.getAttribute('data-wl-refresh');
    if (!isLocalUrl(url)) return false;
    try {
      const result = await http.request(url, { key: 'refresh:' + url, headers: { Accept: 'text/html' } });
      apply(element, result, element, element.getAttribute('data-wl-swap') || 'inner');
      return true;
    } catch (error) {
      bus.emit('refresh:error', { element, url, error });
      return false;
    }
  }

  function apply(element, result, into, mode) {
    const fragment = result.headers.get('X-WebLogic-Fragment') === '1' && typeof result.data === 'string'
      && isSameOrigin(result.response.url || globalThis.location.href, globalThis.location.origin);
    if (fragment && into) {
      if (mode === 'outer') unmount(into);
      else for (const child of [...into.children]) unmount(child);
      for (const inserted of swap(into, result.data, mode === 'outer' ? 'outer' : 'inner')) enhance(inserted);
      return;
    }
    const data = result.data && typeof result.data === 'object' ? result.data : {};
    if (typeof data.message === 'string' && data.message) ui.toast(data.message, 'success');
    if (Array.isArray(data.commands)) commands.run(data.commands);
  }

  function target(element) {
    const selector = element.getAttribute('data-wl-target');
    if (selector) {
      try {
        return document.querySelector(selector);
      } catch {
        return null;
      }
    }
    return element.closest('[data-wl-widget]') || element;
  }

  function schedule(element) {
    if (timers.has(element)) return;
    const seconds = Math.max(MIN_INTERVAL_SECONDS, Number(element.getAttribute('data-wl-refresh-interval')) || 0);
    const id = setInterval(() => {
      if (!element.isConnected) {
        clearInterval(id);
        timers.delete(element);
        return;
      }
      if (document.visibilityState === 'visible') refresh(element);
    }, seconds * 1000);
    timers.set(element, id);
  }

  return { bind, action, refresh };
}
