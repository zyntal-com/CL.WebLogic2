/**
 * Passkeys (WebAuthn). The server sends options as WebAuthn JSON ({ options, state }); this module
 * turns them into navigator.credentials calls and posts { credential, state, ...extra } back.
 * Declarative: <button data-wl-passkey="signin|register|verify" data-wl-passkey-begin="/url"
 * data-wl-passkey-finish="/url">, plus data-wl-passkey="autofill" for passkey autofill on a
 * username field (autocomplete="username webauthn").
 */
import { isLocalUrl } from './url.js';

/** base64url (no padding) → ArrayBuffer. */
export function toBuffer(value) {
  if (typeof value !== 'string') throw new TypeError('Expected a base64url string.');
  const base64 = value.replace(/-/g, '+').replace(/_/g, '/');
  const binary = atob(base64 + '='.repeat((4 - (base64.length % 4)) % 4));
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
  return bytes.buffer;
}

/** ArrayBuffer or view → base64url (no padding). */
export function toBase64Url(buffer) {
  const bytes = buffer instanceof ArrayBuffer ? new Uint8Array(buffer) : new Uint8Array(buffer.buffer, buffer.byteOffset, buffer.byteLength);
  let binary = '';
  for (let i = 0; i < bytes.length; i++) binary += String.fromCharCode(bytes[i]);
  return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

const descriptors = (list) => (Array.isArray(list) ? list.map((d) => ({ ...d, id: toBuffer(d.id) })) : undefined);

/** PublicKeyCredentialCreationOptionsJSON → options for navigator.credentials.create. */
export function creationOptions(json) {
  return {
    ...json,
    challenge: toBuffer(json.challenge),
    user: { ...json.user, id: toBuffer(json.user.id) },
    excludeCredentials: descriptors(json.excludeCredentials) || []
  };
}

/** PublicKeyCredentialRequestOptionsJSON → options for navigator.credentials.get. */
export function requestOptions(json) {
  return { ...json, challenge: toBuffer(json.challenge), allowCredentials: descriptors(json.allowCredentials) || [] };
}

/** A PublicKeyCredential → its JSON form (what PublicKeyCredential.toJSON() returns). */
export function credentialToJson(credential) {
  if (typeof credential.toJSON === 'function') {
    try {
      return credential.toJSON();
    } catch {
      // Some browsers throw for unknown extension results; fall back to manual encoding.
    }
  }
  const r = credential.response;
  const response = { clientDataJSON: toBase64Url(r.clientDataJSON) };
  if (r.attestationObject) {
    response.attestationObject = toBase64Url(r.attestationObject);
    response.transports = typeof r.getTransports === 'function' ? r.getTransports() : [];
  } else {
    response.authenticatorData = toBase64Url(r.authenticatorData);
    response.signature = toBase64Url(r.signature);
    if (r.userHandle) response.userHandle = toBase64Url(r.userHandle);
  }
  return {
    id: credential.id,
    rawId: toBase64Url(credential.rawId),
    type: credential.type,
    authenticatorAttachment: credential.authenticatorAttachment || null,
    clientExtensionResults: typeof credential.getClientExtensionResults === 'function' ? credential.getClientExtensionResults() : {},
    response
  };
}

/** The passkeys API: create/get, the begin → browser → finish round trip, autofill and data attributes. */
export function createPasskeys({ http, ui, bus, commands, navigate, credentials, publicKeyCredential }) {
  const store = () => credentials || globalThis.navigator?.credentials;
  const pkc = () => publicKeyCredential || globalThis.PublicKeyCredential;
  let autofill = null;

  const isSupported = () => typeof pkc() === 'function' && !!store();

  async function isAutofillSupported() {
    const type = pkc();
    return isSupported() && typeof type.isConditionalMediationAvailable === 'function' && (await type.isConditionalMediationAvailable());
  }

  function stopAutofill() {
    if (autofill) autofill.abort();
    autofill = null;
  }

  async function create(options, { signal } = {}) {
    stopAutofill(); // only one WebAuthn request may be pending
    const type = pkc();
    const publicKey = typeof type?.parseCreationOptionsFromJSON === 'function' ? type.parseCreationOptionsFromJSON(options) : creationOptions(options);
    return credentialToJson(await store().create({ publicKey, signal }));
  }

  async function get(options, { signal, conditional = false } = {}) {
    if (!conditional) stopAutofill();
    const type = pkc();
    const publicKey = typeof type?.parseRequestOptionsFromJSON === 'function' ? type.parseRequestOptionsFromJSON(options) : requestOptions(options);
    const request = { publicKey, signal };
    if (conditional) request.mediation = 'conditional';
    return credentialToJson(await store().get(request));
  }

  /** begin (POST) → browser ceremony → finish (POST { credential, state, ...extra }); resolves with the finish response data. */
  async function run(kind, beginUrl, finishUrl, extra = {}, options = {}) {
    const begin = (await http.post(beginUrl, {})).data;
    const credential = kind === 'create' ? await create(begin.options, options) : await get(begin.options, options);
    const data = (await http.post(finishUrl, { ...extra, credential, state: begin.state })).data;
    bus.emit('passkey:done', { kind, data });
    return data;
  }

  /** Applies a finish response the way forms do: message, commands, local redirect. */
  function apply(data) {
    if (!data || typeof data !== 'object') return;
    if (typeof data.message === 'string' && data.message) ui.toast(data.message, data.ok === false ? 'danger' : 'success');
    if (Array.isArray(data.commands)) commands.run(data.commands);
    if (isLocalUrl(data.redirect)) navigate(data.redirect);
  }

  function failed(error, element) {
    const cancelled = error && (error.name === 'NotAllowedError' || error.name === 'AbortError');
    if (!cancelled) ui.toast(error && error.problem ? error.problem.detail || error.problem.title : 'The passkey could not be used.', 'danger');
    bus.emit('passkey:error', { element, error, cancelled });
  }

  /** Starts passkey autofill for a username field; the returned promise settles when the user picks one. */
  async function startAutofill(beginUrl, finishUrl, extra = {}) {
    if (!(await isAutofillSupported())) return null;
    stopAutofill();
    const controller = new AbortController();
    autofill = controller;
    try {
      return await run('get', beginUrl, finishUrl, extra, { signal: controller.signal, conditional: true });
    } finally {
      if (autofill === controller) autofill = null;
    }
  }

  function extras(element) {
    const extra = {};
    const name = element.getAttribute('data-wl-passkey-name');
    if (name) extra.name = document.querySelector(name)?.value || '';
    const remember = element.getAttribute('data-wl-passkey-remember');
    if (remember) extra.remember = !!document.querySelector(remember)?.checked;
    return extra;
  }

  function bind(root) {
    for (const element of root.querySelectorAll('[data-wl-passkey]')) {
      if (element.wlPasskeyBound) continue;
      element.wlPasskeyBound = true;
      const action = element.getAttribute('data-wl-passkey');
      const begin = element.getAttribute('data-wl-passkey-begin');
      const finish = element.getAttribute('data-wl-passkey-finish');
      if (!isLocalUrl(begin) || !isLocalUrl(finish)) continue;
      if (!isSupported()) {
        if (action !== 'autofill') element.hidden = true; // no passkeys in this browser: hide the button
        continue;
      }
      if (action === 'autofill') {
        startAutofill(begin, finish, extras(element)).then(apply, (error) => failed(error, element));
        continue;
      }
      element.addEventListener('click', async (event) => {
        event.preventDefault();
        if (element.getAttribute('aria-busy') === 'true') return;
        element.setAttribute('aria-busy', 'true');
        try {
          apply(await run(action === 'register' ? 'create' : 'get', begin, finish, extras(element)));
        } catch (error) {
          failed(error, element);
        } finally {
          element.removeAttribute('aria-busy');
        }
      });
    }
  }

  return {
    isSupported,
    isAutofillSupported,
    create,
    get,
    register: (beginUrl, finishUrl, extra) => run('create', beginUrl, finishUrl, extra),
    signIn: (beginUrl, finishUrl, extra) => run('get', beginUrl, finishUrl, extra),
    verify: (beginUrl, finishUrl, extra) => run('get', beginUrl, finishUrl, extra),
    startAutofill,
    stopAutofill,
    bind
  };
}
