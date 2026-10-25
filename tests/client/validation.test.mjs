import { test } from 'node:test';
import assert from 'node:assert/strict';
import { validateField, validateAll, format, isEmpty } from '../../src/CL.WebLogic/Client/src/validation.js';

test('required and empties', () => {
  const field = { name: 'name', label: 'Name', required: true };
  for (const empty of ['', '   ', null, undefined, [], false]) assert.equal(validateField(field, empty), 'Name is required.');
  assert.equal(validateField(field, 'x'), null);
  assert.equal(validateField({ name: 'opt' }, ''), null);
  assert.equal(isEmpty(0), false);
});

test('length counts code points', () => {
  const field = { name: 'n', label: 'N', minLength: 2, maxLength: 3 };
  assert.equal(validateField(field, '😀'), 'N must be at least 2 characters.');
  assert.equal(validateField(field, '😀😀😀'), null);
  assert.equal(validateField(field, 'abcd'), 'N must be at most 3 characters.');
});

test('email, url, number, ranges and dates', () => {
  assert.equal(validateField({ name: 'e', type: 'email' }, 'a@b.co'), null);
  assert.notEqual(validateField({ name: 'e', type: 'email' }, 'a@b'), null);
  assert.notEqual(validateField({ name: 'e', type: 'email' }, 'a b@c.de'), null);
  assert.equal(validateField({ name: 'u', type: 'url' }, 'https://x.test/'), null);
  assert.notEqual(validateField({ name: 'u', type: 'url' }, 'javascript:alert(1)'), null);
  const age = { name: 'age', label: 'Age', type: 'number', min: 18, max: 120 };
  assert.equal(validateField(age, '17'), 'Age must be at least 18.');
  assert.equal(validateField(age, 'abc'), 'Age must be a number.');
  assert.equal(validateField(age, '40'), null);
  assert.notEqual(validateField({ name: 'd', type: 'date', min: '2026-01-01' }, '2025-12-31'), null);
});

test('pattern is anchored and bad patterns are ignored', () => {
  const field = { name: 'zip', label: 'Zip', pattern: '\\d{4}' };
  assert.equal(validateField(field, '1234'), null);
  assert.notEqual(validateField(field, '12345'), null);
  assert.equal(validateField({ name: 'x', pattern: '(' }, 'anything'), null);
});

test('equalTo, custom messages and validateAll', () => {
  const schema = {
    fields: [
      { name: 'password', label: 'Password', required: true },
      { name: 'confirm', label: 'Confirm', equalTo: 'password', equalToLabel: 'Password', messages: { equalTo: 'Passwords differ.' } },
      { name: 'hidden', required: true, visible: false }
    ]
  };
  assert.deepEqual(validateAll(schema, { password: 'a', confirm: 'b' }), { confirm: ['Passwords differ.'] });
  assert.deepEqual(validateAll(schema, { password: 'a', confirm: 'a' }), {});
  assert.equal(format('{a} and {b}', { a: 1 }), '1 and {b}');
});
