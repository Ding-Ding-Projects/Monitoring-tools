#!/usr/bin/env node
// Generates CHANGELOG.md from Git history: one section per release tag (vMAJOR.MINOR.PATCH),
// newest first, plus an Unreleased section for commits after the latest tag.
//   node scripts/changelog.mjs [--out CHANGELOG.md]
// Every entry is a real commit subject with its full SHA linked to the commit page, so each
// reference can be checked. Nothing is invented or summarised.
import { execFileSync } from 'node:child_process';
import fs from 'node:fs';

const git = (a) => execFileSync('git', a, { encoding: 'utf8', maxBuffer: 1 << 28 }).trim();
const i = process.argv.indexOf('--out');
const out = i >= 0 ? process.argv[i + 1] : 'CHANGELOG.md';
const slug = 'Ding-Ding-Projects/Monitoring-tools';

const tags = git(['tag', '--list', 'v*', '--sort=-v:refname']).split('\n').filter((t) => /^v\d+\.\d+\.\d+$/.test(t));
const date = (ref) => git(['log', '-1', '--format=%cs', ref]);
const commits = (range) =>
  git(['log', '--no-merges', '--format=%H%x09%s', range]).split('\n').filter(Boolean).map((l) => {
    const [sha, ...s] = l.split('\t');
    return { sha, subject: s.join('\t') };
  });
const entry = (c) => `- ${c.subject} ([\`${c.sha.slice(0, 7)}\`](https://github.com/${slug}/commit/${c.sha}))`;

const lines = [
  '# Changelog',
  '',
  'Generated from Git history by `node scripts/changelog.mjs`; do not edit by hand. Each release lists the commits',
  'that landed since the previous release tag, newest first. Dim sum code names appear in the release notes of',
  'releases published after that feature was added.',
  '',
];
const head = git(['rev-parse', 'HEAD']);
const newest = tags[0];
const unreleased = newest ? commits(`${newest}..HEAD`) : commits('HEAD');
if (unreleased.length) {
  lines.push('## Unreleased', '', `Commits after ${newest ?? 'the start of history'}, up to \`${head.slice(0, 7)}\`.`, '', ...unreleased.map(entry), '');
}
tags.forEach((t, k) => {
  const prev = tags[k + 1];
  const cs = commits(prev ? `${prev}..${t}` : t);
  lines.push(`## ${t.slice(1)} (${date(t)})`, '', `[Release page](https://github.com/${slug}/releases/tag/${t})`, '', ...(cs.length ? cs.map(entry) : ['- No commits of its own.']), '');
});
fs.writeFileSync(out, lines.join('\n'));
console.log(`Wrote ${out}: ${tags.length} releases, ${unreleased.length} unreleased commits`);
