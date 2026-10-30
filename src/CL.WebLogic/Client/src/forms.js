/**
 * Ajax forms: <form data-wl-form>. Client-side validation from the embedded schema
 * (<script type="application/json" data-wl-form-schema>), a double-submit guard, upload
 * progress for file fields, and handling of the server's form result
 * ({ ok, errors, summary, message, redirect, commands, data, step }). Conditional fields,
 * repeaters, autocomplete and remote validation come from formextras.js.
 */
import { readJson, setBusy } from './dom.js';
import { HttpError, normalizeErrors, parseProblem, readBody } from './http.js';
import { createFormExtras, isHidden } from './formextras.js';
import { isLocalUrl, isSameOrigin } from './url.js';
import { defaultMessages, validateAll, validateField } from './validation.js';

/**
 * Collects form values: name → string, or string[] when a name repeats; files as File objects.
 * @param {HTMLFormElement} form
 */
export function readFormValues(form) {
  const values = {};
  for (const [name, value] of new FormData(form)) {
    values[name] = name in values ? [].concat(values[name], value) : value;
  }
  return values;
}

/**
 * @param {{ http: any, ui: any, commands: any, bus: any, navigate: (url: string) => any, config: any }} services
 */
export function createForms({ http, ui, commands, bus, navigate, config }) {
  const bound = new WeakSet();

  const extras = createFormExtras({ http, ui, config });
  const schemaOf = (form) => readJson(form, 'script[type="application/json"][data-wl-form-schema]') || { fields: [] };
  const messagesOf = (schema) => ({ ...defaultMessages, ...(schema.messages || {}) });

  /** Enhances every form[data-wl-form] in root. */
  function bind(root) {
    const forms = root.matches && root.matches('form[data-wl-form]') ? [root] : [];
    forms.push(...root.querySelectorAll('form[data-wl-form]'));
    for (const form of forms) attach(form);
  }

  function attach(form) {
    if (bound.has(form)) return;
    bound.add(form);
    if (form.getAttribute('data-wl-validate') !== 'native') form.noValidate = true;
    extras.attach(form);

    form.addEventListener('submit', (event) => {
      event.preventDefault();
      submit(form, event.submitter || null);
    });
    if (form.getAttribute('data-wl-live') === 'off') return;
    form.addEventListener('focusout', (event) => {
      const name = event.target && event.target.name;
      if (validateNamed(form, name, false) === null) remoteCheck(form, name);
    });
    form.addEventListener('input', (event) => validateNamed(form, event.target && event.target.name, true));
  }

  /** Re-checks one field; onlyIfInvalid limits it to clearing errors while typing. */
  function validateNamed(form, name, onlyIfInvalid) {
    if (!name || name === config.csrfField || isHidden(form, name)) return undefined;
    const schema = schemaOf(form);
    const field = (schema.fields || []).find((f) => f.name === name);
    if (!field) return undefined;
    const element = form.elements.namedItem(name);
    const invalid = element && (element.getAttribute ? element.getAttribute('aria-invalid') === 'true' : false);
    if (onlyIfInvalid && !invalid) return undefined;
    const values = readFormValues(form);
    const error = validateField(field, values[name], values, messagesOf(schema));
    ui.fieldError(form, name, error);
    return error;
  }

  /** Server-side check of fields marked remote (after the local rules passed). */
  function remoteCheck(form, name) {
    const field = (schemaOf(form).fields || []).find((f) => f.name === name);
    if (field && field.remote && form.elements.namedItem(name)?.value) extras.remoteValidate(form, name);
  }

  /**
   * Validates on the client; shows and returns the errors.
   * @param {HTMLFormElement} form
   */
  function validate(form) {
    const schema = schemaOf(form);
    const visible = { ...schema, fields: (schema.fields || []).map((f) => (isHidden(form, f.name) ? { ...f, visible: false } : f)) };
    const errors = validateAll(visible, readFormValues(form), messagesOf(schema));
    ui.clearErrors(form);
    if (Object.keys(errors).length > 0) ui.showErrors(form, errors);
    return errors;
  }

  /**
   * Submits a form (validation, CSRF, progress, result handling). Resolves with the result or null.
   * @param {HTMLFormElement} form
   * @param {HTMLElement | null} [submitter]
   */
  async function submit(form, submitter = null) {
    if (form.dataset.wlBusy === '1') return null; // double-submit guard
    const skipValidation = submitter && submitter.hasAttribute('formnovalidate');
    if (!skipValidation && Object.keys(validate(form)).length > 0) {
      bus.emit('form:invalid', { form });
      return null;
    }
    const proceed = form.dispatchEvent(new CustomEvent('wl:form-submit', { bubbles: true, cancelable: true }));
    if (!proceed) return null;

    let data;
    try {
      data = new FormData(form, submitter || undefined);
    } catch {
      data = new FormData(form);
    }
    const url = (submitter && submitter.getAttribute('formaction')) || form.getAttribute('action') || globalThis.location.href;
    const method = ((submitter && submitter.getAttribute('formmethod')) || form.getAttribute('method') || 'post').toUpperCase();

    form.dataset.wlBusy = '1';
    setBusy(form, true);
    ui.progress.start();
    try {
      const result = hasFiles(form)
        ? await upload(form, url, method, data)
        : (await http.request(url, { method, form: data, headers: { Accept: 'application/json' }, key: 'form:' + url })).data;
      handleResult(form, result);
      return result;
    } catch (error) {
      handleError(form, error);
      return null;
    } finally {
      delete form.dataset.wlBusy;
      setBusy(form, false);
      ui.progress.done();
    }
  }

  function handleResult(form, result) {
    const r = result && typeof result === 'object' ? result : {};
    ui.clearErrors(form);
    if (r.ok === false) {
      ui.showErrors(form, normalizeErrors(r.errors), typeof r.summary === 'string' ? r.summary : r.message);
      bus.emit('form:error', { form, result: r });
      return;
    }
    if (typeof r.message === 'string' && r.message) ui.toast(r.message, 'success');
    if (Array.isArray(r.commands)) commands.run(r.commands);
    if (form.hasAttribute('data-wl-reset')) form.reset();
    bus.emit('form:success', { form, result: r });
    form.dispatchEvent(new CustomEvent('wl:form-success', { bubbles: true, detail: r }));
    if (isLocalUrl(r.redirect)) navigate(r.redirect);
  }

  function handleError(form, error) {
    if (error instanceof HttpError) {
      const errors = error.problem.errors;
      ui.showErrors(form, errors, Object.keys(errors).length > 0 ? error.problem.title : error.problem.detail || error.problem.title);
    } else if (error && (error.name === 'AbortError' || error.name === 'TimeoutError')) {
      ui.toast('The request was cancelled or timed out.', 'warning');
    } else {
      ui.toast('Something went wrong. Please try again.', 'danger');
    }
    bus.emit('form:error', { form, error });
  }

  /** XMLHttpRequest upload with progress (fetch has no upload progress). */
  async function upload(form, url, method, data) {
    // The token only ever goes to this site, like every other request (http.js).
    const sameOrigin = isSameOrigin(url, globalThis.location.origin);
    const token = sameOrigin ? await http.csrfToken() : null;
    const progress = form.querySelector('progress[data-wl-form-progress]');
    return new Promise((resolve, reject) => {
      const xhr = new XMLHttpRequest();
      xhr.open(method, url);
      xhr.setRequestHeader('X-Requested-With', 'WebLogic');
      xhr.setRequestHeader('Accept', 'application/json');
      if (token) xhr.setRequestHeader(config.csrfHeader, token);
      xhr.upload.addEventListener('progress', (event) => {
        if (!event.lengthComputable) return;
        if (progress) {
          progress.max = event.total;
          progress.value = event.loaded;
        }
        bus.emit('form:progress', { form, loaded: event.loaded, total: event.total });
      });
      xhr.addEventListener('load', async () => {
        const response = new Response(xhr.responseText, { status: xhr.status, headers: { 'Content-Type': xhr.getResponseHeader('Content-Type') || '' } });
        const body = await readBody(response);
        if (xhr.status >= 200 && xhr.status < 300) resolve(body);
        else reject(new HttpError(xhr.status, parseProblem(xhr.status, body), null, body));
      });
      xhr.addEventListener('error', () => reject(new HttpError(0, parseProblem(0, null), null)));
      xhr.addEventListener('abort', () => reject(new DOMException('Upload aborted', 'AbortError')));
      xhr.send(data);
    });
  }

  return { bind, submit, validate, readValues: readFormValues, refresh: extras.refresh, addItem: extras.addItem };
}

/** @param {HTMLFormElement} form */
function hasFiles(form) {
  return [...form.querySelectorAll('input[type="file"]')].some((input) => input.files && input.files.length > 0);
}
