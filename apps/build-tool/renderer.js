// Phases follow the tagged lines the batch entrypoints print, so progress is real, not guessed.
const PHASES = [
  { id: 'vocab', name: 'Vocabulary lock', start: /^\[build\] checking the maintainer vocabulary lock/, ok: /^\[vocabulary\] (lock current|skipped|lock renewed)/, fail: /^\[(vocabulary|build)\] FAILED: vocabulary/ },
  { id: 'deps', name: 'Dependencies', start: /^\[build\] fetching dependencies/, ok: /^\[dependencies\] all dependencies ready/, fail: /^\[(dependencies\] FAILED|build\] FAILED: dependencies)/ },
  { id: 'publish', name: 'Publish', start: /^\[build\] publishing version/, ok: /^\[build\] built out\\MonitoringTools\.exe/, fail: /^\[build\] FAILED: (dotnet|out)/ },
  { id: 'bundle', name: 'Bundle check', start: /^\[build\] built out\\MonitoringTools\.exe/, ok: /dependencies bundled\)/, fail: /bundle floor/ },
  { id: 'installer', name: 'Installer', start: /^\[installer\]/, ok: /^\[installer\] dist\\MonitoringTools-Setup\.exe and/, fail: /^\[installer\] FAILED/ },
];

const $ = (id) => document.getElementById(id);
let repo = null, running = null, state = {}, timers = {}, lastAction = null, lastOutput = null;

function renderPhases(action) {
  const list = PHASES.filter((p) => p.id !== 'installer' || action === 'installer');
  $('phases').style.gridTemplateColumns = `repeat(${list.length},minmax(0,1fr))`;
  $('phases').innerHTML = '';
  list.forEach((p) => {
    const el = document.createElement('div');
    el.className = 'phase ' + (state[p.id] || '');
    el.setAttribute('role', 'listitem');
    el.id = 'phase-' + p.id;
    const v = state[p.id] === 'ok' ? '✓ ' + (timers[p.id] || '') : state[p.id] === 'fail' ? 'stopped' : state[p.id] === 'run' ? 'running' : 'waiting';
    el.innerHTML = `<div class="n"></div><div class="v"></div>`;
    el.querySelector('.n').textContent = p.name;
    el.querySelector('.v').textContent = v;
    el.setAttribute('aria-label', `${p.name}: ${v}`);
    $('phases').appendChild(el);
  });
}

function setPhase(id, s) {
  if (s === 'run') timers[id + 'start'] = Date.now();
  if (s === 'ok' && timers[id + 'start']) timers[id] = ((Date.now() - timers[id + 'start']) / 1000).toFixed(1) + ' s';
  state[id] = s;
  renderPhases(lastAction);
}

function line(text) {
  const log = $('log');
  const span = document.createElement('div');
  span.textContent = text;
  if (/FAILED|error|mismatch/i.test(text)) span.className = 'bad';
  else if (/^\[(vocabulary|dependencies|build|installer)\]/.test(text)) span.className = /ready|current|built|installed|verified/.test(text) ? 'good' : 'tag';
  log.appendChild(span);
  if (log.childNodes.length > 4000) log.removeChild(log.firstChild);
  log.scrollTop = log.scrollHeight;
  for (const p of PHASES) {
    if (p.fail.test(text)) setPhase(p.id, 'fail');
    else if (p.ok.test(text)) setPhase(p.id, 'ok');
    else if (p.start.test(text) && state[p.id] !== 'ok') setPhase(p.id, 'run');
  }
}

function setBusy(action) {
  running = action;
  document.querySelectorAll('.action').forEach((b) => { b.disabled = !!action; b.classList.toggle('busy', b.dataset.action === action); });
  $('cancel').disabled = !action;
  $('pick').disabled = !!action;
  $('version').disabled = !!action;
}

function status(text, kind) { $('status').textContent = text; $('status').className = kind || ''; }

function validVersion() {
  const v = $('version').value.trim();
  const ok = /^\d{1,4}\.\d{1,4}\.\d{1,6}(-[0-9A-Za-z.]{1,20})?$/.test(v);
  $('version').setAttribute('aria-invalid', ok ? 'false' : 'true');
  return ok ? v : null;
}

async function start(action) {
  if (running) return;
  if (!repo) { status('Choose the repository folder first.', 'bad'); return; }
  const v = validVersion();
  if (!v) { status('Use a version like 1.2.3.', 'bad'); $('version').focus(); return; }
  lastAction = action; state = {}; timers = {};
  $('log').innerHTML = ''; $('retry').hidden = true; $('open-out').hidden = true;
  renderPhases(action);
  setBusy(action);
  const r = await window.build.run(action, repo, v);
  if (!r.started) { setBusy(null); status(r.error, 'bad'); return; }
  status(`Running ${action === 'installer' ? 'build-installer.bat' : 'build.bat'} · process ${r.pid}`);
}

window.build.onLog(line);
window.build.onDone((r) => {
  setBusy(null);
  PHASES.forEach((p) => { if (state[p.id] === 'run') setPhase(p.id, r.code === 0 ? 'ok' : 'fail'); });
  lastOutput = r.output;
  $('retry').hidden = false;
  if (r.code === 0) {
    $('open-out').hidden = false;
    const extra = r.action === 'run' ? (r.launched && !String(r.launched).startsWith('failed') ? ' · started the app' : ' · could not start the app: ' + r.launched) : '';
    status(`Finished in ${r.seconds} s · exit code 0${extra}`, 'good');
  } else {
    status(r.signal || r.code === 1 && /Cancel/.test($('status').textContent) ? `Cancelled after ${r.seconds} s · exit code ${r.code}` : `Stopped after ${r.seconds} s · exit code ${r.code}. The log above shows where.`, 'bad');
  }
});

document.querySelectorAll('.action').forEach((b) => b.addEventListener('click', () => start(b.dataset.action)));
$('cancel').addEventListener('click', async () => { if (await window.build.cancel()) status('Cancelling…'); });
$('retry').addEventListener('click', () => start(lastAction));
$('open-out').addEventListener('click', () => { if (lastOutput) window.build.open(lastOutput.replace(/[^\\/]+$/, '')); });
$('version').addEventListener('input', validVersion);
$('pick').addEventListener('click', async () => {
  const r = await window.build.pickRepo();
  if (r.repo) showRepo(r); else if (r.error) status(r.error, 'bad');
});

function showRepo(r) {
  repo = r.repo;
  $('repo').textContent = r.repo ? `${r.repo}${r.branch ? ' · ' + r.branch : ''}${r.clean === true ? ' · clean' : r.clean === false ? ' · uncommitted changes' : ''}` : 'Not found. Choose the Monitoring-tools folder.';
}

(async () => { showRepo(await window.build.repo()); renderPhases('build'); })();

// ---------------------------------------------------------------- command palette (Ctrl+Shift+F)
const items = [
  { title: 'Build', detail: 'build.bat', run: () => start('build'), el: 'act-build' },
  { title: 'Build and run', detail: 'build.bat, then start the app', run: () => start('run'), el: 'act-run' },
  { title: 'Build installer', detail: 'build-installer.bat', run: () => start('installer'), el: 'act-installer' },
  { title: 'Cancel the running build', detail: 'stops the whole process tree', run: () => $('cancel').click(), el: 'cancel' },
  { title: 'Choose repository folder', detail: 'settings', run: () => $('pick').click(), el: 'pick' },
  { title: 'Version', detail: 'settings', run: () => $('version').focus(), el: 'version' },
  { title: 'Build output log', detail: 'view', run: () => $('log').focus(), el: 'log' },
];
let sel = 0, shown = items;
function openPalette() { $('palette').hidden = false; $('pq').value = ''; sel = 0; filter(); $('pq').focus(); }
function closePalette() { $('palette').hidden = true; }
function filter() {
  const q = $('pq').value.trim();
  let test = () => true;
  if (q && $('pregex').getAttribute('aria-pressed') === 'true') { try { const rx = new RegExp(q, 'i'); test = (i) => rx.test(i.title) || rx.test(i.detail); } catch { test = () => false; } }
  else if (q) test = (i) => (i.title + ' ' + i.detail).toLowerCase().includes(q.toLowerCase());
  shown = items.filter(test);
  $('plist').innerHTML = '';
  shown.forEach((it, i) => {
    const li = document.createElement('li');
    li.setAttribute('role', 'option'); li.setAttribute('aria-selected', String(i === sel));
    const a = document.createElement('span'); a.textContent = it.title;
    const b = document.createElement('small'); b.textContent = it.detail;
    li.append(a, b);
    li.addEventListener('click', () => choose(it));
    $('plist').appendChild(li);
  });
}
function choose(it) {
  closePalette();
  const el = $(it.el);
  if (el) { el.scrollIntoView({ block: 'center' }); el.classList.add('flash'); setTimeout(() => el.classList.remove('flash'), 1700); }
  it.run();
}
$('pq').addEventListener('input', () => { sel = 0; filter(); });
$('pregex').addEventListener('click', () => { const b = $('pregex'); b.setAttribute('aria-pressed', String(b.getAttribute('aria-pressed') !== 'true')); filter(); $('pq').focus(); });
$('palette-open').addEventListener('click', openPalette);
$('palette').addEventListener('click', (e) => { if (e.target.id === 'palette') closePalette(); });
document.addEventListener('keydown', (e) => {
  if (e.ctrlKey && e.shiftKey && e.key.toLowerCase() === 'f') { e.preventDefault(); $('palette').hidden ? openPalette() : closePalette(); return; }
  if ($('palette').hidden) return;
  if (e.key === 'Escape') closePalette();
  else if (e.key === 'ArrowDown') { e.preventDefault(); sel = Math.min(sel + 1, shown.length - 1); filter(); }
  else if (e.key === 'ArrowUp') { e.preventDefault(); sel = Math.max(sel - 1, 0); filter(); }
  else if (e.key === 'Enter' && shown[sel]) { e.preventDefault(); choose(shown[sel]); }
});
