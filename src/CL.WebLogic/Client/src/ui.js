/**
 * The UI layer: toasts, overlays, field errors, progress and announcements, delegated to a
 * pluggable adapter (plain by default; weblogic.bootstrap5.js installs a Bootstrap 5 adapter).
 * Adapters may implement any subset; missing methods fall back to the plain adapter.
 */
import { plainAdapter } from './adapters/plain.js';

export function createUi() {
  let adapter = plainAdapter;

  const call = (name, args) => {
    const fn = typeof adapter[name] === 'function' ? adapter[name] : plainAdapter[name];
    return fn.apply(adapter, args);
  };

  return {
    /** Replaces the adapter. */
    setAdapter(next) {
      if (!next || typeof next !== 'object') throw new TypeError('WebLogic: an adapter must be an object.');
      adapter = next;
    },
    get adapter() {
      return adapter;
    },
    /** Shows a toast (text only). */
    toast: (message, variant = 'info', options = {}) => call('toast', [String(message), variant, options]),
    /** Shows a short overlay message. */
    overlay: (title, message, variant = 'success', duration = 2000) => call('overlay', [String(title), String(message), variant, duration]),
    /** Shows field errors ({ field: [messages] }) and an optional summary. */
    showErrors: (form, errors, summary) => call('showErrors', [form, errors || {}, summary || '']),
    /** Sets or clears (message null) the error of one field. */
    fieldError: (form, name, message) => call('fieldError', [form, name, message]),
    /** Removes all errors from a form. */
    clearErrors: (form) => call('clearErrors', [form]),
    progress: {
      start: () => call('progressStart', []),
      done: (failed = false) => call('progressDone', [failed])
    },
    /** Announces text to screen readers. */
    announce: (message) => call('announce', [String(message)])
  };
}
