// Monitoring Tools Build Tool: a desktop front end for the repository's own entrypoints.
// Build runs build.bat, Build installer runs build-installer.bat, and Build and run runs
// build.bat and then starts out\MonitoringTools.exe (which installs itself for this user and
// starts in the tray). The child exit code is shown exactly as returned.
const { app, BrowserWindow, ipcMain, dialog, shell } = require('electron');
const { spawn, execFile } = require('node:child_process');
const fs = require('node:fs');
const path = require('node:path');

let win;
let child = null;
const settingsFile = () => path.join(app.getPath('userData'), 'settings.json');

function readSettings() {
  try { return JSON.parse(fs.readFileSync(settingsFile(), 'utf8')); } catch { return {}; }
}
function writeSettings(s) {
  try { fs.mkdirSync(path.dirname(settingsFile()), { recursive: true }); fs.writeFileSync(settingsFile(), JSON.stringify(s, null, 2)); } catch { }
}

function isRepo(dir) {
  return !!dir && fs.existsSync(path.join(dir, 'build.bat')) && fs.existsSync(path.join(dir, 'build-installer.bat'))
    && fs.existsSync(path.join(dir, 'src', 'MonitoringTools', 'MonitoringTools.csproj'));
}

// Look beside the portable exe, then its parents, then the remembered folder.
function findRepo() {
  const s = readSettings();
  const start = process.env.PORTABLE_EXECUTABLE_DIR || path.dirname(app.getPath('exe'));
  let d = start;
  for (let i = 0; i < 5 && d; i++) { if (isRepo(d)) return d; const p = path.dirname(d); if (p === d) break; d = p; }
  return isRepo(s.repo) ? s.repo : null;
}

function gitInfo(repo) {
  return new Promise((resolve) => {
    execFile('git', ['-C', repo, 'status', '--porcelain', '-b'], { windowsHide: true }, (err, out) => {
      if (err) return resolve({ branch: null, clean: null });
      const lines = out.split(/\r?\n/).filter(Boolean);
      const branch = (lines[0] || '').replace(/^## /, '').split('...')[0];
      resolve({ branch, clean: lines.length <= 1 });
    });
  });
}

function createWindow() {
  win = new BrowserWindow({
    width: 1120, height: 740, minWidth: 860, minHeight: 560, backgroundColor: '#0D0F13',
    title: 'Monitoring Tools Build Tool', titleBarStyle: 'hidden',
    titleBarOverlay: { color: '#0D0F13', symbolColor: '#C9CDD4', height: 44 },
    webPreferences: { preload: path.join(__dirname, 'preload.js'), contextIsolation: true, sandbox: true, nodeIntegration: false },
  });
  win.loadFile('index.html');
  win.webContents.setWindowOpenHandler(({ url }) => { if (/^https:\/\//.test(url)) shell.openExternal(url); return { action: 'deny' }; });
}

app.whenReady().then(createWindow);
app.on('window-all-closed', () => { if (child) killTree(child.pid); app.quit(); });

ipcMain.handle('repo:get', async () => {
  const repo = findRepo();
  return repo ? { repo, ...(await gitInfo(repo)) } : { repo: null };
});

ipcMain.handle('repo:pick', async () => {
  const r = await dialog.showOpenDialog(win, { title: 'Choose the Monitoring-tools folder', properties: ['openDirectory'] });
  if (r.canceled || !r.filePaths[0]) return { repo: null, cancelled: true };
  const dir = r.filePaths[0];
  if (!isRepo(dir)) return { repo: null, error: 'That folder does not contain build.bat, build-installer.bat and the app project.' };
  writeSettings({ ...readSettings(), repo: dir });
  return { repo: dir, ...(await gitInfo(dir)) };
});

ipcMain.handle('open:path', (e, p) => shell.openPath(p));

// Versions are passed straight to the batch files, so only plain dotted numbers are allowed.
const versionOk = (v) => /^\d{1,4}\.\d{1,4}\.\d{1,6}(-[0-9A-Za-z.]{1,20})?$/.test(v);

ipcMain.handle('run', async (e, { action, repo, version }) => {
  if (child) return { started: false, error: 'A build is already running.' };
  if (!isRepo(repo)) return { started: false, error: 'Choose the repository folder first.' };
  if (version && !versionOk(version)) return { started: false, error: 'Use a version like 1.2.3.' };
  const script = action === 'installer' ? 'build-installer.bat' : 'build.bat';
  const args = ['/d', '/s', '/c', `"${path.join(repo, script)}"${version ? ' ' + version : ''}`];
  const started = Date.now();
  child = spawn('cmd.exe', args, { cwd: repo, windowsHide: true, windowsVerbatimArguments: true, env: { ...process.env, SILENT: '1' } });
  const send = (line) => win && win.webContents.send('log', line);
  const pipe = (stream) => {
    let buf = '';
    stream.on('data', (d) => {
      buf += d.toString();
      const parts = buf.split(/\r?\n/);
      buf = parts.pop();
      parts.forEach(send);
    });
    stream.on('end', () => { if (buf) send(buf); });
  };
  pipe(child.stdout); pipe(child.stderr);
  const pid = child.pid;
  child.on('close', (code, signal) => {
    const exe = path.join(repo, 'out', 'MonitoringTools.exe');
    const setup = path.join(repo, 'dist', 'MonitoringTools-Setup.exe');
    child = null;
    let launched = null;
    if (action === 'run' && code === 0 && fs.existsSync(exe)) {
      try { spawn(exe, [], { cwd: path.dirname(exe), detached: true, stdio: 'ignore' }).unref(); launched = exe; } catch (err) { launched = 'failed: ' + err.message; }
    }
    win && win.webContents.send('done', {
      action, code: code === null ? -1 : code, signal, seconds: Math.round((Date.now() - started) / 100) / 10,
      output: action === 'installer' ? setup : exe, launched,
    });
  });
  return { started: true, pid };
});

function killTree(pid) {
  try { execFile('taskkill', ['/PID', String(pid), '/T', '/F'], { windowsHide: true }); } catch { }
}

ipcMain.handle('cancel', () => {
  if (!child) return false;
  killTree(child.pid);
  return true;
});
