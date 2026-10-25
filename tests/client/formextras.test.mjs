import { test } from 'node:test';
import assert from 'node:assert/strict';
import { matches, scopeOf } from '../../src/CL.WebLogic/Client/src/formextras.js';

test('scope of repeated field keys', () => {
  assert.equal(scopeOf('Name'), '');
  assert.equal(scopeOf('Lines[2].Product'), 'Lines[2].');
  assert.equal(scopeOf('Orders[1].Lines[0].Qty'), 'Orders[1].Lines[0].');
});

test('show-when conditions compare case-insensitively and accept lists', () => {
  const condition = { field: 'Topic', values: ['Billing', 'true'] };
  assert.equal(matches(condition, 'billing'), true);
  assert.equal(matches(condition, 'General'), false);
  assert.equal(matches(condition, ['General', 'Billing']), true);
  assert.equal(matches(condition, []), false);
  assert.equal(matches({ field: 'x' }, 'anything'), false);
});
