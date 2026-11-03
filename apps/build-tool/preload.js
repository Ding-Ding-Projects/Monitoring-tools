const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('build', {
  repo: () => ipcRenderer.invoke('repo:get'),
  pickRepo: () => ipcRenderer.invoke('repo:pick'),
  run: (action, repo, version) => ipcRenderer.invoke('run', { action, repo, version }),
  cancel: () => ipcRenderer.invoke('cancel'),
  open: (p) => ipcRenderer.invoke('open:path', p),
  onLog: (fn) => ipcRenderer.on('log', (e, line) => fn(line)),
  onDone: (fn) => ipcRenderer.on('done', (e, r) => fn(r)),
});
