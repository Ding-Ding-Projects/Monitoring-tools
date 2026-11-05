/* Monitoring Tools site: preferences, language modes, search with a regex builder, command palette, tabs, right-click
   menus with working shortcuts, per-element appearance, toy locks with the unlock ladder, a toy authenticator,
   notifications, the dim sum surprise, the interactive diagram and the page's small motions.
   Everything a visitor changes is kept in this browser's local storage and never sent anywhere. */
(function () {
  'use strict';
  var root = document.body.getAttribute('data-root') || './';
  var here = document.body.getAttribute('data-page') || 'index.html';
  var html = document.documentElement;
  function $(s, c) { return (c || document).querySelector(s); }
  function $all(s, c) { return Array.prototype.slice.call((c || document).querySelectorAll(s)); }
  function load(key, fallback) { try { var v = localStorage.getItem(key); return v ? JSON.parse(v) : fallback; } catch (e) { return fallback; } }
  function save(key, val) { try { localStorage.setItem(key, JSON.stringify(val)); } catch (e) { /* storage may be blocked */ } }
  function esc(s) { return String(s).replace(/[&<>"]/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]; }); }
  function el(tag, attrs, text) { var e = document.createElement(tag); if (attrs) for (var k in attrs) e.setAttribute(k, attrs[k]); if (text != null) e.textContent = text; return e; }
  var reduceMotion = function () { return html.dataset.motion === 'reduce' || (window.matchMedia && matchMedia('(prefers-reduced-motion: reduce)').matches); };
  var typing = function (t) { var tag = (t.tagName || '').toLowerCase(); return tag === 'input' || tag === 'textarea' || tag === 'select' || t.isContentEditable; };

  /* ---------- preferences ---------- */
  var DEFAULTS = { theme: '', motion: '', text: '', lang: 'en', funEn: 2, funYue: 2, accent: '', dock: '', dimsum: true };
  var prefs = Object.assign({}, DEFAULTS, load('mt-site-prefs', {}));
  function hexToRgb(h) { var n = parseInt(h.slice(1), 16); return [n >> 16 & 255, n >> 8 & 255, n & 255]; }
  function lum(h) { return hexToRgb(h).map(function (v) { v /= 255; return v <= 0.03928 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4); }).reduce(function (a, v, i) { return a + v * [0.2126, 0.7152, 0.0722][i]; }, 0); }
  function contrast(a, b) { var x = lum(a), y = lum(b); return (Math.max(x, y) + 0.05) / (Math.min(x, y) + 0.05); }
  function hsl(h) { var c = hexToRgb(h).map(function (v) { return v / 255; }), mx = Math.max.apply(null, c), mn = Math.min.apply(null, c), l = (mx + mn) / 2, s = 0, hue = 0, d = mx - mn;
    if (d) { s = l > 0.5 ? d / (2 - mx - mn) : d / (mx + mn); hue = mx === c[0] ? (c[1] - c[2]) / d + (c[1] < c[2] ? 6 : 0) : mx === c[1] ? (c[2] - c[0]) / d + 2 : (c[0] - c[1]) / d + 4; hue *= 60; }
    return [Math.round(hue), Math.round(s * 100), Math.round(l * 100)]; }
  function mix(h, t, k) { var a = hexToRgb(h), b = hexToRgb(t); return '#' + a.map(function (v, i) { return ('0' + Math.round(v + (b[i] - v) * k).toString(16)).slice(-2); }).join('').toUpperCase(); }
  function readable(h) { var bg = isDark() ? '#070B09' : '#F3F8F5', to = isDark() ? '#FFFFFF' : '#000000', out = h; for (var k = 0; k <= 1 && contrast(out, bg) < 4.5; k += 0.05) out = mix(h, to, k); return out; }
  function isDark() { return html.dataset.theme === 'dark' || (html.dataset.theme !== 'light' && !(window.matchMedia && matchMedia('(prefers-color-scheme: light)').matches)); }
  function applyPrefs() {
    if (prefs.theme === 'light' || prefs.theme === 'dark') html.dataset.theme = prefs.theme; else delete html.dataset.theme;
    if (prefs.motion === 'reduce') html.dataset.motion = 'reduce'; else delete html.dataset.motion;
    if (prefs.text) html.dataset.text = prefs.text; else delete html.dataset.text;
    if (prefs.dock === 'left') html.dataset.dock = 'left'; else delete html.dataset.dock;
    html.dataset.lang = prefs.lang || 'en';
    if (prefs.accent && /^#[0-9a-fA-F]{6}$/.test(prefs.accent)) html.style.setProperty('--accent-pick', readable(prefs.accent)); else html.style.removeProperty('--accent-pick');
    save('mt-site-prefs', prefs);
    syncControls();
  }
  function setPref(k, v) { prefs[k] = v; applyPrefs(); if (k === 'lang') initLanguage(); }

  /* ---------- notifications: non-blocking, kept in a notification centre ---------- */
  var toasts = $('#toasts');
  function notify(msg, action) {
    var log = load('mt-site-notes', []); log.unshift({ t: msg, at: new Date().toISOString() }); save('mt-site-notes', log.slice(0, 50));
    if (!toasts) return;
    var t = el('div', { 'class': 'toast' });
    t.appendChild(el('span', { 'class': 'toast-dot', 'aria-hidden': 'true' }));
    t.appendChild(el('span', { 'class': 'toast-msg' }, msg));
    if (action) { var b = el('button', { type: 'button', 'class': 'toast-act' }, action.label); b.addEventListener('click', function () { action.run(); t.remove(); }); t.appendChild(b); }
    var x = el('button', { type: 'button', 'class': 'toast-x', 'aria-label': 'Dismiss' }, '×'); x.addEventListener('click', function () { t.remove(); }); t.appendChild(x);
    toasts.appendChild(t);
    while (toasts.children.length > 3) toasts.firstChild.remove();
    var hold = false; t.addEventListener('mouseenter', function () { hold = true; }); t.addEventListener('mouseleave', function () { hold = false; });
    var left = 4000; var iv = setInterval(function () { if (!hold) left -= 250; if (left <= 0) { clearInterval(iv); t.classList.add('out'); setTimeout(function () { t.remove(); }, 300); } }, 250);
  }

  /* ---------- language modes and the two humour sliders ---------- */
  var T = {
    'skip': '跳去內容', 'nav.home': '主頁', 'nav.docs': '說明', 'nav.diagram': '點樣運作', 'nav.shots': '相簿', 'nav.status': '進度', 'nav.settings': '設定',
    'lang.both': '雙語', 'search.label': '搜尋網站', 'search.builder': '正則', 'hero.live': 'Windows 10 同 11 用', 'hero.a': '部電腦，', 'hero.b': '靜靜雞睇實。',
    'hero.lead': '一個托盤小程式：話你知啱啱複製咗乜、點解斷咗網，仲可以將複製嘅路徑加個 :mp4 變成轉好嘅檔案。', 'hero.download': '下載安裝程式', 'hero.docs': '睇說明',
    'hero.boundary': '呢個網站只係介紹同下載個程式，佢唔係個程式本身，亦唔會喺你部電腦度行任何嘢。', 'stats.areas': '範疇', 'stats.articles': '篇文章', 'stats.net': '要唔要上網', 'stats.never': '唔使',
    'tasks.tag': '由你想做嘅事開始', 'tasks.h': '我想…', 'all.tag': '每個功能，冇一個遺漏', 'boundary': '呢個網站係 Monitoring Tools 嘅說明，唔係個程式。',
    'boundary.more': '呢度冇嘢會行你裝咗嘅程式；所有示範都有標明。說明對應最新發佈嘅版本，文章話未做嘅就係未有。', 'set.tag': '設定 · 只存喺呢個瀏覽器', 'set.h': '將個網站', 'set.h2': '變成你嘅。'
  };
  var EN = {};
  var JOKES = {
    en: ['Your file was converted.', 'Converted. The original is safe, as always.', 'Converted. FFmpeg did the heavy lifting; we just pointed.', 'Done. Your video changed outfits and kept its name.', 'Done. The old file is untouched, unlike our snack drawer.', 'Done before your tea went cold. Barely.'],
    yue: ['檔案轉換好咗。', '轉好咗，原本個檔冇郁過。', '轉好咗，FFmpeg 出力，我哋負責指路。', '搞掂，條片換咗件衫，個名照舊。', '搞掂，原本個檔冇人掂過，同我哋啲零食唔同。', '搞掂，杯茶仲未凍呀。']
  };
  function initLanguage() {
    var lang = prefs.lang || 'en';
    $all('[data-t]').forEach(function (n) {
      var k = n.getAttribute('data-t'); if (!(k in EN)) EN[k] = n.textContent;
      var yue = T[k];
      if (lang === 'yue' && yue) { n.textContent = yue; n.setAttribute('lang', 'yue'); }
      else if (lang === 'both' && yue && !n.closest('nav, .seg, .tabstrip, button, label')) { n.innerHTML = esc(EN[k]) + '<span class="both" lang="yue">' + esc(yue) + '</span>'; n.removeAttribute('lang'); }
      else { n.textContent = EN[k]; n.removeAttribute('lang'); }
    });
    $all('[data-lang]').forEach(function (b) { b.setAttribute('aria-pressed', b.getAttribute('data-lang') === lang ? 'true' : 'false'); });
    var fs = $('#fun-sample');
    if (fs) { var en = JOKES.en[prefs.funEn], yu = JOKES.yue[prefs.funYue]; fs.innerHTML = lang === 'yue' ? '<span lang="yue">' + esc(yu) + '</span>' : lang === 'both' ? esc(en) + '<span class="both" lang="yue">' + esc(yu) + '</span>' : esc(en); }
  }
  document.addEventListener('click', function (e) { var b = e.target.closest('[data-lang]'); if (b) setPref('lang', b.getAttribute('data-lang')); });

  /* ---------- theme button and settings page controls ---------- */
  function toggleTheme() { setPref('theme', isDark() ? 'light' : 'dark'); notify(isDark() ? 'Dark theme on.' : 'Light theme on.', { label: 'Undo', run: toggleTheme }); }
  var tb = $('#theme-btn'); if (tb) tb.addEventListener('click', toggleTheme);
  function syncControls() {
    $all('[data-theme-set]').forEach(function (b) { b.setAttribute('aria-pressed', b.getAttribute('data-theme-set') === (prefs.theme || '') ? 'true' : 'false'); });
    $all('[data-text-set]').forEach(function (b) { b.setAttribute('aria-pressed', b.getAttribute('data-text-set') === (prefs.text || '') ? 'true' : 'false'); });
    $all('[data-dock]').forEach(function (b) { b.setAttribute('aria-pressed', b.getAttribute('data-dock') === (prefs.dock || '') ? 'true' : 'false'); });
    $all('[data-accent]').forEach(function (b) { b.setAttribute('aria-pressed', (prefs.accent || '#6FDBB4').toUpperCase() === b.getAttribute('data-accent').toUpperCase() ? 'true' : 'false'); });
    var rm = $('#rm'); if (rm) rm.setAttribute('aria-checked', prefs.motion === 'reduce' ? 'true' : 'false');
    var ds = $('#ds'); if (ds) ds.setAttribute('aria-checked', prefs.dimsum ? 'true' : 'false');
    var fe = $('#fun-en'), fy = $('#fun-yue');
    if (fe) { fe.value = prefs.funEn; $('#fun-en-v').textContent = prefs.funEn; }
    if (fy) { fy.value = prefs.funYue; $('#fun-yue-v').textContent = prefs.funYue; }
    var tr = $('#translator');
    if (tr) {
      var a = prefs.accent || '#6FDBB4', h = hsl(a), ink = isDark() ? '#070B09' : '#F3F8F5', cr = contrast(a, ink), used = prefs.accent ? readable(a) : null;
      tr.innerHTML = '<span>HEX</span><span>' + a.toUpperCase() + '</span><span>RGB</span><span>' + hexToRgb(a).join(', ') + '</span><span>HSL</span><span>' + h[0] + '° ' + h[1] + '% ' + h[2] + '%</span><span>Contrast</span><span class="' + (cr >= 4.5 ? 'ok' : 'bad') + '">' + cr.toFixed(1) + ':1 on this background, ' + (cr >= 4.5 ? 'readable as text' : 'too faint for text') + '</span>' + (used && used !== a.toUpperCase() ? '<span>Used for text</span><span class="ok">' + used + ', ' + contrast(used, ink).toFixed(1) + ':1, shifted so it can be read</span>' : '');
      var any = $('#accent-any'); if (any) any.value = a;
    }
  }
  document.addEventListener('click', function (e) {
    var b;
    if ((b = e.target.closest('[data-theme-set]'))) setPref('theme', b.getAttribute('data-theme-set'));
    else if ((b = e.target.closest('[data-text-set]'))) setPref('text', b.getAttribute('data-text-set'));
    else if ((b = e.target.closest('[data-dock]'))) setPref('dock', b.getAttribute('data-dock'));
    else if ((b = e.target.closest('[data-accent]'))) setPref('accent', b.getAttribute('data-accent'));
    else if (e.target.closest('#rm')) setPref('motion', prefs.motion === 'reduce' ? '' : 'reduce');
    else if (e.target.closest('#ds')) setPref('dimsum', !prefs.dimsum);
  });
  ['fun-en', 'fun-yue'].forEach(function (id) { var r = $('#' + id); if (r) r.addEventListener('input', function () { prefs[id === 'fun-en' ? 'funEn' : 'funYue'] = Number(r.value); applyPrefs(); initLanguage(); }); });
  var anyc = $('#accent-any'); if (anyc) anyc.addEventListener('input', function () { setPref('accent', anyc.value.toUpperCase()); });

  /* presets, export, import, reset */
  function renderPresets() {
    var ul = $('#presets'); if (!ul) return;
    var list = [{ n: 'Night shift', p: { theme: 'dark', accent: '#6FDBB4', text: '', motion: '' }, note: 'default' }, { n: 'Daylight desk', p: { theme: 'light', accent: '#9CCBFF', text: 'large', motion: '' }, note: 'light, 115 %' }, { n: 'Calm', p: { theme: '', accent: '#CFBCFF', text: '', motion: 'reduce' }, note: 'reduce motion' }].concat(load('mt-site-presets', []));
    ul.innerHTML = '';
    list.forEach(function (it) {
      var li = el('li'), b = el('button', { type: 'button', 'class': 'preset' });
      b.innerHTML = '<span class="pdot" style="background:' + esc(it.p.accent || '#6FDBB4') + '"></span>' + esc(it.n) + '<span class="mono">' + esc(it.note || 'yours') + '</span>';
      b.addEventListener('click', function () { Object.assign(prefs, it.p); applyPrefs(); initLanguage(); notify('Preset "' + it.n + '" applied.'); });
      li.appendChild(b); ul.appendChild(li);
    });
  }
  function bind(id, fn) { var n = $('#' + id); if (n) n.addEventListener('click', fn); }
  bind('preset-save', function () { var n = prompt('Name this preset'); if (!n) return; var l = load('mt-site-presets', []); l.push({ n: n.slice(0, 40), p: { theme: prefs.theme, accent: prefs.accent, text: prefs.text, motion: prefs.motion } }); save('mt-site-presets', l); renderPresets(); notify('Preset saved.'); });
  bind('export', function () {
    var data = {}; ['mt-site-prefs', 'mt-site-presets', 'mt-site-tabs', 'mt-site-look', 'mt-site-flags'].forEach(function (k) { data[k] = load(k, null); });
    var a = el('a', { download: 'monitoring-tools-site-settings.json', href: URL.createObjectURL(new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' })) }); document.body.appendChild(a); a.click(); a.remove();
    notify('Settings exported as a file. Locks and codes are left out.');
  });
  var imp = $('#import');
  if (imp) imp.addEventListener('change', function () {
    var f = imp.files[0]; if (!f) return; var rd = new FileReader();
    rd.onload = function () { try { var d = JSON.parse(rd.result); Object.keys(d).forEach(function (k) { if (/^mt-site-(prefs|presets|tabs|look|flags)$/.test(k) && d[k] != null) save(k, d[k]); }); prefs = Object.assign({}, DEFAULTS, load('mt-site-prefs', {})); applyPrefs(); initLanguage(); applyLooks(); renderPresets(); notify('Settings imported.'); } catch (e) { notify('That file is not a settings export: ' + e.message); } };
    rd.readAsText(f); imp.value = '';
  });
  bind('reset-all', function () {
    var before = JSON.stringify(prefs);
    prefs = Object.assign({}, DEFAULTS); applyPrefs(); initLanguage();
    notify('Settings reset to the defaults.', { label: 'Undo', run: function () { prefs = JSON.parse(before); applyPrefs(); initLanguage(); } });
  });

  /* ---------- per-element appearance editor ---------- */
  function keyOf(n) {
    if (n.id) return '#' + n.id;
    var parts = [];
    while (n && n !== document.body && parts.length < 6) { var p = n.parentNode, i = Array.prototype.indexOf.call(p.children, n); parts.unshift(n.tagName.toLowerCase() + ':' + i); if (n.id) { parts[0] = '#' + n.id; break; } n = p; }
    return here + '|' + parts.join('>');
  }
  function findByKey(k) {
    if (k.charAt(0) === '#') return document.getElementById(k.slice(1));
    var bits = k.split('|'); if (bits[0] !== here) return null; var path = bits[1].split('>'), n = document.body;
    for (var i = 0; i < path.length && n; i++) { var seg = path[i]; if (seg.charAt(0) === '#') { n = document.getElementById(seg.slice(1)); continue; } n = n.children[Number(seg.split(':')[1])]; }
    return n;
  }
  function applyLooks() {
    var looks = load('mt-site-look', {});
    Object.keys(looks).forEach(function (k) { var n = findByKey(k), v = looks[k]; if (!n) return; if (v.color) n.style.color = v.color; if (v.bg) n.style.background = v.bg; if (v.size) n.style.fontSize = v.size + 'em'; if (v.radius != null) n.style.borderRadius = v.radius + 'px'; n.setAttribute('data-edited', ''); });
  }
  function initAppearanceEditor(target) {
    var k = keyOf(target), looks = load('mt-site-look', {}), v = looks[k] || {};
    sheet('Edit appearance', function (body) {
      body.innerHTML = '<p class="hint">Changes this one element on this site, in this browser. <span class="mono">' + esc(target.tagName.toLowerCase()) + '</span></p>' +
        '<label>Text colour <input type="color" id="ae-c" value="' + esc(v.color || '#E4EBE6') + '"></label>' +
        '<label>Background <input type="color" id="ae-b" value="' + esc(v.bg || '#121916') + '"></label>' +
        '<label>Text size <input type="range" id="ae-s" min="0.8" max="1.6" step="0.05" value="' + (v.size || 1) + '"></label>' +
        '<label>Corner radius <input type="range" id="ae-r" min="0" max="40" step="1" value="' + (v.radius != null ? v.radius : 16) + '"></label>' +
        '<div class="btn-row"><button type="button" class="btn tonal" id="ae-reset">Reset this element</button><button type="button" class="btn filled" id="ae-done">Done</button></div>';
      function upd(field, val) { looks = load('mt-site-look', {}); looks[k] = Object.assign(looks[k] || {}, (function () { var o = {}; o[field] = val; return o; })()); save('mt-site-look', looks); applyLooks(); }
      $('#ae-c', body).addEventListener('input', function (e) { upd('color', e.target.value); });
      $('#ae-b', body).addEventListener('input', function (e) { upd('bg', e.target.value); });
      $('#ae-s', body).addEventListener('input', function (e) { upd('size', Number(e.target.value)); });
      $('#ae-r', body).addEventListener('input', function (e) { upd('radius', Number(e.target.value)); });
      $('#ae-reset', body).addEventListener('click', function () { looks = load('mt-site-look', {}); delete looks[k]; save('mt-site-look', looks); target.removeAttribute('style'); target.removeAttribute('data-edited'); closeSheet(); notify('Element reset.'); });
      $('#ae-done', body).addEventListener('click', closeSheet);
    });
  }
  bind('reset-elements', function () { save('mt-site-look', {}); $all('[data-edited]').forEach(function (n) { n.removeAttribute('style'); n.removeAttribute('data-edited'); }); notify('Every edited element is back to normal.'); });

  /* ---------- a small modal sheet used by the editors, locks and the ladder ---------- */
  var sh = $('#sheet'), shLast = null;
  function sheet(title, fill) { if (!sh) return; shLast = document.activeElement; $('#sheet-title').textContent = title; var b = $('#sheet-body'); b.innerHTML = ''; fill(b); sh.hidden = false; var f = $('input, button', b); if (f) f.focus(); }
  function closeSheet() { if (!sh) return; sh.hidden = true; if (shLast && shLast.focus) shLast.focus(); }
  if (sh) { sh.addEventListener('click', function (e) { if (e.target === sh) closeSheet(); }); document.addEventListener('keydown', function (e) { if (e.key === 'Escape' && !sh.hidden) closeSheet(); }); }

  /* ---------- toy locks with the unlock ladder ---------- */
  function hashPin(pin, salt) {
    var data = new TextEncoder().encode(salt + ':' + pin);
    if (window.crypto && crypto.subtle) return crypto.subtle.digest('SHA-256', data).then(function (b) { return Array.prototype.map.call(new Uint8Array(b), function (x) { return ('0' + x.toString(16)).slice(-2); }).join(''); });
    return Promise.resolve(String(data.length));
  }
  function initLocks() {
    var locks = load('mt-site-locks', {});
    Object.keys(locks).forEach(function (k) { var n = findByKey(k); if (n && !n.querySelector(':scope > .lockcover')) cover(n, k); });
    var ul = $('#locks');
    if (ul) {
      ul.innerHTML = '';
      var keys = Object.keys(locks);
      if (!keys.length) ul.appendChild(el('li', { 'class': 'hint' }, 'Nothing is locked.'));
      keys.forEach(function (k) { var li = el('li'); li.appendChild(el('span', { 'class': 'mono' }, locks[k].label)); var b = el('button', { type: 'button', 'class': 'btn tonal' }, 'Unlock'); b.addEventListener('click', function () { askUnlock(k, null); }); li.appendChild(b); ul.appendChild(li); });
    }
  }
  function cover(n, k) {
    n.classList.add('locked');
    var c = el('button', { type: 'button', 'class': 'lockcover', 'aria-label': 'Locked. Press to unlock.' });
    c.innerHTML = '<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><path d="M7 11V7a5 5 0 0 1 10 0v4M5 11h14v10H5z"/></svg><span>Locked</span>';
    c.addEventListener('click', function (e) { e.preventDefault(); e.stopPropagation(); askUnlock(k, n); });
    n.appendChild(c);
  }
  function lockElement(target) {
    var k = keyOf(target), label = (target.getAttribute('aria-label') || target.textContent || target.tagName).trim().replace(/\s+/g, ' ').slice(0, 40);
    sheet('Lock this element', function (b) {
      b.innerHTML = '<p class="hint">A toy lock for "' + esc(label) + '", kept in this browser. It protects nothing. Choose a code of 4 to 8 digits.</p><label>Code <input id="lk-pin" class="mono" inputmode="numeric" pattern="[0-9]{4,8}" maxlength="8" autocomplete="off"></label><div class="btn-row"><button type="button" class="btn filled" id="lk-go">Lock</button></div>';
      $('#lk-go', b).addEventListener('click', function () {
        var pin = $('#lk-pin', b).value; if (!/^\d{4,8}$/.test(pin)) { notify('Use 4 to 8 digits.'); return; }
        var salt = Math.random().toString(36).slice(2);
        hashPin(pin, salt).then(function (h) { var l = load('mt-site-locks', {}); l[k] = { h: h, s: salt, label: label }; save('mt-site-locks', l); closeSheet(); initLocks(); notify('Locked "' + label + '".'); });
      });
    });
  }
  function release(k, n) { var l = load('mt-site-locks', {}); delete l[k]; save('mt-site-locks', l); n = n || findByKey(k); if (n) { n.classList.remove('locked'); var c = n.querySelector(':scope > .lockcover'); if (c) c.remove(); n.classList.add('unlocked'); setTimeout(function () { n.classList.remove('unlocked'); }, 700); } initLocks(); notify('Unlocked.'); }
  function askUnlock(k, n) {
    var l = load('mt-site-locks', {})[k]; if (!l) return;
    var waitUntil = load('mt-site-clock', 0);
    sheet('Unlock', function (b) {
      b.innerHTML = '<p class="hint">"' + esc(l.label) + '"</p><div class="pin-dots" id="pd"></div><div class="keypad">' + '123456789'.split('').concat(['⌫', '0', '↵']).map(function (d) { return '<button type="button" class="key mono" data-k="' + d + '">' + d + '</button>'; }).join('') + '</div>' +
        '<p class="hint">Forgot it? ' + (Date.now() < waitUntil ? 'The ladder is resting until ' + new Date(waitUntil).toLocaleTimeString() + '.' : '<button type="button" class="linkish" id="go-ladder">Climb the unlock ladder</button>') + '</p>';
      var pin = '', pd = $('#pd', b);
      function dots(state) { pd.innerHTML = ''; for (var i = 0; i < Math.max(4, pin.length); i++) pd.appendChild(el('span', { 'class': 'pd' + (i < pin.length ? ' on' : '') + (state ? ' ' + state : '') })); }
      dots();
      $all('[data-k]', b).forEach(function (kb) { kb.addEventListener('click', function () {
        var d = kb.getAttribute('data-k');
        if (d === '⌫') { pin = pin.slice(0, -1); dots(); return; }
        if (d !== '↵') { if (pin.length < 8) pin += d; dots(); if (pin.length < 4) return; if (d !== '↵' && pin.length < 8) { /* wait for Enter or a longer code */ } }
        if (d === '↵' || pin.length === 8) hashPin(pin, l.s).then(function (h) { if (h === l.h) { closeSheet(); release(k, n); } else { b.classList.remove('shake'); void b.offsetWidth; b.classList.add('shake'); pin = ''; dots('bad'); } });
      }); });
      var gl = $('#go-ladder', b); if (gl) gl.addEventListener('click', function () { initLadder(function () { release(k, n); }); });
    });
  }
  /* The unlock ladder: dim sum, ten easy sums, whack-a-mole, then the clock. The same rungs as the app. */
  function initLadder(onWin) {
    var dishes = window.SITE_DISHES || [];
    function rungDimSum(fails) {
      if (fails >= 5 || dishes.length < 4) return rungSums();
      var pick = [], used = {}; while (pick.length < 4) { var i = Math.floor(Math.random() * dishes.length); if (!used[i]) { used[i] = 1; pick.push(dishes[i]); } }
      var ans = pick[Math.floor(Math.random() * 4)];
      sheet('Rung 1 of 4: dim sum', function (b) {
        b.innerHTML = '<p>Which dish is <strong lang="yue">' + esc(ans[1]) + '</strong>?</p><div class="ladder-opts">' + pick.map(function (p, i) { return '<button type="button" class="btn tonal" data-i="' + i + '">' + esc(p[0]) + '</button>'; }).join('') + '</div><p class="hint">' + (5 - fails) + ' tries before the sums.</p>';
        $all('[data-i]', b).forEach(function (x) { x.addEventListener('click', function () { if (pick[Number(x.getAttribute('data-i'))] === ans) { closeSheet(); onWin(); } else rungDimSum(fails + 1); }); });
      });
    }
    function rungSums() {
      var qs = []; for (var i = 0; i < 10; i++) { var a = 2 + Math.floor(Math.random() * 9), c = 2 + Math.floor(Math.random() * 9); qs.push([a, c]); }
      sheet('Rung 2 of 4: ten easy sums', function (b) {
        b.innerHTML = '<form id="sums" class="sums">' + qs.map(function (q, i) { return '<label class="mono">' + q[0] + ' + ' + q[1] + ' = <input inputmode="numeric" data-i="' + i + '" required size="3"></label>'; }).join('') + '<button class="btn filled">Check</button></form>';
        $('#sums', b).addEventListener('submit', function (e) { e.preventDefault(); var ok = $all('[data-i]', b).every(function (x, i) { return Number(x.value) === qs[i][0] + qs[i][1]; }); if (ok) { closeSheet(); onWin(); } else rungMole(); });
      });
    }
    function rungMole() {
      sheet('Rung 3 of 4: whack-a-mole', function (b) {
        b.innerHTML = '<p class="hint">15 seconds. Press the square that says MOLE, or its number key. Hit 8 to win.</p><div class="mole" id="mole">' + [1, 2, 3, 4, 5, 6, 7, 8, 9].map(function (n) { return '<button type="button" class="key mono" data-m="' + n + '">' + n + '</button>'; }).join('') + '</div><p class="mono" id="mole-s">0 hits</p>';
        var hits = 0, up = 0, end = Date.now() + 15000;
        function pop() { $all('[data-m]', b).forEach(function (x) { x.textContent = x.getAttribute('data-m'); x.classList.remove('up'); }); up = 1 + Math.floor(Math.random() * 9); var u = $('[data-m="' + up + '"]', b); if (u) { u.textContent = 'MOLE'; u.classList.add('up'); } }
        function hit(n) { if (n === up) { hits++; $('#mole-s', b).textContent = hits + ' hits'; if (hits >= 8) { done(); closeSheet(); onWin(); return; } pop(); } }
        function key(e) { if (/^[1-9]$/.test(e.key)) hit(Number(e.key)); }
        function done() { clearInterval(iv); document.removeEventListener('keydown', key); }
        $all('[data-m]', b).forEach(function (x) { x.addEventListener('click', function () { hit(Number(x.getAttribute('data-m'))); }); });
        document.addEventListener('keydown', key);
        var iv = setInterval(function () { if (Date.now() > end) { done(); rungClock(); } else pop(); }, 900); pop();
      });
    }
    function rungClock() { save('mt-site-clock', Date.now() + 10 * 60000); sheet('Rung 4 of 4: the clock', function (b) { b.innerHTML = '<p>The ladder rests for 10 minutes. You are exactly where you started; the lock is unchanged.</p><button type="button" class="btn filled" id="ok">OK</button>'; $('#ok', b).addEventListener('click', closeSheet); }); }
    rungDimSum(0);
  }

  /* ---------- toy authenticator (TOTP, RFC 6238) ---------- */
  function b32(s) { var a = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ234567', bits = '', out = []; s = s.toUpperCase().replace(/[^A-Z2-7]/g, ''); for (var i = 0; i < s.length; i++) bits += ('00000' + a.indexOf(s[i]).toString(2)).slice(-5); for (var j = 0; j + 8 <= bits.length; j += 8) out.push(parseInt(bits.slice(j, j + 8), 2)); return new Uint8Array(out); }
  function totp(secret) {
    var counter = Math.floor(Date.now() / 30000), buf = new ArrayBuffer(8), v = new DataView(buf); v.setUint32(4, counter);
    return crypto.subtle.importKey('raw', b32(secret), { name: 'HMAC', hash: 'SHA-1' }, false, ['sign']).then(function (key) { return crypto.subtle.sign('HMAC', key, buf); }).then(function (sig) {
      var h = new Uint8Array(sig), o = h[h.length - 1] & 15, code = ((h[o] & 127) << 24 | h[o + 1] << 16 | h[o + 2] << 8 | h[o + 3]) % 1000000; return ('000000' + code).slice(-6);
    });
  }
  function initAuthenticator() {
    var ul = $('#auth'); if (!ul || !(window.crypto && crypto.subtle)) return;
    function draw() {
      var list = load('mt-site-auth', []); ul.innerHTML = '';
      if (!list.length) ul.appendChild(el('li', { 'class': 'hint' }, 'No codes yet.'));
      list.forEach(function (it, i) {
        var li = el('li'), left = 30 - Math.floor(Date.now() / 1000) % 30;
        li.innerHTML = '<svg class="ring" viewBox="0 0 36 36" aria-hidden="true"><circle cx="18" cy="18" r="15"/><circle cx="18" cy="18" r="15" class="arc" style="stroke-dashoffset:' + (94 * (1 - left / 30)).toFixed(1) + '"/></svg><span class="an">' + esc(it.n) + '</span><span class="code mono" aria-live="off">…</span><button type="button" class="btn tonal" data-del="' + i + '">Remove</button>';
        totp(it.s).then(function (c) { $('.code', li).textContent = c.slice(0, 3) + ' ' + c.slice(3); });
        ul.appendChild(li);
      });
      $all('[data-del]', ul).forEach(function (b) { b.addEventListener('click', function () { var l = load('mt-site-auth', []); l.splice(Number(b.getAttribute('data-del')), 1); save('mt-site-auth', l); draw(); }); });
    }
    bind('auth-add', function () { var n = $('#auth-name').value.trim() || 'Code', s = $('#auth-secret').value.trim(); if (b32(s).length < 10) { notify('That secret is too short to be Base32.'); return; } var l = load('mt-site-auth', []); l.push({ n: n.slice(0, 30), s: s }); save('mt-site-auth', l); $('#auth-secret').value = ''; draw(); notify('Code added.'); });
    draw(); setInterval(draw, 1000);
  }

  /* ---------- search with plain and regex modes and an anchored builder ---------- */
  var q = $('#q'), modeBtn = $('#mode'), bBtn = $('#builder-btn'), builder = $('#builder'), list = $('#results');
  var rxIn = $('#rx-pattern'), rxStatus = $('#rx-status'), sample = $('#rx-sample'), sampleOut = $('#rx-out'), explain = $('#rx-explain');
  var index = window.SITE_INDEX || [];
  var sel = -1, shown = [], regexMode = false;
  var flags = load('mt-site-flags', { i: true, m: false, s: false, u: false });
  var saved = load('mt-site-search', {});
  if (saved && saved.regex) regexMode = true;
  function flagString() { return ['i', 'm', 's', 'u'].filter(function (f) { return flags[f]; }).join(''); }
  function syncFlagBoxes() { $all('[data-flag]').forEach(function (b) { b.checked = !!flags[b.getAttribute('data-flag')]; }); }
  function setStatus(msg, bad) { if (rxStatus) { rxStatus.textContent = msg; rxStatus.classList.toggle('bad', !!bad); } }
  function compile() { try { return { rx: new RegExp(q.value, flagString()) }; } catch (e) { return { error: e.message }; } }
  function highlight(text, matcher) { var m = matcher(text); if (!m) return esc(text); return esc(text.slice(0, m.index)) + '<mark>' + esc(m[0]) + '</mark>' + esc(text.slice(m.index + m[0].length)); }
  function closeList() { list.hidden = true; q.setAttribute('aria-expanded', 'false'); q.removeAttribute('aria-activedescendant'); sel = -1; }
  function mark() { $all('a', list).forEach(function (a, i) { a.setAttribute('aria-selected', i === sel ? 'true' : 'false'); if (i === sel) { a.scrollIntoView({ block: 'nearest' }); q.setAttribute('aria-activedescendant', a.id); } }); }
  function explainPattern(p) {
    if (!explain) return;
    var parts = [], names = { '^': 'start of the text (or line with m)', '$': 'end of the text (or line with m)', '\\b': 'a word edge', '\\d': 'one digit', '\\w': 'one word character', '\\s': 'one space character', '.': 'any one character', '|': 'or', '*': 'repeat any number of times', '+': 'repeat one or more times', '?': 'optional' };
    var re = /\\[bdwsBDWS.]|\[[^\]]*\]|\((?:\?:)?|\)|[|^$.*+?]|\\.|[^\\[\]()|^$.*+?]+/g, m;
    while ((m = re.exec(p)) && parts.length < 30) { var t = m[0]; parts.push('<li><code>' + esc(t) + '</code> ' + esc(names[t] || (t.charAt(0) === '[' ? 'one character from the set' : t.charAt(0) === '(' ? 'start a group' : t === ')' ? 'end the group' : 'the text "' + t + '"')) + '</li>'); }
    explain.innerHTML = parts.join('') || '<li>Type a pattern to see it explained, piece by piece.</li>';
  }
  function run() {
    var value = q.value.trim(); q.parentNode.classList.remove('invalid'); explainPattern(q.value);
    if (!value) { closeList(); setStatus(regexMode ? 'Type a pattern to search.' : 'Switch regex mode on to search with this pattern.'); updateSample(); return; }
    var test, matcher, stoppedEarly = false;
    if (regexMode) {
      var c = compile();
      if (c.error) { q.parentNode.classList.add('invalid'); setStatus('This pattern is not valid: ' + c.error, true); list.hidden = false; q.setAttribute('aria-expanded', 'true'); list.innerHTML = '<li class="empty" role="presentation">The pattern is not valid, so nothing is filtered. ' + esc(c.error) + '</li>'; shown = []; return; }
      var rx = c.rx; test = function (e) { rx.lastIndex = 0; return rx.test(e.t + '\n' + e.c + '\n' + e.x); }; matcher = function (t) { rx.lastIndex = 0; return rx.exec(t); };
    } else {
      var words = value.toLowerCase().split(/\s+/);
      test = function (e) { var h = (e.t + ' ' + e.c + ' ' + e.x).toLowerCase(); return words.every(function (w) { return h.indexOf(w) >= 0; }); };
      matcher = function (t) { var l = t.toLowerCase(); for (var i = 0; i < words.length; i++) { var k = l.indexOf(words[i]); if (k >= 0) return { index: k, 0: t.substr(k, words[i].length) }; } return null; };
    }
    var started = Date.now(), hits = [];
    for (var i = 0; i < index.length; i++) { if (Date.now() - started > 150) { stoppedEarly = true; break; } if (test(index[i])) hits.push(index[i]); }
    var titleHit = function (e) { var m = matcher(e.t); return m ? 0 : 1; }; hits.sort(function (a, b) { return titleHit(a) - titleHit(b); });
    shown = hits.slice(0, 40); list.hidden = false; q.setAttribute('aria-expanded', 'true');
    if (!shown.length) list.innerHTML = '<li class="empty" role="presentation">Nothing matches' + (stoppedEarly ? ' before the time limit' : '') + '.</li>';
    else list.innerHTML = shown.map(function (e, i) { return '<li role="presentation"><a role="option" id="r' + i + '" aria-selected="false" href="' + esc(root + e.u) + '"><span class="r-title">' + highlight(e.t, matcher) + '</span><span class="r-meta">' + esc(e.c) + ' · ' + esc(e.x) + '</span></a></li>'; }).join('');
    sel = shown.length ? 0 : -1; mark();
    var msg = hits.length + (hits.length === 1 ? ' result' : ' results') + (hits.length > shown.length ? ', showing the first ' + shown.length : '') + '.'; if (stoppedEarly) msg += ' The search hit its time limit and stopped early.'; setStatus(msg); updateSample();
  }
  function updateSample() {
    if (!sampleOut) return; var text = sample.value; if (!text) { sampleOut.textContent = ''; return; }
    var c = compile(); if (!q.value || c.error) { sampleOut.textContent = text; return; }
    var rx = new RegExp(c.rx.source, flagString().replace('g', '') + 'g'), out = '', last = 0, n = 0, t0 = Date.now(), m;
    while ((m = rx.exec(text)) && n < 500) { if (Date.now() - t0 > 100) break; if (m[0] === '') { rx.lastIndex++; continue; } out += esc(text.slice(last, m.index)) + '<mark>' + esc(m[0]) + '</mark>'; last = m.index + m[0].length; n++; }
    sampleOut.innerHTML = out + esc(text.slice(last)); if (rxStatus && regexMode) rxStatus.textContent += ' ' + n + (n === 1 ? ' match' : ' matches') + ' in your sample.';
  }
  function setMode(on) { regexMode = on; modeBtn.setAttribute('aria-pressed', on ? 'true' : 'false'); save('mt-site-search', { regex: on }); q.placeholder = on ? 'Regular expression, for example ^search|regex' : 'Search features, tasks and screenshots (press /)'; run(); }
  if (q && modeBtn) {
    syncFlagBoxes(); modeBtn.setAttribute('aria-pressed', regexMode ? 'true' : 'false'); if (regexMode) q.placeholder = 'Regular expression, for example ^search|regex';
    modeBtn.addEventListener('click', function () { setMode(!regexMode); q.focus(); });
    q.addEventListener('input', function () { if (rxIn && rxIn.value !== q.value) rxIn.value = q.value; run(); });
    q.addEventListener('focus', function () { if (q.value) run(); });
    q.addEventListener('keydown', function (e) {
      if (e.key === 'ArrowDown') { e.preventDefault(); if (list.hidden) run(); else { sel = Math.min(sel + 1, shown.length - 1); mark(); } }
      else if (e.key === 'ArrowUp') { e.preventDefault(); sel = Math.max(sel - 1, 0); mark(); }
      else if (e.key === 'Enter' && sel >= 0 && shown[sel]) { e.preventDefault(); location.href = root + shown[sel].u; }
      else if (e.key === 'Escape') { if (!list.hidden) closeList(); else if (q.value) { q.value = ''; if (rxIn) rxIn.value = ''; run(); } }
    });
    document.addEventListener('click', function (e) { if (!e.target.closest('.search')) { closeList(); } });
    bBtn.addEventListener('click', function () { var open = builder.hidden; builder.hidden = !open; bBtn.setAttribute('aria-expanded', open ? 'true' : 'false'); if (open) { rxIn.value = q.value; rxIn.focus(); } });
    rxIn.addEventListener('input', function () { q.value = rxIn.value; if (!regexMode) setMode(true); else run(); });
    $all('[data-tok]').forEach(function (t) { t.addEventListener('click', function () { var v = t.getAttribute('data-tok'), s = rxIn.selectionStart || rxIn.value.length; rxIn.value = rxIn.value.slice(0, s) + v + rxIn.value.slice(s); rxIn.focus(); rxIn.dispatchEvent(new Event('input')); }); });
    $all('.builder-tabs [data-view]').forEach(function (t) { t.addEventListener('click', function () { var v = t.getAttribute('data-view'); $all('.builder-tabs [data-view]').forEach(function (x) { x.setAttribute('aria-selected', x === t ? 'true' : 'false'); x.setAttribute('aria-pressed', x === t ? 'true' : 'false'); }); $all('#builder [data-pane]').forEach(function (p) { p.hidden = p.getAttribute('data-pane') !== v; }); }); });
    $all('[data-flag]').forEach(function (b) { b.addEventListener('change', function () { flags[b.getAttribute('data-flag')] = b.checked; save('mt-site-flags', flags); run(); }); });
    sample.addEventListener('input', function () { run(); });
    if (rxIn) rxIn.value = '';
    explainPattern('');
  }

  /* ---------- settings search with its own regex toggle ---------- */
  var sq = $('#set-q');
  if (sq) {
    var srx = false, sb = $('#set-rx');
    var filterSet = function () {
      var v = sq.value.trim(), rows = $all('.set-row, .set-group'), m = null, n = 0;
      if (srx && v) { try { m = new RegExp(v, 'i'); sq.classList.remove('invalid'); } catch (e) { sq.classList.add('invalid'); $('#set-count').textContent = 'Not a valid pattern: ' + e.message; return; } }
      $all('.set-row').forEach(function (r) { var t = r.textContent.toLowerCase(), on = !v || (m ? m.test(r.textContent) : v.toLowerCase().split(/\s+/).every(function (w) { return t.indexOf(w) >= 0; })); r.hidden = !on; if (on && v) n++; });
      $('#set-count').textContent = v ? n + ' matching' : '';
    };
    sq.addEventListener('input', filterSet);
    sb.addEventListener('click', function () { srx = !srx; sb.setAttribute('aria-pressed', srx ? 'true' : 'false'); filterSet(); });
    if (location.hash) { var t = $(location.hash); if (t) pulse(t); }
  }

  /* ---------- command palette (Ctrl+Shift+F): pages, sections, settings, actions and elements on this page ---------- */
  function pulse(n) { if (!n) return; var d = n.closest('details'); if (d) d.open = true; n.scrollIntoView({ block: 'center', behavior: reduceMotion() ? 'auto' : 'smooth' }); n.classList.remove('pulse'); void n.offsetWidth; n.classList.add('pulse'); setTimeout(function () { n.classList.remove('pulse'); }, 2200); }
  var ACTIONS = [];
  function initPalette() {
    var box = $('#palette'), pq = $('#pal-q'), pl = $('#pal-list'); if (!box) return;
    var kind = '', items = [], psel = 0, last = null;
    function gather() {
      var out = [];
      $all('main h1, main h2, main h3, main [id], main a.btn, main .area, main .task-card').forEach(function (n) {
        if (n.closest('.toc, .results, #palette')) return; var t = (n.getAttribute('aria-label') || n.textContent || '').trim().replace(/\s+/g, ' ').replace(/^#\s*/, ''); if (!t || t.length > 90) return;
        out.push({ t: t, c: 'On this page', k: 'here', n: n });
      });
      index.forEach(function (e) { out.push({ t: e.t, c: e.c, k: e.c === 'Setting' ? 'setting' : 'page', u: root + e.u }); });
      ACTIONS.forEach(function (a) { out.push({ t: a.t, c: 'Action · ' + a.key, k: 'action', run: a.run }); });
      var seen = {}; return out.filter(function (o) { var key = o.k + o.t; if (seen[key]) return false; seen[key] = 1; return true; });
    }
    function draw() {
      var v = pq.value.trim().toLowerCase(), all = gather();
      items = all.filter(function (o) { return (!kind || o.k === kind) && (!v || (o.t + ' ' + o.c).toLowerCase().indexOf(v) >= 0); }).slice(0, 40);
      psel = Math.min(psel, Math.max(0, items.length - 1));
      pl.innerHTML = items.length ? items.map(function (o, i) { return '<li role="option" id="p' + i + '" aria-selected="' + (i === psel) + '" style="--i:' + Math.min(i, 10) + '"><span class="pk">' + esc(o.k === 'here' ? 'Element' : o.k === 'setting' ? 'Setting' : o.k === 'action' ? 'Action' : 'Page') + '</span><span class="pt">' + esc(o.t) + '</span><span class="pc mono">' + esc(o.c) + '</span></li>'; }).join('') : '<li class="empty">Nothing matches.</li>';
      pq.setAttribute('aria-activedescendant', items.length ? 'p' + psel : '');
      $all('li[role=option]', pl).forEach(function (li, i) { li.addEventListener('click', function () { psel = i; go(); }); });
    }
    function open() { last = document.activeElement; box.hidden = false; pq.value = ''; psel = 0; draw(); pq.focus(); }
    function close() { box.hidden = true; if (last && last.focus) last.focus(); }
    function go() { var o = items[psel]; if (!o) return; close(); if (o.n) pulse(o.n); else if (o.run) o.run(); else location.href = o.u; }
    pq.addEventListener('input', function () { psel = 0; draw(); });
    pq.addEventListener('keydown', function (e) { if (e.key === 'ArrowDown') { e.preventDefault(); psel = Math.min(psel + 1, items.length - 1); draw(); $('#p' + psel).scrollIntoView({ block: 'nearest' }); } else if (e.key === 'ArrowUp') { e.preventDefault(); psel = Math.max(psel - 1, 0); draw(); $('#p' + psel).scrollIntoView({ block: 'nearest' }); } else if (e.key === 'Enter') { e.preventDefault(); go(); } else if (e.key === 'Escape') close(); });
    box.addEventListener('click', function (e) { if (e.target === box) close(); });
    $all('.kchip', box).forEach(function (c) { c.addEventListener('click', function () { kind = c.getAttribute('data-k'); $all('.kchip', box).forEach(function (x) { x.setAttribute('aria-pressed', x === c ? 'true' : 'false'); }); psel = 0; draw(); pq.focus(); }); });
    initPalette.open = open;
  }

  /* ---------- browser-style tabs: open pages, pins, groups by area, reordering, recently closed ---------- */
  var GROUP_COLOURS = { home: 'var(--accent)', docs: 'var(--sky)', status: 'var(--amber)', shots: 'var(--violet)', settings: 'var(--on-2)', diagram: 'var(--sky)' };
  function initTabs() {
    var strip = $('#tabstrip'); if (!strip) return;
    var st = load('mt-site-tabs', { tabs: [], closed: [], collapsed: {} });
    var title = document.body.getAttribute('data-title') || document.title, cat = document.body.getAttribute('data-cat') || '';
    if (!st.tabs.some(function (t) { return t.p === here; })) {
      var at = -1; st.tabs.forEach(function (t, i) { if (cat && t.g === cat && !t.pin) at = i; });
      if (at >= 0) st.tabs.splice(at + 1, 0, { p: here, t: title, g: cat }); else st.tabs.push({ p: here, t: title, g: cat });
    }
    st.tabs.forEach(function (t) { if (t.p === here) { t.t = title; t.g = cat; } });
    if (st.tabs.length > 30) st.tabs = st.tabs.filter(function (t, i) { return t.pin || t.p === here || i >= st.tabs.length - 30; });
    save('mt-site-tabs', st);
    function persist() { save('mt-site-tabs', st); }
    function draw() {
      strip.innerHTML = '';
      var rest = st.tabs.filter(function (t) { return !t.pin; }), firstAt = {};
      rest.forEach(function (t, i) { if (!(t.g in firstAt)) firstAt[t.g] = i; });
      rest = rest.map(function (t, i) { return [t, i]; }).sort(function (x, y) { return (firstAt[x[0].g] - firstAt[y[0].g]) || (x[1] - y[1]); }).map(function (x) { return x[0]; });
      var ordered = st.tabs.filter(function (t) { return t.pin; }).concat(rest);
      var lastG = null;
      ordered.forEach(function (t) {
        if (!t.pin && t.g && t.g !== lastG && ordered.filter(function (x) { return !x.pin && x.g === t.g; }).length > 1) {
          var gb = el('button', { type: 'button', 'class': 'tgroup', 'aria-expanded': st.collapsed[t.g] ? 'false' : 'true', style: '--g:' + (GROUP_COLOURS[t.g] || 'var(--sky)') });
          gb.textContent = ((window.SITE_CATS || {})[t.g] || t.g) + ' · ' + ordered.filter(function (x) { return !x.pin && x.g === t.g; }).length;
          gb.addEventListener('click', function () { st.collapsed[t.g] = !st.collapsed[t.g]; persist(); draw(); });
          strip.appendChild(gb);
        }
        lastG = t.pin ? lastG : t.g;
        if (!t.pin && st.collapsed[t.g] && t.p !== here) return;
        var a = el('a', { href: root + t.p, role: 'tab', 'class': 'tab' + (t.pin ? ' pin' : ''), 'aria-selected': t.p === here ? 'true' : 'false', draggable: 'true', 'data-p': t.p, style: '--g:' + (GROUP_COLOURS[t.g] || 'var(--line)'), title: t.t });
        a.appendChild(el('span', { 'class': 'tg', 'aria-hidden': 'true' }));
        a.appendChild(el('span', { 'class': 'tt' }, t.pin ? t.t.slice(0, 1) : t.t));
        if (!t.pin) { var x = el('button', { type: 'button', 'class': 'tx', 'aria-label': 'Close ' + t.t }, '×'); x.addEventListener('click', function (e) { e.preventDefault(); e.stopPropagation(); closeTab(t.p); }); a.appendChild(x); }
        a.addEventListener('auxclick', function (e) { if (e.button === 1 && !t.pin) { e.preventDefault(); closeTab(t.p); } });
        a.addEventListener('dragstart', function (e) { e.dataTransfer.setData('text/plain', t.p); a.classList.add('dragging'); });
        a.addEventListener('dragend', function () { a.classList.remove('dragging'); });
        a.addEventListener('dragover', function (e) { e.preventDefault(); a.classList.add('over'); });
        a.addEventListener('dragleave', function () { a.classList.remove('over'); });
        a.addEventListener('drop', function (e) { e.preventDefault(); var from = e.dataTransfer.getData('text/plain'); move(from, t.p); });
        strip.appendChild(a);
      });
      var more = el('button', { type: 'button', 'class': 'tmore', 'aria-haspopup': 'dialog', 'aria-label': 'Find a tab' }, 'Tabs ▾ ' + st.tabs.length);
      more.addEventListener('click', finder); strip.appendChild(more);
    }
    function move(from, to) { var i = st.tabs.findIndex(function (t) { return t.p === from; }), j = st.tabs.findIndex(function (t) { return t.p === to; }); if (i < 0 || j < 0 || i === j) return; var it = st.tabs.splice(i, 1)[0]; st.tabs.splice(j, 0, it); persist(); draw(); }
    function closeTab(p) { var i = st.tabs.findIndex(function (t) { return t.p === p; }); if (i < 0) return; var t = st.tabs.splice(i, 1)[0]; st.closed.unshift(t); st.closed = st.closed.slice(0, 25); persist(); if (p === here) { var nxt = st.tabs[i] || st.tabs[i - 1]; location.href = root + (nxt ? nxt.p : 'index.html'); return; } draw(); notify('Closed "' + t.t + '".', { label: 'Undo', run: function () { st.closed.shift(); st.tabs.splice(i, 0, t); persist(); draw(); } }); }
    function finder() {
      sheet('Find a tab', function (b) {
        b.innerHTML = '<div class="seg" role="tablist" aria-label="Find a tab"><button type="button" data-w="open" aria-pressed="true">Open tabs</button><button type="button" data-w="closed" aria-pressed="false">Recently closed</button><button type="button" data-w="groups" aria-pressed="false">Groups</button><button type="button" data-w="all" aria-pressed="false">Every page</button></div><label class="sr-only" for="tf-q">Filter</label><input id="tf-q" placeholder="Type part of a title, or a regular expression"><ul class="tf-list" id="tf-list"></ul>';
        var w = 'open', fq = $('#tf-q', b), fl = $('#tf-list', b);
        function src() {
          if (w === 'open') return st.tabs.map(function (t) { return { t: t.t, u: root + t.p, c: t.pin ? 'pinned' : t.g }; });
          if (w === 'closed') return st.closed.map(function (t) { return { t: t.t, u: root + t.p, c: 'closed' }; });
          if (w === 'groups') { var g = {}; st.tabs.forEach(function (t) { if (t.g) (g[t.g] = g[t.g] || []).push(t); }); return Object.keys(g).map(function (k) { return { t: ((window.SITE_CATS || {})[k] || k) + ' (' + g[k].length + ')', u: root + g[k][0].p, c: 'group' }; }); }
          return index.filter(function (e) { return e.u.indexOf('#') < 0; }).map(function (e) { return { t: e.t, u: root + e.u, c: e.c }; });
        }
        function d() { var v = fq.value, m = null; try { m = v ? new RegExp(v, 'i') : null; } catch (e) { m = null; } var l = src().filter(function (o) { return !v || (m ? m.test(o.t) : o.t.toLowerCase().indexOf(v.toLowerCase()) >= 0); }).slice(0, 60); fl.innerHTML = l.length ? l.map(function (o) { return '<li><a href="' + esc(o.u) + '">' + esc(o.t) + '<span class="mono">' + esc(o.c || '') + '</span></a></li>'; }).join('') : '<li class="hint">Nothing here.</li>'; }
        $all('[data-w]', b).forEach(function (x) { x.addEventListener('click', function () { w = x.getAttribute('data-w'); $all('[data-w]', b).forEach(function (y) { y.setAttribute('aria-pressed', y === x ? 'true' : 'false'); }); d(); }); });
        fq.addEventListener('input', d); d();
      });
    }
    initTabs.pin = function () { st.tabs.forEach(function (t) { if (t.p === here) t.pin = !t.pin; }); persist(); draw(); notify('Tab ' + (st.tabs.some(function (t) { return t.p === here && t.pin; }) ? 'pinned.' : 'unpinned.')); };
    initTabs.close = function () { closeTab(here); };
    draw();
  }

  /* ---------- right-click menus: every item shows the shortcut that really does the same thing ---------- */
  var ctxTarget = null;
  function initContextMenu() {
    var menu = $('#ctxmenu'); if (!menu) return;
    var ITEMS = [
      ['Command palette', 'Ctrl+Shift+F', function () { initPalette.open(); }],
      ['Search this site', '/', function () { if (q) { q.focus(); q.select(); } }],
      ['Copy a link to here', 'Alt+L', function () { copyLink(ctxTarget); }],
      ['Edit appearance…', 'Alt+E', function () { initAppearanceEditor(ctxTarget || $('main')); }],
      ['Lock this element…', 'Alt+K', function () { lockElement(ctxTarget || $('main')); }],
      ['Pin or unpin this tab', 'Alt+P', function () { initTabs.pin(); }],
      ['Close this tab', 'Alt+W', function () { initTabs.close(); }],
      ['Switch light and dark', 'Alt+T', function () { toggleTheme(); }],
      ['Reduce motion on or off', 'Alt+M', function () { setPref('motion', prefs.motion === 'reduce' ? '' : 'reduce'); notify(prefs.motion === 'reduce' ? 'Motion reduced.' : 'Motion back on.'); }]
    ];
    ACTIONS = ITEMS.map(function (it) { return { t: it[0], key: it[1], run: it[2] }; });
    function close() { menu.hidden = true; }
    function open(x, y) {
      menu.innerHTML = '<div class="ctx-filter"><label class="sr-only" for="ctx-q">Filter actions</label><input id="ctx-q" placeholder="Filter actions" autocomplete="off"></div><div class="ctx-items"></div><p class="ctx-hint mono">Shift+right-click for the browser\'s own menu</p>';
      var box = $('.ctx-items', menu), f = $('#ctx-q', menu), cur = 0;
      function draw() {
        var v = f.value.toLowerCase(), list = ITEMS.filter(function (it) { return it[0].toLowerCase().indexOf(v) >= 0; });
        box.innerHTML = ''; cur = Math.min(cur, list.length - 1);
        list.forEach(function (it, i) { var b = el('button', { type: 'button', role: 'menuitem', 'class': 'ctx-item' + (i === cur ? ' on' : '') }); b.innerHTML = '<span>' + esc(it[0]).replace(new RegExp('(' + v.replace(/[.*+?^${}()|[\]\\]/g, '\\$&') + ')', 'i'), v ? '<mark>$1</mark>' : '$1') + '</span><span class="kbd">' + esc(it[1]) + '</span>'; b.addEventListener('click', function () { close(); it[2](); }); box.appendChild(b); });
        if (!list.length) box.appendChild(el('p', { 'class': 'hint' }, 'No action matches.'));
        menu._list = list; menu._cur = cur;
      }
      f.addEventListener('input', function () { cur = 0; draw(); });
      f.addEventListener('keydown', function (e) { var l = menu._list || []; if (e.key === 'ArrowDown') { e.preventDefault(); cur = Math.min(cur + 1, l.length - 1); draw(); } else if (e.key === 'ArrowUp') { e.preventDefault(); cur = Math.max(cur - 1, 0); draw(); } else if (e.key === 'Enter' && l[cur]) { e.preventDefault(); close(); l[cur][2](); } else if (e.key === 'Escape') close(); });
      draw(); menu.hidden = false;
      var w = menu.offsetWidth, h = menu.offsetHeight, vw = innerWidth, vh = innerHeight;
      menu.style.left = Math.max(8, Math.min(x, vw - w - 8)) + 'px'; menu.style.top = Math.max(8, Math.min(y, vh - h - 8)) + 'px';
      f.focus();
    }
    document.addEventListener('contextmenu', function (e) { if (e.shiftKey || typing(e.target)) return; e.preventDefault(); ctxTarget = e.target.closest('section, .panel, .card, li, p, h1, h2, h3, figure, a, button, img') || e.target; open(e.clientX, e.clientY); });
    document.addEventListener('click', function (e) { if (!menu.hidden && !e.target.closest('#ctxmenu')) close(); });
    document.addEventListener('scroll', close, { passive: true });
    document.addEventListener('keydown', function (e) { if (e.key === 'Escape') close(); if (e.key === 'ContextMenu' || (e.shiftKey && e.key === 'F10')) { e.preventDefault(); ctxTarget = document.activeElement; var r = ctxTarget.getBoundingClientRect(); open(r.left + 8, r.bottom + 4); } });
    // the shortcuts themselves
    document.addEventListener('keydown', function (e) {
      if (typing(e.target) && !(e.ctrlKey && e.shiftKey)) return;
      var k = (e.key || '').toLowerCase(), t = document.activeElement && document.activeElement !== document.body ? document.activeElement : null;
      if (e.ctrlKey && e.shiftKey && k === 'f') { e.preventDefault(); initPalette.open(); return; }
      if (e.key === '/' && !e.ctrlKey && !e.metaKey && !e.altKey) { e.preventDefault(); if (q) { q.focus(); q.select(); } return; }
      if (!e.altKey || e.ctrlKey || e.metaKey) return;
      var map = { l: function () { copyLink(t); }, e: function () { initAppearanceEditor(t || $('main')); }, k: function () { lockElement(t || $('main')); }, p: initTabs.pin, w: initTabs.close, t: toggleTheme, m: ITEMS[8][2], r: function () { if (modeBtn) setMode(!regexMode); }, b: function () { if (bBtn) bBtn.click(); } };
      if (map[k]) { e.preventDefault(); map[k](); }
    });
  }
  function copyLink(n) {
    var a = n && (n.id ? n : n.closest('[id]')), url = location.href.split('#')[0] + (a && a.id ? '#' + a.id : '');
    (navigator.clipboard ? navigator.clipboard.writeText(url) : Promise.reject()).then(function () { notify('Link copied.'); }, function () { prompt('Copy this link', url); });
  }

  /* ---------- the page's small motions ---------- */
  function initMotion() {
    if (window.IntersectionObserver) {
      var io = new IntersectionObserver(function (es) { es.forEach(function (e) { if (e.isIntersecting) { e.target.classList.add('in'); io.unobserve(e.target); } }); }, { rootMargin: '0px 0px -8% 0px' });
      $all('.reveal, .card-grid > li, .areas > li, .shot-strip > li, .gallery > li').forEach(function (n) { io.observe(n); });
      var cio = new IntersectionObserver(function (es) { es.forEach(function (e) { if (!e.isIntersecting) return; cio.unobserve(e.target); var n = e.target, to = Number(n.getAttribute('data-count')); if (reduceMotion() || !to) return; var t0 = performance.now(); (function step(t) { var p = Math.min(1, (t - t0) / 1600); n.textContent = Math.round(to * (1 - Math.pow(1 - p, 3))); if (p < 1) requestAnimationFrame(step); })(t0); }); });
      $all('[data-count]').forEach(function (n) { cio.observe(n); });
    } else $all('.reveal').forEach(function (n) { n.classList.add('in'); });
    // nav pill that springs to the current page
    var nav = $('.nav'), pill = $('.nav-pill'), cur = nav && $('[aria-current="page"]', nav);
    function place(a) { if (!pill || !a) { if (pill) pill.style.opacity = 0; return; } pill.style.opacity = 1; pill.style.width = a.offsetWidth + 'px'; pill.style.transform = 'translateX(' + (a.offsetLeft) + 'px)'; }
    if (nav) { place(cur); $all('a', nav).forEach(function (a) { a.addEventListener('mouseenter', function () { place(a); }); a.addEventListener('focus', function () { place(a); }); }); nav.addEventListener('mouseleave', function () { place(cur); }); addEventListener('resize', function () { place(cur); }); }
    // reading progress
    var rp = $('#read-progress');
    if (rp) addEventListener('scroll', function () { var h = document.documentElement, p = h.scrollTop / Math.max(1, h.scrollHeight - h.clientHeight); rp.style.transform = 'scaleX(' + p.toFixed(3) + ')'; }, { passive: true });
  }

  /* ---------- resizable contents panel on articles ---------- */
  function initPanels() {
    var hd = $('#toc-handle'), toc = $('#toc'); if (!hd || !toc) return;
    var w = load('mt-site-toc', 260); function set(v) { w = Math.max(180, Math.min(420, v)); toc.style.width = w + 'px'; hd.setAttribute('aria-valuenow', w); save('mt-site-toc', w); }
    set(w);
    hd.addEventListener('pointerdown', function (e) { var x0 = e.clientX, w0 = w; hd.setPointerCapture(e.pointerId); hd.setAttribute('data-drag', 'true'); function mv(ev) { set(w0 + ev.clientX - x0); } function up() { hd.removeEventListener('pointermove', mv); hd.removeEventListener('pointerup', up); hd.removeAttribute('data-drag'); } hd.addEventListener('pointermove', mv); hd.addEventListener('pointerup', up); });
    hd.addEventListener('keydown', function (e) { if (e.key === 'ArrowLeft') { e.preventDefault(); set(w - 16); } else if (e.key === 'ArrowRight') { e.preventDefault(); set(w + 16); } else if (e.key === 'Home') set(180); else if (e.key === 'End') set(420); });
    hd.addEventListener('dblclick', function () { set(260); notify('Contents panel back to its default width.'); });
  }

  /* ---------- interactive diagram ---------- */
  function initDiagram() {
    var dia = $('#dia'); if (!dia) return;
    var nodes = $all('.dia-node', dia), step = 0, play = !reduceMotion(), hover = true, timer = null;
    var pb = $('#dia-play'), dots = $('#dia-dots');
    function show() {
      nodes.forEach(function (n, i) { n.setAttribute('aria-current', i === step ? 'step' : 'false'); n.classList.toggle('done', i < step); });
      var n = nodes[step], txt = n.getAttribute('data-text').split('|');
      $('#dia-tag').textContent = 'Step ' + (step + 1) + ' of ' + nodes.length + ' · ' + $('.dia-w', n).textContent;
      $('#dia-title').textContent = step === 3 ? (hover ? 'The toast waits for you' : 'The toast shows, then goes') : $('.dia-t', n).textContent;
      $('#dia-text').textContent = txt.length > 1 ? (hover ? txt[0] : txt[1]) : txt[0];
      dots.innerHTML = nodes.map(function (_, i) { return '<span class="' + (i === step ? 'now' : i < step ? 'done' : '') + '"></span>'; }).join('');
      var ex = $('.dia-explain .panel'); ex.classList.remove('swap'); void ex.offsetWidth; ex.classList.add('swap');
    }
    function tick() { clearInterval(timer); if (play) timer = setInterval(function () { step = (step + 1) % nodes.length; show(); }, 2800); pb.textContent = play ? 'Pause' : 'Play'; pb.setAttribute('aria-pressed', play ? 'true' : 'false'); }
    nodes.forEach(function (n, i) { n.addEventListener('click', function () { step = i; play = false; tick(); show(); }); });
    pb.addEventListener('click', function () { play = !play; tick(); });
    $('#dia-replay').addEventListener('click', function () { step = 0; play = true; tick(); show(); });
    $('#dia-hover').addEventListener('click', function () { hover = true; this.setAttribute('aria-pressed', 'true'); $('#dia-ignore').setAttribute('aria-pressed', 'false'); show(); });
    $('#dia-ignore').addEventListener('click', function () { hover = false; this.setAttribute('aria-pressed', 'true'); $('#dia-hover').setAttribute('aria-pressed', 'false'); show(); });
    show(); tick();
  }

  /* ---------- dim sum surprise ---------- */
  function initDimSum() {
    var d = $('#dimsum'), list = window.SITE_DISHES || []; if (!d || !prefs.dimsum || !list.length) return;
    var n = load('mt-site-visits', 0) + 1; save('mt-site-visits', n);
    if (n % 4 !== 1) return;
    var dish = list[Math.floor(Math.random() * list.length)];
    d.innerHTML = '<span class="bao" aria-hidden="true"><i></i><i></i><b></b></span><span><span class="tag amber">Dim sum surprise</span><span class="ds-name">' + esc(dish[0]) + ' · <span lang="yue">' + esc(dish[1]) + '</span></span></span><button type="button" class="toast-x" aria-label="Put the dim sum away">×</button>';
    d.hidden = false; $('.toast-x', d).addEventListener('click', function () { d.classList.add('out'); setTimeout(function () { d.hidden = true; }, 300); });
  }

  /* ---------- notification centre and storage report on the settings page ---------- */
  function initSettingsReports() {
    var ul = $('#notes');
    if (ul) { var log = load('mt-site-notes', []); ul.innerHTML = log.length ? log.slice(0, 12).map(function (n) { return '<li><span>' + esc(n.t) + '</span><span class="mono">' + esc(new Date(n.at).toLocaleString()) + '</span></li>'; }).join('') : '<li class="hint">No notifications yet.</li>'; bind('notes-clear', function () { save('mt-site-notes', []); ul.innerHTML = '<li class="hint">No notifications yet.</li>'; }); }
    var dl = $('#store');
    if (dl) { var rows = [['Settings and presets', ['mt-site-prefs', 'mt-site-presets']], ['Open tabs, pins, groups', ['mt-site-tabs']], ['Edited elements', ['mt-site-look']], ['Toy locks and codes', ['mt-site-locks', 'mt-site-auth', 'mt-site-clock']], ['Notifications', ['mt-site-notes']], ['Search options', ['mt-site-flags', 'mt-site-search', 'mt-site-toc']]];
      dl.innerHTML = rows.map(function (r) { var b = 0; r[1].forEach(function (k) { try { b += (localStorage.getItem(k) || '').length; } catch (e) { } }); return '<dt>' + esc(r[0]) + '</dt><dd>' + (b / 1024).toFixed(1) + ' KB</dd>'; }).join(''); }
  }

  /* ---------- existing page helpers: task filter, demo, status filter, lightbox ---------- */
  var tf = $('#task-filter');
  if (tf) {
    var cards = $all('#task-grid li'), count = $('#task-count');
    var apply = function (g) { var n = 0; cards.forEach(function (li) { var on = g === 'all' || li.getAttribute('data-group') === g; li.hidden = !on; if (on) { li.classList.remove('in'); void li.offsetWidth; li.classList.add('in'); n++; } }); count.textContent = 'Showing ' + n + ' of ' + cards.length + ' tasks.'; };
    $all('button', tf).forEach(function (b) { b.addEventListener('click', function () { $all('button', tf).forEach(function (x) { x.setAttribute('aria-pressed', x === b ? 'true' : 'false'); }); apply(b.getAttribute('data-group')); }); });
    apply('all');
  }
  var demo = $('#demo');
  if (demo) {
    var dpath = $('#demo-path'), out = $('#demo-out'), cmd = 'mp4', btns = $all('#demo-cmds button');
    var describe = function () {
      var p = dpath.value.trim().replace(/^"(.*)"$/, '$1'); if (!p) { out.textContent = 'Type or paste a path to see the explanation.'; return; }
      var leaf = p.split(/[\\/]/).pop(), hasExt = /\.[A-Za-z0-9]{1,5}$/.test(leaf);
      if (cmd === 'push') out.textContent = 'When ' + p + ' and :push are copied, the command stages the changes in that repository, commits them with your message template and pushes.';
      else if (!hasExt) out.textContent = 'A folder: every compatible file inside ' + p + ' would be converted to ' + cmd + ', each written beside its original. Nothing is overwritten.';
      else out.textContent = 'Would create ' + p.replace(/\.[A-Za-z0-9]{1,5}$/, '.' + cmd) + ' beside the original. Nothing is overwritten. The article lists which file types each command accepts.';
      out.classList.remove('swap'); void out.offsetWidth; out.classList.add('swap');
    };
    btns.forEach(function (b) { b.addEventListener('click', function () { cmd = b.getAttribute('data-cmd'); btns.forEach(function (x) { x.setAttribute('aria-pressed', x === b ? 'true' : 'false'); }); describe(); }); });
    dpath.addEventListener('input', describe); describe();
  }
  $all('.table-wrap table').forEach(function (t) {
    var heads = $all('thead th', t).map(function (h) { return h.textContent.trim().toLowerCase(); }), col = heads.indexOf('status'); if (col < 0) return;
    var rows = $all('tbody tr', t), words = {};
    rows.forEach(function (r) { var w = (r.children[col].textContent.trim().toLowerCase().split(/\s+/)[0] || '').replace(/[^a-z/]/g, ''); r.setAttribute('data-status', w); if (w) words[w] = (words[w] || 0) + 1; });
    var keys = Object.keys(words); if (keys.length < 2) return;
    var bar = el('div', { 'class': 'status-bar', role: 'group', 'aria-label': 'Filter by status' }), note = el('p', { 'class': 'hint', role: 'status' });
    var mk = function (key, label) { var b = el('button', { type: 'button', 'class': 'chip', 'aria-pressed': key === 'all' ? 'true' : 'false' }, label); b.addEventListener('click', function () { $all('button', bar).forEach(function (x) { x.setAttribute('aria-pressed', x === b ? 'true' : 'false'); }); var n = 0; rows.forEach(function (r) { var on = key === 'all' || r.getAttribute('data-status') === key; r.classList.toggle('dim', !on); if (on) n++; }); note.textContent = n + ' of ' + rows.length + ' rows shown.'; }); bar.appendChild(b); };
    mk('all', 'All (' + rows.length + ')'); keys.sort().forEach(function (k) { mk(k, k + ' (' + words[k] + ')'); });
    var wrap = t.closest('.table-wrap'); wrap.parentNode.insertBefore(bar, wrap); wrap.parentNode.insertBefore(note, wrap);
  });
  var lb = $('#lightbox');
  if (lb) {
    var lbImg = $('img', lb), lbCap = $('#lightbox-cap'), lastFocus = null;
    var closeLb = function () { lb.hidden = true; if (lastFocus) lastFocus.focus(); };
    $all('.shot-btn').forEach(function (b) { b.addEventListener('click', function () { lastFocus = b; lbImg.src = b.getAttribute('data-full'); lbImg.alt = b.getAttribute('data-cap'); lbCap.textContent = b.getAttribute('data-cap'); lb.hidden = false; $('#lightbox-close').focus(); }); });
    $('#lightbox-close').addEventListener('click', closeLb);
    lb.addEventListener('click', function (e) { if (e.target === lb) closeLb(); });
    document.addEventListener('keydown', function (e) { if (e.key === 'Escape' && !lb.hidden) closeLb(); });
  }
  var gf = $('#gallery-filter');
  if (gf) $all('button', gf).forEach(function (b) { b.addEventListener('click', function () { var k = b.getAttribute('data-k'); $all('button', gf).forEach(function (x) { x.setAttribute('aria-pressed', x === b ? 'true' : 'false'); }); var n = 0; $all('.gallery > li').forEach(function (li) { var on = !k || li.getAttribute('data-k') === k; li.hidden = !on; if (on) n++; }); $('#gallery-count').textContent = n + ' shown'; }); });

  /* ---------- start ---------- */
  applyPrefs(); initLanguage(); applyLooks(); renderPresets();
  initPalette(); initTabs(); initContextMenu(); initLocks(); initAuthenticator(); initMotion(); initPanels(); initDiagram(); initDimSum(); initSettingsReports();
})();
