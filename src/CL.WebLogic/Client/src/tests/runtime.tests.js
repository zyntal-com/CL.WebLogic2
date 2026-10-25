import { createCommands } from '../commands.js';
import { swap } from '../dom.js';
import { createModules } from '../modules.js';
import { createUi } from '../ui.js';
import { shouldIntercept } from '../navigation.js';
import { assert, test, tick } from './testkit.js';

test('modules mount once and unmount with cleanup', (fixture) => {
  const modules = createModules({});
  let mounts = 0;
  let unmounts = 0;
  modules.define('counter', (element) => {
    mounts++;
    element.dataset.mounted = '1';
    return () => unmounts++;
  });
  fixture.innerHTML = '<div data-wl-module="counter unknown"></div>';
  modules.mount(fixture);
  modules.mount(fixture);
  assert.equal(mounts, 1);
  modules.unmount(fixture);
  assert.equal(unmounts, 1);
  assert.equal(modules.mountedCount, 0);
});

test('commands: classes, remove, replace; external redirects ignored', (fixture) => {
  fixture.innerHTML = '<p id="a"></p><p id="b"></p><div id="c">old</div>';
  const assigned = [];
  const enhanced = [];
  const commands = createCommands({
    ui: createUi(),
    navigate: () => {},
    swap,
    enhance: (root) => enhanced.push(root),
    unmount: () => {},
    location: { assign: (url) => assigned.push(url), reload: () => {} }
  });
  commands.run([
    { type: 'addClass', selector: '#a', className: 'x y' },
    { type: 'addClass', selector: '#a', className: 'bad"class' },
    { type: 'remove', selector: '#b' },
    { type: 'replace', selector: '#c', html: '<div id="c2">new</div>' },
    { type: 'redirect', url: 'https://evil.test/' },
    { type: 'redirect', url: '//evil.test/' },
    { type: 'eval', code: 'alert(1)' },
    { type: 'redirect', url: '/next' }
  ]);
  assert.ok(fixture.querySelector('#a').classList.contains('y'));
  assert.equal(fixture.querySelector('#b'), null);
  assert.equal(fixture.querySelector('#c2').textContent, 'new');
  assert.equal(enhanced.length, 1);
  assert.deepEqual(assigned, ['/next']);
});

test('toasts render text only', async () => {
  const ui = createUi();
  const toast = ui.toast('<b>hi</b>', 'success', { duration: 0 });
  assert.equal(toast.querySelector('b'), null);
  assert.ok(toast.textContent.includes('<b>hi</b>'));
  toast.remove();
  await tick();
});

test('field errors are linked with aria-describedby and cleared', (fixture) => {
  fixture.innerHTML = '<form><div data-wl-field><input name="email"></div></form>';
  const form = fixture.querySelector('form');
  const ui = createUi();
  ui.showErrors(form, { email: ['Bad <address>'], other: ['Unplaced'] }, 'Fix the errors');
  const input = form.querySelector('input');
  assert.equal(input.getAttribute('aria-invalid'), 'true');
  const error = document.getElementById(input.getAttribute('aria-describedby'));
  assert.equal(error.textContent, 'Bad <address>');
  assert.ok(form.querySelector('.wl-form-summary').textContent.includes('Unplaced'));
  ui.clearErrors(form);
  assert.equal(form.querySelectorAll('[data-wl-error]').length, 0);
  assert.equal(input.hasAttribute('aria-describedby'), false);
});

test('navigation intercepts only plain same-origin clicks', (fixture) => {
  fixture.innerHTML = '<a id="in" href="/page">in</a><a id="out" href="https://evil.test/">out</a><a id="blank" href="/x" target="_blank">b</a>'
    + '<a id="dl" href="/f" download>d</a><div data-wl-nav="off"><a id="off" href="/y">o</a></div><a id="hash" href="#top">h</a>';
  const click = (extra = {}) => ({ defaultPrevented: false, button: 0, metaKey: false, ctrlKey: false, shiftKey: false, altKey: false, ...extra });
  const loc = { origin: location.origin, pathname: '/somewhere-else', search: '' };
  assert.ok(shouldIntercept(fixture.querySelector('#in'), click(), loc));
  assert.equal(shouldIntercept(fixture.querySelector('#in'), click({ ctrlKey: true }), loc), false);
  for (const id of ['out', 'blank', 'dl', 'off', 'hash']) assert.equal(shouldIntercept(fixture.querySelector('#' + id), click(), loc), false, id);
});

test('the global runtime is started', () => {
  assert.ok(globalThis.WebLogic, 'WebLogic is loaded');
  assert.equal(typeof globalThis.WebLogic.navigate, 'function');
});
