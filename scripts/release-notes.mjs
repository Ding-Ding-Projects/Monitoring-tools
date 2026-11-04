#!/usr/bin/env node
// Builds the release notes and the README figures block for one release.
//
//   node scripts/release-notes.mjs --version 1.0.13 --ordinal 13 \
//        --started 2026-01-01T00:00:00Z --ended 2026-01-01T00:20:00Z \
//        --counts counts.json --intro intro.md --checks "text" --out notes.md [--readme README.md]
//
// counts.json is the output of `node scripts/line-counts.mjs --json`.
// Everything here is deterministic given its arguments, except the optional HEAD request that
// confirms the chosen dish photo is really published (a failed request only omits the photo).
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const argv = process.argv.slice(2);
const opt = (name, fallback = '') => {
  const i = argv.indexOf(`--${name}`);
  return i >= 0 ? argv[i + 1] : fallback;
};

const version = opt('version');
const ordinal = Number.parseInt(opt('ordinal', '-1'), 10);
const started = opt('started');
const ended = opt('ended');
const counts = JSON.parse(fs.readFileSync(opt('counts'), 'utf8'));
const intro = opt('intro') ? fs.readFileSync(opt('intro'), 'utf8').trim() : '';
const checks = opt('checks', 'No checks were recorded for this release.');
const outFile = opt('out');
const readmeFile = opt('readme');

const fmt = (n) => n.toLocaleString('en-US');

// ---- timing -------------------------------------------------------------------------------
function timingSection() {
  const s = Date.parse(started);
  const e = Date.parse(ended);
  if (!Number.isFinite(s) || !Number.isFinite(e) || e < s) {
    return '- Workflow started: not available\n- Workflow completed: not available\n- Workflow duration: not available\n\nTiming evidence was missing, so nothing is estimated here.';
  }
  const secs = Math.round((e - s) / 1000);
  const pad = (n) => String(n).padStart(2, '0');
  const dur = `${pad(Math.floor(secs / 3600))}:${pad(Math.floor((secs % 3600) / 60))}:${pad(secs % 60)}`;
  const iso = (ms) => new Date(ms).toISOString().replace(/\.\d{3}Z$/, 'Z');
  return `- Workflow started: ${iso(s)}\n- Workflow completed: ${iso(e)}\n- Workflow duration: ${dur}\n\nMeasured by the workflow itself, from the start of the build job to the end of the publication step.`;
}

// ---- dim sum code name --------------------------------------------------------------------
async function codeNameSection() {
  const list = JSON.parse(fs.readFileSync(path.join(here, 'dim-sum-code-names.json'), 'utf8'));
  if (!(ordinal >= 0) || ordinal >= list.dishes.length) {
    return { text: 'No unused dim sum code name was available for this release, so it ships with its version number alone.', name: null };
  }
  const d = list.dishes[ordinal];
  const url = `${list.source}/releases/download/${d.release}/${d.file}`;
  let published = false;
  try {
    const r = await fetch(url, { method: 'HEAD', redirect: 'follow' });
    published = r.ok;
  } catch { /* offline or blocked: handled below */ }
  const label = `${d.en} · ${d.zhHant}`;
  if (!published) {
    return {
      text: `Planned code name: ${label}. Its public photo could not be confirmed as published when these notes were written, so the code name and photo link are omitted and this release is identified by its version alone.`,
      name: null,
    };
  }
  return {
    text:
      `**${label}**\n\n` +
      `[![${d.en}, a dim sum dish](${url})](${url})\n\n` +
      `Photo: [${d.en}](${url}), published in the public [dim sum photo catalogue](${list.source}). The photo is linked, not copied. ` +
      `Code names are picked by release order from \`scripts/dim-sum-code-names.json\` (entry ${ordinal + 1} of ${list.dishes.length}), so each release takes a different dish.`,
    name: label,
  };
}

// ---- human time estimate ------------------------------------------------------------------
// Formula (also printed in the output):
//   code lines  = non-blank lines of Source + Tests + Styles and markup
//   doc lines   = non-blank lines of Documentation
//   code days   = code lines / 50 (fast) to code lines / 20 (slow)
//   doc days    = doc lines / 150 (fast) to doc lines / 75 (slow)
//   total       = code days + doc days, in 8-hour working days; months = days / 20
// No multiplier is applied. Generated files, lockfiles, design prototypes and vendored trees are
// excluded exactly as in the line counts above.
function estimate() {
  const c = counts.categories;
  const nb = (k) => c[k]?.nonBlank ?? 0;
  const code = nb('Source') + nb('Tests') + nb('Styles and markup');
  const docs = nb('Documentation');
  const fastDays = code / 50 + docs / 150;
  const slowDays = code / 20 + docs / 75;
  const months = (d) => d / 20;
  const round = (n) => Math.round(n);
  return { code, docs, fastDays, slowDays, text:
    `This is an estimate, not a measurement: nobody wrote this project by hand, and the true figure could be well outside this range.\n\n` +
    `- Inputs: ${fmt(code)} non-blank hand-written code lines (source, tests, styles and markup) and ${fmt(docs)} non-blank documentation lines, taken from the line counts above. Generated files, lockfiles, design prototypes and vendored or build-output trees are excluded, exactly as in the counts.\n` +
    `- Assumed rates: code 50 lines per working day (fast) to 20 (slow); documentation 150 to 75 lines per working day; 8-hour days; 20 working days per month; no complexity multiplier.\n` +
    `- Arithmetic: ${fmt(code)}/50 + ${fmt(docs)}/150 = ${fmt(round(fastDays))} days; ${fmt(code)}/20 + ${fmt(docs)}/75 = ${fmt(round(slowDays))} days.\n` +
    `- Estimate: about ${fmt(round(fastDays))} to ${fmt(round(slowDays))} working days for one person, roughly ${months(fastDays).toFixed(0)} to ${months(slowDays).toFixed(0)} working months. This is information about scale, not a claim of merit.`,
  };
}

// ---- assemble -----------------------------------------------------------------------------
const cn = await codeNameSection();
const est = estimate();
const countsTable = fs.existsSync(opt('counts-md')) && opt('counts-md') ? fs.readFileSync(opt('counts-md'), 'utf8').trim() : '';

const parts = [];
parts.push(`# Monitoring Tools v${version}${cn.name ? `, ${cn.name}` : ''}`);
if (intro) parts.push(intro);
parts.push('## Dim sum code name\n\n' + cn.text);
parts.push('## Workflow timing\n\n' + timingSection());
parts.push('## Checks\n\n' + checks);
parts.push(
  '## Line counts\n\nCounted by this release workflow, not by hand, with `node scripts/line-counts.mjs` (runnable locally on any commit).\n\n' +
    (countsTable || 'The full table was not attached to this run.'),
);
parts.push('## How long a human would have taken\n\n' + est.text);
const notes = parts.join('\n\n') + '\n';
if (outFile) fs.writeFileSync(outFile, notes);
else process.stdout.write(notes);

// ---- README figures block -----------------------------------------------------------------
if (readmeFile) {
  const start = '<!-- release-figures:start -->';
  const end = '<!-- release-figures:end -->';
  const block =
    `${start}\n` +
    `Figures from release v${version} (commit \`${counts.commit.slice(0, 12)}\`), refreshed automatically by the release workflow. The release notes are the record; this is a convenience copy.\n\n` +
    `- Project total: ${fmt(counts.project.total)} lines (${fmt(counts.project.nonBlank)} non-blank); grand total with excluded rows: ${fmt(counts.grand.total)} lines.\n` +
    `- Source ${fmt(counts.categories.Source?.total ?? 0)}, tests ${fmt(counts.categories.Tests?.total ?? 0)}, styles and markup ${fmt(counts.categories['Styles and markup']?.total ?? 0)}, documentation ${fmt(counts.categories.Documentation?.total ?? 0)}, configuration and data ${fmt(counts.categories['Configuration and data']?.total ?? 0)}.\n` +
    `- Agent-written surviving lines: ${fmt(counts.agentLines)} of ${fmt(counts.blameLines)}, by \`git blame\`.\n` +
    `- Human time estimate: about ${fmt(Math.round(est.fastDays))} to ${fmt(Math.round(est.slowDays))} working days for one person, an estimate rather than a measurement (method below).\n` +
    `${end}`;
  const text = fs.readFileSync(readmeFile, 'utf8');
  const re = new RegExp(`${start}[\\s\\S]*?${end}`);
  if (!re.test(text)) { console.error('README figures markers not found'); process.exit(1); }
  fs.writeFileSync(readmeFile, text.replace(re, () => block));
}
