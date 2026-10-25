import { createDashboard, readLayout } from '../dashboard.js';
import { Bus } from '../core.js';
import { swap } from '../dom.js';
import { createUi } from '../ui.js';
import { assert, test } from './testkit.js';

const markup = (version = 1) => `<div data-wl-dashboard="home" data-wl-dashboard-url="/_weblogic/dashboard/home" data-wl-dashboard-version="${version}">
  <section data-wl-dashboard-zone="main">
    <div data-wl-dashboard-item="a" data-wl-dashboard-widget="greeting" data-wl-dashboard-params="{&quot;name&quot;:&quot;Ada&quot;}">A <button data-wl-dashboard-remove>×</button></div>
    <div data-wl-dashboard-item="b" data-wl-dashboard-widget="clock" data-wl-dashboard-params="{}">B</div>
  </section>
  <section data-wl-dashboard-zone="side"></section>
</div><button data-wl-dashboard-edit="home">Edit</button>`;

const active = [];

function setup(fixture, respond) {
  while (active.length) active.pop().destroy();
  fixture.innerHTML = markup();
  const requests = [];
  const http = {
    request: async (url, options = {}) => {
      requests.push({ url, options });
      return respond(url, options);
    }
  };
  const dashboard = createDashboard({ http, ui: createUi(), bus: new Bus(), swap, enhance: () => {}, unmount: () => {}, root: fixture });
  dashboard.bind(fixture);
  active.push(dashboard);
  return { dashboard, requests, element: fixture.querySelector('[data-wl-dashboard]') };
}

const saved = (version) => async () => ({ data: { version }, headers: new Headers() });
const tick = () => new Promise((resolve) => setTimeout(resolve, 0));

test('dashboard layout is read from the DOM with parameters', (fixture) => {
  fixture.innerHTML = markup();
  assert.deepEqual(readLayout(fixture.querySelector('[data-wl-dashboard]')), [
    { instanceId: 'a', widgetName: 'greeting', zone: 'main', order: 0, parameters: { name: 'Ada' } },
    { instanceId: 'b', widgetName: 'clock', zone: 'main', order: 1, parameters: {} }
  ]);
});

test('edit toggle makes items draggable and focusable', (fixture) => {
  const { element } = setup(fixture, saved(2));
  const item = element.querySelector('[data-wl-dashboard-item="a"]');
  assert.equal(item.draggable, false);
  fixture.querySelector('[data-wl-dashboard-edit]').click();
  assert.equal(element.hasAttribute('data-wl-dashboard-editing'), true);
  assert.equal(item.draggable, true);
  assert.equal(item.tabIndex, 0);
  assert.equal(fixture.querySelector('[data-wl-dashboard-edit]').getAttribute('aria-pressed'), 'true');
});

test('keyboard moves reorder and save with the version', async (fixture) => {
  const { element, requests } = setup(fixture, saved(2));
  fixture.querySelector('[data-wl-dashboard-edit]').click();
  const item = element.querySelector('[data-wl-dashboard-item="a"]');
  item.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowRight', altKey: true, bubbles: true }));
  await tick();

  assert.equal(element.querySelector('[data-wl-dashboard-zone="side"]').contains(item), true);
  assert.equal(requests[0].options.method, 'PUT');
  assert.equal(requests[0].options.json.version, 1);
  assert.deepEqual(requests[0].options.json.items.map((i) => i.instanceId + '@' + i.zone), ['b@main', 'a@side']);
  assert.equal(element.getAttribute('data-wl-dashboard-version'), '2');
});

test('keys do nothing outside edit mode', async (fixture) => {
  const { element, requests } = setup(fixture, saved(2));
  element.querySelector('[data-wl-dashboard-item="a"]').dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown', altKey: true, bubbles: true }));
  await tick();
  assert.equal(requests.length, 0);
});

test('remove saves without the item', async (fixture) => {
  const { element, requests } = setup(fixture, saved(2));
  fixture.querySelector('[data-wl-dashboard-edit]').click();
  element.querySelector('[data-wl-dashboard-remove]').click();
  await tick();
  assert.equal(element.querySelector('[data-wl-dashboard-item="a"]'), null);
  assert.deepEqual(requests[0].options.json.items.map((i) => i.instanceId), ['b']);
});

test('a conflict reloads the dashboard from the server', async (fixture) => {
  const { dashboard, element, requests } = setup(fixture, async (url, options) => {
    if (options.method === 'PUT') {
      const error = new Error('conflict');
      error.status = 409;
      throw error;
    }
    return { data: markup(7).split('<button data-wl-dashboard-edit')[0], headers: new Headers({ 'X-WebLogic-Fragment': '1' }) };
  });
  assert.equal(await dashboard.save(element), null);
  assert.equal(requests[1].url, '/_weblogic/dashboard/home?format=html&zone=main&zone=side');
  assert.equal(fixture.querySelector('[data-wl-dashboard]').getAttribute('data-wl-dashboard-version'), '7');
});
