import { createForms } from '../forms.js';
import { Bus } from '../core.js';
import { createUi } from '../ui.js';
import { assert, test } from './testkit.js';

function setup(fixture, html, responder) {
  fixture.innerHTML = html;
  const requests = [];
  const navigated = [];
  const http = {
    csrfToken: async () => 'token',
    request: async (url, options) => {
      requests.push({ url, options });
      return { data: responder(url, options) };
    }
  };
  const forms = createForms({ http, ui: createUi(), commands: { run: () => {} }, bus: new Bus(), navigate: (u) => navigated.push(u), config: { csrfField: '__wl_csrf', csrfHeader: 'X-CSRF-Token' } });
  const form = fixture.querySelector('form');
  forms.bind(fixture);
  return { forms, form, requests, navigated };
}

const schema = (fields) => `<script type="application/json" data-wl-form-schema>${JSON.stringify({ fields })}</script>`;

test('client validation blocks submit and shows errors', async (fixture) => {
  const { forms, form, requests } = setup(fixture,
    `<form data-wl-form action="/save" method="post">${schema([{ name: 'email', label: 'E-mail', type: 'email', required: true }])}<input name="email"></form>`,
    () => ({ ok: true }));
  assert.equal(await forms.submit(form), null);
  assert.equal(requests.length, 0);
  assert.equal(form.querySelector('.wl-field-error').textContent, 'E-mail is required.');
});

test('server errors are shown; success redirects locally only', async (fixture) => {
  let reply = { ok: false, errors: { name: ['Taken'] }, summary: 'Please fix' };
  const { forms, form, navigated } = setup(fixture, '<form data-wl-form action="/save"><input name="name" value="x"></form>', () => reply);
  await forms.submit(form);
  assert.equal(form.querySelector('.wl-field-error').textContent, 'Taken');
  reply = { ok: true, redirect: 'https://evil.test/' };
  await forms.submit(form);
  assert.equal(navigated.length, 0);
  reply = { ok: true, redirect: '/done' };
  await forms.submit(form);
  assert.deepEqual(navigated, ['/done']);
  assert.equal(form.querySelectorAll('[data-wl-error]').length, 0);
});

test('commands only run when this site answered the form', async (fixture) => {
  // Security review: a cross-origin form response could run commands (and swap HTML in).
  fixture.innerHTML = '<form data-wl-form action="/save"><input name="name" value="x"></form>';
  const ran = [];
  let answeredBy = globalThis.location.origin + '/save';
  const http = {
    csrfToken: async () => 'token',
    request: async () => ({ data: { ok: true, commands: [{ type: 'toast', message: 'hi' }] }, response: { url: answeredBy } })
  };
  const forms = createForms({ http, ui: createUi(), commands: { run: (c) => ran.push(...c) }, bus: new Bus(), navigate: () => {}, config: { csrfField: '__wl_csrf', csrfHeader: 'X-CSRF-Token' } });
  const form = fixture.querySelector('form');
  forms.bind(fixture);
  await forms.submit(form);
  assert.equal(ran.length, 1);
  answeredBy = 'https://evil.test/save';
  await forms.submit(form);
  assert.equal(ran.length, 1);
});

test('double submit is ignored while busy', async (fixture) => {
  let release;
  const { forms, form, requests } = setup(fixture, '<form data-wl-form action="/slow"><button>Go</button></form>', () => ({ ok: true }));
  const slow = new Promise((resolve) => (release = resolve));
  const original = forms.submit;
  const first = original(form);
  const second = original(form);
  release();
  await slow;
  await first;
  assert.equal(await second, null);
  assert.equal(requests.length, 1);
});
