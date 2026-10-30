import { test } from 'node:test';
import assert from 'node:assert/strict';
import { Bus, parseConfig, defaultConfig, takeFlash } from '../../src/CL.WebLogic/Client/src/core.js';
import { isLocalUrl, isSameOrigin, toLocalPath } from '../../src/CL.WebLogic/Client/src/url.js';

test('parseConfig merges defaults and survives garbage', () => {
  assert.equal(parseConfig('{"basePath":"/app"}').basePath, '/app');
  assert.equal(parseConfig('{"basePath":"/app"}').csrfHeader, defaultConfig.csrfHeader);
  assert.deepEqual(parseConfig('not json'), { ...defaultConfig });
  assert.deepEqual(parseConfig('[1,2]'), { ...defaultConfig });
  assert.deepEqual(parseConfig(''), { ...defaultConfig });
});

test('takeFlash returns the message once and only as known variants', () => {
  const config = parseConfig('{"flash":{"message":"Saved.","variant":"success"}}');
  assert.deepEqual(takeFlash(config), { message: 'Saved.', variant: 'success' });
  assert.equal(takeFlash(config), null);
  assert.deepEqual(takeFlash({ flash: { message: 'x', variant: '<b>' } }), { message: 'x', variant: 'info' });
  for (const bad of [null, undefined, {}, { flash: 'x' }, { flash: { message: '' } }, { flash: { message: 42 } }]) assert.equal(takeFlash(bad), null);
});

test('Bus on/once/off', () => {
  const bus = new Bus();
  const seen = [];
  const off = bus.on('x', (d) => seen.push('on:' + d));
  bus.once('x', (d) => seen.push('once:' + d));
  bus.emit('x', 1);
  bus.emit('x', 2);
  off();
  bus.emit('x', 3);
  assert.deepEqual(seen, ['on:1', 'once:1', 'on:2']);
});

test('isLocalUrl matches the server rule', () => {
  for (const ok of ['/', '/a?b=c#d', '/path/to']) assert.equal(isLocalUrl(ok), true, ok);
  for (const bad of ['', 'https://evil.test', '//evil.test', '/\\evil.test', 'javascript:alert(1)', 'relative', '/ok\nx', null, 42]) {
    assert.equal(isLocalUrl(bad), false, String(bad));
  }
});

test('same-origin helpers', () => {
  assert.equal(isSameOrigin('/x', 'https://site.test'), true);
  assert.equal(isSameOrigin('https://evil.test/x', 'https://site.test'), false);
  assert.equal(toLocalPath('https://site.test/a?b#c', 'https://site.test'), '/a?b#c');
  assert.equal(toLocalPath('https://evil.test/a', 'https://site.test'), null);
});
