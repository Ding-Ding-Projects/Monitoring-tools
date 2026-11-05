import { ic, btn, chip, badge, field, seg, sw, cb, rb, slider, prog, banner, kv, card, sect, win, cell, workingBody, doneBody, errorBody } from "./lib.mjs";
const close = { note: "", cancel: false, primary: { label: "Close" } };
const tn = (h, l = 38) => `background:linear-gradient(135deg,hsl(${h} 32% ${l}%),hsl(${h + 30} 28% ${l - 10}%))`;

// ---------------------------------------------------------------- 11 Data
const rows = [["1001", "Ava Chen", "Toronto", "128.50", "2025-03-02"], ["1002", "Luis Ortega", "Madrid", "64.00", "2025-03-02"], ["1003", "Mei Tanaka", "Osaka", "212.75", "2025-03-03"], ["1004", "Sam Okafor", "Lagos", "39.90", "2025-03-04"], ["1005", "Ines Rossi", "Milan", "305.00", "2025-03-04"]];
const dtable = `<div class="list"><div class="lr hd" style="grid-template-columns:64px 1.1fr 1fr 80px 100px"><span>id</span><span>name</span><span>city</span><span class="r">total</span><span class="r">date</span></div>${rows.map((r, i) => `<div class="lr${i === 2 ? " sel" : ""}" style="grid-template-columns:64px 1.1fr 1fr 80px 100px;min-height:38px"><span class="mono">${r[0]}</span><span>${r[1]}</span><span>${r[2]}</span><span class="r">${r[3]}</span><span class="r">${r[4]}</span></div>`).join("")}</div>`;
const tabs = (on) => seg(["Table", "Text"], on);
function data() {
  const main = win({ title: "Data viewer and converter", icon: "Table", file: "orders-march.csv", meta: "1,204 rows · 5 columns · 62 KB", w: 800,
    body: `${banner("ok", "Valid CSV", "Comma separated, header row found, every row has 5 values. Shown: the first 500 rows.")}<div style="display:flex;gap:12px;align-items:center;flex-wrap:wrap">${tabs(0)}<span class="sub">Separator</span>${chip("Comma", true)}${chip("Semicolon")}${chip("Tab")}${chip("Auto")}</div>${dtable}
      ${card(`${sect("CONVERT TO")}<div style="display:flex;gap:12px;flex-wrap:wrap;align-items:center">${seg(["JSON", "YAML", "CSV"], 0)}${sw("Pretty print", "Indent 2 spaces", true)}</div>${field("Save as", "orders-march.json", { i: "File" })}`)}`,
    foot: { note: "Convert saves a new file. Format in place rewrites this one.", left: btn("Format in place", { k: "ghost", i: "Lines" }), primary: { label: "Convert", i: "Refresh" } } });
  const bad = win({ title: "Data viewer and converter", icon: "Table", file: "settings.json", meta: "412 bytes", w: 800,
    body: `${banner("bad", "Invalid JSON at line 7, column 14", "A comma is missing after the value on line 6. Nothing can be converted until it is fixed.")}<div class="pre"><span style="color:var(--textsoft)">5   </span>  "retries": 3,\n<span style="color:var(--textsoft)">6   </span>  "mode": "fast"\n<span class="hlb">7   </span>  <span class="hlb">"path"</span>: "D:\\\\data"\n<span style="color:var(--textsoft)">8   </span>}</div><div style="display:flex;gap:8px">${btn("Go to the error", { k: "ghost", i: "Search" })}${btn("Copy message", { k: "ghost", i: "Copy" })}</div>`,
    foot: { note: "", primary: { label: "Convert", dis: true } } });
  const yaml = win({ title: "Data viewer and converter", icon: "Table", file: "config.yaml", meta: "1.1 KB", w: 800,
    body: `${banner("ok", "Valid YAML", "2 levels, 14 keys. No duplicate keys, no tabs used for indentation.")}${tabs(1)}<div class="pre">server:\n  host: example.org\n  port: 8080\n  tls: true\nfeatures:\n  - search\n  - export\n  - <span class="hlg">notifications</span></div>${field("Convert to", "JSON, pretty printed", { i: "Braces", after: ic("Chevron", "s") })}`,
    foot: { note: "", primary: { label: "Convert", i: "Refresh" } } });
  const done = win({ title: "Data viewer and converter", icon: "Table", file: "orders-march.csv", meta: "converted", w: 800,
    body: doneBody({ head: "Saved orders-march.json", text: "1,204 rows became 1,204 objects with the header names as keys.", rows: [["Size", "188 KB (CSV was 62 KB)"], ["Numbers", "total kept as numbers, id kept as text"]], actions: btn("Show in folder", { k: "ghost", i: "Folder" }) + btn("Copy to clipboard", { k: "ghost", i: "Copy" }) }), foot: close });
  return { cells: [cell("Ready, valid table", main), cell("Invalid file", bad), cell("Text view, YAML", yaml), cell("Done", done)] };
}

// ---------------------------------------------------------------- 12 Encoding
const erow = (n, enc, eol, k, sel) => `<div class="lr${sel ? " sel" : ""}" style="grid-template-columns:auto 1fr 150px 90px auto"><span>${cb(true)}</span><span class="n"><b>${n}</b></span><span>${enc}</span><span>${eol}</span>${badge(k[0], k[1])}</div>`;
function encoding() {
  const list = `<div class="list"><div class="lr hd" style="grid-template-columns:auto 1fr 150px 90px auto"><span style="width:20px"></span><span>File</span><span>Encoding found</span><span>Line ends</span><span style="width:84px">Result</span></div>${erow("notes.txt", "Windows-1252", "CRLF", ["Will change", "info"], true)}${erow("readme-old.txt", "UTF-16 LE, BOM", "CRLF", ["Will change", "info"], false)}${erow("script.sh", "UTF-8", "Mixed", ["Mixed ends", "warn"], false)}${erow("data.csv", "UTF-8", "LF", ["Already ok", "ok"], false)}</div>`;
  const main = win({ title: "Encoding and line endings", icon: "Text", file: "4 text files", meta: "notes.txt and 3 more", w: 840,
    body: `${list}<div class="row2"><div class="fld"><span class="lbl">Text encoding</span>${seg(["UTF-8", "UTF-8 with BOM", "UTF-16"], 0)}</div><div class="fld"><span class="lbl">Line endings</span>${seg(["Keep", "LF", "CRLF"], 1)}</div></div>
      <div class="fld"><span class="lbl">notes.txt, first lines after the change (visible marks on)</span><div class="pre">Caf<span class="hlg">é</span> menu for Friday<span style="color:var(--textmute)">␊</span>\nPrices in <span class="hlg">€</span>, not $<span style="color:var(--textmute)">␊</span>\n(Windows-1252 bytes E9 and 80 become UTF-8)</div></div>
      ${sw("Keep a backup", "Original saved as notes.txt.bak next to the file", true)}`,
    foot: { note: "3 of 4 files will change", primary: { label: "Convert 3 files", i: "Check" } } });
  const mixed = win({ title: "Encoding and line endings", icon: "Text", file: "script.sh", meta: "1 file · 3.4 KB", w: 840,
    body: `${banner("warn", "This file mixes line endings", "186 lines end with LF and 14 end with CRLF. Converting to LF makes them all the same, which shell scripts need.", btn("Show mixed lines", { k: "ghost", sm: 1 }))}<div class="pre">#!/bin/sh<span class="hl">␍␊</span>\necho start<span style="color:var(--textmute)">␊</span>\nrun --fast<span class="hl">␍␊</span></div>`,
    foot: { note: "", primary: { label: "Convert to LF", i: "Check" } } });
  const bad = win({ title: "Encoding and line endings", icon: "Text", file: "export-legacy.txt", meta: "stopped", w: 840,
    body: errorBody({ head: "Could not convert without losing characters", text: "Byte 0x81 at position 1,042 is not valid in Windows-1252. The file is probably a different encoding, such as Shift-JIS.", why: "Nothing was written. Choose the source encoding by hand and try again.", actions: btn("Read it as Shift-JIS", { k: "acc" }) + btn("Choose encoding", { k: "ghost", i: "Chevron" }) }), foot: close });
  const done = win({ title: "Encoding and line endings", icon: "Text", file: "4 text files", meta: "done", w: 840,
    body: doneBody({ head: "Converted 3 files", text: "Backups were saved as .bak next to each file.", rows: [["notes.txt", "Windows-1252 → UTF-8, CRLF → LF"], ["readme-old.txt", "UTF-16 LE → UTF-8, CRLF → LF"], ["script.sh", "Line ends made LF (14 lines fixed)"], ["data.csv", "Skipped, already matches"]], actions: btn("Show in folder", { k: "ghost", i: "Folder" }) + btn("Undo from backups", { k: "text", i: "Restart" }) }), foot: close });
  return { cells: [cell("Ready, batch of 4", main), cell("Mixed line endings", mixed), cell("Cannot decode", bad), cell("Done", done)] };
}

// ---------------------------------------------------------------- 13 Subtitles
const crow = (n, a, b, t, ch, bad) => `<div class="lr" style="grid-template-columns:36px 118px 118px 1fr;min-height:40px"><span class="mono sub">${n}</span><span class="mono${ch ? "" : ""}">${a}</span><span class="mono">${b}</span><span class="n" style="${bad ? "color:var(--amber)" : ""}">${t}</span></div>`;
function subs() {
  const table = `<div class="list"><div class="lr hd" style="grid-template-columns:36px 118px 118px 1fr"><span>#</span><span>Start</span><span>End</span><span>Text</span></div>${crow(1, "00:00:04,200", "00:00:06,800", "Welcome back to the show.")}${crow(2, "00:00:07,000", "00:00:10,400", "Today we are fixing the garden gate.")}${crow(3, "00:00:10,900", "00:00:13,300", "First, the hinge.")}</div>`;
  const main = win({ title: "Subtitle timing", icon: "Subtitle", file: "episode-04.srt", meta: "612 cues · 41:08", w: 800,
    body: `${table}${seg(["Shift", "Stretch", "Convert"], 0)}
      <div class="row2">${field("Move all by", "+2.400", { unit: "seconds", mono: 1, focus: 1, hint: "Negative moves earlier. Cue 1 would start at 00:00:06,600." })}<div class="fld"><span class="lbl">Result for the first and last cue</span><div class="in dis"><span class="v mono">00:00:06,600 · 00:41:00,700</span></div></div></div>
      <div class="banner info">${ic("Info", "l")}<div class="grow"><b>Out of sync by a changing amount?</b>Use Stretch: set where the first and last cue should land and every cue is spread between them.</div></div>
      ${field("Save as", "episode-04.shifted.srt", { i: "File" })}`,
    foot: { note: "612 cues change", primary: { label: "Save", i: "Check" } } });
  const stretch = win({ title: "Subtitle timing", icon: "Subtitle", file: "episode-04.srt", meta: "612 cues", w: 800,
    body: `${seg(["Shift", "Stretch", "Convert"], 1)}<div class="row2">${field("First cue should start at", "00:00:05,100", { mono: 1, focus: 1, hint: "Now 00:00:04,200" })}${field("Last cue should start at", "00:42:31,000", { mono: 1, hint: "Now 00:40:58,300" })}</div><div class="card"><div class="sub">Speed change: <b style="font-weight:500;color:var(--text)">× 1.0374</b> (23.976 → 24.873 fps equivalent). Longest cue stays under 7 s.</div></div>`,
    foot: { note: "", primary: { label: "Save", i: "Check" } } });
  const conv = win({ title: "Subtitle timing", icon: "Subtitle", file: "episode-04.srt", meta: "612 cues", w: 800,
    body: `${seg(["Shift", "Stretch", "Convert"], 2)}<div class="row2">${field("From", "SubRip (.srt)", { dis: 1 })}<div class="fld"><span class="lbl">To</span>${seg(["WebVTT (.vtt)", "SubRip (.srt)"], 0)}</div></div>${card(`<div class="sub">Commas in times become dots, a WEBVTT header is added, and basic styling tags are kept. Unsupported positioning is dropped and listed afterwards.</div>`)}${field("Save as", "episode-04.vtt", { i: "File" })}`,
    foot: { note: "", primary: { label: "Convert", i: "Refresh" } } });
  const bad = win({ title: "Subtitle timing", icon: "Subtitle", file: "episode-09.srt", meta: "problem found", w: 800,
    body: `${banner("warn", "2 cues overlap and 1 is broken", "The file can still be shifted. Fix or skip the cues below first if you want a clean result.")}<div class="list">${crow(214, "00:11:02,100", "00:11:05,900", "Line that runs long", false, true)}${crow(215, "00:11:04,300", "00:11:06,000", "Starts before the last one ends", false, true)}${crow(281, "00:16:xx,xxx", "00:16:41,200", "Time cannot be read", false, true)}</div><div style="display:flex;gap:8px">${btn("Fix overlaps", { k: "ghost", i: "Check" })}${btn("Skip broken cue", { k: "ghost" })}</div>`,
    foot: { note: "", primary: { label: "Save", i: "Check" } } });
  return { cells: [cell("Ready, shift", main), cell("Stretch", stretch), cell("Convert", conv), cell("Problem cues", bad)] };
}

// ---------------------------------------------------------------- 14 Font
function fontTool() {
  const sample = (px, t) => `<div style="display:flex;align-items:baseline;gap:14px;padding:6px 0;border-top:1px solid var(--line)"><span class="mono" style="width:44px;flex:none;font-size:12px;color:var(--text)">${px}</span><span style="font-family:'Roboto',serif;font-size:${px}px;line-height:1.15;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;min-width:0">${t}</span></div>`;
  const main = win({ title: "Font preview and install", icon: "Font", file: "FiraSans-Regular.ttf", meta: "Fira Sans · Regular · 438 KB", w: 780,
    body: `<div class="in focus" style="height:44px">${ic("Text", "s")}<span class="v">The five boxing wizards jump quickly</span></div><div>${sample(72, "Aa Gg 123")}${sample(36, "The five boxing wizards jump")}${sample(18, "The five boxing wizards jump quickly 0123456789")}${sample(12, "Sphinx of black quartz, judge my vow. Pack my box with five dozen liquor jugs.")}</div>
      ${card(kv([["Family", "Fira Sans · Regular (400)"], ["Glyphs", "1,610 · Latin, Cyrillic, Greek"], ["Licence", "SIL Open Font License 1.1"]]))}
      ${sw("Install for this user only", "No administrator rights. Other people on this computer do not get it.", true)}`,
    foot: { note: "The preview does not install anything.", primary: { label: "Install", i: "Plus" } } });
  const done = win({ title: "Font preview and install", icon: "Font", file: "FiraSans-Regular.ttf", meta: "installed", w: 780,
    body: doneBody({ head: "Fira Sans is installed for you", text: "Apps that are already open may need a restart to list it.", rows: [["Location", "Your user fonts folder"], ["Removal", "Open this tool again and choose Uninstall"]], actions: btn("Uninstall", { k: "danger", i: "Trash" }) + btn("Open fonts settings", { k: "text" }) }), foot: close });
  const already = win({ title: "Font preview and install", icon: "Font", file: "FiraSans-Regular.ttf", meta: "already installed", w: 780,
    body: `${banner("info", "This font is already installed", "The same file is installed for you. Installing again would change nothing.")}<div style="display:flex;gap:8px">${btn("Uninstall", { k: "danger", i: "Trash" })}</div>`, foot: { note: "", cancel: false, primary: { label: "Close" } } });
  const bad = win({ title: "Font preview and install", icon: "Font", file: "broken-font.otf", meta: "could not read", w: 780,
    body: errorBody({ head: "This is not a usable font file", text: "The font tables are damaged, so Windows would refuse it.", why: "Nothing was installed and the file was not changed.", actions: btn("Copy details", { k: "ghost", i: "Copy" }) }), foot: close });
  return { cells: [cell("Ready", main), cell("Done", done), cell("Already installed", already), cell("Unreadable file", bad)] };
}

// ---------------------------------------------------------------- 15 Disc image
const drow = (d, kind, name, size, on) => `<div class="lr" style="grid-template-columns:1fr 90px;min-height:40px"><span class="n" style="padding-left:${d * 22}px;gap:8px">${kind === "d" ? ic("Chevron", "s") : cb(on)}${ic(kind === "d" ? "Folder" : "File", "s")}<b style="font-weight:${kind === "d" ? 500 : 400}">${name}</b></span><span class="r">${size}</span><span></span></div>`;
function disc() {
  const tree = `<div class="list">${drow(0, "d", "sources", "", false)}${drow(1, "f", "install.wim", "3.9 GB", false)}${drow(0, "d", "boot", "", false)}${drow(0, "f", "setup.exe", "88 KB", true)}${drow(0, "f", "autorun.inf", "1 KB", false)}</div>`;
  const main = win({ title: "Disc image", icon: "Disc", file: "Win-install.iso", meta: "ISO 9660 · 5.2 GB", w: 780,
    body: `<div style="display:flex;gap:12px;flex-wrap:wrap"><span class="badge info">Not mounted</span><span class="sub">Mounting adds a read-only drive. The .iso file is never changed.</span></div>${tree}
      <div style="display:flex;gap:8px;flex-wrap:wrap">${btn("Mount as a drive", { k: "acc", i: "Disc" })}${btn("Extract selected", { k: "ghost", i: "Down" })}${btn("Browse without mounting", { k: "text", i: "Folder" })}</div>`,
    foot: { note: "1 file selected · 88 KB", cancel: "Close", primary: null } });
  const work = win({ title: "Disc image", icon: "Disc", file: "Win-install.iso", meta: "mounting", w: 780,
    body: workingBody({ head: "Attaching the image as a drive", sub: "Asking Windows to attach the image. This usually takes a second or two.", pct: 40, ind: true }), foot: { note: "", primary: null, cancel: "Cancel" } });
  const mounted = win({ title: "Disc image", icon: "Disc", file: "Win-install.iso", meta: "mounted as E:", w: 780,
    body: `${banner("ok", "Mounted as drive E:", "Read-only. Eject when you are done so the .iso can be moved or deleted.")}${tree}<div style="display:flex;gap:8px;flex-wrap:wrap">${btn("Open drive E:", { k: "acc", i: "Folder" })}${btn("Extract selected", { k: "ghost", i: "Down" })}${btn("Eject", { k: "ghost", i: "Eject" })}</div>`,
    foot: { note: "Leaving this window open does not unmount it.", cancel: "Close", primary: null } });
  const bad = win({ title: "Disc image", icon: "Disc", file: "backup.iso", meta: "could not mount", w: 780,
    body: errorBody({ head: "Windows could not mount this image", text: "The file is on a network share that does not allow mounting, or it is not a valid disc image.", why: "Nothing was changed. You can still browse and extract the contents without mounting, or copy the file to a local disk first.", actions: btn("Browse without mounting", { k: "acc", i: "Folder" }) + btn("Copy details", { k: "ghost", i: "Copy" }) }), foot: { note: "", cancel: "Close", primary: null } });
  return { cells: [cell("Ready, not mounted", main), cell("Mounting", work), cell("Mounted", mounted), cell("Could not mount", bad)] };
}

export const tools3 = [
  { file: "DataViewer", num: 11, title: "Data viewer and converter", tags: ["Opens for .csv .tsv .json .yaml .yml", "Engine: System.Text.Json, YamlDotNet, own CSV reader"], build: data },
  { file: "EncodingLines", num: 12, title: "Encoding and line endings", tags: ["Opens for .txt .csv .log .sh .md and other text", "Engine: .NET Encoding, own detector"], build: encoding },
  { file: "SubtitleTiming", num: 13, title: "Subtitle timing", tags: ["Opens for .srt .vtt", "Engine: own cue parser, no external tool"], build: subs },
  { file: "FontInstall", num: 14, title: "Font preview and install", tags: ["Opens for .ttf .otf .ttc .woff", "Engine: WPF GlyphTypeface, per-user font install"], build: fontTool },
  { file: "DiscImage", num: 15, title: "Disc image", tags: ["Opens for .iso (and .img .vhd .vhdx)", "Engine: Windows disk image mount, 7-Zip to browse"], build: disc },
];
