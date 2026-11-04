#!/usr/bin/env node
// Deterministic line counter used by the release workflow and runnable locally.
//
//   node scripts/line-counts.mjs [--json] [--json-out <file>] [--ref <git ref>]
//
// It counts every file Git tracks at the chosen ref (default HEAD), so untracked and ignored
// files never count and the result depends only on the commit. Output is the Markdown table
// published in release notes, or JSON with --json.
//
// Rules, stated again in the output so the numbers can be checked:
//  * Categories come from file extension and path (see classify()).
//  * Excluded from the project total but still listed: generated files, historical design
//    prototypes, and vendored or build-output trees.
//  * A "line" is a newline-terminated line, plus a final unterminated line if present. A trailing
//    newline is NOT an extra line, which keeps the totals equal to the `git blame` line counts.
//  * Authorship is per surviving line from `git blame`, never from summed diffs. A line is
//    agent-written when its commit author name matches an automation identity (name contains
//    "Claude" or "[bot]") or the commit carries a Co-Authored-By trailer naming an agent
//    (Claude, Copilot, Codex, Gemini, or a bot).
//  * Binary files (those containing a NUL byte) have no lines and are skipped.
import { execFileSync } from 'node:child_process';
import fs from 'node:fs';

const args = process.argv.slice(2);
const asJson = args.includes('--json');
const refIdx = args.indexOf('--ref');
const ref = refIdx >= 0 ? args[refIdx + 1] : 'HEAD';

const git = (a, opts = {}) =>
  execFileSync('git', a, { encoding: 'buffer', maxBuffer: 1 << 30, stdio: ['ignore', 'pipe', 'pipe'], ...opts });
const gitText = (a) => git(a).toString('utf8');

const SOURCE = new Set(['.cs', '.js', '.mjs', '.cjs', '.ts', '.ps1', '.bat', '.cmd', '.sh']);
const MARKUP = new Set(['.xaml', '.css', '.html', '.htm', '.svg', '.xml']);
const DOCS = new Set(['.md', '.txt']);
const LANG = {
  '.cs': 'C#', '.xaml': 'XAML', '.js': 'JavaScript', '.mjs': 'JavaScript', '.cjs': 'JavaScript', '.ts': 'TypeScript',
  '.ps1': 'PowerShell', '.bat': 'Batch', '.cmd': 'Batch', '.sh': 'Shell', '.css': 'CSS', '.html': 'HTML', '.htm': 'HTML',
  '.svg': 'SVG', '.xml': 'XML', '.md': 'Markdown', '.txt': 'Text', '.json': 'JSON', '.yml': 'YAML', '.yaml': 'YAML',
  '.csproj': 'MSBuild', '.props': 'MSBuild', '.manifest': 'XML',
};

const ext = (p) => {
  const base = p.split('/').pop();
  const i = base.lastIndexOf('.');
  return i > 0 ? base.slice(i).toLowerCase() : '';
};

// Returns { category, excluded } where excluded is a reason string or null.
function classify(path) {
  if (/(^|\/)(node_modules|\.deps|bin|obj|dist|out|vendor)\//.test(path)) return { category: 'Vendored or build output', excluded: 'vendored or build-output tree' };
  if (/(^|\/)package-lock\.json$/.test(path)) return { category: 'Generated', excluded: 'lockfile' };
  if (/^docs\/screenshots\//.test(path)) return { category: 'Generated', excluded: 'generated screenshots and their index' };
  if (/^design\//.test(path)) return { category: 'Design prototypes', excluded: 'historical design prototypes, not shipped code' };
  const e = ext(path);
  const isTest = /(^|\/)(tests?|__tests__)\//i.test(path) || /SelfTest|\.test\.|\.spec\./i.test(path);
  if (SOURCE.has(e) || e === '.cs') return { category: isTest ? 'Tests' : 'Source', excluded: null };
  if (MARKUP.has(e)) return { category: 'Styles and markup', excluded: null };
  if (DOCS.has(e)) return { category: 'Documentation', excluded: null };
  return { category: 'Configuration and data', excluded: null };
}

function isAgentIdentity(name) {
  return /claude|\[bot\]|copilot|codex|gemini/i.test(name);
}

// sha -> true when the commit carries a Co-Authored-By trailer naming an agent.
function agentCommits() {
  const out = gitText(['log', ref, '--format=%H%x09%(trailers:key=Co-Authored-By,valueonly,separator=%x3B)']);
  const map = new Map();
  for (const line of out.split('\n')) {
    if (!line) continue;
    const [sha, trailers = ''] = line.split('\t');
    map.set(sha, /claude|copilot|codex|gemini|\[bot\]/i.test(trailers));
  }
  return map;
}

const files = gitText(['ls-tree', '-r', '--name-only', '-z', ref]).split('\0').filter(Boolean);
const agentBySha = agentCommits();

const rows = new Map(); // key -> {total, nonBlank, files}
const langs = new Map();
const excludedRows = new Map();
const authors = new Map(); // author name -> {lines, agent}
let counted = { total: 0, nonBlank: 0 }; // project
let grand = { total: 0, nonBlank: 0 };
let blameLines = 0;
let skippedBinary = 0;

const bump = (m, k, t, nb) => {
  const r = m.get(k) ?? { total: 0, nonBlank: 0, files: 0 };
  r.total += t; r.nonBlank += nb; r.files += 1;
  m.set(k, r);
};

function countLines(text) {
  if (text.length === 0) return { total: 0, nonBlank: 0 };
  const lines = text.split('\n');
  if (lines[lines.length - 1] === '') lines.pop();
  let nb = 0;
  for (const l of lines) if (l.trim() !== '') nb++;
  return { total: lines.length, nonBlank: nb };
}

for (const path of files) {
  let buf;
  try { buf = git(['show', `${ref}:${path}`]); } catch { continue; }
  if (buf.includes(0)) { skippedBinary++; continue; }
  const { total, nonBlank } = countLines(buf.toString('utf8'));
  const { category, excluded } = classify(path);
  grand.total += total; grand.nonBlank += nonBlank;
  if (excluded) { bump(excludedRows, `${category} (${excluded})`, total, nonBlank); continue; }
  counted.total += total; counted.nonBlank += nonBlank;
  bump(rows, category, total, nonBlank);
  bump(langs, LANG[ext(path)] ?? 'Other', total, nonBlank);

  if (total === 0) continue;
  // Surviving-line authorship.
  const blame = gitText(['blame', '--line-porcelain', ref, '--', path]).split('\n');
  let sha = '';
  for (const l of blame) {
    if (/^[0-9a-f]{40} /.test(l)) sha = l.slice(0, 40);
    else if (l.startsWith('author ')) {
      const name = l.slice(7);
      const agent = isAgentIdentity(name) || agentBySha.get(sha) === true;
      const key = name;
      const a = authors.get(key) ?? { lines: 0, agentLines: 0 };
      a.lines++; if (agent) a.agentLines++;
      authors.set(key, a);
      blameLines++;
    }
  }
}

const agentTotal = [...authors.values()].reduce((s, a) => s + a.agentLines, 0);
const agree = blameLines === counted.total;
const fmt = (n) => n.toLocaleString('en-US');
const sha = gitText(['rev-parse', ref]).trim();

const result = {
  commit: sha,
  command: `node scripts/line-counts.mjs${ref === 'HEAD' ? '' : ` --ref ${ref}`}`,
  project: counted,
  grand,
  categories: Object.fromEntries(rows),
  languages: Object.fromEntries(langs),
  excluded: Object.fromEntries(excludedRows),
  authors: Object.fromEntries(authors),
  agentLines: agentTotal,
  blameLines,
  blameAgrees: agree,
  skippedBinaryFiles: skippedBinary,
};

const jo = args.indexOf('--json-out');
if (jo >= 0) fs.writeFileSync(args[jo + 1], JSON.stringify(result, null, 2));

if (asJson) {
  console.log(JSON.stringify(result, null, 2));
  process.exit(agree ? 0 : 1);
}

const table = (title, m, order) => {
  const keys = order ? order.filter((k) => m.has(k)) : [...m.keys()].sort();
  const out = [`| ${title} | Files | Lines | Non-blank |`, '| --- | ---: | ---: | ---: |'];
  for (const k of keys) { const r = m.get(k); out.push(`| ${k} | ${fmt(r.files)} | ${fmt(r.total)} | ${fmt(r.nonBlank)} |`); }
  return out.join('\n');
};

const lines = [];
lines.push(`Counted at commit \`${sha.slice(0, 12)}\` with \`${result.command}\`.`, '');
lines.push(table('Category', rows, ['Source', 'Tests', 'Styles and markup', 'Documentation', 'Configuration and data']));
lines.push('', `**Project total: ${fmt(counted.total)} lines (${fmt(counted.nonBlank)} non-blank).**`, '');
lines.push(table('Language', langs));
lines.push('', 'Excluded from the project total, still counted in the grand total:', '');
lines.push(table('Excluded group', excludedRows));
lines.push('', `**Grand total of everything counted: ${fmt(grand.total)} lines (${fmt(grand.nonBlank)} non-blank).**`, '');
lines.push('Surviving lines by author (`git blame`, project total only):', '', '| Author | Lines | Kind |', '| --- | ---: | --- |');
for (const [name, a] of [...authors.entries()].sort((x, y) => y[1].lines - x[1].lines)) {
  const kind = a.agentLines === a.lines ? 'agent' : a.agentLines === 0 ? 'person' : `${fmt(a.agentLines)} agent, ${fmt(a.lines - a.agentLines)} person`;
  lines.push(`| ${name} | ${fmt(a.lines)} | ${kind} |`);
}
lines.push('', `Agent-written lines: ${fmt(agentTotal)} of ${fmt(blameLines)} (${blameLines ? ((agentTotal / blameLines) * 100).toFixed(1) : '0.0'}%). Rule: a line is agent-written when its commit author name looks like an automation identity or the commit has a Co-Authored-By trailer naming an agent. This is a statement of fact, neither a boast nor an apology.`);
lines.push('', `Consistency check: blame attributes ${fmt(blameLines)} lines and the counter found ${fmt(counted.total)}; ${agree ? 'they agree' : 'THEY DISAGREE, treat these figures as wrong'}. Binary files skipped: ${skippedBinary}.`);
console.log(lines.join('\n'));
process.exit(agree ? 0 : 1);
