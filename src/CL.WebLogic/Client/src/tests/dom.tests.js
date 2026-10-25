import { el, parseHtml, swap, setBusy, isSafeUrlValue } from '../dom.js';
import { assert, test } from './testkit.js';

test('el creates text, never markup', () => {
  const node = el('p', { class: ['a', 'b'] }, '<img src=x onerror=alert(1)>');
  assert.equal(node.children.length, 0);
  assert.equal(node.textContent, '<img src=x onerror=alert(1)>');
  assert.equal(node.className, 'a b');
});

test('el refuses event handler attributes', () => {
  assert.throws(() => el('a', { onclick: 'x()' }));
});

test('el drops javascript: and data: URLs', () => {
  assert.equal(el('a', { href: ' JaVa\tScript:alert(1)' }).hasAttribute('href'), false);
  assert.equal(el('img', { src: 'data:text/html,x' }).hasAttribute('src'), false);
  assert.equal(el('a', { href: '/ok' }).getAttribute('href'), '/ok');
  assert.ok(isSafeUrlValue('https://example.test'));
});

test('parseHtml removes executable scripts but keeps JSON data', () => {
  const fragment = parseHtml('<div>x</div><script>window.__pwned=1</script><script type="module">x</script><script type="application/json" id="d">{"a":1}</script>');
  assert.equal(fragment.querySelectorAll('script').length, 1);
  assert.equal(fragment.querySelector('script').type, 'application/json');
});

test('swap inserts and returns the new elements', (fixture) => {
  fixture.innerHTML = '<div id="t">old</div>';
  const inserted = swap(fixture.querySelector('#t'), '<span>a</span><span>b</span>', 'inner');
  assert.equal(inserted.length, 2);
  assert.equal(fixture.querySelector('#t').textContent, 'ab');
  swap(fixture.querySelector('#t'), '<p id="n">new</p>', 'outer');
  assert.ok(fixture.querySelector('#n') && !fixture.querySelector('#t'));
});

test('setBusy disables submit buttons and restores them', (fixture) => {
  fixture.innerHTML = '<form><button>Go</button><button type="button">Other</button><input type="submit" disabled></form>';
  const form = fixture.querySelector('form');
  setBusy(form, true);
  assert.ok(form.querySelector('button').disabled);
  assert.equal(form.querySelector('button[type=button]').disabled, false);
  setBusy(form, false);
  assert.equal(form.querySelector('button').disabled, false);
  assert.ok(form.querySelector('input').disabled, 'a button disabled before stays disabled');
});
