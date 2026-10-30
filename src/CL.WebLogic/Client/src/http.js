/** fetch wrapper: CSRF tokens for same-origin unsafe requests, timeouts, keyed cancellation, ProblemDetails. */
import { isSameOrigin } from './url.js';

const SAFE_METHODS = new Set(['GET', 'HEAD', 'OPTIONS']);

/**
 * A non-2xx response; `problem` is the parsed ProblemDetails (or a stand-in), `data` the parsed body as it came
 * (JSON or text), for APIs whose errors have another shape.
 */
export class HttpError extends Error {
  /**
   * @param {number} status
   * @param {{ title: string, detail: string, type: string, status: number, errors: Record<string, string[]> }} problem
   * @param {Response | null} response
   * @param {unknown} [data]
   */
  constructor(status, problem, response, data = null) {
    super(problem.detail || problem.title || `HTTP ${status}`);
    this.name = 'HttpError';
    this.status = status;
    this.problem = problem;
    this.response = response;
    this.data = data;
  }
}

/**
 * Normalizes validation errors to { field: [messages] }.
 * @param {unknown} errors
 * @returns {Record<string, string[]>}
 */
export function normalizeErrors(errors) {
  const result = {};
  if (!errors || typeof errors !== 'object' || Array.isArray(errors)) return result;
  for (const [field, value] of Object.entries(errors)) {
    const messages = (Array.isArray(value) ? value : [value]).filter((m) => typeof m === 'string' && m.length > 0);
    if (messages.length > 0) result[field] = messages;
  }
  return result;
}

/**
 * Builds a problem object from a response body (ProblemDetails JSON or anything else).
 * @param {number} status
 * @param {unknown} data
 */
export function parseProblem(status, data) {
  const body = data && typeof data === 'object' ? /** @type {Record<string, unknown>} */ (data) : {};
  const text = (value) => (typeof value === 'string' ? value : '');
  return {
    status,
    type: text(body.type),
    title: text(body.title) || (status === 0 ? 'Network error' : `Request failed (${status})`),
    // `message` is the common non-ProblemDetails field ({ error, message }).
    detail: text(body.detail) || text(body.message),
    errors: normalizeErrors(body.errors)
  };
}

/**
 * Creates the HTTP client.
 * @param {{ config: { basePath: string, csrfHeader: string, csrfUrl: string }, fetchImpl?: typeof fetch, origin?: string }} options
 */
export function createHttp({ config, fetchImpl, origin }) {
  const doFetch = fetchImpl || ((...args) => globalThis.fetch(...args));
  const siteOrigin = origin || (globalThis.location ? globalThis.location.origin : 'http://localhost');
  const inflight = new Map();
  let token = null;
  let pending = null;

  /** The CSRF token (fetched once from csrfUrl, refreshed on demand). */
  function csrfToken(refresh = false) {
    if (token && !refresh) return Promise.resolve(token);
    if (pending && !refresh) return pending;
    pending = doFetch(config.basePath + config.csrfUrl, {
      credentials: 'same-origin',
      headers: { 'X-Requested-With': 'WebLogic', Accept: 'application/json' }
    })
      .then(async (response) => {
        const data = await readBody(response);
        if (!response.ok || !data || typeof data.token !== 'string') throw new HttpError(response.status, parseProblem(response.status, data), response, data);
        token = data.token;
        return token;
      })
      .finally(() => {
        pending = null;
      });
    return pending;
  }

  /**
   * Sends a request. Resolves with { response, data, status, headers } for 2xx; rejects with HttpError otherwise.
   * @param {string} url
   * @param {{ method?: string, headers?: HeadersInit, body?: BodyInit, json?: unknown, form?: FormData | URLSearchParams,
   *           key?: string, timeout?: number, signal?: AbortSignal, retryCsrf?: boolean }} [options]
   */
  async function request(url, options = {}) {
    const method = (options.method || 'GET').toUpperCase();
    const sameOrigin = isSameOrigin(url, siteOrigin);
    const headers = new Headers(options.headers || {});
    if (sameOrigin) headers.set('X-Requested-With', 'WebLogic');
    if (!headers.has('Accept')) headers.set('Accept', 'application/json, text/html;q=0.9, */*;q=0.1');

    let body = options.body;
    if (options.json !== undefined) {
      body = JSON.stringify(options.json);
      headers.set('Content-Type', 'application/json');
    } else if (options.form) {
      body = options.form;
    }

    if (options.key) inflight.get(options.key)?.abort();
    const controller = new AbortController();
    if (options.key) inflight.set(options.key, controller);
    const signals = [controller.signal];
    if (options.signal) signals.push(options.signal);
    if (options.timeout !== 0) signals.push(AbortSignal.timeout(options.timeout ?? 30000));
    const signal = signals.length === 1 ? signals[0] : AbortSignal.any(signals);

    // The token is only ever sent to this site.
    const needsToken = !SAFE_METHODS.has(method) && sameOrigin;
    const send = async (refresh) => {
      if (needsToken) headers.set(config.csrfHeader, await csrfToken(refresh));
      return doFetch(url, { method, headers, body, signal, credentials: 'same-origin' });
    };

    try {
      let response = await send(false);
      if (response.status === 403 && needsToken && options.retryCsrf !== false && !(body instanceof ReadableStream)) {
        response = await send(true); // the token may have rotated (sign-in, sign-out)
      }
      const data = await readBody(response);
      if (!response.ok) throw new HttpError(response.status, parseProblem(response.status, data), response, data);
      return { response, data, status: response.status, headers: response.headers };
    } finally {
      if (options.key && inflight.get(options.key) === controller) inflight.delete(options.key);
    }
  }

  return {
    request,
    csrfToken,
    /** Replaces the cached token (e.g. from a freshly rendered page). */
    setToken(value) {
      token = typeof value === 'string' && value.length > 0 ? value : null;
    },
    get: (url, options) => request(url, { ...options, method: 'GET' }),
    post: (url, json, options) => request(url, { ...options, method: 'POST', json }),
    /** Cancels the in-flight request with this key. */
    abort(key) {
      inflight.get(key)?.abort();
    }
  };
}

/** Reads JSON (by content type) or text. */
export async function readBody(response) {
  if (response.status === 204 || response.status === 304) return null;
  const type = response.headers.get('Content-Type') || '';
  if (type.includes('json')) {
    try {
      return await response.json();
    } catch {
      return null;
    }
  }
  return response.text();
}
