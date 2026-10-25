/**
 * Dashboard editing for [data-wl-dashboard] elements rendered by the server: drag and drop
 * between [data-wl-dashboard-zone] sections, keyboard moves (Alt+Arrow keys on a focused item),
 * removing ([data-wl-dashboard-remove]) and adding widgets. Every change is saved with PUT to
 * data-wl-dashboard-url carrying the layout version; a conflict (409) reloads the dashboard.
 * Editing is on while the dashboard has data-wl-dashboard-editing; [data-wl-dashboard-edit="key"]
 * buttons toggle it.
 */

import { isLocalUrl } from './url.js';

/**
 * The layout as the DOM shows it: items in zone order with their parameters.
 * @param {Element} dashboard
 */
export function readLayout(dashboard) {
  const items = [];
  for (const zone of dashboard.querySelectorAll('[data-wl-dashboard-zone]')) {
    let order = 0;
    for (const item of zone.querySelectorAll(':scope > [data-wl-dashboard-item]')) {
      let parameters = {};
      try {
        parameters = JSON.parse(item.getAttribute('data-wl-dashboard-params') || '{}') || {};
      } catch {
        parameters = {};
      }
      items.push({
        instanceId: item.getAttribute('data-wl-dashboard-item'),
        widgetName: item.getAttribute('data-wl-dashboard-widget'),
        zone: zone.getAttribute('data-wl-dashboard-zone'),
        order: order++,
        parameters
      });
    }
  }
  return items;
}

/**
 * @param {{ http: any, ui: any, bus: any, swap: Function, enhance: (root: Element) => void, unmount: (root: Element) => void, root?: Document | Element }} services
 */
export function createDashboard({ http, ui, bus, swap, enhance, unmount, root: eventRoot }) {
  const events = eventRoot || document;
  const handlers = {
    click: (event) => onClick(event),
    keydown: (event) => onKey(event),
    dragstart: (event) => onDragStart(event),
    dragover: (event) => onDragOver(event),
    drop: (event) => onDrop(event),
    dragend: () => (dragged = null)
  };
  const bound = new WeakSet();
  let listening = false;
  let dragged = null;

  // Only dashboards this instance bound: another runtime on the page never acts on them twice.
  const dashboardOf = (node) => {
    const dashboard = node.closest('[data-wl-dashboard]');
    return dashboard && bound.has(dashboard) ? dashboard : null;
  };
  const editing = (dashboard) => dashboard && dashboard.hasAttribute('data-wl-dashboard-editing');

  /** Starts handling dashboards in root. */
  function bind(root) {
    if (!listening) {
      listening = true;
      for (const [type, handler] of Object.entries(handlers)) events.addEventListener(type, handler);
    }
    const dashboards = root.matches && root.matches('[data-wl-dashboard]') ? [root] : [];
    dashboards.push(...root.querySelectorAll('[data-wl-dashboard]'));
    for (const dashboard of dashboards) {
      bound.add(dashboard);
      setEditing(dashboard, editing(dashboard));
    }
  }

  /**
   * Turns editing on or off.
   * @param {Element} dashboard
   * @param {boolean} on
   */
  function setEditing(dashboard, on) {
    dashboard.toggleAttribute('data-wl-dashboard-editing', on);
    for (const item of dashboard.querySelectorAll('[data-wl-dashboard-item]')) {
      item.draggable = on;
      if (on) item.tabIndex = 0;
      else item.removeAttribute('tabindex');
      if (on) item.setAttribute('aria-grabbed', 'false');
      else item.removeAttribute('aria-grabbed');
    }
  }

  /**
   * Saves the layout shown in the DOM (or items); resolves with the saved layout or null.
   * @param {Element} dashboard
   * @param {any[]} [items]
   */
  async function save(dashboard, items) {
    const url = dashboard.getAttribute('data-wl-dashboard-url');
    if (!isLocalUrl(url)) return null;
    const version = Number(dashboard.getAttribute('data-wl-dashboard-version') || 0);
    try {
      const { data } = await http.request(url, { method: 'PUT', json: { version, items: items || readLayout(dashboard) }, key: 'dashboard:' + url });
      dashboard.setAttribute('data-wl-dashboard-version', String(data.version));
      bus.emit('dashboard:saved', { dashboard, layout: data });
      return data;
    } catch (error) {
      if (error && error.status === 409) {
        ui.toast('This dashboard was changed elsewhere and has been reloaded.', 'warning');
        await reload(dashboard);
      } else {
        ui.toast((error && error.problem && (error.problem.detail || error.problem.title)) || 'The dashboard could not be saved.', 'danger');
      }
      bus.emit('dashboard:error', { dashboard, error });
      return null;
    }
  }

  /**
   * Re-renders the dashboard from the server; resolves with the new element.
   * @param {Element} dashboard
   */
  async function reload(dashboard) {
    const url = dashboard.getAttribute('data-wl-dashboard-url');
    if (!isLocalUrl(url)) return dashboard;
    const zones = [...dashboard.querySelectorAll('[data-wl-dashboard-zone]')].map((zone) => 'zone=' + encodeURIComponent(zone.getAttribute('data-wl-dashboard-zone')));
    const wasEditing = editing(dashboard);
    const result = await http.request(url + '?format=html' + (zones.length ? '&' + zones.join('&') : ''), { headers: { Accept: 'text/html' }, key: 'dashboard-html:' + url });
    if (result.headers.get('X-WebLogic-Fragment') !== '1' || typeof result.data !== 'string') return dashboard;
    unmount(dashboard);
    const [next] = swap(dashboard, result.data, 'outer');
    if (!next) return dashboard;
    bound.add(next);
    enhance(next);
    setEditing(next, wasEditing);
    return next;
  }

  /**
   * Adds a widget to a zone, saves and reloads.
   * @param {Element} dashboard
   * @param {string} widgetName
   * @param {string} [zone]
   * @param {Record<string, string>} [parameters]
   */
  async function add(dashboard, widgetName, zone = 'main', parameters = {}) {
    const items = readLayout(dashboard);
    items.push({ instanceId: '', widgetName, zone, order: items.filter((i) => i.zone === zone).length, parameters });
    const saved = await save(dashboard, items);
    return saved ? reload(dashboard) : null;
  }

  /**
   * Removes an item and saves.
   * @param {Element} item
   */
  async function remove(item) {
    const dashboard = dashboardOf(item);
    if (!dashboard) return null;
    unmount(item);
    item.remove();
    return save(dashboard);
  }

  /**
   * Moves an item to a zone at an index and saves.
   * @param {Element} item
   * @param {Element} zone
   * @param {number} index
   */
  function move(item, zone, index) {
    const siblings = [...zone.querySelectorAll(':scope > [data-wl-dashboard-item]')].filter((node) => node !== item);
    const before = siblings[Math.max(0, Math.min(index, siblings.length))] || null;
    zone.insertBefore(item, before);
    item.focus?.();
    return save(dashboardOf(zone));
  }

  function onClick(event) {
    const target = event.target instanceof Element ? event.target : null;
    if (!target) return;
    const toggle = target.closest('[data-wl-dashboard-edit]');
    if (toggle) {
      const key = toggle.getAttribute('data-wl-dashboard-edit');
      const dashboard = [...document.querySelectorAll('[data-wl-dashboard]')].find((d) => bound.has(d) && d.getAttribute('data-wl-dashboard') === key);
      if (dashboard) {
        setEditing(dashboard, !editing(dashboard));
        toggle.setAttribute('aria-pressed', editing(dashboard) ? 'true' : 'false');
      }
      return;
    }
    const removeButton = target.closest('[data-wl-dashboard-remove]');
    if (removeButton && editing(dashboardOf(removeButton))) {
      event.preventDefault();
      remove(removeButton.closest('[data-wl-dashboard-item]'));
    }
  }

  function onKey(event) {
    if (!event.altKey || !event.target.matches?.('[data-wl-dashboard-item]')) return;
    const item = event.target;
    const dashboard = dashboardOf(item);
    if (!editing(dashboard)) return;
    const zones = [...dashboard.querySelectorAll('[data-wl-dashboard-zone]')];
    const zone = item.parentElement;
    const position = [...zone.querySelectorAll(':scope > [data-wl-dashboard-item]')].indexOf(item);
    const zoneIndex = zones.indexOf(zone);
    if (event.key === 'ArrowUp' && position > 0) move(item, zone, position - 1);
    else if (event.key === 'ArrowDown') move(item, zone, position + 1);
    else if (event.key === 'ArrowLeft' && zoneIndex > 0) move(item, zones[zoneIndex - 1], position);
    else if (event.key === 'ArrowRight' && zoneIndex < zones.length - 1) move(item, zones[zoneIndex + 1], position);
    else return;
    event.preventDefault();
  }

  function onDragStart(event) {
    const item = event.target instanceof Element ? event.target.closest('[data-wl-dashboard-item]') : null;
    if (!item || !editing(dashboardOf(item))) return;
    dragged = item;
    item.setAttribute('aria-grabbed', 'true');
    event.dataTransfer?.setData('text/plain', item.getAttribute('data-wl-dashboard-item'));
  }

  function onDragOver(event) {
    if (dragged && event.target instanceof Element && event.target.closest('[data-wl-dashboard-zone]') && dashboardOf(event.target) === dashboardOf(dragged)) {
      event.preventDefault();
    }
  }

  function onDrop(event) {
    const zone = event.target instanceof Element ? event.target.closest('[data-wl-dashboard-zone]') : null;
    if (!dragged || !zone || dashboardOf(zone) !== dashboardOf(dragged)) return;
    event.preventDefault();
    const items = [...zone.querySelectorAll(':scope > [data-wl-dashboard-item]')].filter((node) => node !== dragged);
    let index = items.length;
    for (let i = 0; i < items.length; i++) {
      const box = items[i].getBoundingClientRect();
      if (event.clientY < box.top + box.height / 2) {
        index = i;
        break;
      }
    }
    dragged.setAttribute('aria-grabbed', 'false');
    const item = dragged;
    dragged = null;
    move(item, zone, index);
  }

  /** Stops listening for dashboard events. */
  function destroy() {
    for (const [type, handler] of Object.entries(handlers)) events.removeEventListener(type, handler);
    listening = false;
  }

  return { bind, setEditing, save, reload, add, remove, move, readLayout, destroy };
}
