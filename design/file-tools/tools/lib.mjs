// Shared building blocks for the file tool artboards. Icon paths are the app's own Icons class (24 grid, stroke),
// plus a short list of additions drawn the same way (listed in HANDOFF.md as icons the implementer must add).
export const P = {
  Text: "M4 6h16M4 12h10M4 18h13", File: "M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8zM14 3v5h5",
  Folder: "M3 7a2 2 0 0 1 2-2h4l2 2h8a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z",
  Image: "M4 5h16v14H4zM4 16l5-5 4 4 2-2 5 5M15 9.5a1.5 1.5 0 1 1 0 .01", Video: "M3 6h13v12H3zM16 10l5-3v10l-5-3",
  Audio: "M9 18V6l11-2v12M9 18a3 3 0 1 1-3-3 3 3 0 0 1 3 3zM20 16a3 3 0 1 1-3-3 3 3 0 0 1 3 3z",
  Palette: "M12 3a9 9 0 1 0 0 18c1.5 0 2-1 2-2s-1-1.5-1-2.5 1-1.5 2-1.5h2a4 4 0 0 0 4-4c0-4.4-4-8-9-8z",
  Braces: "M8 4c-2 0-3 1-3 3v2c0 1-1 2-2 2 1 0 2 1 2 2v2c0 2 1 3 3 3M16 4c2 0 3 1 3 3v2c0 1 1 2 2 2-1 0-2 1-2 2v2c0 2-1 3-3 3",
  Bolt: "M13 3L4 14h7l-1 7 9-11h-7z", Close: "M6 6l12 12M18 6L6 18", Check: "M5 12l5 5 9-10",
  Clock: "M12 3a9 9 0 1 1 0 18 9 9 0 0 1 0-18zM12 7v5l3 2", Restart: "M3 12a9 9 0 1 0 3-6.7L3 8M3 3v5h5",
  Lines: "M4 7h16M4 12h16M4 17h10", Trash: "M4 7h16M9 7V4h6v3M6 7l1 13h10l1-13", Pause: "M9 5v14M15 5v14",
  Shield: "M12 3l8 3v6c0 5-3.5 8-8 9-4.5-1-8-4-8-9V6z", Box: "M12 3l8 4.5v9L12 21l-8-4.5v-9zM12 12l8-4.5M12 12v9M12 12L4 7.5",
  Code: "M8 8l-4 4 4 4M16 8l4 4-4 4M14 5l-4 14", Plus: "M12 5v14M5 12h14", Play: "M7 5l12 7-12 7z",
  Archive: "M3 5h18v4H3zM5 9v10h14V9M10 13h4", Help: "M12 3a9 9 0 1 1 0 18 9 9 0 0 1 0-18zM9.5 9.5a2.5 2.5 0 1 1 3.5 2.3c-.6.3-1 .9-1 1.6V14M12 17h.01",
  Search: "M11 4a7 7 0 1 1 0 14 7 7 0 0 1 0-14zM20 20l-4-4", Globe: "M12 3a9 9 0 1 1 0 18 9 9 0 0 1 0-18zM3 12h18M12 3c3 3 3 15 0 18M12 3c-3 3-3 15 0 18",
  // additions, same grid and stroke
  Scissors: "M6 5a3 3 0 1 0 0 6 3 3 0 0 0 0-6zM6 13a3 3 0 1 0 0 6 3 3 0 0 0 0-6zM8.5 9.5L20 18M8.5 14.5L20 6",
  Hash: "M5 9h14M5 15h14M10 4L8 20M16 4l-2 16", Pin: "M12 21s-7-6.2-7-11a7 7 0 0 1 14 0c0 4.8-7 11-7 11zM12 7.5a2.5 2.5 0 1 0 0 5 2.5 2.5 0 0 0 0-5z",
  Cert: "M5 4h14v11H5zM8 8h8M8 11h5M9 15l-1 5 3-1.5 3 1.5-1-5", Disc: "M12 3a9 9 0 1 1 0 18 9 9 0 0 1 0-18zM12 10a2 2 0 1 0 0 4 2 2 0 0 0 0-4z",
  Font: "M5 20L11 4h2l6 16M7.5 14h9", Table: "M4 5h16v14H4zM4 10h16M10 5v14", Warn: "M12 4l9 16H3zM12 10v4M12 17h.01",
  Info: "M12 3a9 9 0 1 1 0 18 9 9 0 0 1 0-18zM12 11v5M12 8h.01", Copy: "M9 9h11v11H9zM5 15V4h11", Lock: "M6 11h12v9H6zM8 11V8a4 4 0 0 1 8 0v3",
  Eject: "M12 5l7 8H5zM5 17h14", Eye: "M2 12s4-7 10-7 10 7 10 7-4 7-10 7S2 12 2 12zM12 9.5a2.5 2.5 0 1 0 0 5 2.5 2.5 0 0 0 0-5z",
  Wave: "M3 12h2M7 8v8M11 4v16M15 7v10M19 10v4", Subtitle: "M3 6h18v12H3zM7 11h4M13 11h4M7 14.5h6", Chevron: "M9 6l6 6-6 6",
  Down: "M12 4v11M7 11l5 5 5-5M5 20h14", Minus: "M5 12h14", Link: "M10 14a4 4 0 0 0 5.7 0l3-3a4 4 0 0 0-5.7-5.7l-1 1M14 10a4 4 0 0 0-5.7 0l-3 3a4 4 0 0 0 5.7 5.7l1-1",
  Key: "M15 4a5 5 0 1 0 0 10 5 5 0 0 0 0-10zM11.5 12.5L4 20v-3M7 17l-2-2", Stamp: "M8 4h8v7l3 3v2H5v-2l3-3zM5 20h14",
  Sliders: "M4 7h10M18 7h2M4 17h2M10 17h10M16 4v6M8 14v6", Crop: "M7 3v14h14M3 7h14v14", Refresh: "M3 12a9 9 0 1 0 3-6.7L3 8M3 3v5h5",
};
export const ic = (n, c = "") => `<svg class="i ${c}" viewBox="0 0 24 24" aria-hidden="true"><path d="${P[n]}"/></svg>`;

export const btn = (label, o = {}) => `<button class="btn ${o.k || "ghost"}${o.sm ? " sm" : ""}"${o.dis ? " disabled" : ""}${o.aria ? ` aria-label="${o.aria}"` : ""}>${o.i ? ic(o.i, "s") : ""}${label}</button>`;
export const chip = (label, on = false, i = "") => `<span class="chip${on ? " on" : ""}">${i ? ic(i, "s") : ""}${label}</span>`;
export const badge = (label, k = "", i = "") => `<span class="badge ${k}">${i ? ic(i, "s") : ""}${label}</span>`;
export const field = (label, value, o = {}) => `<div class="fld"${o.style ? ` style="${o.style}"` : ""}><span class="lbl">${label}</span><div class="in${o.focus ? " focus" : ""}${o.err ? " err" : ""}${o.dis ? " dis" : ""}">${o.i ? ic(o.i, "s") : ""}<span class="v${o.mono ? " mono" : ""}">${value}</span>${o.unit ? `<span class="u">${o.unit}</span>` : ""}${o.after || ""}</div>${o.hint ? `<span class="hint${o.hk ? " " + o.hk : ""}">${o.hint}</span>` : ""}</div>`;
export const seg = (opts, on) => `<div class="seg" role="radiogroup">${opts.map((x, i) => `<span class="${i === on ? "on" : ""}" role="radio" aria-checked="${i === on}">${i === on ? ic("Check", "s") : ""}${x}</span>`).join("")}</div>`;
export const sw = (title, sub, on = false) => `<div class="sw"><div class="t"><b>${title}</b>${sub ? `<span>${sub}</span>` : ""}</div><span class="tg${on ? " on" : ""}" role="switch" aria-checked="${on}"></span></div>`;
export const cb = (on) => `<span class="cb${on ? " on" : ""}">${on ? ic("Check") : ""}</span>`;
export const rb = (on) => `<span class="rb${on ? " on" : ""}"></span>`;
export const slider = (label, valText, pct, extra = "") => `<div class="fld"><div class="sl-top"><b>${label}</b><span>${valText}</span></div><div class="slider"><div class="tr"></div><div class="fi" style="width:${pct}%"></div><div class="th" style="left:${pct}%"></div></div>${extra}</div>`;
export const prog = (pct, ind = false) => `<div class="prog${ind ? " ind" : ""}" role="progressbar" aria-valuemin="0" aria-valuemax="100"${ind ? "" : ` aria-valuenow="${pct}"`}><i style="width:${pct}%"></i></div>`;
export const banner = (k, title, text, act = "", i = null) => `<div class="banner ${k}" role="${k === "bad" ? "alert" : "status"}">${ic(i || { ok: "Check", warn: "Warn", bad: "Warn", info: "Info" }[k], "l")}<div class="grow"><b>${title}</b>${text}</div>${act}</div>`;
export const kv = (rows) => `<dl class="kv">${rows.map(([k, v]) => `<dt>${k}</dt><dd>${v}</dd>`).join("")}</dl>`;
export const card = (inner, style = "") => `<div class="card"${style ? ` style="${style}"` : ""}>${inner}</div>`;
export const sect = (t) => `<div class="sect">${t}</div>`;

let uid = 0;
// One window. `foot` is {note, left, cancel, primary:{label,i,dis}}.
export function win({ title, tool = "", icon, file, meta, w = 720, body, foot, label }) {
  uid++;
  const f = foot || {};
  const prim = f.primary === null ? "" : btn(f.primary?.label || "Done", { k: "acc", i: f.primary?.i, dis: f.primary?.dis });
  return `<section class="win" role="dialog" aria-modal="true" aria-labelledby="t${uid}" style="width:${w}px">
  <header class="wh"><span class="tile">${ic(icon, "l")}</span><div class="wt"><h2 class="tool" id="t${uid}" style="margin:0">${title}</h2><div class="file">${file ? `<b>${file}</b>` : ""}${meta ? `<span>${meta}</span>` : ""}</div></div><button class="xbtn" aria-label="Close (Esc)">${ic("Close")}</button></header>
  <div class="wb">${body}</div>
  <footer class="wf">${f.left ? f.left : ""}<div class="note" aria-live="polite">${f.note || ""}</div>${f.cancel === false ? "" : btn(f.cancel || "Cancel", { k: "ghost" })}${prim}</footer>
</section>`;
}
export const cell = (cap, html, tag = "") => `<div class="cell"><div class="cap"><b>${cap}</b>${tag}</div>${html}</div>`;

// Generic state bodies shared by every tool.
export const workingBody = ({ head, sub, pct, rows = [], ind = false }) => `<div class="card" style="gap:14px"><div style="display:flex;align-items:baseline;gap:10px"><span class="big">${ind ? "Working" : pct + "%"}</span><span class="sub">${head}</span></div>${prog(pct, ind)}<div class="sub">${sub}</div></div>${rows.length ? `<div class="list">${rows.join("")}</div>` : ""}`;
export const doneBody = ({ head, text, rows, actions }) => `${banner("ok", head, text)}${rows ? card(kv(rows)) : ""}<div style="display:flex;gap:8px;flex-wrap:wrap">${actions}</div>`;
export const errorBody = ({ head, text, why, actions }) => `${banner("bad", head, text)}<div class="card"><div class="sect">WHAT WAS NOT CHANGED</div><div class="sub">${why}</div></div><div style="display:flex;gap:8px;flex-wrap:wrap">${actions}</div>`;

// Deterministic pseudo random for waveforms and thumbnails.
export function rnd(seed) { let s = seed; return () => (s = (s * 1664525 + 1013904223) % 4294967296) / 4294967296; }
export function board({ num, title, tags, cells, one = false, theme = "" }) {
  return `<!doctype html>
<html lang="en"${theme ? ` data-theme="${theme}"` : ""}>
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>${title}</title>
<link rel="stylesheet" href="./fonts.css">
<link rel="stylesheet" href="./file-tools.css">
<script>(function(){var q=new URLSearchParams(location.search).get("theme");if(q)document.documentElement.setAttribute("data-theme",q);else if(matchMedia("(prefers-color-scheme: light)").matches)document.documentElement.setAttribute("data-theme","light");})();</script>
</head>
<body>
<main class="board">
  <header class="bhead"><h1><span class="num">${num} ·</span> ${title}</h1><div class="bmeta">${tags.map((t) => `<span class="tag">${t}</span>`).join("")}<button class="theme-toggle" onclick="var r=document.documentElement;r.setAttribute('data-theme',r.getAttribute('data-theme')==='light'?'dark':'light')">${ic("Palette", "s")}Light or dark</button></div></header>
  <div class="grid${one ? " one" : ""}">${cells.join("\n")}</div>
</main>
</body>
</html>
`;
}
