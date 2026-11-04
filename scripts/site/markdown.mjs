// A small, dependency-free Markdown renderer for the project documentation.
// Supports: headings with ids, paragraphs, bullet and numbered lists (nested by indent), tables, fenced code,
// block quotes, rules, inline code, bold, italic, links and images. Everything else is escaped as text.

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
  s = esc(s);
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
  return { html: out.join('\n'), headings, title };
}
