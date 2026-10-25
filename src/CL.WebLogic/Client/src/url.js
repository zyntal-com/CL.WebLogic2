/** URL helpers. "Local" means a path on this site, the same rule as the server's WebUrl.IsLocal. */

const CONTROL = /[\u0000-\u001f\u007f]/;

/**
 * True for "/path?query#hash" on this site; false for absolute, protocol-relative ("//host"),
 * backslash ("/\\host") or control-character URLs.
 * @param {unknown} url
 */
export function isLocalUrl(url) {
  if (typeof url !== 'string' || url.length === 0 || url.length > 4096) return false;
  if (CONTROL.test(url) || url[0] !== '/') return false;
  return !(url.length > 1 && (url[1] === '/' || url[1] === '\\'));
}

/**
 * True when url (absolute or relative) resolves to origin.
 * @param {string} url
 * @param {string} origin
 */
export function isSameOrigin(url, origin) {
  try {
    return new URL(url, origin).origin === origin;
  } catch {
    return false;
  }
}

/**
 * The path, query and hash of a same-origin URL, or null for other origins.
 * @param {string} url
 * @param {string} origin
 */
export function toLocalPath(url, origin) {
  try {
    const parsed = new URL(url, origin);
    return parsed.origin === origin ? parsed.pathname + parsed.search + parsed.hash : null;
  } catch {
    return null;
  }
}
