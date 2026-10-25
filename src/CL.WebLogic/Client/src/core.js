/**
 * Configuration and the event bus.
 * The server writes the configuration as JSON into <script type="application/json" id="wl-config">.
 */

/** Defaults for every configuration value. */
export const defaultConfig = Object.freeze({
  version: '0.0.0',
  basePath: '',
  csrfHeader: 'X-CSRF-Token',
  csrfField: '__wl_csrf',
  csrfUrl: '/_weblogic/csrf',
  navigation: true,
  debug: false,
  realtime: null
});

/**
 * Parses configuration JSON; anything invalid falls back to the defaults.
 * @param {string | null | undefined} text
 */
export function parseConfig(text) {
  if (!text) return { ...defaultConfig };
  try {
    const value = JSON.parse(text);
    return value && typeof value === 'object' && !Array.isArray(value) ? { ...defaultConfig, ...value } : { ...defaultConfig };
  } catch {
    return { ...defaultConfig };
  }
}

/**
 * Reads the configuration from a document.
 * @param {Document | null | undefined} doc
 */
export function readConfig(doc) {
  const element = doc ? doc.getElementById('wl-config') : null;
  return parseConfig(element ? element.textContent : '');
}

/** A small typed event bus on top of EventTarget. */
export class Bus {
  #target = new EventTarget();

  /**
   * Subscribes; returns a function that unsubscribes.
   * @param {string} type
   * @param {(detail: any, event: CustomEvent) => void} handler
   */
  on(type, handler) {
    const listener = (event) => handler(event.detail, event);
    this.#target.addEventListener(type, listener);
    return () => this.#target.removeEventListener(type, listener);
  }

  /**
   * Subscribes for one event.
   * @param {string} type
   * @param {(detail: any) => void} handler
   */
  once(type, handler) {
    const off = this.on(type, (detail) => {
      off();
      handler(detail);
    });
    return off;
  }

  /**
   * Publishes an event.
   * @param {string} type
   * @param {any} [detail]
   */
  emit(type, detail) {
    this.#target.dispatchEvent(new CustomEvent(type, { detail }));
  }
}
