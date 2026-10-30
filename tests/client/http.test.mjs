import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createHttp, HttpError, normalizeErrors, parseProblem } from '../../src/CL.WebLogic/Client/src/http.js';

const config = { basePath: '', csrfHeader: 'X-CSRF-Token', csrfUrl: '/_weblogic/csrf' };
const json = (body, status = 200) => new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });

function fakeFetch(handler) {
  const calls = [];
  const fn = async (url, init = {}) => {
    calls.push({ url: String(url), init });
    return handler(String(url), init, calls.length);
  };
  fn.calls = calls;
  return fn;
}

test('unsafe same-origin requests carry the CSRF token (fetched once)', async () => {
  const fetchImpl = fakeFetch((url) => (url.endsWith('/_weblogic/csrf') ? json({ token: 'T1' }) : json({ ok: true })));
  const http = createHttp({ config, fetchImpl, origin: 'https://site.test' });
  await http.post('/a', { x: 1 });
  await http.post('/b', { x: 2 });
  const posts = fetchImpl.calls.filter((c) => !c.url.endsWith('/csrf'));
  assert.equal(fetchImpl.calls.filter((c) => c.url.endsWith('/csrf')).length, 1);
  assert.equal(posts[0].init.headers.get('X-CSRF-Token'), 'T1');
  assert.equal(posts[0].init.headers.get('X-Requested-With'), 'WebLogic');
  assert.equal(posts[0].init.headers.get('Content-Type'), 'application/json');
});

test('the token is never sent to another origin', async () => {
  const fetchImpl = fakeFetch(() => json({ ok: true }));
  const http = createHttp({ config, fetchImpl, origin: 'https://site.test' });
  await http.post('https://api.other.test/x', {});
  assert.equal(fetchImpl.calls.length, 1);
  assert.equal(fetchImpl.calls[0].init.headers.has('X-CSRF-Token'), false);
  assert.equal(fetchImpl.calls[0].init.headers.has('X-Requested-With'), false);
});

test('a 403 refreshes the token and retries once', async () => {
  let tokens = 0;
  const fetchImpl = fakeFetch((url, init) => {
    if (url.endsWith('/csrf')) return json({ token: 'T' + ++tokens });
    return init.headers.get('X-CSRF-Token') === 'T2' ? json({ ok: true }) : json({ title: 'Forbidden' }, 403);
  });
  const http = createHttp({ config, fetchImpl, origin: 'https://site.test' });
  const result = await http.post('/a', {});
  assert.equal(result.status, 200);
  assert.equal(tokens, 2);
});

test('errors become HttpError with parsed ProblemDetails', async () => {
  const fetchImpl = fakeFetch(() => json({ title: 'Invalid', detail: 'Fix it', errors: { name: 'Required', tags: ['A', 'B'] } }, 400));
  const http = createHttp({ config, fetchImpl, origin: 'https://site.test' });
  await assert.rejects(http.get('/x'), (error) => {
    assert.ok(error instanceof HttpError);
    assert.equal(error.status, 400);
    assert.equal(error.problem.detail, 'Fix it');
    assert.deepEqual(error.problem.errors, { name: ['Required'], tags: ['A', 'B'] });
    return true;
  });
});

test('keyed requests cancel the previous one', async () => {
  const fetchImpl = fakeFetch((url, init) => new Promise((resolve, reject) => {
    init.signal.addEventListener('abort', () => reject(init.signal.reason));
    if (url.endsWith('/second')) resolve(json({ ok: 2 }));
  }));
  const http = createHttp({ config, fetchImpl, origin: 'https://site.test' });
  const first = http.get('/first', { key: 'k' });
  const second = http.get('/second', { key: 'k' });
  await assert.rejects(first);
  assert.deepEqual((await second).data, { ok: 2 });
});

test('errors keep the body; a plain message becomes the detail', async () => {
  const body = { success: false, error: 'abuse_block', message: 'Come back in 24 hours.' };
  const fetchImpl = fakeFetch(() => json(body, 429));
  const http = createHttp({ config, fetchImpl, origin: 'https://site.test' });
  const error = await http.get('/api/thing').then(() => null, (e) => e);
  assert.ok(error instanceof HttpError);
  assert.equal(error.status, 429);
  assert.deepEqual(error.data, body);
  assert.equal(error.problem.detail, 'Come back in 24 hours.');
  assert.equal(error.message, 'Come back in 24 hours.');
});

test('problem helpers', () => {
  assert.equal(parseProblem(500, 'oops').title, 'Request failed (500)');
  assert.equal(parseProblem(400, { detail: 'd', message: 'm' }).detail, 'd');
  assert.deepEqual(normalizeErrors({ a: '', b: [1, 'x'] }), { b: ['x'] });
  assert.deepEqual(normalizeErrors(['x']), {});
});
