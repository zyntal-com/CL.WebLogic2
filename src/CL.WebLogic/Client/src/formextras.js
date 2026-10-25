/**
 * Form behaviours driven by server-rendered markup: conditional fields (data-wl-show-when),
 * repeaters (data-wl-repeater + <template>), autocomplete (data-wl-autocomplete) and remote
 * field validation (schema `remote: true`). Option text is always set with textContent.
 */
import { el } from './dom.js';

const INDEX = '__index__';
const INDEXED_ATTRIBUTES = ['name', 'id', 'for', 'data-wl-field', 'data-wl-error', 'aria-describedby', 'aria-controls'];
const MAX_OPTIONS = 50;

/** The scope prefix of a (possibly repeated) field key: 'Items[2].Name' → 'Items[2].'. */
export function scopeOf(key) {
  const at = String(key).lastIndexOf('].');
  return at < 0 ? '' : key.slice(0, at + 2);
}

/**
 * The value a show-when condition compares: checkboxes give 'true'/'false', radios the checked
 * value, multi-selects and checkbox lists an array.
 * @param {HTMLFormElement} form
 * @param {string} name
 */
export function controlValue(form, name) {
  const fields = [...form.elements].filter((element) => element.name === name && !element.disabled);
  if (fields.length === 0) return '';
  const first = fields[0];
  if (first.type === 'checkbox' && fields.length === 1 && first.value === 'true') return first.checked ? 'true' : 'false';
  if (first.type === 'checkbox' || first.type === 'radio') {
    const checked = fields.filter((field) => field.checked).map((field) => field.value);
    return first.type === 'radio' ? (checked[0] ?? '') : checked;
  }
  if (first.multiple) return [...first.selectedOptions].map((option) => option.value);
  return first.value;
}

/** True when the condition { field, values } holds for value. */
export function matches(condition, value) {
  const wanted = (condition.values || []).map((v) => String(v).toLowerCase());
  return [].concat(value).some((v) => wanted.includes(String(v).toLowerCase()));
}

/** True when the field wrapper of name is hidden (hidden fields are neither validated nor posted). */
export function isHidden(form, name) {
  const wrapper = [...form.querySelectorAll('[data-wl-field]')].find((node) => node.getAttribute('data-wl-field') === name);
  return Boolean(wrapper && wrapper.closest('[hidden]'));
}

/**
 * @param {{ http: any, ui: any, config: any }} services
 */
export function createFormExtras({ http, ui, config }) {
  /** Applies every show-when condition of the form. */
  function refresh(form) {
    // Repeat until stable so chains (A shows B shows C) settle in one call.
    for (let pass = 0; pass < 5; pass++) {
      let changed = false;
      for (const wrapper of form.querySelectorAll('[data-wl-show-when]')) {
        if (wrapper.closest('template')) continue;
        let condition;
        try {
          condition = JSON.parse(wrapper.getAttribute('data-wl-show-when'));
        } catch {
          continue;
        }
        const key = wrapper.getAttribute('data-wl-field') || '';
        const show = matches(condition, controlValue(form, scopeOf(key) + condition.field));
        if (wrapper.hidden === show) {
          wrapper.hidden = !show;
          setDisabled(wrapper, !show);
          changed = true;
        }
      }
      if (!changed) break;
    }
  }

  function setDisabled(wrapper, disabled) {
    for (const input of wrapper.querySelectorAll('input, select, textarea, button')) {
      if (disabled && !input.disabled) {
        input.disabled = true;
        input.setAttribute('data-wl-hidden-disabled', '');
      } else if (!disabled && input.hasAttribute('data-wl-hidden-disabled')) {
        input.disabled = false;
        input.removeAttribute('data-wl-hidden-disabled');
      }
    }
  }

  /** Adds an item to a repeater (from its template); returns the item or null at the maximum. */
  function addItem(repeater) {
    const items = repeater.querySelector('[data-wl-repeater-items]');
    const template = repeater.querySelector('template[data-wl-repeater-template]');
    const max = Number(repeater.getAttribute('data-wl-repeater-max') || 50);
    if (!items || !template || items.querySelectorAll(':scope > [data-wl-repeater-item]').length >= max) return null;

    const index = nextIndex(repeater, items);
    const fragment = template.content.cloneNode(true);
    for (const node of fragment.querySelectorAll('*')) {
      for (const attribute of INDEXED_ATTRIBUTES) {
        const value = node.getAttribute(attribute);
        if (value && value.includes(INDEX)) node.setAttribute(attribute, value.split(INDEX).join(String(index)));
      }
    }
    const item = fragment.querySelector('[data-wl-repeater-item]');
    items.append(fragment);
    updateButtons(repeater);
    return item;
  }

  function nextIndex(repeater, items) {
    const key = repeater.getAttribute('data-wl-repeater');
    let next = Number(repeater.dataset.wlRepeaterNext || 0);
    for (const input of items.querySelectorAll('[name]')) {
      const match = input.name.startsWith(key + '[') ? /^\[(\d+)\]/.exec(input.name.slice(key.length)) : null;
      if (match) next = Math.max(next, Number(match[1]) + 1);
    }
    repeater.dataset.wlRepeaterNext = String(next + 1);
    return next;
  }

  function removeItem(repeater, item) {
    const min = Number(repeater.getAttribute('data-wl-repeater-min') || 0);
    const items = repeater.querySelectorAll('[data-wl-repeater-items] > [data-wl-repeater-item]');
    if (items.length <= min) return false;
    item.remove();
    updateButtons(repeater);
    return true;
  }

  function updateButtons(repeater) {
    const min = Number(repeater.getAttribute('data-wl-repeater-min') || 0);
    const max = Number(repeater.getAttribute('data-wl-repeater-max') || 50);
    const count = repeater.querySelectorAll('[data-wl-repeater-items] > [data-wl-repeater-item]').length;
    const add = repeater.querySelector(':scope > [data-wl-repeater-add]');
    if (add) add.disabled = count >= max;
    for (const remove of repeater.querySelectorAll('[data-wl-repeater-items] [data-wl-repeater-remove]')) remove.disabled = count <= min;
  }

  /** Server-side validation of one field; shows the result. */
  async function remoteValidate(form, name) {
    const formId = form.getAttribute('data-wl-form');
    if (!formId) return null;
    const data = new FormData(form);
    for (const [key, value] of [...data]) {
      if (typeof value !== 'string') data.delete(key);
    }
    const url = `${config.basePath}/_weblogic/forms/${encodeURIComponent(formId)}/validate/${encodeURIComponent(name)}`;
    try {
      const { data: result } = await http.request(url, { method: 'POST', form: data, key: `remote:${formId}:${name}` });
      const message = result && Array.isArray(result.errors) && result.errors.length > 0 ? String(result.errors[0]) : null;
      ui.fieldError(form, name, message);
      return message;
    } catch {
      return null; // the submit validates again
    }
  }

  function attachAutocomplete(form, input) {
    if (input.dataset.wlAutocompleteBound) return;
    input.dataset.wlAutocompleteBound = '1';
    const provider = input.getAttribute('data-wl-autocomplete');
    const listId = (input.id || input.name.replace(/[^\w-]/g, '_')) + '-listbox';
    const list = el('ul', { id: listId, role: 'listbox', class: 'wl-autocomplete', hidden: true });
    input.after(list);
    input.setAttribute('aria-controls', listId);
    let timer = null;
    let active = -1;

    const options = () => [...list.querySelectorAll('[role="option"]')];
    const close = () => {
      list.hidden = true;
      active = -1;
      input.setAttribute('aria-expanded', 'false');
      input.removeAttribute('aria-activedescendant');
    };
    const choose = (option) => {
      input.value = option.getAttribute('data-value');
      close();
      input.dispatchEvent(new Event('change', { bubbles: true }));
    };
    const highlight = (index) => {
      const all = options();
      if (all.length === 0) return;
      active = (index + all.length) % all.length;
      all.forEach((option, i) => option.setAttribute('aria-selected', i === active ? 'true' : 'false'));
      input.setAttribute('aria-activedescendant', all[active].id);
    };

    async function search() {
      const term = input.value.trim();
      if (term.length < Number(input.getAttribute('data-wl-autocomplete-min') || 1)) return close();
      const query = new URLSearchParams({ q: term, form: form.getAttribute('data-wl-form') || '', field: input.name });
      try {
        const { data } = await http.request(`${config.basePath}/_weblogic/forms/options/${encodeURIComponent(provider)}?${query}`, { key: 'ac:' + listId });
        const items = (data && Array.isArray(data.options) ? data.options : []).slice(0, MAX_OPTIONS);
        list.replaceChildren(...items.map((item, i) =>
          el('li', { id: `${listId}-${i}`, role: 'option', 'aria-selected': 'false', 'data-value': String(item.value ?? ''), 'aria-disabled': item.disabled ? 'true' : null },
            String(item.label ?? item.value ?? ''))));
        list.hidden = items.length === 0;
        input.setAttribute('aria-expanded', items.length > 0 ? 'true' : 'false');
        active = -1;
      } catch {
        close();
      }
    }

    input.addEventListener('input', () => {
      clearTimeout(timer);
      timer = setTimeout(search, 250);
    });
    input.addEventListener('keydown', (event) => {
      if (list.hidden) return;
      if (event.key === 'ArrowDown') highlight(active + 1);
      else if (event.key === 'ArrowUp') highlight(active - 1);
      else if (event.key === 'Enter' && active >= 0) choose(options()[active]);
      else if (event.key === 'Escape') close();
      else return;
      event.preventDefault();
    });
    input.addEventListener('blur', () => setTimeout(close, 150));
    list.addEventListener('mousedown', (event) => {
      const option = event.target.closest('[role="option"]');
      if (option && option.getAttribute('aria-disabled') !== 'true') {
        event.preventDefault();
        choose(option);
      }
    });
  }

  /** Wires the behaviours of one form. */
  function attach(form) {
    refresh(form);
    for (const repeater of form.querySelectorAll('[data-wl-repeater]')) updateButtons(repeater);
    for (const input of form.querySelectorAll('[data-wl-autocomplete]')) attachAutocomplete(form, input);
    form.addEventListener('change', () => refresh(form));
    form.addEventListener('click', (event) => {
      const add = event.target.closest('[data-wl-repeater-add]');
      const remove = event.target.closest('[data-wl-repeater-remove]');
      if (add) {
        const item = addItem(add.closest('[data-wl-repeater]'));
        if (item) {
          refresh(form);
          for (const input of item.querySelectorAll('[data-wl-autocomplete]')) attachAutocomplete(form, input);
          item.querySelector('input, select, textarea')?.focus();
        }
      } else if (remove) {
        removeItem(remove.closest('[data-wl-repeater]'), remove.closest('[data-wl-repeater-item]'));
      }
    });
  }

  return { attach, refresh, addItem, removeItem, remoteValidate };
}
