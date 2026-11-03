// Bakes the project site, its screenshots and the repository's documentation into content/,
// so the desktop companion carries everything and works with no network.
const fs = require('node:fs');
const path = require('node:path');
const { marked } = require('marked');

const repo = path.resolve(__dirname, '..', '..');
const out = path.join(__dirname, 'content');
fs.rmSync(out, { recursive: true, force: true });
fs.mkdirSync(path.join(out, 'docs'), { recursive: true });

// 1. The site itself, with a Docs link added to its navigation.
let index = fs.readFileSync(path.join(repo, 'site', 'index.html'), 'utf8');
index = index.replace('<a class="hide-sm" href="#clipboard">Clipboard</a>',
  '<a href="docs/guide.html">Guide</a>\n      <a href="docs/roadmap.html">Roadmap</a>\n      <a class="hide-sm" href="#clipboard">Clipboard</a>');
fs.writeFileSync(path.join(out, 'index.html'), index);

// 2. Screenshots rendered by CI, committed under docs/screenshots.
const shots = path.join(repo, 'docs', 'screenshots');
if (fs.existsSync(shots)) fs.cpSync(shots, path.join(out, 'screenshots'), { recursive: true });

// 3. Documentation pages from the repository's Markdown, sharing the site's look.
const pages = [
  { file: 'README.md', slug: 'guide', title: 'Guide' },
  { file: 'ROADMAP.md', slug: 'roadmap', title: 'Roadmap' },
];
const nav = pages.map((p) => `<a href="${p.slug}.html" data-slug="${p.slug}">${p.title}</a>`).join('');
const style = fs.readFileSync(path.join(__dirname, 'docs.css'), 'utf8');
const palette = fs.readFileSync(path.join(__dirname, 'docs-palette.js'), 'utf8');
for (const p of pages) {
  let md = fs.readFileSync(path.join(repo, p.file), 'utf8');
  md = md.replace(/\(docs\/screenshots\//g, '(../screenshots/');
  const body = marked.parse(md, { gfm: true });
  const html = `<!doctype html><html lang="en"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>${p.title} · Monitoring Tools</title><style>${style}</style></head><body>
<nav class="top"><a class="brand" href="../index.html">Monitoring Tools</a>${nav.replace(`data-slug="${p.slug}"`, `data-slug="${p.slug}" aria-current="page"`)}
<button id="open-palette" class="kbd" aria-label="Open the command palette">Ctrl+Shift+F</button></nav>
<main class="doc">${body}</main>
<script>${palette}</script></body></html>`;
  fs.writeFileSync(path.join(out, 'docs', `${p.slug}.html`), html);
}
console.log(`[site] baked ${pages.length} documentation pages and ${fs.existsSync(shots) ? fs.readdirSync(shots).length : 0} screenshot files into content/`);
