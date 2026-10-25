import { test } from 'node:test';
import assert from 'node:assert/strict';
import { normalizeCommand, COMMANDS } from '../../src/CL.WebLogic/Client/src/commands.js';
import { backoffDelay, parseEnvelope, toWebSocketUrl } from '../../src/CL.WebLogic/Client/src/realtime.js';

test('commands are allowlisted and validated', () => {
  assert.deepEqual(COMMANDS, ['toast', 'overlay', 'redirect', 'navigate', 'reload', 'replace', 'remove', 'addClass', 'removeClass']);
  assert.equal(normalizeCommand({ type: 'eval', code: 'x' }), null);
  assert.equal(normalizeCommand({ type: 'redirect', url: 'https://evil.test' }), null);
  assert.equal(normalizeCommand({ type: 'navigate', url: '//evil.test' }), null);
  assert.deepEqual(normalizeCommand({ type: 'redirect', url: '/ok' }), { type: 'redirect', url: '/ok' });
  assert.equal(normalizeCommand({ type: 'addClass', selector: 'p', className: 'a" onclick="x' }), null);
  assert.deepEqual(normalizeCommand({ type: 'toast', message: 'hi', variant: 'weird' }), { type: 'toast', message: 'hi', variant: 'info' });
  assert.equal(normalizeCommand({ type: 'overlay', title: 't', message: 'm', duration: 1e9 }).duration, 60000);
  assert.equal(normalizeCommand(null), null);
  assert.equal(normalizeCommand('toast'), null);
});

test('backoff grows exponentially, is capped and jittered', () => {
  const mid = () => 0.5;
  assert.equal(backoffDelay(0, { random: mid }), 1000);
  assert.equal(backoffDelay(3, { random: mid }), 8000);
  assert.equal(backoffDelay(20, { random: mid }), 30000);
  assert.equal(backoffDelay(0, { random: () => 0 }), 750);
  assert.equal(backoffDelay(0, { random: () => 1 }), 1250);
});

test('envelopes are validated', () => {
  assert.deepEqual(parseEnvelope('{"id":7,"type":"post.created","channel":"posts","data":{"id":1}}'),
    { id: '7', type: 'post.created', channel: 'posts', data: { id: 1 }, timestamp: null });
  assert.equal(parseEnvelope('{"type":"<script>"}'), null);
  assert.equal(parseEnvelope('{"data":1}'), null);
  assert.equal(parseEnvelope('nope'), null);
  assert.equal(toWebSocketUrl('/_weblogic/realtime/ws', 'https://site.test/page'), 'wss://site.test/_weblogic/realtime/ws');
  assert.equal(toWebSocketUrl('/ws', 'http://localhost:5000/'), 'ws://localhost:5000/ws');
});
