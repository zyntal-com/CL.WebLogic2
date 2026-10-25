/**
 * WebLogic client entry: builds the runtime from the page configuration, exposes it as
 * `WebLogic` (also on globalThis) and starts it when the DOM is ready.
 */
import { Bus, readConfig } from './core.js';
import { createCommands } from './commands.js';
import { createDashboard } from './dashboard.js';
import { append, el, parseHtml, qs, qsa, readJson, setBusy, swap } from './dom.js';
import { createForms } from './forms.js';
import { createHttp } from './http.js';
import { createModules } from './modules.js';
import { createNavigation } from './navigation.js';
import { createRealtime } from './realtime.js';
import { createUi } from './ui.js';
import { isLocalUrl, isSameOrigin } from './url.js';
import { validateAll, validateField } from './validation.js';
import { createWidgets } from './widgets.js';

function create() {
  const config = readConfig(document);
  const bus = new Bus();
  const ui = createUi();
  const http = createHttp({ config });
  const api = {};
  const modules = createModules(api);
  const enhance = (root) => {
    forms.bind(root);
    widgets.bind(root);
    dashboard.bind(root);
    modules.mount(root);
  };
  const navigation = createNavigation({
    bus,
    ui,
    config,
    beforeSwap: (shell) => modules.unmount(shell),
    afterSwap: (shell) => enhance(shell),
    onConfig: (next) => Object.assign(config, next)
  });
  const navigate = (url) => navigation.navigate(url);
  const commands = createCommands({ ui, navigate, swap, enhance, unmount: modules.unmount });
  const forms = createForms({ http, ui, commands, bus, navigate, config });
  const widgets = createWidgets({ http, commands, ui, bus, swap, enhance, unmount: modules.unmount });
  const dashboard = createDashboard({ http, ui, bus, swap, enhance, unmount: modules.unmount });
  const realtime = createRealtime({ bus, config });
  let started = false;

  function start() {
    if (started) return;
    started = true;
    for (const install of (globalThis.WebLogicPending || []).splice(0)) install(api);
    navigation.start();
    enhance(document.body);
    if (config.realtime && config.realtime.autoConnect) realtime.connect();
    bus.emit('ready', api);
    document.dispatchEvent(new CustomEvent('wl:ready', { detail: api }));
  }

  Object.assign(api, {
    version: config.version,
    config,
    bus,
    on: (type, handler) => bus.on(type, handler),
    http,
    ui,
    dom: { el, append, qs, qsa, parseHtml, swap, readJson, setBusy },
    modules: { define: modules.define, mount: modules.mount, unmount: modules.unmount },
    commands: { run: commands.run },
    forms: { bind: forms.bind, submit: forms.submit, validate: forms.validate, readValues: forms.readValues, refresh: forms.refresh, addItem: forms.addItem },
    validation: { validateField, validateAll },
    widgets: { action: widgets.action, refresh: widgets.refresh },
    dashboard: { setEditing: dashboard.setEditing, save: dashboard.save, reload: dashboard.reload, add: dashboard.add, remove: dashboard.remove, move: dashboard.move, readLayout: dashboard.readLayout },
    realtime,
    navigate,
    enhance,
    url: { isLocalUrl, isSameOrigin },
    start
  });
  return Object.freeze(api);
}

/** The WebLogic runtime (null outside a browser). */
export const WebLogic = typeof document === 'undefined' ? null : create();

if (WebLogic) {
  globalThis.WebLogic = WebLogic;
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', () => WebLogic.start(), { once: true });
  else queueMicrotask(() => WebLogic.start());
}
