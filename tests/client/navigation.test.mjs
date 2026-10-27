import { test } from 'node:test';
import assert from 'node:assert/strict';
import { sameLayout } from '../../src/CL.WebLogic/Client/src/navigation.js';

// The smallest element stand-in sameLayout needs.
const shell = (layout) => ({ getAttribute: (name) => (name === 'data-wl-layout' ? layout ?? null : null) });

test('sameLayout compares the data-wl-layout attributes when the theme sets them', () => {
  assert.equal(sameLayout(shell('site'), shell('site'), { layout: '/a.html' }, { layout: '/b.html' }), true);
  assert.equal(sameLayout(shell('site'), shell('auth'), { layout: '/a.html' }, { layout: '/a.html' }), false);
});

test('sameLayout falls back to the layout the server wrote into wl-config', () => {
  assert.equal(sameLayout(shell(), shell(), { layout: '/layouts/site.html' }, { layout: '/layouts/site.html' }), true);
  assert.equal(sameLayout(shell(), shell(), { layout: '/layouts/site.html' }, { layout: '/layouts/auth.html' }), false);
  assert.equal(sameLayout(shell('site'), shell(), { layout: '/layouts/site.html' }, { layout: '/layouts/site.html' }), false);
});

test('sameLayout treats two pages without any layout information as the same (as before)', () => {
  assert.equal(sameLayout(shell(), shell(), {}, {}), true);
  assert.equal(sameLayout(shell(), shell(), { layout: null }, {}), true);
});
