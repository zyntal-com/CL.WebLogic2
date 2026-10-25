/**
 * Page modules: elements with data-wl-module="name other" are mounted with the registered
 * factory, and unmounted (cleanup called) before their part of the page is replaced.
 */

const NAME = /^[a-z][\w.-]{0,63}$/i;

/**
 * @param {object} context Passed to every factory as the second argument (the WebLogic API).
 */
export function createModules(context) {
  /** @type {Map<string, (element: Element, context: object) => any>} */
  const definitions = new Map();
  /** @type {Map<Element, Map<string, (() => void) | null>>} */
  const mounted = new Map();
  let root = null;

  /**
   * Registers a module. The factory may return a cleanup function or an object with unmount().
   * @param {string} name
   * @param {(element: Element, context: object) => any} factory
   */
  function define(name, factory) {
    if (!NAME.test(name)) throw new Error(`WebLogic: invalid module name '${name}'.`);
    if (typeof factory !== 'function') throw new TypeError('WebLogic: a module factory must be a function.');
    definitions.set(name, factory);
    if (root) mount(root); // late definitions mount on the current page
  }

  /** Mounts every module element in (and including) container. */
  function mount(container) {
    if (!container) return;
    root ??= container.ownerDocument ? container.ownerDocument.body : container;
    const elements = container.matches && container.matches('[data-wl-module]') ? [container] : [];
    elements.push(...container.querySelectorAll('[data-wl-module]'));
    for (const element of elements) {
      for (const name of (element.getAttribute('data-wl-module') || '').split(/\s+/).filter(Boolean)) {
        const factory = definitions.get(name);
        const instances = mounted.get(element) || new Map();
        if (!factory || instances.has(name)) continue;
        try {
          const result = factory(element, context);
          const cleanup = typeof result === 'function' ? result : result && typeof result.unmount === 'function' ? () => result.unmount() : null;
          instances.set(name, cleanup);
          mounted.set(element, instances);
        } catch (error) {
          console.error(`WebLogic: module '${name}' failed to mount`, error);
        }
      }
    }
  }

  /** Unmounts every mounted module in (and including) container. */
  function unmount(container) {
    for (const [element, instances] of [...mounted]) {
      if (container !== element && !container.contains(element)) continue;
      for (const [name, cleanup] of instances) {
        try {
          cleanup?.();
        } catch (error) {
          console.error(`WebLogic: module '${name}' failed to unmount`, error);
        }
      }
      mounted.delete(element);
    }
  }

  return {
    define,
    mount,
    unmount,
    /** @param {string} name */
    has: (name) => definitions.has(name),
    /** Number of mounted elements (for tests). */
    get mountedCount() {
      return mounted.size;
    }
  };
}
