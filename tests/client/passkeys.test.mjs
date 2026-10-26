import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createPasskeys, creationOptions, credentialToJson, requestOptions, toBase64Url, toBuffer } from '../../src/CL.WebLogic/Client/src/passkeys.js';

const bytes = (...values) => new Uint8Array(values).buffer;

test('base64url round-trips without padding and with URL-safe characters', () => {
  const data = bytes(0xfb, 0xff, 0x00, 0x3e, 0x3f);
  const encoded = toBase64Url(data);
  assert.equal(encoded, '-_8APj8');
  assert.deepEqual(new Uint8Array(toBuffer(encoded)), new Uint8Array(data));
  assert.equal(toBase64Url(new Uint8Array([1, 2, 3]).subarray(1)), 'AgM');
  assert.throws(() => toBuffer(42));
});

test('options JSON becomes binary where WebAuthn wants buffers', () => {
  const create = creationOptions({
    challenge: 'AQID',
    rp: { id: 'site.test', name: 'Site' },
    user: { id: 'BAU', name: 'ada', displayName: 'Ada' },
    pubKeyCredParams: [{ type: 'public-key', alg: -7 }],
    excludeCredentials: [{ type: 'public-key', id: 'Bgc', transports: ['internal'] }]
  });
  assert.deepEqual(new Uint8Array(create.challenge), new Uint8Array([1, 2, 3]));
  assert.deepEqual(new Uint8Array(create.user.id), new Uint8Array([4, 5]));
  assert.equal(create.user.name, 'ada');
  assert.deepEqual(new Uint8Array(create.excludeCredentials[0].id), new Uint8Array([6, 7]));
  assert.deepEqual(create.excludeCredentials[0].transports, ['internal']);

  const get = requestOptions({ challenge: 'AQID', rpId: 'site.test', userVerification: 'required' });
  assert.deepEqual(get.allowCredentials, []);
  assert.equal(get.userVerification, 'required');
});

test('credentials serialize to the JSON the server expects (manual fallback)', () => {
  const registration = credentialToJson({
    id: 'AQI',
    rawId: bytes(1, 2),
    type: 'public-key',
    authenticatorAttachment: 'platform',
    getClientExtensionResults: () => ({ credProps: { rk: true } }),
    response: { clientDataJSON: bytes(3), attestationObject: bytes(4), getTransports: () => ['internal'] }
  });
  assert.deepEqual(registration, {
    id: 'AQI', rawId: 'AQI', type: 'public-key', authenticatorAttachment: 'platform',
    clientExtensionResults: { credProps: { rk: true } },
    response: { clientDataJSON: 'Aw', attestationObject: 'BA', transports: ['internal'] }
  });

  const assertion = credentialToJson({
    id: 'AQI', rawId: bytes(1, 2), type: 'public-key',
    response: { clientDataJSON: bytes(3), authenticatorData: bytes(5), signature: bytes(6), userHandle: bytes(7) }
  });
  assert.deepEqual(assertion.response, { clientDataJSON: 'Aw', authenticatorData: 'BQ', signature: 'Bg', userHandle: 'Bw' });

  // toJSON() is preferred when the browser has it.
  assert.deepEqual(credentialToJson({ toJSON: () => ({ native: true }) }), { native: true });
});

function harness({ getResult, createResult } = {}) {
  const posts = [];
  const events = [];
  const calls = [];
  const http = {
    post: async (url, json) => {
      posts.push({ url, json });
      return url.endsWith('/begin') ? { data: { options: { challenge: 'AQID', rpId: 'site.test', user: { id: 'AQ' } }, state: 'S1' } } : { data: { redirect: '/home' } };
    }
  };
  const credentials = {
    get: (request) => {
      calls.push(request);
      if (getResult === 'hang') {
        // Like a pending autofill request: settles only when aborted.
        return new Promise((_, reject) => request.signal.addEventListener('abort', () => reject(Object.assign(new Error('aborted'), { name: 'AbortError' }))));
      }
      return Promise.resolve(getResult || { toJSON: () => ({ id: 'cred' }) });
    },
    create: async (request) => {
      calls.push(request);
      return createResult || { toJSON: () => ({ id: 'new' }) };
    }
  };
  function FakePublicKeyCredential() {}
  FakePublicKeyCredential.isConditionalMediationAvailable = async () => true;
  const passkeys = createPasskeys({
    http,
    ui: { toast: (message, variant) => events.push(['toast', message, variant]) },
    bus: { emit: (type, detail) => events.push([type, detail]) },
    commands: { run: () => {} },
    navigate: (url) => events.push(['navigate', url]),
    credentials,
    publicKeyCredential: FakePublicKeyCredential
  });
  return { passkeys, posts, events, calls };
}

test('signIn runs begin, the browser ceremony and finish with the state', async () => {
  const { passkeys, posts, calls, events } = harness();
  const data = await passkeys.signIn('/pk/begin', '/pk/finish', { remember: true });
  assert.deepEqual(data, { redirect: '/home' });
  assert.deepEqual(posts.map((p) => p.url), ['/pk/begin', '/pk/finish']);
  assert.deepEqual(posts[1].json, { remember: true, credential: { id: 'cred' }, state: 'S1' });
  assert.deepEqual(new Uint8Array(calls[0].publicKey.challenge), new Uint8Array([1, 2, 3]));
  assert.equal(calls[0].mediation, undefined);
  assert.equal(events.find((e) => e[0] === 'passkey:done')[1].kind, 'get');
});

test('register uses navigator.credentials.create', async () => {
  const { passkeys, posts, calls } = harness();
  await passkeys.register('/pk/r/begin', '/pk/r/finish', { name: 'Laptop' });
  assert.ok(calls[0].publicKey.user.id instanceof ArrayBuffer);
  assert.deepEqual(posts[1].json, { name: 'Laptop', credential: { id: 'new' }, state: 'S1' });
});

test('autofill uses conditional mediation and is aborted by a modal ceremony', async () => {
  const { passkeys, calls } = harness({ getResult: 'hang' });
  const pending = passkeys.startAutofill('/pk/begin', '/pk/finish');
  await new Promise((resolve) => setTimeout(resolve, 10));
  assert.equal(calls[0].mediation, 'conditional');
  passkeys.stopAutofill();
  await assert.rejects(pending, { name: 'AbortError' });
});
