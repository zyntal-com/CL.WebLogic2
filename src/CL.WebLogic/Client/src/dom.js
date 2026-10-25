/**
 * DOM helpers. Untrusted data only ever becomes text nodes or attribute values (el, setText);
 * parseHtml / swap are for HTML rendered by this site's server, and drop executable scripts.
 */

/** Script types that never execute (JSON data blocks); every other type is treated as code. */
export const INERT_SCRIPT_TYPES = new Set(['application/json', 'application/ld+json', 'text/template']);

/** True when a script element would run (or change how modules load) if inserted. */
export const isExecutableScript = (script) => !INERT_SCRIPT_TYPES.has((script.getAttribute('type') || '').trim().toLowerCase().split(';')[0].trim());
const URL_ATTRIBUTES = new Set(['href', 'src', 'action', 'formaction', 'xlink:href', 'poster']);

/** @param {ParentNode} root @param {string} selector */
export const qs = (root, selector) => root.querySelector(selector);

/** @param {ParentNode} root @param {string} selector */
export const qsa = (root, selector) => [...root.querySelectorAll(selector)];

/**
 * False for javascript:, vbscript: and data: URLs (whitespace and control characters ignored).
 * @param {unknown} value
 */
export function isSafeUrlValue(value) {
  const normalized = String(value).replace(/[\u0000- ]/g, '').toLowerCase();
  return !/^(javascript|vbscript|data):/.test(normalized);
}

/**
 * Creates an element. Children that are not nodes become text. Event-handler attributes are
 * refused, unsafe URLs in URL attributes are dropped.
 * @param {string} tag
 * @param {Record<string, any> | null} [attributes]
 * @param {...any} children
 */
export function el(tag, attributes, ...children) {
  const node = document.createElement(tag);
  for (const [key, value] of Object.entries(attributes || {})) {
    if (value === null || value === undefined || value === false) continue;
    if (/^on/i.test(key)) throw new Error(`WebLogic: '${key}' is not allowed; use addEventListener.`);
    if (key === 'class') node.className = Array.isArray(value) ? value.filter(Boolean).join(' ') : String(value);
    else if (key === 'dataset') Object.assign(node.dataset, value);
    else if (key === 'text') node.textContent = String(value);
    else if (URL_ATTRIBUTES.has(key.toLowerCase()) && !isSafeUrlValue(value)) continue;
    else node.setAttribute(key, value === true ? '' : String(value));
  }
  append(node, children);
  return node;
}

/**
 * Appends children (nodes as-is, everything else as text).
 * @param {Node} node
 * @param {any[]} children
 */
export function append(node, children) {
  for (const child of children.flat(Infinity)) {
    if (child === null || child === undefined || child === false) continue;
    node.appendChild(child instanceof Node ? child : document.createTextNode(String(child)));
  }
  return node;
}

/**
 * Parses server-rendered HTML into a fragment, removing executable scripts (JSON data blocks stay).
 * @param {string} html
 */
export function parseHtml(html) {
  const template = document.createElement('template');
  template.innerHTML = String(html); // server-rendered markup only
  for (const script of template.content.querySelectorAll('script')) {
    if (isExecutableScript(script)) script.remove();
  }
  return template.content;
}

/**
 * Puts server-rendered HTML into the page; returns the inserted elements.
 * @param {Element} target
 * @param {string} html
 * @param {'inner' | 'outer' | 'append' | 'prepend' | 'before' | 'after'} [mode]
 */
export function swap(target, html, mode = 'inner') {
  const fragment = parseHtml(html);
  const inserted = [...fragment.children];
  switch (mode) {
    case 'outer': target.replaceWith(fragment); break;
    case 'append': target.append(fragment); break;
    case 'prepend': target.prepend(fragment); break;
    case 'before': target.before(fragment); break;
    case 'after': target.after(fragment); break;
    default: target.replaceChildren(fragment); break;
  }
  return inserted;
}

/**
 * Reads a JSON data block (<script type="application/json">).
 * @param {ParentNode} root
 * @param {string} selector
 */
export function readJson(root, selector) {
  const script = root.querySelector(selector);
  if (!script) return null;
  try {
    return JSON.parse(script.textContent || '');
  } catch {
    return null;
  }
}

/**
 * Marks an element busy: aria-busy, and disables it (or its submit buttons for forms).
 * @param {HTMLElement} element
 * @param {boolean} busy
 */
export function setBusy(element, busy) {
  element.toggleAttribute('aria-busy', busy);
  const targets = element instanceof HTMLFormElement
    ? element.querySelectorAll('button[type="submit"], button:not([type]), input[type="submit"]')
    : [element];
  for (const target of targets) {
    if (busy) {
      if (!target.disabled) {
        target.dataset.wlDisabledByBusy = '1';
        target.disabled = true;
      }
    } else if (target.dataset.wlDisabledByBusy) {
      delete target.dataset.wlDisabledByBusy;
      target.disabled = false;
    }
  }
}
