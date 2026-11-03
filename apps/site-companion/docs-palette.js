(function () {
  // Ctrl+Shift+F on documentation pages: headings, sections and pages; Enter jumps and highlights.
  var wrap = document.createElement('div');
  wrap.className = 'palette'; wrap.hidden = true;
  wrap.setAttribute('role', 'dialog'); wrap.setAttribute('aria-label', 'Command palette');
  wrap.innerHTML = '<div class="box"><input type="search" aria-label="Search this documentation" placeholder="Search headings and pages"><ul role="listbox"></ul></div>';
  document.body.appendChild(wrap);
  var input = wrap.querySelector('input'), list = wrap.querySelector('ul'), sel = 0, shown = [];
  function entries() {
    var out = [];
    document.querySelectorAll('.doc h1, .doc h2, .doc h3, .doc li strong').forEach(function (h) { out.push({ title: h.textContent.trim(), group: 'This page', el: h }); });
    document.querySelectorAll('nav.top a').forEach(function (a) { out.push({ title: a.textContent.trim(), group: 'Page', href: a.getAttribute('href') }); });
    return out;
  }
  function render() {
    var q = input.value.trim().toLowerCase();
    shown = entries().filter(function (e) { return !q || (e.title + ' ' + e.group).toLowerCase().indexOf(q) >= 0; }).slice(0, 80);
    sel = Math.min(sel, Math.max(shown.length - 1, 0));
    list.innerHTML = '';
    shown.forEach(function (e, i) {
      var li = document.createElement('li'); li.setAttribute('role', 'option'); li.setAttribute('aria-selected', String(i === sel));
      var a = document.createElement('span'); a.textContent = e.title; var b = document.createElement('small'); b.textContent = e.group;
      li.appendChild(a); li.appendChild(b);
      li.addEventListener('click', function () { go(e); });
      list.appendChild(li);
    });
  }
  function open() { wrap.hidden = false; input.value = ''; sel = 0; render(); input.focus(); }
  function close() { wrap.hidden = true; }
  function go(e) {
    close();
    if (e.href) { location.href = e.href; return; }
    e.el.scrollIntoView({ block: 'center' }); e.el.classList.add('flash');
    setTimeout(function () { e.el.classList.remove('flash'); }, 1800);
  }
  input.addEventListener('input', function () { sel = 0; render(); });
  wrap.addEventListener('click', function (ev) { if (ev.target === wrap) close(); });
  var btn = document.getElementById('open-palette'); if (btn) btn.addEventListener('click', open);
  document.addEventListener('keydown', function (ev) {
    if (ev.ctrlKey && ev.shiftKey && ev.key.toLowerCase() === 'f') { ev.preventDefault(); wrap.hidden ? open() : close(); return; }
    if (wrap.hidden) return;
    if (ev.key === 'Escape') close();
    else if (ev.key === 'ArrowDown') { ev.preventDefault(); sel = Math.min(sel + 1, shown.length - 1); render(); }
    else if (ev.key === 'ArrowUp') { ev.preventDefault(); sel = Math.max(sel - 1, 0); render(); }
    else if (ev.key === 'Enter' && shown[sel]) { ev.preventDefault(); go(shown[sel]); }
  });
})();
