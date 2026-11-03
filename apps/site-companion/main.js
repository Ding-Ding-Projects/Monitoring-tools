// Monitoring Tools Site: the project site and its documentation, offline, in one portable exe.
// Pages are baked in by prepare.js; links inside the app stay inside, anything else opens in the browser.
const { app, BrowserWindow, shell } = require('electron');
const path = require('node:path');

function createWindow() {
  const win = new BrowserWindow({
    width: 1280, height: 860, minWidth: 420, minHeight: 520, backgroundColor: '#08090C',
    title: 'Monitoring Tools', autoHideMenuBar: true,
    webPreferences: { contextIsolation: true, sandbox: true, nodeIntegration: false },
  });
  const root = path.join(__dirname, 'content');
  const inside = (url) => url.startsWith('file://') && decodeURI(new URL(url).pathname).replace(/^\/(\w:)/, '$1').replace(/\//g, path.sep).startsWith(root);
  win.webContents.setWindowOpenHandler(({ url }) => { if (/^https?:\/\//.test(url)) shell.openExternal(url); return { action: 'deny' }; });
  win.webContents.on('will-navigate', (e, url) => {
    if (inside(url)) return;
    e.preventDefault();
    if (/^https?:\/\//.test(url)) shell.openExternal(url);
  });
  win.loadFile(path.join(root, 'index.html'));
}

app.whenReady().then(createWindow);
app.on('window-all-closed', () => app.quit());
