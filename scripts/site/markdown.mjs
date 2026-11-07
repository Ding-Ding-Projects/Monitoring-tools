// A small, dependency-free Markdown renderer for the project documentation.
// Supports: headings with ids, paragraphs, bullet and numbered lists (nested by indent), tables, fenced code,
// block quotes, rules, inline code, bold, italic, links and images, and :shortcode: emoji (the same list as the app).
// Everything else is escaped as text. A document with nothing under its title says so.

// The app's emoji shortcodes (src/MonitoringTools/Core/MarkdownText.cs). An unknown one, and a time such as 12:30:45,
// stays as it was written; code is never touched.
export const EMOJI = {
  rocket: '\u{1F680}', tada: '\u{1F389}', sparkles: '\u2728', bug: '\u{1F41B}', fire: '\u{1F525}', star: '\u2B50', star2: '\u{1F31F}',
  white_check_mark: '\u2705', heavy_check_mark: '\u2714\uFE0F', check: '\u2714\uFE0F', x: '\u274C', warning: '\u26A0\uFE0F', no_entry: '\u26D4',
  construction: '\u{1F6A7}', lock: '\u{1F512}', unlock: '\u{1F513}', memo: '\u{1F4DD}', pencil: '\u{1F4DD}', pencil2: '\u270F\uFE0F',
  book: '\u{1F4D6}', books: '\u{1F4DA}', package: '\u{1F4E6}', wrench: '\u{1F527}', hammer: '\u{1F528}', gear: '\u2699\uFE0F', zap: '\u26A1',
  art: '\u{1F3A8}', recycle: '\u267B\uFE0F', arrow_up: '\u2B06\uFE0F', arrow_down: '\u2B07\uFE0F', arrow_right: '\u27A1\uFE0F', arrow_left: '\u2B05\uFE0F',
  heart: '\u2764\uFE0F', green_heart: '\u{1F49A}', thumbsup: '\u{1F44D}', '+1': '\u{1F44D}', thumbsdown: '\u{1F44E}', '-1': '\u{1F44E}', eyes: '\u{1F440}',
  bulb: '\u{1F4A1}', mag: '\u{1F50D}', link: '\u{1F517}', calendar: '\u{1F4C5}', clock: '\u{1F552}', hourglass: '\u231B',
  chart_with_upwards_trend: '\u{1F4C8}', bar_chart: '\u{1F4CA}', computer: '\u{1F4BB}', bell: '\u{1F514}', camera: '\u{1F4F7}',
  floppy_disk: '\u{1F4BE}', cd: '\u{1F4BF}', dvd: '\u{1F4C0}', file_folder: '\u{1F4C1}', open_file_folder: '\u{1F4C2}', page_facing_up: '\u{1F4C4}',
  clipboard: '\u{1F4CB}', pushpin: '\u{1F4CC}', shield: '\u{1F6E1}\uFE0F', test_tube: '\u{1F9EA}', boom: '\u{1F4A5}', clap: '\u{1F44F}', wave: '\u{1F44B}',
  smile: '\u{1F604}', thinking: '\u{1F914}', dumpling: '\u{1F95F}', tea: '\u{1F375}', information_source: '\u2139\uFE0F', new: '\u{1F195}',
  globe: '\u{1F310}', earth_asia: '\u{1F30F}', moon: '\u{1F319}', sunny: '\u2600\uFE0F',
};
export const withEmoji = (text) => text.replace(/:([a-z0-9_+\-]{1,40}):/gi, (m, k) => EMOJI[k.toLowerCase()] || m);

export const esc = (s) => String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');

export function slugify(text) {
  return text.toLowerCase().replace(/<[^>]*>/g, '').replace(/[^a-z0-9\s-]/g, '').trim().replace(/\s+/g, '-') || 'section';
}

export function plainText(md) {
  return md
    .replace(/```[\s\S]*?```/g, ' ')
    .replace(/!\[([^\]]*)\]\([^)]*\)/g, '$1')
    .replace(/\[([^\]]*)\]\([^)]*\)/g, '$1')
    .replace(/[`*_>#|]/g, '')
    .replace(/^\s*[-+]\s+/gm, '')
    .replace(/\s+/g, ' ')
    .trim();
}

// ctx.resolveLink(url) -> { href, external } ; ctx.resolveImage(url) -> src
export function inline(src, ctx) {
  const codes = [];
  let s = src.replace(/`([^`]+)`/g, (_, c) => { codes.push(c); return `\u0000${codes.length - 1}\u0000`; });
  s = esc(withEmoji(s));
  s = s.replace(/!\[([^\]]*)\]\(([^)\s]+)(?:\s+&quot;[^&]*&quot;)?\)/g, (_, alt, url) =>
    `<img src="${esc(ctx.resolveImage(url.replace(/&amp;/g, '&')))}" alt="${alt}" loading="lazy">`);
  s = s.replace(/\[([^\]]+)\]\(([^)\s]+)(?:\s+&quot;[^&]*&quot;)?\)/g, (_, text, url) => {
    const r = ctx.resolveLink(url.replace(/&amp;/g, '&'));
    const extra = r.external ? ' rel="noopener noreferrer"' : '';
    const cls = r.source ? ' class="source-link"' : '';
    return `<a href="${esc(r.href)}"${extra}${cls}>${text}</a>`;
  });
  s = s.replace(/&lt;(https?:\/\/[^\s&]+)&gt;/g, '<a href="$1" rel="noopener noreferrer">$1</a>');
  s = s.replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>');
  s = s.replace(/(^|[\s(>])\*([^*\s][^*]*)\*(?=[\s).,;:!?<]|$)/g, '$1<em>$2</em>');
  s = s.replace(/(^|[\s(>])_([^_\s][^_]*)_(?=[\s).,;:!?<]|$)/g, '$1<em>$2</em>');
  s = s.replace(/\u0000(\d+)\u0000/g, (_, i) => `<code>${esc(codes[Number(i)])}</code>`);
  return s;
}

const isFence = (l) => /^\s*```/.test(l);
const isHeading = (l) => /^#{1,6}\s+\S/.test(l);
const isListItem = (l) => /^\s*([-*+]|\d+[.)])\s+\S/.test(l);
const isQuote = (l) => /^\s*>/.test(l);
const isRule = (l) => /^\s*([-*_])\1\1+\s*$/.test(l);
const isTableSep = (l) => /^\s*\|?\s*:?-{2,}:?\s*(\|\s*:?-{2,}:?\s*)*\|?\s*$/.test(l) && l.includes('-');
const splitRow = (l) => {
  let t = l.trim();
  if (t.startsWith('|')) t = t.slice(1);
  if (t.endsWith('|') && !t.endsWith('\\|')) t = t.slice(0, -1);
  return t.split(/(?<!\\)\|/).map((c) => c.trim().replace(/\\\|/g, '|'));
};

// Returns { html, headings: [{level, text, id}], title }
export function render(md, ctx) {
  const lines = md.replace(/\r\n?/g, '\n').split('\n');
  const headings = [];
  const usedIds = new Map();
  const out = [];
  let title = '';

  const uniqueId = (text) => {
    const base = slugify(text);
    const n = usedIds.get(base) || 0;
    usedIds.set(base, n + 1);
    return n ? `${base}-${n}` : base;
  };

  function parseList(start) {
    const m0 = lines[start].match(/^(\s*)([-*+]|\d+[.)])\s+/);
    const baseIndent = m0[1].length;
    const ordered = /\d/.test(m0[2]);
    const items = [];
    let i = start;
    while (i < lines.length) {
      const line = lines[i];
      const m = line.match(/^(\s*)([-*+]|\d+[.)])\s+(.*)$/);
      if (m && m[1].length === baseIndent) {
        items.push({ text: [m[3]], children: null });
        i++;
      } else if (m && m[1].length > baseIndent && items.length) {
        const sub = parseList(i);
        items[items.length - 1].children = sub.html;
        i = sub.next;
      } else if (line.trim() && /^\s+\S/.test(line) && !isListItem(line) && items.length) {
        items[items.length - 1].text.push(line.trim());
        i++;
      } else break;
    }
    const tag = ordered ? 'ol' : 'ul';
    const html = `<${tag}>${items.map((it) => `<li>${inline(it.text.join(' '), ctx)}${it.children || ''}</li>`).join('')}</${tag}>`;
    return { html, next: i };
  }

  let i = 0;
  while (i < lines.length) {
    const line = lines[i];
    if (!line.trim()) { i++; continue; }

    if (isFence(line)) {
      const lang = line.trim().slice(3).trim();
      const buf = [];
      i++;
      while (i < lines.length && !isFence(lines[i])) buf.push(lines[i++]);
      i++;
      out.push(`<div class="code-wrap"><pre><code${lang ? ` class="lang-${esc(lang)}"` : ''}>${esc(buf.join('\n'))}</code></pre></div>`);
      continue;
    }
    if (isHeading(line)) {
      const m = line.match(/^(#{1,6})\s+(.*?)\s*#*\s*$/);
      const level = m[1].length;
      const text = m[2];
      if (level === 1 && !title) { title = plainText(text); i++; continue; }
      const id = uniqueId(plainText(text));
      headings.push({ level, text: plainText(text), id });
      out.push(`<h${level} id="${id}"><a class="anchor" href="#${id}" aria-label="Link to this section">#</a>${inline(text, ctx)}</h${level}>`);
      i++;
      continue;
    }
    if (isRule(line)) { out.push('<hr>'); i++; continue; }
    if (line.includes('|') && i + 1 < lines.length && isTableSep(lines[i + 1])) {
      const head = splitRow(line);
      i += 2;
      const rows = [];
      while (i < lines.length && lines[i].trim() && lines[i].includes('|')) rows.push(splitRow(lines[i++]));
      out.push(`<div class="table-wrap" tabindex="0" role="region" aria-label="Table, scrolls sideways"><table><thead><tr>${head.map((c) => `<th scope="col">${inline(c, ctx)}</th>`).join('')}</tr></thead><tbody>${rows.map((r) => `<tr>${head.map((_, k) => `<td>${inline(r[k] || '', ctx)}</td>`).join('')}</tr>`).join('')}</tbody></table></div>`);
      continue;
    }
    if (isQuote(line)) {
      const buf = [];
      while (i < lines.length && isQuote(lines[i])) buf.push(lines[i++].replace(/^\s*>\s?/, ''));
      out.push(`<blockquote>${render(buf.join('\n'), ctx).html}</blockquote>`);
      continue;
    }
    if (isListItem(line)) {
      const r = parseList(i);
      out.push(r.html);
      i = r.next;
      continue;
    }
    const buf = [];
    while (i < lines.length && lines[i].trim() && !isFence(lines[i]) && !isHeading(lines[i]) && !isListItem(lines[i]) && !isQuote(lines[i]) && !isRule(lines[i])
      && !(lines[i].includes('|') && i + 1 < lines.length && isTableSep(lines[i + 1]))) buf.push(lines[i++].trim());
    out.push(`<p>${inline(buf.join(' '), ctx)}</p>`);
  }
  // nothing under the title: say so instead of a blank page
  if (!out.some((h) => !/^<h1[\s>]/.test(h))) out.push('<p class="empty-doc">Nothing is written here yet.</p>');
  return { html: out.join('\n'), headings, title };
}
