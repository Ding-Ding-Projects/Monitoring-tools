// Bakes the project site into content/, so the desktop companion carries every page, article
// and screenshot and works with no network. The site is built by scripts/build-site.mjs, which
// renders the repository's documentation, so the companion and the published site are the same.
const fs = require('node:fs');
const path = require('node:path');
const { execFileSync } = require('node:child_process');

const repo = path.resolve(__dirname, '..', '..');
const out = path.join(__dirname, 'content');
const dist = path.join(repo, 'site', 'dist');

execFileSync(process.execPath, [path.join(repo, 'scripts', 'build-site.mjs')], { cwd: repo, stdio: 'inherit' });
if (!fs.existsSync(path.join(dist, 'index.html'))) throw new Error('site/dist/index.html was not built');

fs.rmSync(out, { recursive: true, force: true });
fs.cpSync(dist, out, { recursive: true });

const pages = fs.existsSync(path.join(dist, 'pages.txt')) ? fs.readFileSync(path.join(dist, 'pages.txt'), 'utf8').split(/\r?\n/).filter(Boolean).length : 0;
console.log(`[site] baked ${pages} site pages into content/`);
