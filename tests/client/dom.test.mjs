import { test } from 'node:test';
import assert from 'node:assert/strict';
import { isExecutableScript } from '../../src/CL.WebLogic/Client/src/dom.js';

const script = (type) => ({ getAttribute: (name) => (name === 'type' ? type : null) });

test('only known data script types are inert; every other type counts as code', () => {
  // Security review: a deny-list missed text/jscript, application/x-javascript, importmap, ...
  for (const type of [null, '', 'text/javascript', 'module', 'text/jscript', 'application/x-javascript', 'application/ecmascript', 'importmap', 'TEXT/JAVASCRIPT ']) {
    assert.equal(isExecutableScript(script(type)), true, String(type));
  }
  for (const type of ['application/json', 'application/ld+json', ' Application/JSON ', 'application/json; charset=utf-8', 'text/template']) {
    assert.equal(isExecutableScript(script(type)), false, type);
  }
});
