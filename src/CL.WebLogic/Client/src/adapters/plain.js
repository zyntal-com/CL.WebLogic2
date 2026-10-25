/** The default UI adapter: small, framework-free markup styled by weblogic.css. */
import { el } from '../dom.js';

const VARIANTS = new Set(['success', 'info', 'warning', 'danger', 'error', 'primary', 'secondary']);
const variantOf = (value) => (VARIANTS.has(value) ? value : 'info');
let progressDepth = 0;

function region(className, attributes) {
  let node = document.querySelector('.' + className);
  if (!node) {
    node = el('div', { class: className, ...attributes });
    document.body.append(node);
  }
  return node;
}

/** A stable id for the error element of a field. */
export function errorId(form, name) {
  if (!form.dataset.wlFormKey) form.dataset.wlFormKey = form.id || 'f' + Math.random().toString(36).slice(2, 10);
  return `wl-err-${form.dataset.wlFormKey}-${String(name).replace(/[^\w-]/g, '_')}`;
}

/** Fields of a form with this name. */
export function fieldsNamed(form, name) {
  return [...form.elements].filter((element) => element.name === name);
}

function describe(element, id, add) {
  const ids = new Set((element.getAttribute('aria-describedby') || '').split(/\s+/).filter(Boolean));
  if (add) ids.add(id);
  else ids.delete(id);
  if (ids.size > 0) element.setAttribute('aria-describedby', [...ids].join(' '));
  else element.removeAttribute('aria-describedby');
}

export const plainAdapter = {
  name: 'plain',
  invalidClass: 'wl-invalid',
  errorClass: 'wl-field-error',
  summaryClass: 'wl-form-summary',

  toast(message, variant, options = {}) {
    const close = el('button', { type: 'button', class: 'wl-toast__close', 'aria-label': 'Close' }, '×');
    const toast = el('div', { class: ['wl-toast', 'wl-toast--' + variantOf(variant)] }, el('span', { class: 'wl-toast__text' }, message), close);
    close.addEventListener('click', () => toast.remove());
    region('wl-toasts', { role: 'status', 'aria-live': 'polite' }).append(toast);
    const duration = options.duration ?? 5000;
    if (duration > 0) setTimeout(() => toast.remove(), duration);
    return toast;
  },

  overlay(title, message, variant, duration) {
    const overlay = el('div', { class: ['wl-overlay', 'wl-overlay--' + variantOf(variant)], role: 'alert' },
      el('div', { class: 'wl-overlay__box' }, el('strong', { class: 'wl-overlay__title' }, title), el('p', {}, message)));
    overlay.addEventListener('click', () => overlay.remove());
    document.body.append(overlay);
    if (duration > 0) setTimeout(() => overlay.remove(), duration);
    return overlay;
  },

  fieldError(form, name, message) {
    const id = errorId(form, name);
    document.getElementById(id)?.remove();
    const fields = fieldsNamed(form, name);
    for (const field of fields) {
      field.classList.toggle(this.invalidClass, Boolean(message));
      if (message) field.setAttribute('aria-invalid', 'true');
      else field.removeAttribute('aria-invalid');
      describe(field, id, Boolean(message));
    }
    if (!message || fields.length === 0) return fields.length > 0;
    const anchor = fields[fields.length - 1];
    const holder = anchor.closest('[data-wl-field]') || anchor.parentElement || form;
    holder.append(el('div', { class: this.errorClass, id, 'data-wl-error': name }, message));
    return true;
  },

  showErrors(form, errors, summary) {
    const unplaced = [];
    let first = null;
    for (const [name, messages] of Object.entries(errors)) {
      const text = [].concat(messages).join(' ');
      if (this.fieldError(form, name, text)) first ??= fieldsNamed(form, name)[0];
      else unplaced.push(text);
    }
    const summaryText = [summary, ...unplaced].filter(Boolean).join(' ');
    if (summaryText) {
      const box = el('div', { class: this.summaryClass, role: 'alert', 'data-wl-error': '' }, summaryText);
      const slot = form.querySelector('[data-wl-form-summary]');
      if (slot) slot.replaceChildren(box);
      else form.prepend(box);
    }
    if (first && typeof first.focus === 'function') first.focus();
  },

  clearErrors(form) {
    for (const node of form.querySelectorAll('[data-wl-error]')) node.remove();
    for (const field of form.querySelectorAll('[aria-invalid="true"]')) {
      field.removeAttribute('aria-invalid');
      field.classList.remove(this.invalidClass);
      describe(field, errorId(form, field.name), false);
    }
  },

  progressStart() {
    progressDepth++;
    const bar = region('wl-progress', { 'aria-hidden': 'true' });
    bar.classList.remove('wl-progress--done', 'wl-progress--failed');
    bar.classList.add('wl-progress--active');
  },

  progressDone(failed) {
    progressDepth = Math.max(0, progressDepth - 1);
    if (progressDepth > 0) return;
    const bar = document.querySelector('.wl-progress');
    if (!bar) return;
    bar.classList.remove('wl-progress--active');
    bar.classList.add(failed ? 'wl-progress--failed' : 'wl-progress--done');
  },

  announce(message) {
    const live = region('wl-announcer', { class: 'wl-announcer wl-sr-only', 'aria-live': 'polite', 'aria-atomic': 'true' });
    live.textContent = '';
    setTimeout(() => {
      live.textContent = message;
    }, 50);
  }
};
