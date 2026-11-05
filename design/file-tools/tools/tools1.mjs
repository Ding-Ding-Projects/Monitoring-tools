import { ic, btn, chip, badge, field, seg, sw, cb, rb, slider, prog, banner, kv, card, sect, win, cell, workingBody, doneBody, errorBody, rnd } from "./lib.mjs";

const tn = (h, l = 38) => `background:linear-gradient(135deg,hsl(${h} 32% ${l}%),hsl(${h + 30} 28% ${l - 10}%))`;
const strip = (n, h0) => Array.from({ length: n }, (_, i) => `<i style="flex:1;${tn((h0 + i * 9) % 360, 36 + (i % 3) * 3)}"></i>`).join("");

// ---------------------------------------------------------------- 1 Video trim
function scrub(inP, outP, mode, ph) {
  const keep = mode === 0;
  const shade = keep
    ? `<i style="position:absolute;left:0;width:${inP}%;top:0;bottom:0;background:rgba(0,0,0,.55)"></i><i style="position:absolute;left:${outP}%;right:0;top:0;bottom:0;background:rgba(0,0,0,.55)"></i>`
    : `<i style="position:absolute;left:${inP}%;width:${outP - inP}%;top:0;bottom:0;background:rgba(0,0,0,.55)"></i>`;
  const h = (p, side) => `<span style="position:absolute;left:${p}%;top:-6px;bottom:-6px;width:14px;margin-left:${side === "in" ? "-14px" : "0"};background:var(--mint);border-radius:${side === "in" ? "8px 0 0 8px" : "0 8px 8px 0"};display:grid;place-items:center;color:var(--mintink)" role="slider" aria-label="${side === "in" ? "Start point" : "End point"}"><i style="width:2px;height:16px;background:var(--mintink);border-radius:1px"></i></span>`;
  return `<div style="position:relative;padding:6px 0">
    <div style="display:flex;height:64px;border-radius:10px;overflow:hidden;gap:1px;position:relative">${strip(16, 190)}${shade}</div>
    ${h(inP, "in")}${h(outP, "out")}
    <span style="position:absolute;left:${ph}%;top:-2px;bottom:-2px;width:2px;background:var(--text);border-radius:1px"></span>
    <div style="display:flex;justify-content:space-between;margin-top:8px" class="sub mono"><span>0:00</span><span>0:18</span><span>0:36</span><span>0:54</span><span>1:12</span></div></div>`;
}
function videoTrim() {
  const main = win({ title: "Trim and cut video", icon: "Scissors", file: "holiday-2025.mp4", meta: "1:12:04 · 1920×1080 · 1.4 GB", w: 760,
    body: `<div style="display:flex;gap:16px;align-items:stretch"><div style="width:236px;height:132px;border-radius:14px;${tn(200, 34)};position:relative;flex:none;display:grid;place-items:center"><span style="width:44px;height:44px;border-radius:22px;background:rgba(0,0,0,.5);display:grid;place-items:center;color:#fff">${ic("Play", "l")}</span><span class="badge" style="position:absolute;left:8px;bottom:8px;background:rgba(0,0,0,.6);color:#fff">0:12.480</span></div>
      <div style="flex:1;display:flex;flex-direction:column;gap:10px;min-width:0">${sect("WHAT TO DO WITH THE SELECTION")}${seg(["Keep selected", "Remove selected"], 0)}<span class="hint">Keep: the clip between the two handles becomes a new file. Remove: the section between them is cut out and the rest is joined.</span></div></div>
      ${scrub(17, 62, 0, 17)}
      <div class="row3">${field("Start", "0:12.480", { mono: 1, focus: 1, i: "Clock" })}${field("End", "0:44.920", { mono: 1, i: "Clock" })}${field("New length", "0:32.440", { mono: 1, dis: 1 })}</div>
      ${banner("ok", "Lossless: no re-encode", "Both points sit on keyframes, so the cut copies the video as it is. It is instant and loses no quality.", "", "Check")}
      <div class="row2">${field("Save as", "holiday-2025-trim.mp4", { i: "File", hint: "Saved next to the original. A name that exists gets a number added; nothing is overwritten." })}
      ${sw("Exact cut instead", "Re-encodes so the cut lands on the exact frame (slower)", false)}</div>`,
    foot: { note: "<b>0:32.440</b> kept · about 690 MB", primary: { label: "Trim", i: "Scissors" }, left: btn("Choose folder", { k: "ghost", i: "Folder" }) } });
  const work = win({ title: "Trim and cut video", icon: "Scissors", file: "holiday-2025.mp4", meta: "keeping 0:12.480 to 0:44.920", w: 760,
    body: workingBody({ head: "Copying video and audio, no re-encode", sub: "420 MB of about 690 MB · 310 MB/s · writing holiday-2025-trim.mp4.part", pct: 61 }),
    foot: { note: "Cancel stops now and deletes the unfinished .part file.", primary: { label: "Trim", i: "Scissors", dis: true } } });
  const done = win({ title: "Trim and cut video", icon: "Scissors", file: "holiday-2025.mp4", meta: "done", w: 760,
    body: doneBody({ head: "Saved holiday-2025-trim.mp4", text: "The original was not changed.", rows: [["Length", "0:32.440"], ["Size", "688 MB (original 1.4 GB)"], ["Method", "Lossless copy, cut at keyframes"]],
      actions: btn("Show in folder", { k: "ghost", i: "Folder" }) + btn("Play", { k: "ghost", i: "Play" }) + btn("Trim another section", { k: "text" }) }),
    foot: { note: "", cancel: false, primary: { label: "Close" } } });
  const err = win({ title: "Trim and cut video", icon: "Scissors", file: "holiday-2025.mp4", meta: "could not trim", w: 760,
    body: errorBody({ head: "The trim stopped at 0:38.2", text: "FFmpeg reported: Invalid data found when processing input (the file is damaged near that point).", why: "No output file was kept. holiday-2025.mp4 is untouched. Try an end point before 0:38 or turn on Exact cut.",
      actions: btn("Try again with exact cut", { k: "acc", i: "Restart" }) + btn("Copy details", { k: "ghost", i: "Copy" }) }),
    foot: { note: "", cancel: false, primary: { label: "Close" } } });
  return { cells: [cell("Ready", main), cell("Working", work), cell("Done", done), cell("Error", err)] };
}

// ---------------------------------------------------------------- 2 Video to GIF
function toGif() {
  const main = win({ title: "Video to GIF or short clip", icon: "Image", file: "demo-run.mp4", meta: "0:48 · 1280×720 · 62 MB", w: 780,
    body: `<div style="display:flex;gap:18px"><div style="flex:none;width:280px;display:flex;flex-direction:column;gap:8px"><div style="height:158px;border-radius:14px;${tn(160, 34)};position:relative;display:grid;place-items:center"><span style="width:44px;height:44px;border-radius:22px;background:rgba(0,0,0,.5);display:grid;place-items:center;color:#fff">${ic("Play", "l")}</span></div><div class="sub">Preview of the result at 480 px wide, looping. Estimate: <b style="font-weight:500;color:var(--text)">7.4 MB</b></div></div>
      <div style="flex:1;display:flex;flex-direction:column;gap:12px;min-width:0">${seg(["GIF", "Animated WebP", "Short MP4"], 0)}<div class="row2">${field("From", "0:08.0", { mono: 1, focus: 1 })}${field("To", "0:14.5", { mono: 1 })}</div><span class="hint">6.5 seconds selected. Drag the handles on the preview or type times.</span>
      <div class="row2">${field("Width", "480", { unit: "px" })}${field("Frames per second", "15", { unit: "fps" })}</div>${sw("Loop forever", "Plays again when it ends", true)}</div></div>
      <div class="card" style="gap:12px">${sw("Fit a target file size", "Lowers width and fps in small steps until the result is under the limit", true)}<div class="row2">${field("Largest size", "8", { unit: "MB" })}<div class="fld"><span class="lbl">Result will be</span><div class="in dis"><span class="v">480 px · 15 fps · 7.4 MB</span></div></div></div></div>
      ${field("Save as", "demo-run.gif", { i: "File" })}`,
    foot: { note: "Estimated <b>7.4 MB</b>, under your 8 MB limit.", primary: { label: "Make GIF", i: "Bolt" } } });
  const work = win({ title: "Video to GIF or short clip", icon: "Image", file: "demo-run.mp4", meta: "making demo-run.gif", w: 780,
    body: workingBody({ head: "Pass 2 of 2: encoding with a custom palette", sub: "Pass 1 built the best 256 colours for this clip. Frame 62 of 98.", pct: 63 }),
    foot: { note: "Cancel deletes the unfinished file.", primary: { label: "Make GIF", dis: true } } });
  const done = win({ title: "Video to GIF or short clip", icon: "Image", file: "demo-run.mp4", meta: "done", w: 780,
    body: doneBody({ head: "Saved demo-run.gif", text: "Fits the 8 MB limit. The video was not changed.", rows: [["Size", "7.3 MB (limit 8 MB)"], ["Picture", "480×270 · 15 fps · 6.5 s · loops"], ["Settings used", "Width and fps unchanged, no step-down needed"]],
      actions: btn("Show in folder", { k: "ghost", i: "Folder" }) + btn("Copy file", { k: "ghost", i: "Copy" }) + btn("Make another", { k: "text" }) }),
    foot: { note: "", cancel: false, primary: { label: "Close" } } });
  const warn = win({ title: "Video to GIF or short clip", icon: "Image", file: "demo-run.mp4", meta: "cannot reach the target", w: 780,
    body: `${banner("warn", "Cannot reach 2 MB", "Even at 160 px and 8 fps the best result is 2.6 MB. Raise the limit, shorten the range, or switch to Short MP4, which is far smaller.", btn("Switch to Short MP4", { k: "acc" }))}
      <div class="card"><div class="sect">TRIED</div><div class="list" style="background:transparent">${[["480 px · 15 fps", "7.4 MB"], ["320 px · 12 fps", "4.1 MB"], ["240 px · 10 fps", "3.0 MB"], ["160 px · 8 fps", "2.6 MB"]].map(([a, b]) => `<div class="lr" style="grid-template-columns:1fr auto;border-top-color:var(--line)"><span>${a}</span><span class="r">${b}</span></div>`).join("")}</div></div>`,
    foot: { note: "Nothing was saved.", primary: { label: "Make GIF", i: "Bolt", dis: true } } });
  return { cells: [cell("Ready", main), cell("Working", work), cell("Done", done), cell("Target too small", warn)] };
}

// ---------------------------------------------------------------- 3 Audio
function waveSvg(inX, outX, w = 696) {
  const r = rnd(7); let bars = "";
  const n = 116;
  for (let i = 0; i < n; i++) { const e = Math.sin((i / n) * Math.PI) * 0.45 + 0.3; const a = Math.max(0.08, Math.min(1, e * (0.4 + r() * 0.9))); const hgt = a * 70; const x = i * (w / n) + 1; const inside = x >= inX && x <= outX; bars += `<rect x="${x.toFixed(1)}" y="${(40 - hgt / 2).toFixed(1)}" width="3.2" height="${hgt.toFixed(1)}" rx="1.6" fill="${inside ? "var(--mint)" : "var(--linestrong)"}"/>`; }
  return `<svg viewBox="0 0 ${w} 80" width="${w}" height="80" role="img" aria-label="Waveform with the selected part highlighted" style="display:block">${bars}<rect x="${inX}" y="0" width="3" height="80" rx="1.5" fill="var(--text)"/><rect x="${outX}" y="0" width="3" height="80" rx="1.5" fill="var(--text)"/></svg>`;
}
function audio() {
  const main = win({ title: "Trim, fade and loudness", icon: "Wave", file: "interview-take3.wav", meta: "4:36 · 48 kHz · stereo · 49 MB", w: 780,
    body: `<div class="card" style="padding:12px 14px">${waveSvg(46, 628)}<div style="display:flex;justify-content:space-between" class="sub mono"><span>0:00</span><span>1:09</span><span>2:18</span><span>3:27</span><span>4:36</span></div></div>
      <div class="row3">${field("Start", "0:03.200", { mono: 1, focus: 1 })}${field("End", "4:21.800", { mono: 1 })}${field("New length", "4:18.600", { mono: 1, dis: 1 })}</div>
      <div class="row2">${slider("Fade in", "0.5 s", 12)}${slider("Fade out", "2.0 s", 40)}</div>
      <div class="card" style="gap:12px"><div style="display:flex;align-items:baseline;justify-content:space-between"><b style="font-weight:500">Loudness</b><span class="sub">Measured now: <b class="mono" style="font-weight:500;color:var(--text)">−21.3 LUFS</b> · true peak −3.1 dB</span></div>
        <div style="display:flex;gap:8px;flex-wrap:wrap">${chip("Leave as it is")}${chip("Podcast −16 LUFS", true)}${chip("Streaming −14 LUFS")}${chip("Broadcast −23 LUFS")}${chip("Custom")}</div>
        <span class="hint">Raises the whole recording by 5.3 dB without clipping. Two passes: measure, then apply.</span></div>
      ${field("Save as", "interview-take3-edit.wav", { i: "File", hint: "Same format as the original. Choose MP3 or FLAC from the format list in the file name field." })}`,
    foot: { note: "<b>4:18.600</b> · −16.0 LUFS target", primary: { label: "Save", i: "Check" } } });
  const work = win({ title: "Trim, fade and loudness", icon: "Wave", file: "interview-take3.wav", meta: "working", w: 780,
    body: workingBody({ head: "Step 2 of 2: applying the changes", sub: "Step 1 measured the loudness at −21.3 LUFS. Now trimming, fading and raising the level.", pct: 48 }),
    foot: { note: "Cancel deletes the unfinished file.", primary: { label: "Save", dis: true } } });
  const done = win({ title: "Trim, fade and loudness", icon: "Wave", file: "interview-take3.wav", meta: "done", w: 780,
    body: doneBody({ head: "Saved interview-take3-edit.wav", text: "The original recording was not changed.", rows: [["Length", "4:18.600 (was 4:36)"], ["Loudness", "−21.3 LUFS before, −16.0 LUFS after"], ["True peak", "−3.1 dB before, −1.0 dB after, no clipping"]],
      actions: btn("Show in folder", { k: "ghost", i: "Folder" }) + btn("Play", { k: "ghost", i: "Play" }) }),
    foot: { note: "", cancel: false, primary: { label: "Close" } } });
  const err = win({ title: "Trim, fade and loudness", icon: "Wave", file: "voice-note.m4a", meta: "could not read", w: 780,
    body: errorBody({ head: "This audio could not be decoded", text: "FFmpeg reported: moov atom not found. The recording was probably cut off while it was being saved.", why: "Nothing was written and voice-note.m4a is untouched.", actions: btn("Copy details", { k: "ghost", i: "Copy" }) }),
    foot: { note: "", cancel: false, primary: { label: "Close" } } });
  return { cells: [cell("Ready", main), cell("Working", work), cell("Done", done), cell("Error", err)] };
}

// ---------------------------------------------------------------- 4 Images resize
const imgRow = (name, meta, h, sel, extra = "") => `<div class="lr${sel ? " sel" : ""}" style="grid-template-columns:auto 36px 1fr auto"><span>${cb(true)}</span><span class="thumb" style="${tn(h)}"></span><span class="n" style="flex-direction:column;align-items:flex-start;gap:0"><b>${name}</b><span class="sub">${meta}</span></span><span class="r">${extra}</span></div>`;
function images() {
  const batch = `<div class="list">${imgRow("IMG_4821.jpg", "4032×3024 · 4.2 MB", 20, true, "→ 640 KB")}${imgRow("IMG_4822.jpg", "4032×3024 · 3.9 MB", 40, false, "→ 598 KB")}${imgRow("beach-panorama.jpg", "9000×2400 · 11.8 MB", 200, false, "→ 1.1 MB")}</div>`;
  const preview = `<div style="position:relative;height:190px;border-radius:14px;overflow:hidden;display:flex"><div style="flex:1;${tn(20, 42)}"></div><div style="flex:1;${tn(20, 40)};filter:saturate(.85)"></div>
    <span style="position:absolute;left:50%;top:0;bottom:0;width:3px;background:var(--text);margin-left:-1px"></span><span style="position:absolute;left:50%;top:50%;width:32px;height:32px;margin:-16px 0 0 -16px;border-radius:16px;background:var(--mint);color:var(--mintink);display:grid;place-items:center">${ic("Sliders", "s")}</span>
    <span class="badge" style="position:absolute;left:10px;bottom:10px;background:rgba(0,0,0,.6);color:#fff">Before · 4.2 MB</span><span class="badge" style="position:absolute;right:10px;bottom:10px;background:rgba(0,0,0,.6);color:#fff">After · 640 KB</span></div>`;
  const main = win({ title: "Resize, crop and compress", icon: "Crop", file: "3 pictures", meta: "IMG_4821.jpg and 2 more · 19.9 MB", w: 840,
    body: `<div style="display:flex;gap:18px"><div style="flex:1.05;display:flex;flex-direction:column;gap:12px;min-width:0">${batch}${preview}</div>
      <div style="flex:1;display:flex;flex-direction:column;gap:12px;min-width:0">${sect("SIZE")}<div class="row2">${field("Width", "1600", { unit: "px", focus: 1 })}${field("Height", "1200", { unit: "px" })}</div>${sw("Keep proportions", "Changing one side sets the other", true)}
      ${sect("CROP")}<div style="display:flex;gap:6px;flex-wrap:wrap">${chip("None")}${chip("1:1")}${chip("4:3", true)}${chip("16:9")}${chip("Free")}</div>
      ${sect("QUALITY")}${seg(["Quality 82", "Target size"], 1)}${field("Aim for under", "700", { unit: "KB each", hint: "Quality is lowered per picture until it fits; panorama needs 74." })}</div></div>`,
    foot: { note: "3 pictures · 19.9 MB → about <b>2.3 MB</b>", primary: { label: "Save 3 copies", i: "Check" }, left: btn("Save in…", { k: "ghost", i: "Folder" }) } });
  const work = win({ title: "Resize, crop and compress", icon: "Crop", file: "8 pictures", meta: "working", w: 840,
    body: `${workingBody({ head: "Picture 3 of 8", sub: "beach-panorama.jpg · finding the quality that fits 700 KB", pct: 31 })}<div class="list">${[["IMG_4821.jpg", "640 KB", 1], ["IMG_4822.jpg", "598 KB", 1], ["beach-panorama.jpg", "working…", 0]].map(([n, s, d]) => `<div class="lr" style="grid-template-columns:1fr auto"><span class="n">${d ? ic("Check", "s") : ic("Refresh", "s")}<b>${n}</b></span><span class="r">${s}</span></div>`).join("")}</div>`,
    foot: { note: "Cancel keeps the pictures already saved.", primary: { label: "Save copies", dis: true } } });
  const done = win({ title: "Resize, crop and compress", icon: "Crop", file: "8 pictures", meta: "7 saved, 1 failed", w: 840,
    body: `${banner("warn", "7 of 8 saved", "27.4 MB became 5.1 MB, 81% smaller. One picture could not be processed.")}<div class="list">${[["IMG_4821.jpg", "640 KB", "ok"], ["IMG_4822.jpg", "598 KB", "ok"], ["scan-0031.tif", "Not saved: unsupported compression", "bad"]].map(([n, s, k]) => `<div class="lr" style="grid-template-columns:1fr auto"><span class="n">${ic(k === "ok" ? "Check" : "Warn", "s")}<b>${n}</b></span><span class="r" style="color:${k === "bad" ? "var(--danger)" : ""}">${s}</span></div>`).join("")}</div><div style="display:flex;gap:8px">${btn("Show in folder", { k: "ghost", i: "Folder" })}${btn("Retry the failed one", { k: "text" })}</div>`,
    foot: { note: "", cancel: false, primary: { label: "Close" } } });
  const empty = win({ title: "Resize, crop and compress", icon: "Crop", file: "nothing selected", w: 840,
    body: `<div class="empty"><span class="tile">${ic("Image", "l")}</span><b>No pictures to work on</b><span>Copy pictures (JPG, PNG, WebP, BMP, GIF, TIFF, HEIC) and open this tool from the notice, or drop them here.</span>${btn("Add pictures", { k: "ghost", i: "Plus" })}</div>`,
    foot: { note: "", primary: { label: "Save copies", dis: true } } });
  return { cells: [cell("Ready, batch of 3", main), cell("Working", work), cell("Done, with one failure", done), cell("Empty", empty)] };
}

// ---------------------------------------------------------------- 5 Privacy
const map = `<div style="height:132px;border-radius:12px;background:var(--sunken);position:relative;overflow:hidden" role="img" aria-label="Map placeholder with a pin"><svg viewBox="0 0 300 132" width="100%" height="132" preserveAspectRatio="none" style="position:absolute;inset:0"><g stroke="var(--line)" stroke-width="6" fill="none" stroke-linecap="round"><path d="M0 98 C70 70 120 110 190 60 S280 40 300 30"/><path d="M60 0 C70 50 110 70 120 132"/><path d="M200 0 C190 40 230 80 250 132"/></g><g stroke="var(--linestrong)" stroke-width="1.5" fill="none"><path d="M0 98 C70 70 120 110 190 60 S280 40 300 30"/></g></svg>
  <span style="position:absolute;left:148px;top:40px;color:var(--danger)"><svg class="i l" viewBox="0 0 24 24" style="width:36px;height:36px;fill:var(--danger);stroke:var(--sunken)"><path d="${"M12 21s-7-6.2-7-11a7 7 0 0 1 14 0c0 4.8-7 11-7 11z"}"/></svg></span>
  <span class="badge" style="position:absolute;left:8px;bottom:8px">Map placeholder, no map service is contacted</span></div>`;
const meta = (on, title, sub, badges = "") => `<div style="display:flex;gap:12px;align-items:flex-start;padding:10px 0;border-top:1px solid var(--line)"><span style="margin-top:2px">${cb(on)}</span><div style="flex:1;min-width:0"><div style="display:flex;gap:8px;align-items:center"><b style="font-weight:500">${title}</b>${badges}</div><div class="sub">${sub}</div></div></div>`;
function privacy() {
  const files = `<div class="list">${[["IMG_4821.jpg", "GPS · 3 more", 20, 1], ["IMG_4822.jpg", "GPS · 2 more", 40, 0], ["screenshot.png", "no hidden data", 120, 0]].map(([n, s, h, sel]) => `<div class="lr${sel ? " sel" : ""}" style="grid-template-columns:36px 1fr auto"><span class="thumb" style="${tn(h)}"></span><span class="n" style="flex-direction:column;align-items:flex-start;gap:0"><b>${n}</b><span class="sub" style="color:inherit;opacity:.85">${s}</span></span>${s.startsWith("GPS") ? badge("Location", "warn", "Pin") : badge("Clean", "ok")}</div>`).join("")}</div>`;
  const main = win({ title: "Image privacy clean-up", icon: "Eye", file: "IMG_4821.jpg", meta: "and 2 more · 2 of 3 have hidden data", w: 860,
    body: `<div style="display:flex;gap:18px"><div style="width:268px;display:flex;flex-direction:column;gap:12px;flex:none">${files}${sw("Apply to all selected pictures", "", true)}</div>
      <div style="flex:1;min-width:0;display:flex;flex-direction:column;gap:2px">${banner("warn", "This picture shows where it was taken", "Anyone you send it to can read the location and the camera's serial number.")}
        <div style="height:12px"></div>${map}<div class="hint" style="margin:6px 0 4px">51.5007° N, 0.1246° W · accurate to about 5 m · altitude 11 m</div>
        ${meta(true, "Location", "GPS position, altitude, direction", badge("Found", "warn"))}${meta(true, "Camera and lens", "Canon EOS R6, serial 0832004711, lens EF 24-70", badge("Found", "warn"))}${meta(true, "Date and time", "12 Jun 2025, 16:42:08 (local)")}${meta(false, "Colour profile", "sRGB. Needed so colours look right; keep it.")}${meta(true, "Embedded thumbnail", "A small copy that can show the uncropped original")}</div></div>
      ${sw("Keep the original", "Clean copies are saved as name-clean.jpg. Turn off to clean the files in place.", true)}`,
    foot: { note: "<b>4 groups</b> will be removed from 2 pictures", primary: { label: "Remove selected", i: "Trash" }, left: btn("Select all", { k: "text" }) } });
  const work = win({ title: "Image privacy clean-up", icon: "Eye", file: "IMG_4821.jpg", meta: "and 2 more", w: 860,
    body: workingBody({ head: "Picture 2 of 2", sub: "Removing metadata without re-compressing the picture.", pct: 70 }),
    foot: { note: "", primary: { label: "Remove selected", dis: true } } });
  const done = win({ title: "Image privacy clean-up", icon: "Eye", file: "IMG_4821.jpg", meta: "done", w: 860,
    body: doneBody({ head: "2 clean copies saved", text: "Picture quality is unchanged. The originals were kept.", rows: [["IMG_4821-clean.jpg", "11 tags removed, 0 left that identify you"], ["IMG_4822-clean.jpg", "9 tags removed"], ["Kept", "Colour profile, orientation"]], actions: btn("Show in folder", { k: "ghost", i: "Folder" }) + btn("Check the copy", { k: "text" }) }),
    foot: { note: "", cancel: false, primary: { label: "Close" } } });
  const empty = win({ title: "Image privacy clean-up", icon: "Eye", file: "screenshot.png", meta: "1 picture", w: 860,
    body: `<div class="empty"><span class="tile" style="color:var(--mint)">${ic("Check", "l")}</span><b>Nothing to clean</b><span>This picture has no location, camera, date or author data. It is safe to share as it is.</span></div>`,
    foot: { note: "", cancel: false, primary: { label: "Close" } } });
  return { cells: [cell("Ready, location found", main), cell("Working", work), cell("Done", done), cell("Empty, nothing hidden", empty)] };
}

export const tools1 = [
  { file: "VideoTrim", num: 1, title: "Trim and cut video", tags: ["Opens for .mp4 .mkv .mov .avi .webm .m4v", "Engine: FFmpeg stream copy"], build: videoTrim },
  { file: "VideoToGif", num: 2, title: "Video to GIF or short clip", tags: ["Opens for the same video types", "Engine: FFmpeg palette, two passes"], build: toGif },
  { file: "AudioEdit", num: 3, title: "Trim, fade and loudness", tags: ["Opens for .wav .mp3 .flac .m4a .ogg .aac", "Engine: FFmpeg loudnorm, afade"], build: audio },
  { file: "ImagesResize", num: 4, title: "Resize, crop and compress", tags: ["Opens for .jpg .png .webp .bmp .gif .tif .heic", "Engine: WPF imaging, FFmpeg for WebP"], build: images },
  { file: "ImagePrivacy", num: 5, title: "Image privacy clean-up", tags: ["Opens for .jpg .png .tif .heic .webp", "Engine: WPF BitmapMetadata, FFmpeg fallback"], build: privacy },
];
