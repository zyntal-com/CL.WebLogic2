import { createForms } from '../forms.js';
import { Bus } from '../core.js';
import { createUi } from '../ui.js';
import { assert, test } from './testkit.js';

function setup(fixture, html, responder = () => ({ ok: true })) {
  fixture.innerHTML = html;
  const requests = [];
  const http = {
    csrfToken: async () => 'token',
    request: async (url, options) => {
      requests.push({ url, options });
      return { data: responder(url, options) };
    }
  };
  const config = { basePath: '', csrfField: '__wl_csrf', csrfHeader: 'X-CSRF-Token' };
  const forms = createForms({ http, ui: createUi(), commands: { run: () => {} }, bus: new Bus(), navigate: () => {}, config });
  const form = fixture.querySelector('form');
  forms.bind(fixture);
  return { forms, form, requests };
}

const schema = (fields) => `<script type="application/json" data-wl-form-schema>${JSON.stringify({ formId: 'f', fields })}</script>`;
const showWhen = (field, values) => `data-wl-show-when="${JSON.stringify({ field, values }).replace(/"/g, '&quot;')}"`;
const change = (element) => element.dispatchEvent(new Event('change', { bubbles: true }));
const tick = () => new Promise((resolve) => setTimeout(resolve, 0));

test('conditional fields toggle, disable their inputs and skip validation', async (fixture) => {
  const { forms, form, requests } = setup(fixture, `<form id="wl-form-f" data-wl-form="f" action="/save">
    ${schema([{ name: 'Topic', label: 'Topic' }, { name: 'Invoice', label: 'Invoice', required: true }])}
    <select name="Topic"><option value="General">General</option><option value="Billing">Billing</option></select>
    <div data-wl-field="Invoice" ${showWhen('Topic', ['Billing'])}><input name="Invoice"></div></form>`);
  const wrapper = form.querySelector('[data-wl-field="Invoice"]');
  assert.equal(wrapper.hidden, true);
  assert.equal(form.elements.namedItem('Invoice').disabled, true);

  await forms.submit(form);
  assert.equal(requests.length, 1); // the hidden required field did not block the submit

  form.elements.namedItem('Topic').value = 'Billing';
  change(form.elements.namedItem('Topic'));
  assert.equal(wrapper.hidden, false);
  assert.equal(form.elements.namedItem('Invoice').disabled, false);
  assert.equal(await forms.submit(form), null);
  assert.equal(form.querySelector('.wl-field-error').textContent, 'Invoice is required.');
});

test('checkbox conditions use true/false', (fixture) => {
  const { form } = setup(fixture, `<form data-wl-form="f">
    <input type="checkbox" name="Ship" value="true">
    <div data-wl-field="Address" ${showWhen('Ship', ['true'])}><input name="Address"></div></form>`);
  const wrapper = form.querySelector('[data-wl-field="Address"]');
  assert.equal(wrapper.hidden, true);
  form.elements.namedItem('Ship').checked = true;
  change(form.elements.namedItem('Ship'));
  assert.equal(wrapper.hidden, false);
});

test('repeaters add from the template and respect min and max', (fixture) => {
  const { forms, form } = setup(fixture, `<form data-wl-form="f">
    <fieldset data-wl-field="Lines" data-wl-repeater="Lines" data-wl-repeater-min="1" data-wl-repeater-max="2">
      <div data-wl-repeater-items><div data-wl-repeater-item><input id="f-Lines_0__Product" name="Lines[0].Product"><button type="button" data-wl-repeater-remove>Remove</button></div></div>
      <template data-wl-repeater-template><div data-wl-repeater-item><label for="f-Lines___index____Product">P</label><input id="f-Lines___index____Product" name="Lines[__index__].Product"><button type="button" data-wl-repeater-remove>Remove</button></div></template>
      <button type="button" data-wl-repeater-add>Add</button></fieldset></form>`);
  const add = form.querySelector('[data-wl-repeater-add]');
  const names = () => [...form.querySelectorAll('[data-wl-repeater-items] input')].map((input) => input.name);
  assert.equal(form.querySelector('[data-wl-repeater-remove]').disabled, true); // at the minimum

  add.click();
  assert.deepEqual(names(), ['Lines[0].Product', 'Lines[1].Product']);
  assert.equal(form.querySelector('label[for="f-Lines_1__Product"]') !== null, true);
  assert.equal(add.disabled, true); // at the maximum
  assert.equal(forms.addItem(form.querySelector('[data-wl-repeater]')), null);

  form.querySelectorAll('[data-wl-repeater-remove]')[0].click();
  assert.deepEqual(names(), ['Lines[1].Product']);
  add.click();
  assert.equal(names()[1], 'Lines[2].Product'); // indexes are never reused
});

test('autocomplete renders options as text and fills the value', async (fixture) => {
  const { form, requests } = setup(fixture, `<form data-wl-form="f"><div data-wl-field="City">
    <input id="f-City" name="City" data-wl-autocomplete="cities" role="combobox" aria-expanded="false"></div></form>`,
    () => ({ options: [{ value: 'cph', label: '<img src=x>Copenhagen' }, { value: 'aar', label: 'Aarhus' }] }));
  const input = form.elements.namedItem('City');
  input.value = 'co';
  input.dispatchEvent(new Event('input', { bubbles: true }));
  await new Promise((resolve) => setTimeout(resolve, 300));
  await tick();

  assert.equal(requests[0].url, '/_weblogic/forms/options/cities?q=co&form=f&field=City');
  const list = form.querySelector('#f-City-listbox');
  assert.equal(list.hidden, false);
  assert.equal(list.querySelector('img'), null);
  assert.equal(list.children[0].textContent, '<img src=x>Copenhagen');
  assert.equal(input.getAttribute('aria-expanded'), 'true');

  input.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown', bubbles: true }));
  input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
  assert.equal(input.value, 'cph');
  assert.equal(list.hidden, true);
});

test('remote fields are checked on the server after local rules pass', async (fixture) => {
  const { form, requests } = setup(fixture, `<form id="wl-form-f" data-wl-form="f">
    ${schema([{ name: 'User', label: 'User', required: true, remote: true }])}<div data-wl-field="User"><input name="User"></div></form>`,
    () => ({ valid: false, errors: ['That name is taken.'] }));
  const input = form.elements.namedItem('User');
  input.dispatchEvent(new FocusEvent('focusout', { bubbles: true }));
  assert.equal(requests.length, 0); // the local rule failed first

  input.value = 'taken';
  input.dispatchEvent(new FocusEvent('focusout', { bubbles: true }));
  await tick();
  assert.equal(requests[0].url, '/_weblogic/forms/f/validate/User');
  assert.equal(requests[0].options.method, 'POST');
  assert.equal(form.querySelector('.wl-field-error').textContent, 'That name is taken.');
});

test('formnovalidate submitters skip client validation (wizard back)', async (fixture) => {
  const { forms, form, requests } = setup(fixture, `<form data-wl-form="f" action="/join">
    ${schema([{ name: 'Name', label: 'Name', required: true }])}<input name="Name">
    <button type="submit" name="__wl_wizard_back" value="1" formnovalidate>Back</button></form>`);
  await forms.submit(form, form.querySelector('button'));
  assert.equal(requests.length, 1);
});

test('schema messages override the defaults', async (fixture) => {
  const body = JSON.stringify({ fields: [{ name: 'Navn', label: 'Navn', required: true }], messages: { required: '{label} skal udfyldes.' } });
  const { forms, form } = setup(fixture, `<form data-wl-form="f"><script type="application/json" data-wl-form-schema>${body}</script><input name="Navn"></form>`);
  await forms.submit(form);
  assert.equal(form.querySelector('.wl-field-error').textContent, 'Navn skal udfyldes.');
});
