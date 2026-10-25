/*
 * Page modules for the starter theme. Elements opt in with data-wl-module="name"; the WebLogic
 * client mounts them on load and after partial navigation, and calls the returned cleanup
 * before the content is replaced. Text from users is always set with textContent.
 */
(function () {
  function install(wl) {
    // The live wall: append realtime messages, post without a page reload.
    wl.modules.define('live-wall', function (root) {
      var list = root.querySelector('[data-live-list]');
      var status = root.querySelector('[data-live-status]');
      var form = root.querySelector('[data-live-form]');

      function item(author, text) {
        var li = document.createElement('li');
        li.className = 'list-group-item';
        var name = document.createElement('strong');
        name.textContent = author;
        var when = document.createElement('span');
        when.className = 'text-body-secondary small ms-1';
        when.textContent = 'just now';
        var body = document.createElement('div');
        body.textContent = text;
        li.append(name, when, body);
        return li;
      }

      var offs = [
        wl.realtime.subscribe('starter.shout', function (envelope) {
          var empty = list.querySelector('[data-live-empty]');
          if (empty) empty.remove();
          list.prepend(item(envelope.data.author, envelope.data.text));
        }),
        wl.realtime.subscribe('starter.member-joined', function (envelope) {
          wl.ui.toast(envelope.data.message, 'info');
        }),
        wl.realtime.subscribe('starter.wall-cleared', function () {
          list.replaceChildren();
        }),
        wl.on('realtime:status', function (detail) {
          if (status) status.textContent = detail.status;
        })
      ];
      if (status) status.textContent = wl.realtime.status;
      if (wl.realtime.status === 'closed') wl.realtime.connect();

      function submit(event) {
        event.preventDefault();
        var input = form.querySelector('[name=text]');
        var error = form.querySelector('[data-live-error]');
        error.textContent = '';
        wl.http.request('/live', { method: 'POST', form: new URLSearchParams({ text: input.value }) })
          .then(function () { input.value = ''; input.focus(); })
          .catch(function (problem) { error.textContent = (problem.problem && problem.problem.title) || 'Could not post.'; });
      }
      if (form) form.addEventListener('submit', submit);

      return function () {
        offs.forEach(function (off) { off(); });
        if (form) form.removeEventListener('submit', submit);
      };
    });

    // Dashboard: "Add widget" menu.
    wl.modules.define('dashboard-add', function (menu) {
      function click(event) {
        var button = event.target.closest('[data-widget]');
        var dashboard = document.querySelector('[data-wl-dashboard="' + menu.getAttribute('data-dashboard') + '"]');
        if (button && dashboard) wl.dashboard.add(dashboard, button.getAttribute('data-widget'), 'main');
      }
      menu.addEventListener('click', click);
      return function () { menu.removeEventListener('click', click); };
    });

    // Notes widget: keep the save button's payload in sync with the textarea.
    wl.modules.define('note', function (root) {
      var text = root.querySelector('textarea');
      var save = root.querySelector('[data-wl-action]');
      function sync() { save.setAttribute('data-wl-payload', JSON.stringify({ text: text.value })); }
      sync();
      text.addEventListener('input', sync);
      return function () { text.removeEventListener('input', sync); };
    });

    // Caching demo: evict the tag through the client (CSRF token fetched on demand).
    wl.modules.define('cache-evict', function (root) {
      var button = root.querySelector('button');
      var status = root.querySelector('[data-evict-status]');
      function click() {
        wl.http.request('/cache/evict', { method: 'POST' }).then(function () {
          status.textContent = 'Evicted — reload to see a fresh render.';
        }).catch(function () {
          status.textContent = 'Eviction failed.';
        });
      }
      button.addEventListener('click', click);
      return function () { button.removeEventListener('click', click); };
    });
  }

  if (globalThis.WebLogic) install(globalThis.WebLogic);
  else (globalThis.WebLogicPending = globalThis.WebLogicPending || []).push(install);
})();
