/* Monitoring Tools site: preferences, search with a regex builder, release lookup, filters and a labelled demo. */
(function () {
  'use strict';
  var root = document.body.getAttribute('data-root') || './';
  var html = document.documentElement;
  function $(s, c) { return (c || document).querySelector(s); }
  function $all(s, c) { return Array.prototype.slice.call((c || document).querySelectorAll(s)); }
  function load(key, fallback) { try { var v = localStorage.getItem(key); return v ? JSON.parse(v) : fallback; } catch (e) { return fallback; } }
  function save(key, val) { try { localStorage.setItem(key, JSON.stringify(val)); } catch (e) { /* storage may be blocked */ } }
  var reduceMotion = function () { return html.dataset.motion === 'reduce' || (window.matchMedia && matchMedia('(prefers-reduced-motion: reduce)').matches); };

  /* ---------- site settings ---------- */
  var prefs = load('mt-site-prefs', {});
  var dlg = $('#settings'), sBtn = $('#settings-btn');
  if (dlg && sBtn) {
    var pt = $('#pref-theme'), pm = $('#pref-motion'), px = $('#pref-text');
    function showPrefs() { pt.value = prefs.theme || 'system'; pm.value = prefs.motion || 'system'; px.value = prefs.text || ''; }
    function applyPrefs() {
      if (prefs.theme === 'light' || prefs.theme === 'dark') html.dataset.theme = prefs.theme; else delete html.dataset.theme;
      if (prefs.motion === 'reduce') html.dataset.motion = 'reduce'; else delete html.dataset.motion;
      if (prefs.text) html.dataset.text = prefs.text; else delete html.dataset.text;
      save('mt-site-prefs', prefs);
    }
    sBtn.addEventListener('click', function () {
      showPrefs();
      if (dlg.showModal) dlg.showModal(); else dlg.setAttribute('open', '');
      sBtn.setAttribute('aria-expanded', 'true');
    });
    dlg.addEventListener('close', function () { sBtn.setAttribute('aria-expanded', 'false'); sBtn.focus(); });
    pt.addEventListener('change', function () { prefs.theme = pt.value === 'system' ? '' : pt.value; applyPrefs(); });
    pm.addEventListener('change', function () { prefs.motion = pm.value === 'system' ? '' : pm.value; applyPrefs(); });
    px.addEventListener('change', function () { prefs.text = px.value; applyPrefs(); });
    $('#pref-reset').addEventListener('click', function () { prefs = {}; applyPrefs(); showPrefs(); });
  }

  /* ---------- search with plain and regex modes and an anchored builder ---------- */
  var q = $('#q'), modeBtn = $('#mode'), bBtn = $('#builder-btn'), builder = $('#builder'), list = $('#results');
  var rxIn = $('#rx-pattern'), rxStatus = $('#rx-status'), sample = $('#rx-sample'), sampleOut = $('#rx-out');
  var index = window.SITE_INDEX || [];
  var sel = -1, shown = [], regexMode = false;
  var flags = load('mt-site-flags', { i: true, m: false, s: false, u: false });
  var saved = load('mt-site-search', {});
  if (saved && saved.regex) regexMode = true;

  function flagString() { return ['i', 'm', 's', 'u'].filter(function (f) { return flags[f]; }).join(''); }
  function syncFlagBoxes() { $all('[data-flag]').forEach(function (b) { b.checked = !!flags[b.getAttribute('data-flag')]; }); }
  function esc(s) { return String(s).replace(/[&<>"]/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]; }); }
  function setStatus(msg, bad) { if (rxStatus) { rxStatus.textContent = msg; rxStatus.classList.toggle('bad', !!bad); } }

  function compile() {
    try { return { rx: new RegExp(q.value, flagString()) }; } catch (e) { return { error: e.message }; }
  }
  function highlight(text, matcher) {
    var m = matcher(text);
    if (!m) return esc(text);
    return esc(text.slice(0, m.index)) + '<mark>' + esc(m[0]) + '</mark>' + esc(text.slice(m.index + m[0].length));
  }
  function closeList() { list.hidden = true; q.setAttribute('aria-expanded', 'false'); q.removeAttribute('aria-activedescendant'); sel = -1; }
  function mark() {
    $all('a', list).forEach(function (a, i) {
      a.setAttribute('aria-selected', i === sel ? 'true' : 'false');
      if (i === sel) { a.scrollIntoView({ block: 'nearest' }); q.setAttribute('aria-activedescendant', a.id); }
    });
  }
  function run() {
    var value = q.value.trim();
    q.parentNode.classList.remove('invalid');
    if (!value) { closeList(); setStatus(regexMode ? 'Type a pattern to search.' : 'Switch regex mode on to search with this pattern.'); updateSample(); return; }
    var test, matcher, stoppedEarly = false;
    if (regexMode) {
      var c = compile();
      if (c.error) {
        q.parentNode.classList.add('invalid');
        setStatus('This pattern is not valid: ' + c.error, true);
        list.hidden = false; q.setAttribute('aria-expanded', 'true');
        list.innerHTML = '<li class="empty" role="presentation">The pattern is not valid, so nothing is filtered. ' + esc(c.error) + '</li>';
        shown = []; return;
      }
      var rx = c.rx;
      test = function (e) { rx.lastIndex = 0; return rx.test(e.t + '\n' + e.c + '\n' + e.x); };
      matcher = function (t) { rx.lastIndex = 0; return rx.exec(t); };
    } else {
      var words = value.toLowerCase().split(/\s+/);
      test = function (e) { var h = (e.t + ' ' + e.c + ' ' + e.x).toLowerCase(); return words.every(function (w) { return h.indexOf(w) >= 0; }); };
      matcher = function (t) { var l = t.toLowerCase(); for (var i = 0; i < words.length; i++) { var k = l.indexOf(words[i]); if (k >= 0) return { index: k, 0: t.substr(k, words[i].length) }; } return null; };
    }
    var started = Date.now(), hits = [];
    for (var i = 0; i < index.length; i++) {
      if (Date.now() - started > 150) { stoppedEarly = true; break; }
      if (test(index[i])) hits.push(index[i]);
    }
    // rank: title matches first
    var titleHit = function (e) { var m = matcher(e.t); return m ? 0 : 1; };
    hits.sort(function (a, b) { return titleHit(a) - titleHit(b); });
    shown = hits.slice(0, 40);
    list.hidden = false; q.setAttribute('aria-expanded', 'true');
    if (!shown.length) list.innerHTML = '<li class="empty" role="presentation">Nothing matches' + (stoppedEarly ? ' before the time limit' : '') + '.</li>';
    else list.innerHTML = shown.map(function (e, i) {
      return '<li role="presentation"><a role="option" id="r' + i + '" aria-selected="false" href="' + esc(root + e.u) + '"><span class="r-title">' + highlight(e.t, matcher) + '</span><span class="r-meta">' + esc(e.c) + ' · ' + esc(e.x) + '</span></a></li>';
    }).join('');
    sel = shown.length ? 0 : -1; mark();
    var msg = hits.length + (hits.length === 1 ? ' result' : ' results') + (hits.length > shown.length ? ', showing the first ' + shown.length : '') + '.';
    if (stoppedEarly) msg += ' The search hit its time limit and stopped early.';
    setStatus(msg);
    updateSample();
  }
  function updateSample() {
    if (!sampleOut) return;
    var text = sample.value;
    if (!text) { sampleOut.textContent = ''; return; }
    var c = compile();
    if (!q.value || c.error) { sampleOut.textContent = text; return; }
    var rx = new RegExp(c.rx.source, flagString().replace('g', '') + 'g'), out = '', last = 0, n = 0, t0 = Date.now(), m;
    while ((m = rx.exec(text)) && n < 500) {
      if (Date.now() - t0 > 100) break;
      if (m[0] === '') { rx.lastIndex++; continue; }
      out += esc(text.slice(last, m.index)) + '<mark>' + esc(m[0]) + '</mark>'; last = m.index + m[0].length; n++;
    }
    sampleOut.innerHTML = out + esc(text.slice(last));
    if (rxStatus && regexMode) rxStatus.textContent += ' ' + n + (n === 1 ? ' match' : ' matches') + ' in your sample.';
  }
  function setMode(on) {
    regexMode = on;
    modeBtn.setAttribute('aria-pressed', on ? 'true' : 'false');
    save('mt-site-search', { regex: on });
    q.placeholder = on ? 'Regular expression, for example ^search|regex' : 'Search features, tasks and screenshots (press /)';
    run();
  }
  if (q && modeBtn) {
    syncFlagBoxes();
    modeBtn.setAttribute('aria-pressed', regexMode ? 'true' : 'false');
    if (regexMode) q.placeholder = 'Regular expression, for example ^search|regex';
    modeBtn.addEventListener('click', function () { setMode(!regexMode); q.focus(); });
    q.addEventListener('input', function () { if (rxIn && rxIn.value !== q.value) rxIn.value = q.value; run(); });
    q.addEventListener('focus', function () { if (q.value) run(); });
    q.addEventListener('keydown', function (e) {
      if (e.key === 'ArrowDown') { e.preventDefault(); if (list.hidden) run(); else { sel = Math.min(sel + 1, shown.length - 1); mark(); } }
      else if (e.key === 'ArrowUp') { e.preventDefault(); sel = Math.max(sel - 1, 0); mark(); }
      else if (e.key === 'Enter' && sel >= 0 && shown[sel]) { e.preventDefault(); location.href = root + shown[sel].u; }
      else if (e.key === 'Escape') { if (!list.hidden) closeList(); else if (q.value) { q.value = ''; if (rxIn) rxIn.value = ''; run(); } }
    });
    document.addEventListener('click', function (e) { if (!e.target.closest('.search')) closeList(); });
    bBtn.addEventListener('click', function () {
      var open = builder.hidden;
      builder.hidden = !open; bBtn.setAttribute('aria-expanded', open ? 'true' : 'false');
      if (open) { rxIn.value = q.value; rxIn.focus(); }
    });
    rxIn.addEventListener('input', function () { q.value = rxIn.value; if (!regexMode) setMode(true); else run(); });
    $all('[data-flag]').forEach(function (b) { b.addEventListener('change', function () { flags[b.getAttribute('data-flag')] = b.checked; save('mt-site-flags', flags); run(); }); });
    sample.addEventListener('input', function () { run(); });
    document.addEventListener('keydown', function (e) {
      var tag = (e.target.tagName || '').toLowerCase();
      var typing = tag === 'input' || tag === 'textarea' || tag === 'select' || e.target.isContentEditable;
      if (e.key === '/' && !typing && !e.ctrlKey && !e.metaKey && !e.altKey) { e.preventDefault(); q.focus(); q.select(); }
      else if (e.ctrlKey && e.shiftKey && (e.key === 'F' || e.key === 'f')) { e.preventDefault(); q.focus(); q.select(); }
      else if (e.altKey && (e.key === 'r' || e.key === 'R')) { e.preventDefault(); setMode(!regexMode); }
      else if (e.altKey && (e.key === 'b' || e.key === 'B')) { e.preventDefault(); bBtn.click(); }
    });
    if (rxIn) rxIn.value = '';
  }

  /* ---------- latest release label (client side, honest fallback) ---------- */
  var ver = $('#version');
  if (ver) {
    var base = 'Windows installer, single file, installs per user.';
    var ctl = window.AbortController ? new AbortController() : null;
    var timer = setTimeout(function () { if (ctl) ctl.abort(); }, 6000);
    fetch('https://api.github.com/repos/Ding-Ding-Projects/Monitoring-tools/releases/latest', ctl ? { signal: ctl.signal, headers: { Accept: 'application/vnd.github+json' } } : {})
      .then(function (r) { if (!r.ok) throw new Error('status ' + r.status); return r.json(); })
      .then(function (j) {
        clearTimeout(timer);
        if (!j || !j.tag_name) throw new Error('no tag');
        var asset = (j.assets || []).filter(function (a) { return a.name === 'MonitoringTools-Setup.exe'; })[0];
        ver.textContent = 'Latest release ' + j.tag_name + '. ' + base + (asset ? ' ' + (asset.size / 1048576).toFixed(0) + ' MB.' : '');
        var s = $('#download span'); if (s) s.textContent = 'Download ' + j.tag_name + ' for Windows';
      })
      .catch(function () {
        clearTimeout(timer);
        ver.innerHTML = base + ' The version number could not be read just now; the button still downloads the latest release. <a href="https://github.com/Ding-Ding-Projects/Monitoring-tools/releases" rel="noopener noreferrer">See all releases</a>.';
      });
  }

  /* ---------- task filter ---------- */
  var tf = $('#task-filter');
  if (tf) {
    var cards = $all('#task-grid li'), count = $('#task-count');
    var apply = function (g) {
      var n = 0;
      cards.forEach(function (li) { var on = g === 'all' || li.getAttribute('data-group') === g; li.hidden = !on; if (on) n++; });
      count.textContent = 'Showing ' + n + ' of ' + cards.length + ' tasks.';
    };
    $all('button', tf).forEach(function (b) {
      b.addEventListener('click', function () {
        $all('button', tf).forEach(function (x) { x.setAttribute('aria-pressed', x === b ? 'true' : 'false'); });
        apply(b.getAttribute('data-group'));
      });
    });
    apply('all');
  }

  /* ---------- labelled demonstration of a quick command ---------- */
  var demo = $('#demo');
  if (demo) {
    var path = $('#demo-path'), out = $('#demo-out'), cmd = 'mp4', btns = $all('#demo-cmds button');
    var describe = function () {
      var p = path.value.trim().replace(/^"(.*)"$/, '$1');
      if (!p) { out.textContent = 'Type or paste a path to see the explanation.'; return; }
      var leaf = p.split(/[\\/]/).pop(), hasExt = /\.[A-Za-z0-9]{1,5}$/.test(leaf);
      if (cmd === 'push') {
        out.textContent = 'When ' + p + ' and :push are copied, the command stages the changes in that repository, commits them with your message template and pushes.';
      } else if (!hasExt) {
        out.textContent = 'A folder: every compatible file inside ' + p + ' would be converted to ' + cmd + ', each written beside its original. Nothing is overwritten.';
      } else {
        out.textContent = 'Would create ' + p.replace(/\.[A-Za-z0-9]{1,5}$/, '.' + cmd) + ' beside the original. Nothing is overwritten. The article lists which file types each command accepts.';
      }
    };
    btns.forEach(function (b) { b.addEventListener('click', function () { cmd = b.getAttribute('data-cmd'); btns.forEach(function (x) { x.setAttribute('aria-pressed', x === b ? 'true' : 'false'); }); describe(); }); });
    path.addEventListener('input', describe);
    describe();
  }

  /* ---------- status filter on any table with a Status column ---------- */
  $all('.table-wrap table').forEach(function (t) {
    var heads = $all('thead th', t).map(function (h) { return h.textContent.trim().toLowerCase(); });
    var col = heads.indexOf('status');
    if (col < 0) return;
    var rows = $all('tbody tr', t), words = {};
    rows.forEach(function (r) { var w = (r.children[col].textContent.trim().toLowerCase().split(/\s+/)[0] || '').replace(/[^a-z/]/g, ''); r.setAttribute('data-status', w); if (w) words[w] = (words[w] || 0) + 1; });
    var keys = Object.keys(words); if (keys.length < 2) return;
    var bar = document.createElement('div'); bar.className = 'status-bar'; bar.setAttribute('role', 'group'); bar.setAttribute('aria-label', 'Filter by status');
    var note = document.createElement('p'); note.className = 'hint'; note.setAttribute('role', 'status');
    var mk = function (key, label) {
      var b = document.createElement('button'); b.type = 'button'; b.className = 'chip'; b.textContent = label; b.setAttribute('aria-pressed', key === 'all' ? 'true' : 'false');
      b.addEventListener('click', function () {
        $all('button', bar).forEach(function (x) { x.setAttribute('aria-pressed', x === b ? 'true' : 'false'); });
        var n = 0; rows.forEach(function (r) { var on = key === 'all' || r.getAttribute('data-status') === key; r.classList.toggle('dim', !on); if (on) n++; });
        note.textContent = n + ' of ' + rows.length + ' rows shown.';
      });
      bar.appendChild(b);
    };
    mk('all', 'All (' + rows.length + ')');
    keys.sort().forEach(function (k) { mk(k, k + ' (' + words[k] + ')'); });
    var wrap = t.closest('.table-wrap');
    wrap.parentNode.insertBefore(bar, wrap); wrap.parentNode.insertBefore(note, wrap);
  });

  /* ---------- screenshot viewer ---------- */
  var lb = $('#lightbox');
  if (lb) {
    var lbImg = $('img', lb), lbCap = $('#lightbox-cap'), lastFocus = null;
    var closeLb = function () { lb.hidden = true; if (lastFocus) lastFocus.focus(); };
    $all('.shot-btn').forEach(function (b) {
      b.addEventListener('click', function () {
        lastFocus = b; lbImg.src = b.getAttribute('data-full'); lbImg.alt = b.getAttribute('data-cap'); lbCap.textContent = b.getAttribute('data-cap');
        lb.hidden = false; $('#lightbox-close').focus();
      });
    });
    $('#lightbox-close').addEventListener('click', closeLb);
    lb.addEventListener('click', function (e) { if (e.target === lb) closeLb(); });
    document.addEventListener('keydown', function (e) { if (e.key === 'Escape' && !lb.hidden) closeLb(); });
  }
})();
