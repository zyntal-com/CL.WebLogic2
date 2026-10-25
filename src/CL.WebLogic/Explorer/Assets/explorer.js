'use strict';
(function () {
  const main = document.getElementById('wl-x-main');
  const nav = document.getElementById('wl-x-nav');
  const meta = document.getElementById('wl-x-meta');

  // Every value becomes text; nothing from the document is parsed as HTML.
  function el(tag, attributes, ...children) {
    const node = document.createElement(tag);
    for (const [key, value] of Object.entries(attributes || {})) {
      if (value === null || value === undefined || value === false) continue;
      if (key === 'text') node.textContent = String(value);
      else node.setAttribute(key, value === true ? '' : String(value));
    }
    for (const child of children.flat()) {
      if (child === null || child === undefined || child === false) continue;
      node.append(child instanceof Node ? child : document.createTextNode(String(child)));
    }
    return node;
  }

  const tags = (values, className) => (values || []).map((value) => el('span', { class: 'tag' + (className ? ' ' + className : '') }, value));
  const access = (item) => item.anonymous
    ? tags(['anonymous'], 'anon')
    : [...tags(item.groups), ...tags(item.permissions), ...tags(item.policies || (item.policy ? [item.policy] : [])), ...((item.groups || []).length + (item.permissions || []).length + (item.policies || []).length || item.policy ? [] : tags(['signed in']))];

  function section(id, title, ...content) {
    nav.append(el('a', { href: '#' + id }, title));
    return el('section', { id }, el('h2', {}, title), ...content);
  }

  function table(headers, rows) {
    return el('table', {}, el('thead', {}, el('tr', {}, headers.map((h) => el('th', { scope: 'col' }, h)))),
      el('tbody', {}, rows.map((cells) => el('tr', {}, cells.map((cell) => el('td', {}, cell))))));
  }

  function routes(data) {
    const search = el('input', { type: 'search', placeholder: 'Filter routes…', 'aria-label': 'Filter routes' });
    const body = table(['Methods', 'Pattern', 'Kind', 'Access', 'Limits', 'Contributor'], data.routes.map((r) => [
      el('code', {}, r.methods.join(', ')),
      [el('code', {}, r.pattern), r.description ? el('div', { class: 'muted' }, r.description) : null],
      r.kind,
      access(r),
      [...tags(r.rateLimit ? ['rate: ' + r.rateLimit] : []), ...tags(r.cors ? ['cors: ' + r.cors] : []), ...tags(r.outputCache ? ['cache: ' + r.outputCache] : []), ...tags(r.maxBodyBytes ? ['body ≤ ' + r.maxBodyBytes] : [])],
      el('code', {}, r.contributor)
    ]));
    search.addEventListener('input', () => {
      const term = search.value.toLowerCase();
      for (const row of body.tBodies[0].rows) row.hidden = term !== '' && !row.textContent.toLowerCase().includes(term);
    });
    return section('routes', `Routes (${data.routes.length})`, search, body);
  }

  function render(data) {
    meta.textContent = `${data.environment} · ${data.version}${data.restartPending ? ' · restart pending' : ''}`;
    main.replaceChildren(
      routes(data),
      section('anonymous', `Anonymous routes (${data.anonymousRoutes.length})`, el('p', { class: 'muted' }, 'Everything reachable without signing in. Review this list before each release.'),
        el('ul', {}, data.anonymousRoutes.map((r) => el('li', {}, el('code', {}, r))))),
      section('pipeline', 'Security pipeline', table(['Phase', 'Order', 'Stage', 'Enabled'], data.stages.map((s) => [s.phase, String(s.order), el('code', { class: s.enabled ? '' : 'off' }, s.name), s.enabled ? 'yes' : 'disabled']))),
      section('widgets', `Widgets (${data.widgets.length})`, table(['Name', 'Access', 'Actions', 'Dashboard', 'Contributor'], data.widgets.map((w) => [
        [el('code', {}, w.name), w.description ? el('div', { class: 'muted' }, w.description) : null], access(w), tags(w.actions), w.dashboard ? 'yes' : 'no', el('code', {}, w.contributor)]))),
      section('areas', 'Widget areas', table(['Area', 'Placements'], data.widgetAreas.map((a) => [el('code', {}, a.area), a.placements.map((p) => el('div', {}, el('code', {}, p.widget), p.instance ? ' #' + p.instance : '', ' (', String(p.order), ')'))]))),
      section('forms', `Forms (${data.forms.length})`, table(['Id', 'Fields', 'Steps', 'Anti-bot'], data.forms.map((f) => [
        [el('code', {}, f.id), el('div', { class: 'muted' }, f.model)], f.fields.map((x) => el('span', { class: 'tag' }, x.name + ': ' + x.type + (x.required ? ' *' : ''))), tags(f.steps), f.antiBot ? 'yes' : 'no']))),
      section('options', 'Options providers', table(['Name', 'Access'], data.optionsProviders.map((p) => [el('code', {}, p.name), access({ anonymous: p.anonymous, policy: p.policy })]))),
      section('realtime', 'Realtime', table(['Setting', 'Value'], Object.entries(data.realtime).map(([k, v]) => [k, String(v)]))),
      section('contributors', `Contributors (${data.contributors.length})`, table(['Id', 'Name', 'Kind', 'Description'], data.contributors.map((c) => [el('code', {}, c.id), c.name, c.kind, c.description || ''])))
    );
  }

  fetch(main.getAttribute('data-source'), { credentials: 'same-origin', headers: { Accept: 'application/json' } })
    .then((response) => {
      if (!response.ok) throw new Error('HTTP ' + response.status);
      return response.json();
    })
    .then(render)
    .catch((error) => main.replaceChildren(el('p', {}, 'Could not load the explorer data: ' + error.message)));
})();
