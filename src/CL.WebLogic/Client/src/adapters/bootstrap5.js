/**
 * Bootstrap 5 UI adapter (load weblogic.bootstrap5.js after weblogic.js). Uses Bootstrap's
 * markup and classes; Bootstrap's JavaScript is not required.
 */
import { el } from '../dom.js';
import { errorId, fieldsNamed, plainAdapter } from './plain.js';

const VARIANTS = { success: 'success', info: 'info', warning: 'warning', danger: 'danger', error: 'danger', primary: 'primary', secondary: 'secondary' };
const variantOf = (value) => VARIANTS[value] || 'info';

function toastContainer() {
  let node = document.querySelector('.wl-bs-toasts');
  if (!node) {
    node = el('div', { class: 'toast-container position-fixed bottom-0 end-0 p-3 wl-bs-toasts', role: 'status', 'aria-live': 'polite' });
    document.body.append(node);
  }
  return node;
}

export const bootstrap5Adapter = {
  ...plainAdapter,
  name: 'bootstrap5',
  invalidClass: 'is-invalid',
  errorClass: 'invalid-feedback d-block',
  summaryClass: 'alert alert-danger',

  toast(message, variant, options = {}) {
    const close = el('button', { type: 'button', class: 'btn-close btn-close-white me-2 m-auto', 'aria-label': 'Close' });
    const toast = el('div', { class: `toast show align-items-center text-bg-${variantOf(variant)} border-0`, role: 'alert', 'aria-atomic': 'true' },
      el('div', { class: 'd-flex' }, el('div', { class: 'toast-body' }, message), close));
    close.addEventListener('click', () => toast.remove());
    toastContainer().append(toast);
    const duration = options.duration ?? 5000;
    if (duration > 0) setTimeout(() => toast.remove(), duration);
    return toast;
  },

  overlay(title, message, variant, duration) {
    const overlay = el('div', { class: 'position-fixed top-0 start-0 w-100 h-100 d-flex align-items-center justify-content-center wl-bs-overlay', role: 'alert' },
      el('div', { class: `alert alert-${variantOf(variant)} shadow mb-0` }, el('h5', { class: 'alert-heading' }, title), el('p', { class: 'mb-0' }, message)));
    overlay.addEventListener('click', () => overlay.remove());
    document.body.append(overlay);
    if (duration > 0) setTimeout(() => overlay.remove(), duration);
    return overlay;
  },

  fieldError(form, name, message) {
    const placed = plainAdapter.fieldError.call(this, form, name, message);
    // Bootstrap expects the feedback right after the control (or its input group).
    const feedback = document.getElementById(errorId(form, name));
    const fields = fieldsNamed(form, name);
    if (feedback && fields.length === 1) (fields[0].closest('.input-group') || fields[0]).after(feedback);
    return placed;
  }
};

/** Installs the adapter now, or as soon as WebLogic loads. */
function install(webLogic) {
  webLogic.ui.setAdapter(bootstrap5Adapter);
}

if (globalThis.WebLogic) install(globalThis.WebLogic);
else (globalThis.WebLogicPending = globalThis.WebLogicPending || []).push(install);
