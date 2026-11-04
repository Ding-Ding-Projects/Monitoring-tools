// Builds the public project site into site/dist from site/src (styles, script, fonts, social preview),
// docs/features/**/*.md (every article, rendered at build time) and docs/screenshots (the real captures).
//   node scripts/build-site.mjs [--out site/dist]
// Environment: SITE_BASE_URL overrides the public base URL used for canonical and social-card URLs;
//              SITE_SCREENSHOTS points at a fresher screenshot folder (with index.json) than docs/screenshots.
// The build is deterministic (no timestamps, sorted inputs) and fails loudly on a missing font, an empty
// category, a broken internal link, or a feature category missing from the hand-written list below.
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';
import { render, esc, plainText, slugify } from './site/markdown.mjs';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const argOut = process.argv.indexOf('--out');
const outDir = path.resolve(root, argOut > 0 ? process.argv[argOut + 1] : 'site/dist');
const srcDir = path.join(root, 'site', 'src');
const docsDir = path.join(root, 'docs', 'features');
const REPO = 'Ding-Ding-Projects/Monitoring-tools';
const REPO_URL = `https://github.com/${REPO}`;
const DOWNLOAD_URL = `${REPO_URL}/releases/latest/download/MonitoringTools-Setup.exe`;
const BASE = (process.env.SITE_BASE_URL || 'https://ding-ding-projects.github.io/Monitoring-tools/').replace(/\/?$/, '/');
const SITE_NAME = 'Monitoring Tools';
const TAGLINE = 'Clipboard toasts, internet watch and one-step quick commands for Windows, with every feature documented.';

// Hand-written enumeration of the feature categories the site must carry. The build fails if one is missing
// or if an unlisted category appears, so a new category is a deliberate edit here and in the docs.
const CATEGORY_META = {
  app: { label: 'The app itself', blurb: 'How it runs, the tray icon and the command palette.', icon: 'M3 12h4l3-7 4 14 3-7h4' },
  appearance: { label: 'Appearance', blurb: 'Theme, colour, density, text size and motion.', icon: 'M12 3a9 9 0 1 0 0 18c1.1 0 1.5-.8 1.2-1.6-.4-1 .3-2 1.4-2H17a4 4 0 0 0 4-4c0-5-4-10.4-9-10.4z' },
  'appearance-editor': { label: 'Appearance editor', blurb: 'Per-element editors and the colour picker.', icon: 'M4 20h4L19 9l-4-4L4 16zM14 6l4 4' },
  clipboard: { label: 'Clipboard', blurb: 'Toasts and the clipboard history.', icon: 'M9 4h6v3H9zM6 6h12v15H6z' },
  commands: { label: 'Quick commands', blurb: 'Convert media, push a repo, run your own scripts.', icon: 'M4 17l6-5-6-5M12 19h8' },
  files: { label: 'Files and Docker', blurb: 'File tools and Docker actions.', icon: 'M4 6h6l2 2h8v11H4z' },
  game: { label: 'Game mode', blurb: 'Keep popups away from the game you are playing.', icon: 'M6 9h4M8 7v4M15 10h.01M18 8h.01M5 5h14a3 3 0 0 1 3 3l-1 8a3 3 0 0 1-5 1.5L14 16h-4l-2 1.5A3 3 0 0 1 3 16L2 8a3 3 0 0 1 3-3z' },
  language: { label: 'Language and voice', blurb: 'App language, humour, narrator and voices.', icon: 'M4 5h10M9 3v2M6 5c1 5 4 8 8 9M12 5c-1 5-4 8-8 9M14 20l4-9 4 9M15.5 17h5' },
  minecraft: { label: 'Minecraft servers', blurb: 'Create and manage servers and generate worlds.', icon: 'M5 5h14v14H5zM5 12h14M12 5v14' },
  modes: { label: 'Modes', blurb: 'School mode, ADHD modes, schedules and the settings file.', icon: 'M12 3v9l6 3M12 21a9 9 0 1 1 0-18 9 9 0 0 1 0 18z' },
  network: { label: 'Network', blurb: 'Internet watch, DNS profiles and public IP history.', icon: 'M2 9a15 15 0 0 1 20 0M5 12.5a10 10 0 0 1 14 0M8.5 16a5 5 0 0 1 7 0M12 19.5h.01' },
  ollama: { label: 'Ollama', blurb: 'Manage local models.', icon: 'M12 3l8 4.5v9L12 21l-8-4.5v-9z' },
  records: { label: 'Records', blurb: 'Changelog, exports, notifications and settings history.', icon: 'M6 3h9l4 4v14H6zM14 3v5h5' },
  safety: { label: 'Safety', blurb: 'Authenticator, element locks, unlock ladder and support tickets.', icon: 'M12 3l8 3v6c0 5-3.5 8-8 9-4.5-1-8-4-8-9V6z' },
  search: { label: 'Search', blurb: 'Search bars everywhere and the regex builder.', icon: 'M11 4a7 7 0 1 0 0 14 7 7 0 0 0 0-14zM20 20l-4-4' },
  tabs: { label: 'Tabs', blurb: 'The tab strip.', icon: 'M3 8h6l1.5-2H21v12H3z' },
  tools: { label: 'Tools', blurb: 'Drawing, external editor, progress recovery and publishing.', icon: 'M14.7 6.3a4 4 0 0 0-5 5L3 18l3 3 6.7-6.7a4 4 0 0 0 5-5L15 12l-3-3z' },
};
const STATUS_SLUG = 'status';

// Hand-written home-page tasks. Each points at an article; the card text is that article's own first paragraph.
const TASKS = [
  { group: 'files', title: 'Convert a video, picture or audio file', to: 'commands/convert-media' },
  { group: 'files', title: 'Commit and push a repository from the clipboard', to: 'commands/push-a-repo' },
  { group: 'clipboard', title: 'Get back something I copied earlier', to: 'clipboard/history' },
  { group: 'clipboard', title: 'Understand the toast that appears when I copy', to: 'clipboard/toasts' },
  { group: 'network', title: 'Work out why the internet dropped', to: 'network/internet-watch' },
  { group: 'network', title: 'Switch DNS servers', to: 'network/dns-profiles' },
  { group: 'search', title: 'Search a page with a regular expression', to: 'search/regex-builder' },
  { group: 'search', title: 'Find anything from the keyboard', to: 'app/command-palette' },
  { group: 'customise', title: 'Make it look the way I like', to: 'appearance/theme' },
  { group: 'customise', title: 'Keep popups away while I game', to: 'game/game-mode' },
  { group: 'safety', title: 'Lock a setting so it cannot be changed by accident', to: 'safety/element-locks' },
  { group: 'safety', title: 'Keep a shared computer for school work', to: 'modes/school-mode' },
];
const TASK_GROUPS = [['all', 'All tasks'], ['files', 'Files and commands'], ['clipboard', 'Clipboard'], ['network', 'Network'], ['search', 'Search'], ['customise', 'Make it yours'], ['safety', 'Safety']];

// ---------------------------------------------------------------- helpers
const posix = path.posix;
const read = (p) => fs.readFileSync(p, 'utf8');
const sha = (b) => crypto.createHash('sha256').update(b).digest('hex');
const fail = (m) => { console.error('[site] ERROR: ' + m); process.exitCode = 1; throw new Error(m); };
const relUrl = (fromPage, toPage) => { const r = posix.relative(posix.dirname(fromPage), toPage); return r || posix.basename(toPage); };
const rootOf = (page) => { const d = posix.dirname(page); return d === '.' ? './' : d.split('/').map(() => '..').join('/') + '/'; };
const trunc = (s, n) => (s.length <= n ? s : s.slice(0, n - 1).replace(/\s+\S*$/, '') + '\u2026');

function writeOut(rel, content) {
  const p = path.join(outDir, rel);
  fs.mkdirSync(path.dirname(p), { recursive: true });
  fs.writeFileSync(p, content);
}
function copyDir(from, to) {
  fs.mkdirSync(to, { recursive: true });
  for (const e of fs.readdirSync(from, { withFileTypes: true }).sort((a, b) => a.name.localeCompare(b.name))) {
    const s = path.join(from, e.name), d = path.join(to, e.name);
    if (e.isDirectory()) copyDir(s, d); else fs.copyFileSync(s, d);
  }
}

// ---------------------------------------------------------------- inputs
// fonts must be complete and match the committed manifest
{
  const m = JSON.parse(read(path.join(srcDir, 'fonts', 'manifest.json')));
  if (!m.files.length) fail('font manifest is empty');
  for (const f of m.files) {
    const p = path.join(srcDir, 'fonts', f.file);
    if (!fs.existsSync(p)) fail('font file missing: ' + f.file + ' (run node scripts/vendor-site-fonts.mjs)');
    if (sha(fs.readFileSync(p)) !== f.sha256) fail('font digest mismatch: ' + f.file);
  }
}

const shotsDir = process.env.SITE_SCREENSHOTS && fs.existsSync(path.join(process.env.SITE_SCREENSHOTS, 'index.json'))
  ? path.resolve(process.env.SITE_SCREENSHOTS) : path.join(root, 'docs', 'screenshots');
const shotIndex = JSON.parse(read(path.join(shotsDir, 'index.json')));
const shotFiles = new Set(fs.readdirSync(shotsDir).filter((f) => f.endsWith('.png')));
const shots = shotIndex.filter((s) => shotFiles.has(s.file));

// documents
const docs = new Map();            // repo path -> doc
const categories = [];
const catDirs = fs.readdirSync(docsDir, { withFileTypes: true }).filter((e) => e.isDirectory()).map((e) => e.name).sort();
for (const slug of Object.keys(CATEGORY_META)) if (!catDirs.includes(slug)) fail(`expected feature category is missing from docs/features: ${slug}`);
for (const slug of catDirs) if (!CATEGORY_META[slug]) fail(`docs/features/${slug} is not in the hand-written category list in scripts/build-site.mjs`);

for (const slug of catDirs) {
  const dir = path.join(docsDir, slug);
  const readmePath = path.join(dir, 'README.md');
  if (!fs.existsSync(readmePath)) fail(`category ${slug} has no README.md`);
  const readmeRepo = `docs/features/${slug}/README.md`;
  const readmeSrc = read(readmePath);
  const names = fs.readdirSync(dir).filter((f) => f.endsWith('.md') && f !== 'README.md').sort();
  // order articles by their appearance in the README, then alphabetically
  const order = [...readmeSrc.matchAll(/\]\(([^)#\s]+\.md)/g)].map((m) => posix.basename(m[1]));
  names.sort((a, b) => {
    const ia = order.indexOf(a), ib = order.indexOf(b);
    return (ia < 0 ? 999 : ia) - (ib < 0 ? 999 : ib) || a.localeCompare(b);
  });
  if (!names.length) fail(`category ${slug} has no articles`);
  const category = { slug, ...CATEGORY_META[slug], readme: { repo: readmeRepo, src: readmeSrc }, articles: [] };
  for (const n of names) {
    const repo = `docs/features/${slug}/${n}`;
    const doc = { repo, category, slug: n.replace(/\.md$/, ''), src: read(path.join(dir, n)), page: `docs/${slug}/${n.replace(/\.md$/, '.html')}` };
    docs.set(repo, doc);
    category.articles.push(doc);
  }
  category.page = `docs/${slug}/index.html`;
  docs.set(readmeRepo, { repo: readmeRepo, category, isReadme: true, page: category.page, src: readmeSrc });
  categories.push(category);
}
// the delivery inventory is the status category: one article, same treatment
{
  const repo = 'docs/features/required-features.md';
  const category = { slug: STATUS_SLUG, label: 'Delivery status', blurb: 'What is done, partial or missing, stated honestly.', icon: 'M5 12l4 4 10-10', articles: [], page: `docs/${STATUS_SLUG}/index.html`, readme: { repo: '', src: '' } };
  const doc = { repo, category, slug: 'required-features', src: read(path.join(root, repo)), page: `docs/${STATUS_SLUG}/required-features.html` };
  docs.set(repo, doc);
  category.articles.push(doc);
  categories.push(category);
}
const byKey = new Map();
for (const c of categories) for (const a of c.articles) byKey.set(`${c.slug}/${a.slug}`, a);
for (const t of TASKS) if (!byKey.has(t.to)) fail(`home task points at a missing article: ${t.to}`);

// ---------------------------------------------------------------- Markdown rendering with link resolution
const brokenLinks = [];
function makeCtx(doc) {
  const dirOfSrc = posix.dirname(doc.repo);
  return {
    resolveImage(url) {
      if (/^https?:/.test(url)) return url;
      const target = posix.normalize(posix.join(dirOfSrc, url));
      if (target.startsWith('docs/screenshots/')) {
        const f = posix.basename(target);
        if (!shotFiles.has(f)) brokenLinks.push(`${doc.repo}: missing screenshot ${f}`);
        return relUrl(doc.page, 'screenshots/' + f);
      }
      brokenLinks.push(`${doc.repo}: image outside the site: ${url}`);
      return url;
    },
    resolveLink(url) {
      if (/^https?:/.test(url)) return { href: url, external: true };
      if (url.startsWith('mailto:')) return { href: url, external: true };
      if (url.startsWith('#')) return { href: url };
      const [p, hash] = url.split('#');
      const target = posix.normalize(posix.join(dirOfSrc, p));
      if (docs.has(target)) return { href: relUrl(doc.page, docs.get(target).page) + (hash ? '#' + hash : '') };
      if (target.startsWith('docs/screenshots/')) return { href: relUrl(doc.page, 'screenshots/' + posix.basename(target)) };
      if (target.endsWith('.md') && target.startsWith('docs/')) { brokenLinks.push(`${doc.repo}: link to unknown article ${url}`); return { href: '#' }; }
      const kind = p.endsWith('/') || !posix.extname(target) ? 'tree' : 'blob';
      return { href: `${REPO_URL}/${kind}/main/${target}`, external: true, source: true };
    },
  };
}
for (const d of docs.values()) {
  const r = render(d.src, makeCtx(d));
  d.html = r.html; d.headings = r.headings; d.title = r.title || d.category.label;
  d.plain = plainText(d.src);
  const firstPara = d.src.replace(/\r/g, '').split('\n').filter((l, i, a) => l.trim() && !/^#/.test(l) && !/^[|>`\-*\d]/.test(l.trim())).shift() || '';
  d.excerpt = trunc(plainText(firstPara), 200);
}
if (brokenLinks.length) fail('broken internal references:\n  ' + brokenLinks.join('\n  '));

// ---------------------------------------------------------------- page chrome
const icon = (d, size = 24) => `<svg width="${size}" height="${size}" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true" focusable="false"><path d="${d}"/></svg>`;
const ICON_DOWNLOAD = 'M12 4v11M7 10l5 5 5-5M5 20h14';
const ICON_SEARCH = 'M11 4a7 7 0 1 0 0 14 7 7 0 0 0 0-14zM20 20l-4-4';
const ICON_GEAR = 'M12 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6zM19 12l2-1-2-4-2 .5-1.5-1L15 4h-4l-.5 2.5-1.5 1L7 7l-2 4 2 1v1l-2 1 2 4 2-.5 1.5 1L11 20h4l.5-2.5 1.5-1 2 .5 2-4-2-1z';

const NAV = [
  ['index.html', 'Home', 'home'],
  ['docs/index.html', 'Documentation', 'docs'],
  [`docs/${STATUS_SLUG}/required-features.html`, 'Delivery status', 'status'],
  ['screenshots/index.html', 'Screenshots', 'shots'],
];

function head({ page, title, description, type = 'website' }) {
  const rootRel = rootOf(page);
  const url = BASE + (page === 'index.html' ? '' : page);
  const fullTitle = page === 'index.html' ? `${SITE_NAME} for Windows` : `${title} \u00b7 ${SITE_NAME}`;
  const img = BASE + 'social-preview.png';
  return `<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>${esc(fullTitle)}</title>
<meta name="description" content="${esc(description)}">
<link rel="canonical" href="${esc(url)}">
<meta name="theme-color" media="(prefers-color-scheme: light)" content="#F5FBF7">
<meta name="theme-color" media="(prefers-color-scheme: dark)" content="#0F1512">
<meta property="og:site_name" content="${SITE_NAME}">
<meta property="og:type" content="${type}">
<meta property="og:title" content="${esc(fullTitle)}">
<meta property="og:description" content="${esc(description)}">
<meta property="og:url" content="${esc(url)}">
<meta property="og:image" content="${img}">
<meta property="og:image:width" content="1200">
<meta property="og:image:height" content="630">
<meta property="og:image:alt" content="The Monitoring Tools control centre showing the overview page with live connection probes">
<meta name="twitter:card" content="summary_large_image">
<meta name="twitter:title" content="${esc(fullTitle)}">
<meta name="twitter:description" content="${esc(description)}">
<meta name="twitter:image" content="${img}">
<link rel="icon" href="${rootRel}favicon.svg" type="image/svg+xml">
<link rel="stylesheet" href="${rootRel}fonts/fonts.css">
<link rel="stylesheet" href="${rootRel}tokens.css">
<link rel="stylesheet" href="${rootRel}style.css">
<script>try{var p=JSON.parse(localStorage.getItem('mt-site-prefs')||'{}'),h=document.documentElement;if(p.theme==='light'||p.theme==='dark')h.dataset.theme=p.theme;if(p.motion==='reduce')h.dataset.motion='reduce';if(p.text)h.dataset.text=p.text;}catch(e){}</script>`;
}

function header(page, active) {
  const r = rootOf(page);
  return `<a class="skip" href="#main">Skip to the content</a>
<header class="appbar">
  <div class="appbar-row">
    <a class="brand" href="${r}index.html"><span class="brand-mark">${icon('M3 12h4l3-7 4 14 3-7h4', 22)}</span><span>Monitoring Tools</span></a>
    <nav class="nav" aria-label="Main"><ul>${NAV.map(([to, label, key]) => `<li><a href="${r}${to}"${key === active ? ' aria-current="page"' : ''}>${label}</a></li>`).join('')}</ul></nav>
    <div class="appbar-actions">
      <button class="icon-btn" id="settings-btn" type="button" aria-haspopup="dialog" aria-expanded="false" aria-controls="settings" aria-label="Site settings">${icon(ICON_GEAR, 22)}</button>
      <a class="btn filled small" href="${DOWNLOAD_URL}">${icon(ICON_DOWNLOAD, 18)}<span>Download</span></a>
    </div>
  </div>
  <div class="appbar-row search-row">
    <div class="search" role="search">
      <div class="search-field">
        <span class="search-icon">${icon(ICON_SEARCH, 20)}</span>
        <label class="sr-only" for="q">Search the site</label>
        <input id="q" type="search" placeholder="Search features, tasks and screenshots (press /)" autocomplete="off" spellcheck="false" role="combobox" aria-expanded="false" aria-controls="results" aria-autocomplete="list">
        <button class="chip-btn" id="mode" type="button" aria-pressed="false" title="Regular expression mode (Alt+R). Off means plain text: every word must appear.">.*<span class="sr-only"> Regular expression mode</span></button>
        <button class="chip-btn" id="builder-btn" type="button" aria-expanded="false" aria-controls="builder" title="Open the regex builder (Alt+B)">Builder</button>
      </div>
      <div class="builder" id="builder" role="group" aria-label="Regex builder" hidden>
        <div class="builder-grid">
          <label for="rx-pattern">Pattern</label>
          <input id="rx-pattern" type="text" class="mono" autocomplete="off" spellcheck="false" placeholder="for example ^convert|\\.mp4\\b">
          <fieldset class="flags"><legend>Flags</legend>
            <label><input type="checkbox" data-flag="i" checked> <code>i</code> ignore case</label>
            <label><input type="checkbox" data-flag="m"> <code>m</code> multiline</label>
            <label><input type="checkbox" data-flag="s"> <code>s</code> dot matches newline</label>
            <label><input type="checkbox" data-flag="u"> <code>u</code> unicode</label>
          </fieldset>
          <p class="builder-status" id="rx-status" role="status" aria-live="polite">Switch regex mode on to search with this pattern.</p>
          <label for="rx-sample">Try it on a sample</label>
          <textarea id="rx-sample" class="mono" rows="3" spellcheck="false" placeholder="Paste a line or two and see the matches highlighted below"></textarea>
          <div class="sample-out mono" id="rx-out" aria-live="polite"></div>
        </div>
        <p class="hint">This builder only searches this site. The pattern runs in your browser with a time limit, so a heavy pattern is stopped rather than freezing the page.</p>
      </div>
      <ul class="results" id="results" role="listbox" aria-label="Search results" hidden></ul>
    </div>
  </div>
</header>
<dialog class="settings" id="settings" aria-labelledby="settings-title">
  <form method="dialog">
    <h2 id="settings-title">Site settings</h2>
    <p class="hint">These are saved in this browser only. Nothing is sent anywhere, and there is no account.</p>
    <label for="pref-theme">Theme</label>
    <select id="pref-theme"><option value="system">Follow the system</option><option value="light">Light</option><option value="dark">Dark</option></select>
    <label for="pref-motion">Motion</label>
    <select id="pref-motion"><option value="system">Follow the system</option><option value="reduce">Reduce motion</option></select>
    <label for="pref-text">Text size</label>
    <select id="pref-text"><option value="">Default</option><option value="large">Large</option><option value="xlarge">Extra large</option></select>
    <div class="actions"><button class="btn tonal" type="button" id="pref-reset">Reset to defaults</button><button class="btn filled" value="close">Done</button></div>
  </form>
</dialog>`;
}

function footer(page) {
  const r = rootOf(page);
  return `<footer class="footer">
  <div class="wrap">
    <p><strong>This site documents Monitoring Tools. It is not the app.</strong> Nothing here runs the installed program; any demonstration is labelled as one. The documentation describes the latest released version, and anything the articles list as not done is not shipped.</p>
    <ul class="footer-links">
      <li><a href="${r}docs/index.html">Documentation</a></li>
      <li><a href="${r}docs/${STATUS_SLUG}/required-features.html">Delivery status</a></li>
      <li><a href="${r}screenshots/index.html">Screenshots</a></li>
      <li><a href="${REPO_URL}" rel="noopener noreferrer">Source on GitHub</a></li>
      <li><a href="${REPO_URL}/releases" rel="noopener noreferrer">All releases</a></li>
    </ul>
    <p class="hint">Fonts and every asset are served from this site: no CDN, no analytics, no tracking.</p>
  </div>
</footer>
<div class="lightbox" id="lightbox" role="dialog" aria-modal="true" aria-label="Screenshot viewer" hidden><button class="icon-btn" id="lightbox-close" type="button" aria-label="Close the screenshot">${icon('M6 6l12 12M18 6L6 18', 22)}</button><img alt=""><p id="lightbox-cap"></p></div>
<script src="${r}search-index.js"></script>
<script src="${r}app.js"></script>`;
}

function pageHtml({ page, title, description, active, body, type }) {
  return `<!doctype html>
<html lang="en">
<head>
${head({ page, title, description, type })}
</head>
<body data-root="${rootOf(page)}">
${header(page, active)}
<main id="main" tabindex="-1">
${body}
</main>
${footer(page)}
</body>
</html>
`;
}

// ---------------------------------------------------------------- pages
const sectionOf = (c) => `${c.label}`;
const articleCard = (from, a, showCat = false) => `<a class="card article-card" href="${relUrl(from, a.page)}">${showCat ? `<span class="eyebrow">${esc(a.category.label)}</span>` : ''}<h3>${esc(a.title)}</h3><p>${esc(a.excerpt)}</p></a>`;
const crumbs = (page, items) => `<nav class="crumbs" aria-label="Breadcrumb"><ol>${items.map(([label, to], i) => i === items.length - 1 ? `<li aria-current="page">${esc(label)}</li>` : `<li><a href="${relUrl(page, to)}">${esc(label)}</a></li>`).join('')}</ol></nav>`;

// Home
function buildHome() {
  const page = 'index.html';
  const demoChips = ['mp4', 'gif', 'webm', 'mp3', 'wav', 'flac', 'png', 'jpg', 'webp', 'avif', 'ico', 'push'];
  const shotFor = (f) => (shotFiles.has(f) ? `<img src="screenshots/${f}" alt="" loading="lazy">` : '');
  const body = `
<section class="hero">
  <div class="wrap hero-grid">
    <div class="hero-copy">
      <p class="eyebrow">For Windows 10 and 11</p>
      <h1>Your clipboard, your connection, one quiet tray icon.</h1>
      <p class="lead">Monitoring Tools shows what you just copied with actions made for it, tells you where the internet dropped, and turns a copied path plus a word like <code>:mp4</code> into a finished file.</p>
      <div class="cta-row">
        <a class="btn filled large" id="download" href="${DOWNLOAD_URL}">${icon(ICON_DOWNLOAD, 22)}<span>Download for Windows</span></a>
        <a class="btn tonal large" href="docs/index.html">Read the documentation</a>
      </div>
      <p class="meta" id="version" aria-live="polite">Windows installer, single file. Looking up the latest release\u2026</p>
      <p class="boundary"><strong>About this site:</strong> it explains the app and links the real download. It is not the app and cannot run it.</p>
    </div>
    <div class="hero-shots" aria-hidden="true">
      <div class="shot-main">${shotFor('control-overview.png')}</div>
      <div class="shot-side">${shotFor('toast-markdown.png')}</div>
    </div>
  </div>
</section>

<section class="band" id="tasks" aria-labelledby="tasks-h">
  <div class="wrap">
    <h2 id="tasks-h">What do you want to do?</h2>
    <p class="section-lead">Pick a job. Each card opens the full article for it.</p>
    <div class="chip-row" role="group" aria-label="Filter tasks" id="task-filter">
      ${TASK_GROUPS.map(([k, l], i) => `<button type="button" class="chip" data-group="${k}" aria-pressed="${i === 0}">${l}</button>`).join('')}
    </div>
    <ul class="card-grid" id="task-grid">
      ${TASKS.map((t) => { const a = byKey.get(t.to); return `<li data-group="${t.group}"><a class="card task-card" href="${relUrl(page, a.page)}"><span class="eyebrow">${esc(a.category.label)}</span><h3>${esc(t.title)}</h3><p>${esc(a.excerpt)}</p></a></li>`; }).join('\n      ')}
    </ul>
    <p class="hint" id="task-count" role="status" aria-live="polite"></p>
  </div>
</section>

<section class="band alt" id="try" aria-labelledby="try-h">
  <div class="wrap split">
    <div>
      <h2 id="try-h">See what a quick command would do</h2>
      <p class="section-lead">Copy a path, add a word, and the command runs when it lands on the clipboard. Conversions keep the folder and name and never overwrite anything.</p>
      <p class="demo-label"><strong>Demonstration only.</strong> This explains the behaviour in words. Nothing runs, and no file is touched.</p>
      <p><a class="btn text" href="${relUrl(page, byKey.get('commands/convert-media').page)}">Read about converting media</a> <a class="btn text" href="${relUrl(page, byKey.get('commands/quick-commands').page)}">All quick commands</a></p>
    </div>
    <div class="demo card" id="demo">
      <label for="demo-path">A copied path</label>
      <input id="demo-path" type="text" class="mono" value="D:\\Footage\\clip.mov" spellcheck="false" autocomplete="off">
      <div class="chip-row" role="group" aria-label="Choose a command" id="demo-cmds">
        ${demoChips.map((c, i) => `<button type="button" class="chip mono" data-cmd="${c}" aria-pressed="${i === 0}">:${c}</button>`).join('')}
      </div>
      <p class="demo-out" id="demo-out" role="status" aria-live="polite"></p>
    </div>
  </div>
</section>

<section class="band" id="all-features" aria-labelledby="all-h">
  <div class="wrap">
    <h2 id="all-h">Every feature, with its own article</h2>
    <p class="section-lead">${[...docs.values()].filter((d) => !d.isReadme).length} articles in ${categories.length} categories. Each ends with suggested next reading.</p>
    <div class="category-grid">
      ${categories.map((c) => `<section class="category-card" aria-labelledby="category-${c.slug}">
        <h3 id="category-${c.slug}"><a href="${relUrl(page, c.page)}"><span class="category-ico">${icon(c.icon, 22)}</span>${esc(c.label)}</a></h3>
        <p>${esc(c.blurb)}</p>
        <ul>${c.articles.map((a) => `<li><a href="${relUrl(page, a.page)}">${esc(a.title)}</a></li>`).join('')}</ul>
      </section>`).join('\n      ')}
    </div>
  </div>
</section>

<section class="band alt" aria-labelledby="shots-h">
  <div class="wrap">
    <h2 id="shots-h">Rendered from the real app</h2>
    <p class="section-lead">The build runs the app in a capture mode on Windows and commits the pictures, so these match the released version.</p>
    <ul class="shot-strip">
      ${['control-clipboard.png', 'offline-dialog.png', 'search-builder-build.png', 'control-appearance.png'].filter((f) => shotFiles.has(f)).map((f) => { const s = shots.find((x) => x.file === f); return `<li><a href="screenshots/index.html#${f.replace(/\.png$/, '')}"><img src="screenshots/${f}" alt="${esc(s ? s.caption : f)}" loading="lazy" width="${s ? s.width : ''}" height="${s ? s.height : ''}"></a></li>`; }).join('\n      ')}
    </ul>
    <p><a class="btn tonal" href="screenshots/index.html">All ${shots.length} screenshots</a></p>
  </div>
</section>

<section class="band">
  <div class="wrap">
    <div class="cta-band">
      <div><h2>Give it a minute in your tray.</h2><p class="section-lead">Run the installer once. It installs per user, and the tray icon can uninstall it again.</p></div>
      <a class="btn filled large" href="${DOWNLOAD_URL}">${icon(ICON_DOWNLOAD, 22)}<span>Download for Windows</span></a>
    </div>
  </div>
</section>`;
  writeOut(page, pageHtml({ page, title: SITE_NAME, description: TAGLINE, active: 'home', body }));
}

function buildDocsHome() {
  const page = 'docs/index.html';
  const body = `<div class="wrap page">
  ${crumbs(page, [['Home', 'index.html'], ['Documentation', page]])}
  <h1>Documentation</h1>
  <p class="lead">Every feature has an article: what it does, how to set it up, how it fails, and how to check it worked. Start from a task, or browse a category.</p>
  <h2>Start from a task</h2>
  <ul class="card-grid">${TASKS.map((t) => { const a = byKey.get(t.to); return `<li><a class="card task-card" href="${relUrl(page, a.page)}"><span class="eyebrow">${esc(a.category.label)}</span><h3>${esc(t.title)}</h3><p>${esc(a.excerpt)}</p></a></li>`; }).join('')}</ul>
  <h2>Browse by category</h2>
  <div class="category-grid">${categories.map((c) => `<section class="category-card" aria-labelledby="c-${c.slug}"><h3 id="c-${c.slug}"><a href="${relUrl(page, c.page)}"><span class="category-ico">${icon(c.icon, 22)}</span>${esc(c.label)}</a></h3><p>${esc(c.blurb)}</p><ul>${c.articles.map((a) => `<li><a href="${relUrl(page, a.page)}">${esc(a.title)}</a></li>`).join('')}</ul></section>`).join('')}</div>
</div>`;
  writeOut(page, pageHtml({ page, title: 'Documentation', description: 'Every Monitoring Tools feature has its own article, organised by what you want to do.', active: 'docs', body }));
}

function buildCategory(c) {
  const page = c.page;
  let intro = '';
  if (c.readme.repo) {
    const d = docs.get(c.readme.repo);
    // the README holds an article table; the cards below replace it, so keep only the prose before the table
    const prose = c.readme.src.split('\n').filter((l, i) => !(/^\s*\|/.test(l)) && !/^Not applicable:/.test(l)).join('\n');
    intro = render(prose, makeCtx(d)).html;
  } else {
    intro = `<p>${esc(c.blurb)}</p>`;
  }
  const body = `<div class="wrap page">
  ${crumbs(page, [['Home', 'index.html'], ['Documentation', 'docs/index.html'], [c.label, page]])}
  <h1>${esc(c.label)}</h1>
  <div class="prose intro">${intro}</div>
  <h2>Articles</h2>
  <ul class="card-grid">${c.articles.map((a) => `<li>${articleCard(page, a)}</li>`).join('')}</ul>
  <h2>Other categories</h2>
  <ul class="pill-list">${categories.filter((x) => x !== c).map((x) => `<li><a class="chip link" href="${relUrl(page, x.page)}">${esc(x.label)}</a></li>`).join('')}</ul>
</div>`;
  writeOut(page, pageHtml({ page, title: c.label, description: `${c.label}: ${c.blurb} ${c.articles.length} article${c.articles.length > 1 ? 's' : ''}.`, active: 'docs', body }));
}

function buildArticle(c, a) {
  const page = a.page;
  const idx = c.articles.indexOf(a);
  const prev = c.articles[idx - 1], next = c.articles[idx + 1];
  // suggested: articles this one links to, then the neighbours, then another category, deduplicated
  const linked = [...a.src.matchAll(/\]\(([^)#\s]+\.md)/g)].map((m) => docs.get(posix.normalize(posix.join(posix.dirname(a.repo), m[1]))))
    .filter((d) => d && !d.isReadme && d !== a);
  const sugg = [];
  for (const d of [...linked, next, prev, ...c.articles]) if (d && d !== a && !sugg.includes(d)) sugg.push(d);
  const catIdx = categories.indexOf(c);
  const other = categories[(catIdx + 1) % categories.length].articles[0];
  if (other && !sugg.includes(other)) sugg.push(other);
  const suggested = sugg.slice(0, 4);
  const toc = a.headings.filter((h) => h.level === 2);
  const body = `<div class="wrap page article-layout">
  ${crumbs(page, [['Home', 'index.html'], ['Documentation', 'docs/index.html'], [c.label, c.page], [a.title, page]])}
  <div class="article-grid">
    ${toc.length > 1 ? `<aside class="toc"><details open><summary>On this page</summary><ol>${toc.map((h) => `<li><a href="#${h.id}">${esc(h.text)}</a></li>`).join('')}</ol></details></aside>` : ''}
    <article class="prose">
      <p class="eyebrow">${esc(c.label)}</p>
      <h1>${esc(a.title)}</h1>
      ${a.html}
      <p class="source-note"><a href="${REPO_URL}/blob/main/${a.repo}" rel="noopener noreferrer">View this article's Markdown source on GitHub</a></p>
      <section class="suggested" aria-labelledby="sugg-h">
        <h2 id="sugg-h">Suggested articles</h2>
        <ul class="card-grid">${suggested.map((d) => `<li>${articleCard(page, d, d.category !== c)}</li>`).join('')}</ul>
        <nav class="prevnext" aria-label="Neighbouring articles">${prev ? `<a href="${relUrl(page, prev.page)}" rel="prev"><span>Previous in ${esc(c.label)}</span>${esc(prev.title)}</a>` : '<span></span>'}${next ? `<a href="${relUrl(page, next.page)}" rel="next"><span>Next in ${esc(c.label)}</span>${esc(next.title)}</a>` : '<span></span>'}</nav>
        <p><a class="btn text" href="${relUrl(page, c.page)}">All ${esc(c.label)} articles</a></p>
      </section>
    </article>
  </div>
</div>`;
  const desc = a.excerpt || `${a.title}: documentation for Monitoring Tools.`;
  writeOut(page, pageHtml({ page, title: a.title, description: desc, active: c.slug === STATUS_SLUG ? 'status' : 'docs', body, type: 'article' }));
}

function buildShots() {
  const page = 'screenshots/index.html';
  const body = `<div class="wrap page">
  ${crumbs(page, [['Home', 'index.html'], ['Screenshots', page]])}
  <h1>Screenshots</h1>
  <p class="lead">${shots.length} pictures rendered from the real app by the release build. Select one to enlarge it.</p>
  <ul class="gallery">${shots.map((s) => `<li id="${esc(s.file.replace(/\.png$/, ''))}"><figure><button type="button" class="shot-btn" data-full="${esc(s.file)}" data-cap="${esc(s.caption)}"><img src="${esc(s.file)}" alt="${esc(s.caption)}" loading="lazy" width="${s.width}" height="${s.height}"></button><figcaption>${esc(s.caption)}</figcaption></figure></li>`).join('')}</ul>
</div>`;
  writeOut(page, pageHtml({ page, title: 'Screenshots', description: 'Real screenshots of every Monitoring Tools page, rendered from the built app.', active: 'shots', body }));
}

function build404() {
  const page = '404.html';
  const body = `<div class="wrap page"><h1>That page is not here</h1><p class="lead">The link may be old. Try the search box above, or start from the documentation.</p><p><a class="btn filled" href="index.html">Home</a> <a class="btn tonal" href="docs/index.html">Documentation</a></p></div>`;
  // 404 may be served from any depth, so it uses absolute site URLs for assets
  let html = pageHtml({ page, title: 'Page not found', description: TAGLINE, active: '', body });
  html = html.replace(/(href|src)="(\.\/)?(fonts\/|tokens\.css|style\.css|favicon\.svg|search-index\.js|app\.js|index\.html|docs\/|screenshots\/)/g, (_, a, __, f) => `${a}="${new URL(f, BASE).pathname}`);
  writeOut(page, html);
}

// search index: pages, sections, tasks and screenshots
function buildSearchIndex() {
  const items = [];
  const add = (t, c, u, x) => items.push({ t, c, u, x: trunc(x, 700) });
  for (const c of categories) {
    add(c.label, 'Category', c.page, c.blurb);
    for (const a of c.articles) {
      add(a.title, c.label, a.page, a.plain);
      const parts = a.src.split(/^## /m).slice(1);
      const h2 = a.headings.filter((h) => h.level === 2);
      parts.forEach((p, i) => { if (h2[i]) add(`${a.title}: ${h2[i].text}`, c.label, `${a.page}#${h2[i].id}`, plainText(p.split('\n').slice(1).join('\n'))); });
    }
  }
  for (const t of TASKS) add(t.title, 'Task', byKey.get(t.to).page, byKey.get(t.to).excerpt);
  for (const s of shots) add(s.caption, 'Screenshot', `screenshots/index.html#${s.file.replace(/\.png$/, '')}`, s.file);
  add('Download Monitoring Tools for Windows', 'Action', 'index.html', 'installer download latest release');
  writeOut('search-index.js', 'window.SITE_INDEX=' + JSON.stringify(items) + ';\n');
  return items.length;
}

// ---------------------------------------------------------------- tokens
function tokensCss() {
  const light = { 'primary': '#006C4F', 'on-primary': '#FFFFFF', 'primary-container': '#8CF8CF', 'on-primary-container': '#002115', 'secondary-container': '#CFE9DB', 'on-secondary-container': '#092017', 'tertiary-container': '#C2E8FD', 'on-tertiary-container': '#001F2A', 'surface': '#F5FBF7', 'surface-low': '#EFF5F1', 'surface-container': '#E9EFEB', 'surface-high': '#E3EAE5', 'surface-highest': '#DDE4E0', 'on-surface': '#171D1A', 'on-surface-variant': '#3F4943', 'outline': '#6F7973', 'outline-variant': '#BFC9C2', 'error': '#BA1A1A', 'amber': '#7A4B00', 'amber-container': '#FFDDB5', 'code-bg': '#E3EAE5' };
  const dark = { 'primary': '#6FDBB4', 'on-primary': '#003827', 'primary-container': '#00513A', 'on-primary-container': '#8CF8CF', 'secondary-container': '#354B41', 'on-secondary-container': '#CFE9DB', 'tertiary-container': '#244C5D', 'on-tertiary-container': '#C2E8FD', 'surface': '#0F1512', 'surface-low': '#171D1A', 'surface-container': '#1B211E', 'surface-high': '#262B28', 'surface-highest': '#303633', 'on-surface': '#DEE4DF', 'on-surface-variant': '#BFC9C2', 'outline': '#89938C', 'outline-variant': '#404943', 'error': '#FFB4AB', 'amber': '#FFB95F', 'amber-container': '#3B2A12', 'code-bg': '#262B28' };
  const decl = (o) => Object.entries(o).map(([k, v]) => `--md-${k}:${v};`).join('');
  return `/* Generated by scripts/build-site.mjs from the Mint seed colour roles used by the app. */
:root{${decl(light)}color-scheme:light}
@media (prefers-color-scheme:dark){:root:not([data-theme="light"]){${decl(dark)}color-scheme:dark}}
:root[data-theme="dark"]{${decl(dark)}color-scheme:dark}
`;
}

// ---------------------------------------------------------------- run
fs.rmSync(outDir, { recursive: true, force: true });
fs.mkdirSync(outDir, { recursive: true });
copyDir(path.join(srcDir, 'fonts'), path.join(outDir, 'fonts'));
for (const f of ['style.css', 'app.js', 'favicon.svg', 'social-preview.png']) fs.copyFileSync(path.join(srcDir, f), path.join(outDir, f));
writeOut('tokens.css', tokensCss());
fs.mkdirSync(path.join(outDir, 'screenshots'), { recursive: true });
for (const f of [...shotFiles].sort()) fs.copyFileSync(path.join(shotsDir, f), path.join(outDir, 'screenshots', f));
fs.copyFileSync(path.join(shotsDir, 'index.json'), path.join(outDir, 'screenshots', 'index.json'));
writeOut('.nojekyll', '');

buildHome();
buildDocsHome();
for (const c of categories) { buildCategory(c); for (const a of c.articles) buildArticle(c, a); }
buildShots();
build404();
const nIndex = buildSearchIndex();

// final guard: every internal href/src in every generated page must resolve to a file or anchor
{
  const problems = [];
  const pages = [];
  (function walk(d) { for (const e of fs.readdirSync(d, { withFileTypes: true })) { const p = path.join(d, e.name); if (e.isDirectory()) walk(p); else if (p.endsWith('.html')) pages.push(p); } })(outDir);
  const idsOf = new Map();
  const ids = (p) => { if (!idsOf.has(p)) idsOf.set(p, new Set([...read(p).matchAll(/\sid="([^"]+)"/g)].map((m) => m[1]))); return idsOf.get(p); };
  for (const p of pages) {
    const html = read(p);
    if (/https?:\/\/(fonts\.(googleapis|gstatic)\.com|cdn\.|unpkg|cdnjs|jsdelivr)/.test(html.replace(/<meta[^>]*>/g, ''))) problems.push(`${path.relative(outDir, p)}: loads a remote asset`);
    if (!/property="og:image"/.test(html) || !/name="twitter:card"/.test(html)) problems.push(`${path.relative(outDir, p)}: missing social tags`);
    for (const m of html.matchAll(/(?:href|src)="([^"]+)"/g)) {
      const u = m[1];
      if (/^(https?:|mailto:|data:|javascript:)/.test(u)) continue;
      if (u.startsWith('/')) continue;
      const [file, hash] = u.split('#');
      const target = file ? path.resolve(path.dirname(p), file) : p;
      if (!fs.existsSync(target)) { problems.push(`${path.relative(outDir, p)}: dead link ${u}`); continue; }
      if (hash && target.endsWith('.html') && !ids(target).has(hash)) problems.push(`${path.relative(outDir, p)}: missing anchor ${u}`);
    }
  }
  if (problems.length) fail('site check failed:\n  ' + problems.join('\n  '));
  const list = pages.map((p) => path.relative(outDir, p).replace(/\\/g, '/')).sort();
  writeOut('pages.txt', list.join('\n') + '\n');
  console.log(`[site] built ${list.length} pages, ${nIndex} search entries, ${shots.length} screenshots into ${path.relative(root, outDir)}`);
}
