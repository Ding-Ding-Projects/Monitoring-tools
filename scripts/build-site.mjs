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
  files: { label: 'Files and Docker', blurb: 'File tools, virtual disks and Docker actions.', icon: 'M4 6h6l2 2h8v11H4z' },
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
  tools: { label: 'Tools', blurb: 'Drawing, Base64 beside a file, external editor, progress recovery and publishing.', icon: 'M14.7 6.3a4 4 0 0 0-5 5L3 18l3 3 6.7-6.7a4 4 0 0 0 5-5L15 12l-3-3z' },
};
const STATUS_SLUG = 'status';

// Hand-written home-page tasks. Each points at an article; the card text is that article's own first paragraph.
const TASKS = [
  { group: 'files', title: 'Convert a video, picture or audio file', to: 'commands/convert-media' },
  { group: 'files', title: 'Commit and push a repository from the clipboard', to: 'commands/push-a-repo' },
  { group: 'files', title: 'Pack files and folders into one virtual disk', to: 'files/virtual-disks' },
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

// ---------------------------------------------------------------- release facts
// The README's release-figures block is written by the release workflow from the release that produced it, so it is
// the build's source for the version, the line counts and the human-time estimate. Nothing here is typed by hand;
// when the block is missing, the pages say the figures are not available instead of inventing them.
const RELEASE = (() => {
  const readme = read(path.join(root, 'README.md'));
  const m = readme.match(/<!-- release-figures:start -->([\s\S]*?)<!-- release-figures:end -->/);
  if (!m) return null;
  const t = m[1];
  const num = (s) => Number(String(s).replace(/,/g, ''));
  const ver = (t.match(/release v(\d+\.\d+\.\d+)/) || [])[1];
  const total = t.match(/Project total: ([\d,]+) lines \(([\d,]+) non-blank\); grand total with excluded rows: ([\d,]+)/);
  const parts = t.match(/Source ([\d,]+), tests ([\d,]+), styles and markup ([\d,]+), documentation ([\d,]+), configuration and data ([\d,]+)/);
  const agent = t.match(/Agent-written surviving lines: ([\d,]+) of ([\d,]+)/);
  const human = t.match(/about ([\d,]+) to ([\d,]+) working days/);
  if (!ver || !total || !parts) return null;
  return {
    version: ver,
    commit: (t.match(/commit `([0-9a-f]+)`/) || [])[1] || '',
    lines: num(total[1]), nonBlank: num(total[2]), grand: num(total[3]),
    parts: [['Source', num(parts[1])], ['Tests', num(parts[2])], ['Documentation', num(parts[4])], ['Styles and markup', num(parts[3])], ['Configuration and data', num(parts[5])]],
    agent: agent ? [num(agent[1]), num(agent[2])] : null,
    human: human ? [num(human[1]), num(human[2])] : null,
  };
})();
const fmt = (n) => n.toLocaleString('en-US');
// The installer button uses the immutable asset URL of the release the figures came from.
const INSTALLER_URL = RELEASE ? `${REPO_URL}/releases/download/v${RELEASE.version}/MonitoringTools-Setup.exe` : DOWNLOAD_URL;
const DISHES = (() => { try { return JSON.parse(read(path.join(root, 'scripts', 'dim-sum-code-names.json'))).dishes.map((d) => [d.en, d.zhHant]); } catch (e) { return []; } })();

// ---------------------------------------------------------------- page chrome
const icon = (d, size = 24) => `<svg width="${size}" height="${size}" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true" focusable="false"><path d="${d}"/></svg>`;
const ICON_DOWNLOAD = 'M12 4v11M7 10l5 5 5-5M5 20h14';
const ICON_SEARCH = 'M11 4a7 7 0 1 0 0 14 7 7 0 0 0 0-14zM20 20l-4-4';
const ICON_GEAR = 'M12 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6zM19 12l2-1-2-4-2 .5-1.5-1L15 4h-4l-.5 2.5-1.5 1L7 7l-2 4 2 1v1l-2 1 2 4 2-.5 1.5 1L11 20h4l.5-2.5 1.5-1 2 .5 2-4-2-1z';
const ICON_MOON = 'M21 13A9 9 0 1 1 11 3a7 7 0 0 0 10 10z';
const ICON_ARROW = 'M5 12h14M13 6l6 6-6 6';
const ICON_PULSE = 'M3 12h4l3-7 4 14 3-7h4';

const NAV = [
  ['index.html', 'Home', 'home', 'nav.home'],
  ['docs/index.html', 'Docs', 'docs', 'nav.docs'],
  ['diagram/index.html', 'How it works', 'diagram', 'nav.diagram'],
  ['screenshots/index.html', 'Gallery', 'shots', 'nav.shots'],
  [`docs/${STATUS_SLUG}/required-features.html`, 'Status', 'status', 'nav.status'],
  ['settings/index.html', 'Settings', 'settings', 'nav.settings'],
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
<meta name="theme-color" content="#6FDBB4">
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
<script>try{var p=JSON.parse(localStorage.getItem('mt-site-prefs')||'{}'),h=document.documentElement;if(p.theme==='light'||p.theme==='dark')h.dataset.theme=p.theme;if(p.motion==='reduce')h.dataset.motion='reduce';if(p.text)h.dataset.text=p.text;if(p.lang)h.dataset.lang=p.lang;if(p.dock==='left')h.dataset.dock='left';if(p.accent&&/^#[0-9a-fA-F]{6}$/.test(p.accent))h.style.setProperty('--accent-pick',p.accent);}catch(e){}</script>`;
}

const searchBlock = `<div class="search" role="search" data-feature="search-regex">
      <div class="search-field">
        <span class="search-icon">${icon(ICON_SEARCH, 20)}</span>
        <label class="sr-only" for="q" data-t="search.label">Search the site</label>
        <input id="q" type="search" placeholder="Search features, tasks and screenshots (press /)" autocomplete="off" spellcheck="false" role="combobox" aria-expanded="false" aria-controls="results" aria-autocomplete="list">
        <button class="chip-btn mono" id="mode" type="button" aria-pressed="false" title="Regular expression mode (Alt+R). Off means plain text: every word must appear.">.*<span class="sr-only"> Regular expression mode</span></button>
        <button class="chip-btn" id="builder-btn" type="button" aria-expanded="false" aria-controls="builder" title="Open the regex builder (Alt+B)" data-t="search.builder">Builder</button>
      </div>
      <div class="builder" id="builder" role="group" aria-label="Regex builder" hidden>
        <div class="builder-tabs" role="tablist" aria-label="Regex builder views">
          <button type="button" role="tab" class="chip" aria-selected="true" aria-pressed="true" data-view="build">Build</button>
          <button type="button" role="tab" class="chip" aria-selected="false" aria-pressed="false" data-view="test">Test</button>
          <button type="button" role="tab" class="chip" aria-selected="false" aria-pressed="false" data-view="explain">Explain</button>
        </div>
        <div class="builder-grid" data-pane="build">
          <label for="rx-pattern">Pattern</label>
          <input id="rx-pattern" type="text" class="mono" autocomplete="off" spellcheck="false" placeholder="for example ^convert|\\.mp4\\b">
          <div class="tokens" id="rx-tokens" aria-label="Insert a token">
            ${[['^', 'start'], ['$', 'end'], ['\\b', 'word edge'], ['\\d+', 'digits'], ['\\w+', 'word'], ['.*', 'anything'], ['(a|b)', 'either'], ['[abc]', 'one of']].map(([t, l]) => `<button type="button" class="tok mono" data-tok="${esc(t)}" title="${l}">${esc(t)}</button>`).join('')}
          </div>
          <fieldset class="flags"><legend>Flags</legend>
            <label><input type="checkbox" data-flag="i" checked> <code>i</code> ignore case</label>
            <label><input type="checkbox" data-flag="m"> <code>m</code> multiline</label>
            <label><input type="checkbox" data-flag="s"> <code>s</code> dot matches newline</label>
            <label><input type="checkbox" data-flag="u"> <code>u</code> unicode</label>
          </fieldset>
          <p class="builder-status" id="rx-status" role="status" aria-live="polite">Switch regex mode on to search with this pattern.</p>
        </div>
        <div class="builder-grid" data-pane="test" hidden>
          <label for="rx-sample">Try it on a sample</label>
          <textarea id="rx-sample" class="mono" rows="3" spellcheck="false" placeholder="Paste a line or two and see the matches highlighted below"></textarea>
          <div class="sample-out mono" id="rx-out" aria-live="polite"></div>
        </div>
        <div class="builder-grid" data-pane="explain" hidden><ol class="explain mono" id="rx-explain" aria-live="polite"></ol></div>
        <p class="hint">This builder only searches this site. The pattern runs in your browser with a time limit, so a heavy pattern is stopped rather than freezing the page. The app's own builder uses the .NET engine.</p>
      </div>
      <ul class="results" id="results" role="listbox" aria-label="Search results" hidden></ul>
    </div>`;

function header(page, active) {
  const r = rootOf(page);
  return `<a class="skip" href="#main" data-t="skip">Skip to the content</a>
<header class="appbar">
  <div class="appbar-row">
    <a class="brand" href="${r}index.html"><span class="brand-mark">${icon(ICON_PULSE, 20)}</span><span>Monitoring Tools</span></a>
    <nav class="nav" aria-label="Main"><span class="nav-pill" aria-hidden="true"></span><ul>${NAV.map(([to, label, key, t]) => `<li><a href="${r}${to}" data-t="${t}"${key === active ? ' aria-current="page"' : ''}>${label}</a></li>`).join('')}</ul></nav>
    ${searchBlock}
    <div class="appbar-actions">
      <div class="seg" role="group" aria-label="Language" data-feature="lang">
        <button type="button" data-lang="en" aria-pressed="true">EN</button><button type="button" data-lang="yue" aria-pressed="false" lang="yue">粵</button><button type="button" data-lang="both" aria-pressed="false" data-t="lang.both">Both</button>
      </div>
      <button class="icon-btn" id="theme-btn" type="button" aria-label="Switch light and dark (Alt+T)" title="Switch light and dark (Alt+T)">${icon(ICON_MOON, 20)}</button>
      <a class="icon-btn" href="${r}settings/index.html" aria-label="Site settings" title="Site settings">${icon(ICON_GEAR, 20)}</a>
    </div>
  </div>
  <div class="tabstrip" id="tabstrip" role="tablist" aria-label="Open pages" data-feature="tabs"></div>
</header>`;
}

function footer(page) {
  const r = rootOf(page);
  return `<footer class="footer">
  <div class="wrap">
    <p class="boundary-line" data-feature="boundary"><strong data-t="boundary">This site documents Monitoring Tools. It is not the app.</strong> <span data-t="boundary.more">Nothing here runs the installed program; every demonstration is labelled as one. The documentation describes the latest released version, and anything the articles list as not done is not shipped.</span></p>
    <ul class="footer-links">
      <li><a href="${r}docs/index.html" data-t="nav.docs">Docs</a></li>
      <li><a href="${r}docs/${STATUS_SLUG}/required-features.html" data-t="nav.status">Status</a></li>
      <li><a href="${r}screenshots/index.html" data-t="nav.shots">Gallery</a></li>
      <li><a href="${r}settings/index.html" data-t="nav.settings">Settings</a></li>
      <li><a href="${r}base64/index.html">Base64</a></li>
      <li><a href="${REPO_URL}" rel="noopener noreferrer">Source on GitHub</a></li>
      <li><a href="${REPO_URL}/releases" rel="noopener noreferrer">All releases</a></li>
    </ul>
    <p class="hint mono">${RELEASE ? `Documents release v${esc(RELEASE.version)}.` : 'Release figures are not available in this build.'} Fonts and every asset are served from this site: no CDN, no analytics, no tracking.</p>
  </div>
</footer>
<div class="toasts" id="toasts" role="region" aria-label="Notifications" aria-live="polite" data-feature="notifications"></div>
<div class="ctxmenu" id="ctxmenu" role="menu" aria-label="Actions" hidden data-feature="context-menu"></div>
<div class="overlay" id="palette" role="dialog" aria-modal="true" aria-label="Command palette" hidden data-feature="palette">
  <div class="palette-box">
    <div class="palette-field">${icon(ICON_SEARCH, 20)}<label class="sr-only" for="pal-q">Go to anything</label><input id="pal-q" type="text" autocomplete="off" spellcheck="false" placeholder="Go to a page, a section, a setting or an element"><span class="kbd">Esc</span></div>
    <div class="palette-kinds" role="group" aria-label="Filter results"><button type="button" class="kchip" data-k="" aria-pressed="true">All</button><button type="button" class="kchip" data-k="here" aria-pressed="false">On this page</button><button type="button" class="kchip" data-k="page" aria-pressed="false">Pages</button><button type="button" class="kchip" data-k="setting" aria-pressed="false">Settings</button><button type="button" class="kchip" data-k="action" aria-pressed="false">Actions</button></div>
    <ul class="palette-list" id="pal-list" role="listbox" aria-label="Results"></ul>
    <p class="palette-foot mono"><span>↑↓ move</span><span>Enter go there</span><span>Ctrl+Shift+F</span></p>
  </div>
</div>
<div class="overlay" id="sheet" role="dialog" aria-modal="true" aria-labelledby="sheet-title" hidden><div class="sheet"><h2 id="sheet-title"></h2><div id="sheet-body"></div></div></div>
<div class="lightbox" id="lightbox" role="dialog" aria-modal="true" aria-label="Screenshot viewer" hidden><button class="icon-btn" id="lightbox-close" type="button" aria-label="Close the screenshot">${icon('M6 6l12 12M18 6L6 18', 22)}</button><img alt=""><p id="lightbox-cap"></p></div>
<script>window.SITE_DISHES=${JSON.stringify(DISHES)};window.SITE_AREAS=${JSON.stringify(Object.fromEntries([...categories.map((c) => [c.slug, c.label]), ['home', 'Home'], ['docs', 'Docs'], ['shots', 'Gallery'], ['settings', 'Settings'], ['diagram', 'How it works']]))};</script>
<script src="${r}search-index.js"></script>
<script src="${r}app.js"></script>`;
}

function pageHtml({ page, title, description, active, body, type, area }) {
  return `<!doctype html>
<html lang="en">
<head>
${head({ page, title, description, type })}
</head>
<body data-root="${rootOf(page)}" data-page="${esc(page)}" data-title="${esc(title)}" data-area="${esc(area || active || '')}">
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
const articleCard = (from, a, showCat = false) => `<a class="card article-card" href="${relUrl(from, a.page)}">${showCat ? `<span class="eyebrow">${esc(a.category.label)}</span>` : ''}<h3>${esc(a.title)}</h3><p>${esc(a.excerpt)}</p><span class="go">Read it ${icon(ICON_ARROW, 16)}</span></a>`;
const crumbs = (page, items) => `<nav class="crumbs" aria-label="Breadcrumb"><ol>${items.map(([label, to], i) => i === items.length - 1 ? `<li aria-current="page">${esc(label)}</li>` : `<li><a href="${relUrl(page, to)}">${esc(label)}</a></li>`).join('')}</ol></nav>`;
const shotMeta = (f) => shots.find((x) => x.file === f);
const img = (page, f, cls = '', alt) => { const s = shotMeta(f); if (!s) return ''; return `<img class="${cls}" src="${relUrl(page, 'screenshots/' + f)}" alt="${esc(alt || s.caption)}" width="${s.width}" height="${s.height}" loading="lazy" decoding="async">`; };

// Home
function buildHome() {
  const page = 'index.html';
  const nArticles = [...docs.values()].filter((d) => !d.isReadme && d.category.slug !== STATUS_SLUG).length;
  const feats = categories.filter((c) => c.slug !== STATUS_SLUG);
  const demoChips = ['mp4', 'gif', 'webm', 'mp3', 'wav', 'flac', 'png', 'jpg', 'webp', 'avif', 'ico', 'push'];
  const v = RELEASE ? `v${RELEASE.version}` : '';
  const body = `
<section class="hero grid-bg" aria-labelledby="hero-h">
  <svg class="heartbeat" viewBox="0 0 1440 160" preserveAspectRatio="none" aria-hidden="true" focusable="false"><path d="M0 100 H260 l24 -70 l34 150 l30 -110 l20 30 H640 l18 -40 l26 90 l22 -50 H1000 l24 -70 l34 150 l30 -110 l20 30 H1440"/></svg>
  <div class="scan" aria-hidden="true"></div>
  <div class="wrap hero-grid">
    <div class="hero-copy">
      <p class="readout mono r" style="--d:0"><span class="live-dot"></span><span class="accent" data-t="hero.live">For Windows 10 and 11</span>${v ? ` · release ${esc(v)}` : ''}</p>
      <h1 id="hero-h" class="r" style="--d:1"><span data-t="hero.a">Your PC,</span><br><span class="accent" data-t="hero.b">watched quietly.</span></h1>
      <p class="lead r" style="--d:2" data-t="hero.lead">A tray app that shows what you copied, tells you why the internet dropped, and turns a copied path plus a word like :mp4 into a finished file.</p>
      <div class="cta-row r" style="--d:3" data-feature="download">
        <a class="btn filled large" id="download" href="${INSTALLER_URL}">${icon(ICON_DOWNLOAD, 22)}<span data-t="hero.download">Download installer</span></a>
        <a class="btn ghost large" href="docs/index.html" data-t="hero.docs">Read the docs</a>
        ${v ? `<span class="mono meta-ver">${esc(v)}<br>Windows x64</span>` : ''}
      </div>
      <p class="meta" id="version" aria-live="polite">${RELEASE ? `Installer for release ${esc(v)}, everything bundled. Installs per user; the tray icon can uninstall it.` : 'Windows installer, single file, installs per user.'}</p>
      <p class="boundary r" style="--d:4" data-t="hero.boundary">This site explains and downloads the app. It is not the app, and nothing on it runs on your computer.</p>
    </div>
    <div class="hero-shots r" style="--d:2">
      <div class="shot-frame">${img(page, 'control-overview.png', 'shot-main', 'Control centre overview: internet status, copies today, game mode and a live latency chart')}</div>
      ${img(page, 'toast-markdown.png', 'shot-toast', 'Clipboard toast for copied Markdown, sliding into the corner the way the app shows it')}
    </div>
  </div>
</section>

<section class="readouts" aria-label="The project in numbers">
  <div class="wrap readout-grid">
    <div><p class="tag" data-t="stats.areas">Areas</p><p class="big mono" data-count="${feats.length}">${feats.length}</p></div>
    <div><p class="tag" data-t="stats.articles">Articles</p><p class="big mono" data-count="${nArticles}">${nArticles}</p></div>
    <div><p class="tag">${RELEASE ? `Lines at ${esc(v)}` : 'Lines'}</p><p class="big mono">${RELEASE ? fmt(RELEASE.lines) : 'n/a'}</p></div>
    <div><p class="tag" data-t="stats.net">Needs internet</p><p class="big mono" data-t="stats.never">never</p></div>
  </div>
</section>

<section class="band" aria-label="Two things it does all day">
  <div class="wrap duo">
    <div class="panel panel-big reveal">
      <span class="tag">01 · Clipboard toasts</span>
      <h2>Copy anything. Get the next step.</h2>
      <p>Bottom right, three seconds, always on top. Hover and the actions fit what you copied: open it, convert it, strip the quotes, move the files.</p>
      <div class="toast-reel" aria-label="Real toasts, one after another"><div class="reel">${img(page, 'toast-markdown.png', '', 'Toast for copied Markdown')}${img(page, 'toast-folder.png', '', 'Toast for a folder with batch conversion chips')}</div></div>
      <div class="kbd-row"><span class="kbd">:mp4</span><span class="kbd">:gif</span><span class="kbd">:push</span><span class="kbd">remove quotes</span></div>
      <p><a class="link-go" href="${relUrl(page, byKey.get('clipboard/toasts').page)}">Clipboard toasts ${icon(ICON_ARROW, 16)}</a></p>
    </div>
    <div class="panel reveal">
      <span class="tag amber">02 · Internet watch</span>
      <h2>It names the step that broke.</h2>
      <ol class="ladder mono" aria-label="Illustration of the probe ladder: adapter and router pass, DNS fails">
        <li><span>Adapter</span><span>ok</span></li><li><span>Router</span><span>4 ms</span></li><li class="fail"><span>DNS</span><span>failing</span></li><li class="wait"><span>Internet</span><span>waiting</span></li>
      </ol>
      <p>Then one press: release and renew, or switch DNS. <em>Illustration of the app's checks.</em></p>
      <p><a class="link-go" href="${relUrl(page, byKey.get('network/internet-watch').page)}">Internet watch ${icon(ICON_ARROW, 16)}</a></p>
    </div>
  </div>
</section>

<section class="band" id="tasks" aria-labelledby="tasks-h">
  <div class="wrap">
    <div class="section-head"><div><p class="tag" data-t="tasks.tag">Start from the job</p><h2 id="tasks-h" data-t="tasks.h">I want to…</h2></div>
    <div class="chip-row" role="group" aria-label="Filter tasks" id="task-filter">
      ${TASK_GROUPS.map(([k, l], i) => `<button type="button" class="chip" data-group="${k}" aria-pressed="${i === 0}">${l}</button>`).join('')}
    </div></div>
    <ul class="card-grid" id="task-grid">
      ${TASKS.map((t, i) => { const a = byKey.get(t.to); return `<li data-group="${t.group}" style="--i:${i}"><a class="card task-card" href="${relUrl(page, a.page)}"><span class="eyebrow">${esc(a.category.label)}</span><h3>${esc(t.title)}</h3><p>${esc(a.excerpt)}</p><span class="go">Read how ${icon(ICON_ARROW, 16)}</span></a></li>`; }).join('\n      ')}
    </ul>
    <p class="hint" id="task-count" role="status" aria-live="polite"></p>
  </div>
</section>

<section class="band" id="try" aria-labelledby="try-h">
  <div class="wrap split">
    <div>
      <p class="tag sky">Demonstration</p>
      <h2 id="try-h">See what a quick command would do</h2>
      <p class="section-lead">Copy a path, add a word, and the command runs when it lands on the clipboard. Conversions keep the folder and name and never overwrite anything.</p>
      <p class="demo-label"><strong>Demonstration only.</strong> This explains the behaviour in words. Nothing runs, and no file is touched.</p>
      <p><a class="link-go" href="${relUrl(page, byKey.get('commands/convert-media').page)}">Converting media ${icon(ICON_ARROW, 16)}</a> <a class="link-go" href="${relUrl(page, byKey.get('commands/quick-commands').page)}">All quick commands ${icon(ICON_ARROW, 16)}</a></p>
    </div>
    <div class="demo panel" id="demo">
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
    <p class="tag" data-t="all.tag">Every feature, none left out</p>
    <h2 id="all-h">${feats.length} areas. ${nArticles} articles.</h2>
    <ul class="areas">
      ${feats.map((c) => `<li><a class="area" href="${relUrl(page, c.page)}"><span class="glyph">${icon(c.icon, 20)}</span><span class="area-txt"><span class="area-name">${esc(c.label)}</span><span class="area-blurb">${esc(c.blurb)}</span></span><span class="mono area-n">${c.articles.length}</span></a></li>`).join('\n      ')}
      <li><a class="area area-all" href="docs/index.html"><span class="glyph">${icon(ICON_ARROW, 20)}</span><span class="area-name">Browse all ${nArticles} articles</span></a></li>
    </ul>
  </div>
</section>

<section class="band" aria-labelledby="shots-h">
  <div class="wrap">
    <p class="tag">Rendered from the real app</p>
    <h2 id="shots-h">See it before you install.</h2>
    <ul class="shot-strip">
      ${['control-clipboard.png', 'offline-dialog.png', 'search-builder-build.png', 'command-palette.png'].filter((f) => shotFiles.has(f)).map((f) => `<li><a href="screenshots/index.html#${f.replace(/\.png$/, '')}">${img(page, f)}</a></li>`).join('\n      ')}
    </ul>
    <p><a class="btn ghost" href="screenshots/index.html">All ${shots.length} captures</a></p>
  </div>
</section>

<section class="band">
  <div class="wrap">
    <a class="panel cta-band grid-bg" href="diagram/index.html">
      <span><span class="tag sky">Interactive diagram</span><span class="cta-h">Step through what happens when you copy a file path.</span><span class="cta-p">Pause, replay, pick any step. An illustration; it does not run the app.</span></span>
      <span class="btn filled">Open the diagram ${icon(ICON_ARROW, 18)}</span>
    </a>
  </div>
</section>
<div class="dimsum" id="dimsum" hidden data-feature="dimsum"></div>`;
  writeOut(page, pageHtml({ page, title: SITE_NAME, description: TAGLINE, active: 'home', body }));
}

// Interactive workflow diagram
function buildDiagram() {
  const page = 'diagram/index.html';
  const steps = [
    ['You copy a path', 'Your clipboard', 'You copy D:\\Footage\\clip.mov with :mp4 on the end. Nothing else to open or click.'],
    ['The watcher notices', 'Clipboard watcher', 'The app listens for clipboard changes the way Windows tells it to. It reads the new text once and never sends it anywhere.'],
    ['It reads what you copied', 'Content check', 'The text is a real file path, the file is a video, and it ends in a known quick command. So this is a conversion, not plain text.'],
    ['The toast shows', 'Toast, bottom right', 'A toast appears bottom right, always on top, for 3 seconds. Hover it and it stays, with the actions for a video.|A toast appears bottom right for 3 seconds and fades. The conversion carries on without it; it is listed later in Activity.'],
    ['FFmpeg converts it', 'Bundled FFmpeg', 'The bundled FFmpeg writes clip.mp4 beside the original. It works offline and the toast shows progress.'],
    ['You get the result', 'Result toast', 'The toast names the new file and its size. An existing file is never overwritten; a number is added instead.'],
  ];
  const body = `<section class="grid-bg diagram-page" data-feature="diagram">
  <div class="wrap page">
    ${crumbs(page, [['Home', 'index.html'], ['How it works', page]])}
    <div class="section-head">
      <div><p class="tag sky">Interactive diagram · an illustration, not the running app</p><h1>What happens when you copy a <span class="accent">file path</span></h1></div>
      <div class="dia-controls">
        <div class="seg" role="group" aria-label="What you do with the toast"><button type="button" id="dia-hover" aria-pressed="true">I hover the toast</button><button type="button" id="dia-ignore" aria-pressed="false">I ignore it</button></div>
        <button type="button" class="btn tonal" id="dia-play" aria-pressed="true">Pause</button>
        <button type="button" class="btn filled" id="dia-replay">Replay</button>
      </div>
    </div>
    <ol class="dia" id="dia" aria-label="Workflow steps">
      ${steps.map(([t, w, x], i) => `<li><button type="button" class="dia-node" data-step="${i}" data-text="${esc(x)}" aria-current="false"><span class="dia-num mono">${i + 1}</span><span class="dia-t">${esc(t)}</span><span class="dia-w mono">${esc(w)}</span></button></li>`).join('')}
    </ol>
    <div class="dia-explain">
      <div class="panel" aria-live="polite"><p class="tag" id="dia-tag"></p><h2 id="dia-title"></h2><p id="dia-text"></p><div class="dia-dots" id="dia-dots" aria-hidden="true"></div></div>
      <div class="panel"><p class="tag">Text version</p><ol class="dia-plain">${steps.map(([t, , x]) => `<li><strong>${esc(t)}.</strong> ${esc(x.split('|')[0])}</li>`).join('')}</ol></div>
    </div>
    <p class="hint">Read the full behaviour in <a href="${relUrl(page, byKey.get('clipboard/toasts').page)}">Clipboard toasts</a> and <a href="${relUrl(page, byKey.get('commands/convert-media').page)}">Convert media</a>.</p>
  </div>
</section>`;
  writeOut(page, pageHtml({ page, title: 'How it works', description: 'An interactive, step-by-step illustration of what Monitoring Tools does when you copy a file path with a quick command.', active: 'diagram', body }));
}

// Base64 beside the file, as the browser can do it: every result is a download
function buildBase64() {
  const page = 'base64/index.html';
  const body = `<section class="wrap page b64-page" data-feature="base64">
  ${crumbs(page, [['Home', 'index.html'], ['Base64', page]])}
  <div class="section-head"><div><p class="tag">Runs in this browser · nothing is uploaded</p><h1>Base64, <span class="accent">and back.</span></h1>
  <p class="lead">Drop a file to get its Base64 text as <span class="mono">name.ext.base64.txt</span>. Drop that text file to get the original back, with its extension. The same as the app's <a href="${relUrl(page, byKey.get('tools/base64-beside-the-file').page)}">Base64 beside the file</a>, with one difference: a web page cannot write next to your file, so <strong>every result here is a download</strong> you save where you like.</p></div></div>
  <div class="panel b64-panel">
    <div class="b64-zone" id="b64-zone" tabindex="0" role="button" aria-describedby="b64-help">
      <span class="b64-mark" aria-hidden="true">${icon('M12 3v12M7 10l5 5 5-5M5 21h14', 28)}</span>
      <span class="b64-zone-t">Drop files here</span>
      <span class="b64-zone-h" id="b64-help">Or press Enter to choose them. A file ending in .base64.txt turns back into its original; anything else becomes Base64 text.</span>
      <input type="file" id="b64-files" multiple hidden>
    </div>
    <div class="b64-run" id="b64-run" hidden><div class="b64-bar" aria-hidden="true"><i id="b64-fill"></i></div><span class="mono" id="b64-now"></span><button type="button" class="btn tonal" id="b64-cancel">Cancel</button></div>
    <p class="hint" id="b64-status" role="status" aria-live="polite"></p>
    <ul class="b64-list" id="b64-list" aria-label="Results"></ul>
    <p class="hint">Each result counts only when a SHA-256 round trip matches: the bytes decoded from the text must hash the same as the original. Decoding accepts the standard and URL-safe alphabets, line breaks, missing padding and a <span class="mono">data:</span> prefix, and stops at the exact byte that is not Base64. Files up to 64 MB each, because the browser holds the whole file in memory.</p>
  </div>
</section>`;
  writeOut(page, pageHtml({ page, title: 'Base64', description: 'Turn a file into Base64 text and back in your browser, each result checked by a SHA-256 round trip and saved as a download.', active: '', body, area: 'tools' }));
}

// Settings for this site
function buildSettings() {
  const page = 'settings/index.html';
  const seeds = [['Mint', '#6FDBB4'], ['Sky', '#9CCBFF'], ['Amber', '#FFB95F'], ['Violet', '#CFBCFF'], ['Coral', '#FF8A80']];
  const row = (label, hint, ctl, id) => `<div class="set-row"${id ? ` id="${id}"` : ''}><div class="set-lbl"><b>${label}</b><span>${hint}</span></div><div class="set-ctl">${ctl}</div></div>`;
  const body = `<div class="wrap page settings-page">
  ${crumbs(page, [['Home', 'index.html'], ['Settings', page]])}
  <div class="section-head"><div><p class="tag" data-t="set.tag">Settings · this browser only</p><h1><span data-t="set.h">Make the site</span> <span class="accent" data-t="set.h2">yours.</span></h1>
  <p class="lead">Kept in this browser's local storage and never sent anywhere. These change this site only, not the app. Press Reset all, or clear the site's data in your browser, to start over.</p></div>
  <div class="set-search"><label class="sr-only" for="set-q">Search settings</label><input id="set-q" type="search" placeholder="Search settings" autocomplete="off"><button type="button" class="chip-btn mono" id="set-rx" aria-pressed="false" title="Regular expression">.*</button><span class="mono hint" id="set-count" role="status"></span></div></div>
  <div class="settings-grid">
    <div class="set-col">
      <section class="panel set-group" aria-labelledby="g-lang"><h2 class="tag" id="g-lang">Language and voice</h2>
        ${row('Language', 'Default English. Bilingual shows the Cantonese line under the English one. Articles stay in English.', `<div class="seg" role="group" aria-label="Language" data-feature="lang"><button type="button" data-lang="en" aria-pressed="true">English</button><button type="button" data-lang="yue" aria-pressed="false" lang="yue">廣東話</button><button type="button" data-lang="both" aria-pressed="false">Bilingual</button></div>`, 'set-lang')}
        ${row('English humour', '0 is plain. Default 2.', '<input type="range" min="0" max="5" step="1" id="fun-en" aria-label="English humour level"><output class="mono" for="fun-en" id="fun-en-v"></output>', 'set-fun-en')}
        ${row('Cantonese humour', 'Its own slider, separate from English. Default 2.', '<input type="range" min="0" max="5" step="1" id="fun-yue" aria-label="Cantonese humour level"><output class="mono" for="fun-yue" id="fun-yue-v"></output>', 'set-fun-yue')}
        <p class="joke" id="fun-sample" data-feature="humour"></p>
      </section>
      <section class="panel set-group" aria-labelledby="g-colour"><h2 class="tag" id="g-colour">Colour and theme</h2>
        ${row('Theme', 'Default: follow your system.', '<div class="seg" role="group" aria-label="Theme"><button type="button" data-theme-set="" aria-pressed="true">System</button><button type="button" data-theme-set="dark" aria-pressed="false">Dark</button><button type="button" data-theme-set="light" aria-pressed="false">Light</button></div>', 'set-theme')}
        ${row('Accent colour', 'Default Mint, the app\'s seed. Pick any colour; the translator checks it can be read.', `<div class="swatches">${seeds.map(([n, h]) => `<button type="button" class="sw" data-accent="${h}" style="--c:${h}" aria-label="${n}" aria-pressed="false"></button>`).join('')}<label class="sw sw-any" title="Any colour"><span class="sr-only">Any colour</span><input type="color" id="accent-any" value="#6FDBB4"></label></div>`, 'set-accent')}
        <div class="translator mono" id="translator" data-feature="colour-picker" aria-live="polite"></div>
      </section>
      <section class="panel set-group" aria-labelledby="g-type"><h2 class="tag" id="g-type">Type, density and motion</h2>
        ${row('Text size', 'Default 100 %.', '<div class="seg" role="group" aria-label="Text size"><button type="button" data-text-set="" aria-pressed="true">100 %</button><button type="button" data-text-set="large" aria-pressed="false">115 %</button><button type="button" data-text-set="xlarge" aria-pressed="false">130 %</button></div>', 'set-text')}
        ${row('Reduce motion', 'Default: follow your system. On stops slides, springs and loops.', '<button type="button" class="switch" role="switch" id="rm" aria-checked="false" aria-label="Reduce motion"><i></i></button>', 'set-motion')}
        ${row('Tab strip', 'Where the open pages sit. Default on top.', '<div class="seg" role="group" aria-label="Dock the tab strip"><button type="button" data-dock="" aria-pressed="true">Top</button><button type="button" data-dock="left" aria-pressed="false">Left</button></div>', 'set-dock')}
        ${row('Dim sum surprise', 'One home page visit in ten shows a dish. On, and it stays on.<span class="ds-said" id="ds-said" role="status" aria-live="polite"></span>', '<button type="button" class="switch" role="switch" id="ds" aria-checked="true" aria-label="Dim sum surprise" data-feature="dimsum-switch"><i></i></button>', 'set-dimsum')}
        ${row('Edit one element', 'Right-click anything and choose Edit appearance… (Alt+E). Reset them all here.', '<button type="button" class="btn tonal" id="reset-elements">Reset edited elements</button>', 'set-elements')}
      </section>
    </div>
    <div class="set-col">
      <section class="panel set-group" aria-labelledby="g-preview"><h2 class="tag" id="g-preview">Live preview</h2>
        <div class="preview"><p class="tag">Quick commands</p><p class="pv-h">Convert a video</p><p class="pv-b">Copy its path with :mp4 and the new file lands beside it.</p><span class="btn filled">Read how</span></div>
      </section>
      <section class="panel set-group" aria-labelledby="g-presets"><h2 class="tag" id="g-presets">Presets</h2>
        <ul class="presets" id="presets"></ul>
        <div class="btn-row"><button type="button" class="btn tonal" id="preset-save">Save as preset</button><button type="button" class="btn tonal" id="export">Export</button><label class="btn tonal" for="import">Import</label><input type="file" id="import" accept="application/json" hidden><button type="button" class="btn danger" id="reset-all">Reset all</button></div>
      </section>
      <section class="panel set-group" aria-labelledby="g-safety" data-feature="locks"><h2 class="tag" id="g-safety">Toy locks and codes</h2>
        <p class="hint">Right-click any element and choose Lock this element… (Alt+K). Locks here are toys kept in this browser: they protect nothing, and clearing the site's data removes them. Forgot a code? Locked elements offer the unlock ladder, the same rungs as the app.</p>
        <ul class="locks" id="locks"></ul>
        <div data-feature="authenticator"><p class="tag sky">Authenticator (toy)</p>
          <p class="hint">Codes from a Base32 secret, like the app's built-in authenticator. Do not put a real account's secret here: this browser keeps it unencrypted.</p>
          <div class="auth-add"><label class="sr-only" for="auth-name">Name</label><input id="auth-name" placeholder="Name"><label class="sr-only" for="auth-secret">Base32 secret</label><input id="auth-secret" class="mono" placeholder="Base32 secret"><button type="button" class="btn tonal" id="auth-add">Add</button></div>
          <ul class="auth" id="auth"></ul></div>
      </section>
      <section class="panel set-group" aria-labelledby="g-notes"><h2 class="tag" id="g-notes">Notification centre</h2><ul class="notes" id="notes"></ul><button type="button" class="btn tonal" id="notes-clear">Clear</button></section>
      <section class="panel set-group" aria-labelledby="g-store"><h2 class="tag" id="g-store">What this browser keeps</h2><dl class="store mono" id="store"></dl></section>
    </div>
  </div>
</div>`;
  writeOut(page, pageHtml({ page, title: 'Settings', description: 'Language, humour, colour, text size, motion, presets, toy locks and an authenticator for the Monitoring Tools site, kept in your browser.', active: 'settings', body }));
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
  writeOut(page, pageHtml({ page, title: c.label, description: `${c.label}: ${c.blurb} ${c.articles.length} article${c.articles.length > 1 ? 's' : ''}.`, active: 'docs', body, area: c.slug }));
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
    ${toc.length > 1 ? `<aside class="toc" id="toc"><details open><summary>On this page</summary><ol>${toc.map((h) => `<li><a href="#${h.id}">${esc(h.text)}</a></li>`).join('')}</ol></details><p class="hint mono toc-hint">Drag the edge to resize</p></aside><div class="handle" id="toc-handle" role="separator" tabindex="0" aria-orientation="vertical" aria-label="Resize the contents panel" aria-controls="toc" aria-valuemin="180" aria-valuemax="420" aria-valuenow="260" data-feature="resizable-panels"><span></span></div>` : ''}
    <article class="prose">
      <p class="eyebrow">${esc(c.label)}</p>
      <h1>${esc(a.title)}</h1>
      <div class="progress" aria-hidden="true"><span id="read-progress"></span></div>
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
  writeOut(page, pageHtml({ page, title: a.title, description: desc, active: c.slug === STATUS_SLUG ? 'status' : 'docs', body, type: 'article', area: c.slug }));
}

function buildShots() {
  const page = 'screenshots/index.html';
  const kindOf = (f) => /^toast-/.test(f) ? 'toast' : /^control-/.test(f) ? 'centre' : /^(search-|regex)/.test(f) ? 'search' : /^(theme-|lang-|scale|appearance)/.test(f) ? 'look' : /^(setup-|mc-|minecraft)/.test(f) ? 'setup' : 'dialog';
  const kinds = [['', 'All'], ['toast', 'Toasts'], ['centre', 'Control centre'], ['dialog', 'Dialogs and tools'], ['search', 'Search'], ['look', 'Themes, languages, scale'], ['setup', 'Setup and servers']];
  const counts = (k) => shots.filter((s) => !k || kindOf(s.file) === k).length;
  const body = `<div class="wrap page">
  ${crumbs(page, [['Home', 'index.html'], ['Gallery', page]])}
  <div class="section-head"><div><p class="tag">Gallery · real captures of the built app</p><h1>See it <span class="accent">before you install.</span></h1></div>
  <p class="hint mono">${shots.length} pictures rendered from the real app by the release build. No mockups. <span id="gallery-count" role="status">${shots.length} shown</span></p></div>
  <div class="chip-row" role="group" aria-label="Filter captures" id="gallery-filter">${kinds.map(([k, l], i) => `<button type="button" class="chip" data-k="${k}" aria-pressed="${i === 0}">${l} <span class="mono">${counts(k)}</span></button>`).join('')}</div>
  <ul class="gallery">${shots.map((s, i) => `<li id="${esc(s.file.replace(/\.png$/, ''))}" data-k="${kindOf(s.file)}" style="--i:${i % 12}"><figure><button type="button" class="shot-btn" data-full="${esc(s.file)}" data-cap="${esc(s.caption)}"><img src="${esc(s.file)}" alt="${esc(s.caption)}" loading="lazy" decoding="async" width="${s.width}" height="${s.height}"></button><figcaption>${esc(s.caption)}</figcaption></figure></li>`).join('')}</ul>
</div>`;
  writeOut(page, pageHtml({ page, title: 'Gallery', description: 'Real screenshots of every Monitoring Tools page, rendered from the built app.', active: 'shots', body }));
}

function build404() {
  const page = '404.html';
  const body = `<div class="wrap page"><h1>That page is not here</h1><p class="lead">The link may be old. Try the search box above, or start from the documentation.</p><p><a class="btn filled" href="index.html">Home</a> <a class="btn tonal" href="docs/index.html">Documentation</a></p></div>`;
  // 404 may be served from any depth, so it uses absolute site URLs for assets
  let html = pageHtml({ page, title: 'Page not found', description: TAGLINE, active: '', body });
  html = html.replace(/(href|src)="(\.\/)?(fonts\/|tokens\.css|style\.css|favicon\.svg|search-index\.js|app\.js|index\.html|docs\/|screenshots\/|diagram\/|settings\/)/g, (_, a, __, f) => `${a}="${new URL(f, BASE).pathname}`);
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
  add('Download Monitoring Tools for Windows', 'Action', 'index.html#download', 'installer download latest release');
  add('How it works: copying a file path', 'Diagram', 'diagram/index.html', 'interactive workflow diagram clipboard watcher toast ffmpeg conversion');
  add('Base64, and back, in your browser', 'Action', 'base64/index.html', 'base64 encode decode file text download sha-256 round trip data url');
  for (const [id, t] of [['set-lang', 'Language'], ['set-fun-en', 'English humour'], ['set-fun-yue', 'Cantonese humour'], ['set-theme', 'Theme'], ['set-accent', 'Accent colour'], ['set-text', 'Text size'], ['set-motion', 'Reduce motion'], ['set-dock', 'Tab strip position'], ['set-dimsum', 'Dim sum surprise'], ['set-elements', 'Edited elements'], ['g-presets', 'Presets, export and import'], ['g-safety', 'Toy locks and the authenticator'], ['g-notes', 'Notification centre']]) add(t, 'Setting', `settings/index.html#${id}`, 'site setting ' + t.toLowerCase());
  writeOut('search-index.js', 'window.SITE_INDEX=' + JSON.stringify(items) + ';\n');
  return items.length;
}

// ---------------------------------------------------------------- tokens
function tokensCss() {
  // The site's "Night shift" scheme: the app's Mint seed roles on a deep ink ground, and the app's light roles.
  const dark = { 'ink': '#070B09', 's0': '#0C1210', 's1': '#121916', 's2': '#18201C', 's3': '#222B27', 'line': '#2A3530', 'on': '#E4EBE6', 'on-2': '#9DAAA3', 'accent': '#6FDBB4', 'accent-2': '#8CF8CF', 'on-accent': '#00281C', 'amber': '#FFB95F', 'sky': '#9CCBFF', 'violet': '#CFBCFF', 'error': '#FFB4AB', 'grid': 'rgba(111,219,180,.07)', 'glow': 'rgba(111,219,180,.45)' };
  const light = { 'ink': '#F3F8F5', 's0': '#FFFFFF', 's1': '#EAF2EE', 's2': '#E1EBE6', 's3': '#D3E0D9', 'line': '#C3D2CA', 'on': '#0E1612', 'on-2': '#46524C', 'accent': '#006C4F', 'accent-2': '#00513A', 'on-accent': '#FFFFFF', 'amber': '#8A5300', 'sky': '#1F5FA8', 'violet': '#6750A4', 'error': '#BA1A1A', 'grid': 'rgba(0,108,79,.07)', 'glow': 'rgba(0,108,79,.25)' };
  const decl = (o) => Object.entries(o).map(([k, v]) => `--${k}:${v};`).join('');
  return `/* Generated by scripts/build-site.mjs: the Night shift scheme from the app's Mint seed colour roles. */
:root{${decl(dark)}color-scheme:dark}
@media (prefers-color-scheme:light){:root:not([data-theme="dark"]){${decl(light)}color-scheme:light}}
:root[data-theme="light"]{${decl(light)}color-scheme:light}
`;
}

// Hand-written list of the shared site features this build must carry. A guard that only checks features already
// present passes on a site with none, so each row names where the feature must appear and the build fails if it does
// not. Features that cannot apply to a static site are listed in docs/features/app/project-site.md with the reason.
const SITE_FEATURES = [
  ['search bar with the anchored regex builder', 'all', 'data-feature="search-regex"'],
  ['three language modes', 'all', 'data-feature="lang"'],
  ['browser-style tab strip', 'all', 'data-feature="tabs"'],
  ['right-click menus with working shortcuts', 'all', 'data-feature="context-menu"'],
  ['command palette that jumps to the element', 'all', 'data-feature="palette"'],
  ['non-blocking notifications', 'all', 'data-feature="notifications"'],
  ['site/app boundary statement', 'all', 'data-feature="boundary"'],
  ['installer download button', 'index.html', 'data-feature="download"'],
  ['dim sum surprise', 'index.html', 'data-feature="dimsum"'],
  ['interactive workflow diagram', 'diagram/index.html', 'data-feature="diagram"'],
  ['humour sliders per language', 'settings/index.html', 'data-feature="humour"'],
  ['colour picker with translator', 'settings/index.html', 'data-feature="colour-picker"'],
  ['toy element locks and unlock ladder', 'settings/index.html', 'data-feature="locks"'],
  ['authenticator', 'settings/index.html', 'data-feature="authenticator"'],
  ['resizable panels', 'docs/clipboard/toasts.html', 'data-feature="resizable-panels"'],
  ['Base64 in the browser', 'base64/index.html', 'data-feature="base64"'],
  ['dim sum switch that stays on', 'settings/index.html', 'data-feature="dimsum-switch"'],
];

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
buildDiagram();
buildSettings();
buildBase64();
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
  for (const [name, where, marker] of SITE_FEATURES) {
    const targets = where === 'all' ? pages : [path.join(outDir, where)];
    for (const t of targets) if (!fs.existsSync(t) || !read(t).includes(marker)) problems.push(`${path.relative(outDir, t)}: missing site feature "${name}"`);
  }
  const js = read(path.join(outDir, 'app.js'));
  for (const fn of ['initTabs', 'initPalette', 'initContextMenu', 'initLocks', 'initLadder', 'initAuthenticator', 'initAppearanceEditor', 'initLanguage', 'notify', 'initBase64', 'initFolds', 'sizer', 'miniBuilder', 'dimSumNo']) if (!js.includes('function ' + fn)) problems.push(`app.js: missing ${fn}`);
  if (problems.length) fail('site check failed:\n  ' + problems.join('\n  '));
  const list = pages.map((p) => path.relative(outDir, p).replace(/\\/g, '/')).sort();
  writeOut('pages.txt', list.join('\n') + '\n');
  console.log(`[site] built ${list.length} pages, ${nIndex} search entries, ${shots.length} screenshots into ${path.relative(root, outDir)}`);
}
