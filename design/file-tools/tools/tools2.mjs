import { ic, btn, chip, badge, field, seg, sw, cb, rb, slider, prog, banner, kv, card, sect, win, cell, workingBody, doneBody, errorBody } from "./lib.mjs";
const tn = (h, l = 38) => `background:linear-gradient(135deg,hsl(${h} 32% ${l}%),hsl(${h + 30} 28% ${l - 10}%))`;
const close = { note: "", cancel: false, primary: { label: "Close" } };

// ---------------------------------------------------------------- 6 Icon
const sizes = [16, 24, 32, 48, 64, 128, 256];
const grid = (bg) => `<div style="background:${bg};border-radius:14px;padding:14px;display:flex;align-items:flex-end;gap:14px;flex-wrap:wrap">${sizes.map((s) => `<div style="display:flex;flex-direction:column;align-items:center;gap:6px"><span style="display:block;width:${Math.min(s, 96)}px;height:${Math.min(s, 96)}px;border-radius:${Math.round(Math.min(s, 96) * 0.22)}px;${tn(165, 44)}"></span><span class="sub mono" style="color:inherit;opacity:.8;font-size:11px">${s}</span></div>`).join("")}</div>`;
function icon() {
  const main = win({ title: "Make an icon", icon: "Stamp", file: "logo-mark.png", meta: "512×512 · transparent", w: 820,
    body: `<div style="display:flex;gap:18px"><div style="flex:none;width:190px;display:flex;flex-direction:column;gap:10px;align-self:stretch"><div style="flex:1;min-height:190px;border-radius:16px;background:repeating-conic-gradient(var(--sunken) 0 25%,var(--surface) 0 50%) 0 0/16px 16px;display:grid;place-items:center"><span style="width:150px;height:150px;border-radius:32px;${tn(165, 44)}"></span></div>${sw("Keep transparency", "", true)}${slider("Padding", "8%", 16)}</div>
      <div style="flex:1;min-width:0;display:flex;flex-direction:column;gap:12px">${sect("SIZES INSIDE THE .ICO")}<div style="display:flex;gap:6px;flex-wrap:wrap">${sizes.map((s) => `<span class="chip on">${ic("Check", "s")}${s}</span>`).join("")}</div><span class="hint">Windows picks the closest size for the taskbar, title bars and Explorer. Keep all seven unless you know you need fewer.</span>
      ${sect("PREVIEW (128 AND 256 SHOWN SMALLER)")}<div class="row2"><div style="color:#171D1A">${grid("#F3F3F3")}</div><div style="color:#fff">${grid("#202020")}</div></div></div></div>
      <div class="row2">${field("Shape", "Keep the picture as it is", { i: "Crop", after: ic("Chevron", "s") })}${field("Save as", "logo-mark.ico", { i: "File" })}</div>`,
    foot: { note: "7 sizes · about <b>94 KB</b>", primary: { label: "Make icon", i: "Check" } } });
  const work = win({ title: "Make an icon", icon: "Stamp", file: "logo-mark.png", meta: "working", w: 820,
    body: workingBody({ head: "Size 5 of 7", sub: "Resampling to 64×64 with a high-quality filter.", pct: 71 }), foot: { note: "", primary: { label: "Make icon", dis: true } } });
  const done = win({ title: "Make an icon", icon: "Stamp", file: "logo-mark.png", meta: "done", w: 820,
    body: doneBody({ head: "Saved logo-mark.ico", text: "Seven sizes in one file. The picture was not changed.", rows: [["Sizes", "16, 24, 32, 48, 64, 128, 256"], ["File size", "93.8 KB (256 px stored compressed)"]], actions: btn("Show in folder", { k: "ghost", i: "Folder" }) + btn("Copy path", { k: "ghost", i: "Copy" }) }), foot: close });
  const warn = win({ title: "Make an icon", icon: "Stamp", file: "small-logo.png", meta: "120×120", w: 820,
    body: `${banner("warn", "This picture is smaller than 256 px", "Sizes 128 and 256 would be enlarged and look soft. Choose them off, or start from a larger picture.", btn("Turn off 128 and 256", { k: "acc" }))}<div style="display:flex;gap:6px;flex-wrap:wrap">${sizes.map((s) => `<span class="chip${s > 120 ? "" : " on"}">${s > 120 ? ic("Warn", "s") : ic("Check", "s")}${s}</span>`).join("")}</div><span class="hint">Not enough pixels: 128 and 256. You can still continue; the result is valid.</span>`,
    foot: { note: "5 sizes selected", primary: { label: "Make icon", i: "Check" } } });
  return { cells: [cell("Ready", main), cell("Working", work), cell("Done", done), cell("Small source picture", warn)] };
}

// ---------------------------------------------------------------- 7 Archive
const trow = (depth, kind, name, size, date, on, sel = false) => `<div class="lr${sel ? " sel" : ""}" style="grid-template-columns:1fr 88px 120px"><span class="n" style="padding-left:${depth * 22}px;gap:8px">${cb(on)}${ic(kind === "d" ? "Folder" : "File", "s")}<b style="font-weight:${kind === "d" ? 500 : 400}">${name}</b></span><span class="r">${size}</span><span class="r">${date}</span></div>`;
const treeHead = `<div class="lr hd" style="grid-template-columns:1fr 88px 120px"><span class="n"><span style="width:28px;display:inline-block"></span>Name</span><span class="r">Size</span><span class="r">Modified</span></div>`;
function archive() {
  const tree = `<div class="list" style="max-height:268px">${treeHead}${trow(0, "d", "project", "", "", true)}${trow(1, "d", "src", "", "", true)}${trow(2, "f", "main.cs", "14 KB", "3 Nov 2025", true, true)}${trow(2, "f", "util.cs", "6 KB", "3 Nov 2025", true)}${trow(1, "d", "assets", "", "", false)}${trow(2, "f", "banner.psd", "48.2 MB", "30 Oct 2025", false)}${trow(1, "f", "README.md", "3 KB", "3 Nov 2025", true)}${trow(1, "f", "notes.txt", "1 KB", "1 Nov 2025", true)}</div>`;
  const main = win({ title: "Extract an archive", icon: "Archive", file: "project-v2.zip", meta: "48 files · 112 MB unpacked · 41 MB zipped", w: 820,
    body: `<div class="in focus" style="height:44px;border-radius:22px">${ic("Search", "s")}<span class="v">Search inside the archive</span><span class="chip xs">.*</span></div>
      ${tree}<div style="display:flex;justify-content:space-between" class="sub"><span><b style="font-weight:500;color:var(--text)">12 of 48</b> selected · 31.4 MB</span><span>${btn("Select all", { k: "text", sm: 1 })}${btn("None", { k: "text", sm: 1 })}</span></div>
      <div class="row2">${field("Extract to", "D:\\Downloads\\project-v2", { i: "Folder", hint: "A new folder is made from the archive name." })}<div class="fld"><span class="lbl">If a file already exists</span>${seg(["Ask", "Skip", "Replace", "Keep both"], 3)}</div></div>`,
    foot: { note: "Files never leave the chosen folder.", primary: { label: "Extract 12 files", i: "Down" } } });
  const pw = win({ title: "Extract an archive", icon: "Archive", file: "taxes-2024.7z", meta: "encrypted · 9 files", w: 820,
    body: `${banner("info", "This archive needs a password", "The file names are visible, the contents are locked. The password is used once and not stored.", "", "Lock")}${field("Password", "••••••••••", { i: "Lock", focus: 1, after: ic("Eye", "s"), hint: "Wrong password for this archive. Nothing was extracted.", hk: "err", err: 1 })}${sw("Show the password while typing", "", false)}`,
    foot: { note: "Try again or Cancel.", primary: { label: "Unlock and extract", i: "Lock" } } });
  const work = win({ title: "Extract an archive", icon: "Archive", file: "project-v2.zip", meta: "extracting", w: 820,
    body: workingBody({ head: "File 7 of 12", sub: "assets/logo.svg → D:\\Downloads\\project-v2 · 14.9 MB of 31.4 MB", pct: 47 }),
    foot: { note: "Cancel stops now; files already written are kept.", primary: { label: "Extract", dis: true } } });
  const done = win({ title: "Extract an archive", icon: "Archive", file: "project-v2.zip", meta: "done", w: 820,
    body: `${banner("ok", "Extracted 12 files, 31.4 MB", "1 file already existed, so Keep both saved it under a new name.")}${card(kv([["Renamed", "notes.txt → notes (2).txt"], ["Not selected", "36 files, including banner.psd"], ["Refused", "0 unsafe paths"]]))}<div style="display:flex;gap:8px">${btn("Open folder", { k: "ghost", i: "Folder" })}${btn("Delete the archive", { k: "danger", i: "Trash" })}</div>`,
    foot: close });
  return { cells: [cell("Ready", main), cell("Password needed", pw), cell("Working", work), cell("Done", done)] };
}

// ---------------------------------------------------------------- 8 Hash
const hashv = (v, parts = []) => `<div class="pre" style="white-space:normal;word-break:break-all;font-size:13px">${v}</div>`;
const H = "9f86d081 884c7d65 9a2feaa0 c55ad015 a3bf4f1b 2b0b822c d15d6c15 b0f00a08";
function hash() {
  const algo = `<div style="display:flex;gap:6px;flex-wrap:wrap;align-items:center">${chip("SHA-256", true, "Check")}${chip("SHA-1")}${chip("MD5")}${chip("SHA-512")}<span class="hint">MD5 and SHA-1 are fine for spotting damage but not for proving authenticity.</span></div>`;
  const comp = (state) => `<div class="fld"><span class="lbl">SHA-256 of this file</span><div class="pre mono" style="white-space:normal;font-size:13px;line-height:1.6;word-spacing:4px">${H}</div></div>`;
  const main = win({ title: "Hash and verify", icon: "Hash", file: "ubuntu-24.04.iso", meta: "5.8 GB", w: 760,
    body: `${algo}${comp()}<div style="display:flex;gap:8px">${btn("Copy hash", { k: "ghost", i: "Copy" })}${btn("Copy as file line", { k: "text" })}</div>
      ${field("Expected value (paste the publisher's checksum)", "Paste here to check it", { i: "Check", focus: 1, hint: "Spaces, dashes and capitals are ignored. Pasting a whole checksum line works too." })}`,
    foot: { note: "Computed in 14 s · 413 MB/s", primary: { label: "Verify", i: "Check", dis: true } } });
  const match = win({ title: "Hash and verify", icon: "Hash", file: "ubuntu-24.04.iso", meta: "5.8 GB", w: 760,
    body: `${algo}${banner("ok", "It matches", "This file is identical to what the publisher published. Algorithm: SHA-256, all 64 characters equal.")}<div class="fld"><span class="lbl">SHA-256 of this file</span><div class="pre mono" style="white-space:normal;font-size:13px;line-height:1.6;word-spacing:4px"><span class="hlg">${H}</span></div></div><div class="fld"><span class="lbl">Expected value</span><div class="in"><span class="v mono">9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08</span></div></div>`,
    foot: { note: "", cancel: false, primary: { label: "Close" } } });
  const bad = win({ title: "Hash and verify", icon: "Hash", file: "ubuntu-24.04.iso", meta: "5.8 GB", w: 760,
    body: `${algo}${banner("bad", "It does not match", "Do not run or install this file. It is damaged or not the file the publisher published. First difference at character 9.")}<div class="fld"><span class="lbl">SHA-256 of this file</span><div class="pre mono" style="white-space:normal;font-size:13px;line-height:1.6;word-spacing:4px">9f86d081 <span class="hlb">884c7d65</span> 9a2feaa0 c55ad015 a3bf4f1b 2b0b822c d15d6c15 b0f00a08</div></div><div class="fld"><span class="lbl">Expected value</span><div class="pre mono" style="white-space:normal;font-size:13px;line-height:1.6;word-spacing:4px">9f86d081 <span class="hlb">1c3a77f0</span> 9a2feaa0 c55ad015 a3bf4f1b 2b0b822c d15d6c15 b0f00a08</div></div><div style="display:flex;gap:8px">${btn("Download it again", { k: "text" })}${btn("Show in folder", { k: "ghost", i: "Folder" })}</div>`,
    foot: { note: "", cancel: false, primary: { label: "Close" } } });
  const work = win({ title: "Hash and verify", icon: "Hash", file: "ubuntu-24.04.iso", meta: "5.8 GB", w: 760,
    body: workingBody({ head: "Reading the file", sub: "3.1 GB of 5.8 GB · 405 MB/s · about 7 s left", pct: 53 }), foot: { note: "Cancel stops reading; nothing is changed.", primary: { label: "Verify", dis: true } } });
  return { cells: [cell("Ready, nothing pasted yet", main), cell("Computing", work), cell("Match", match), cell("Mismatch", bad)] };
}

// ---------------------------------------------------------------- 9 Signature
const chain = (rows) => `<div class="list">${rows.map(([n, s, lvl, k]) => `<div class="lr" style="grid-template-columns:1fr auto;padding-left:${14 + lvl * 24}px"><span class="n">${ic(lvl === 0 ? "Shield" : "Cert", "s")}<b>${n}</b></span>${badge(s, k)}</div>`).join("")}</div>`;
function signed() {
  const main = win({ title: "Who signed this?", icon: "Shield", file: "SetupClient-3.4.1.exe", meta: "installer · 84.2 MB", w: 780,
    body: `${banner("ok", "Signed by Fabrikam Software Ltd, and the file is intact", "The signature checks out and the certificate chains to a trusted root. This tells you who made it and that it was not altered, not that it is safe.")}
      ${card(kv([["Publisher", "<b style='font-weight:500'>Fabrikam Software Ltd</b> · Toronto, Canada"], ["Signed on", "21 Oct 2025, 14:08 UTC (trusted timestamp)"], ["Certificate valid", "to 14 Mar 2026, was valid when signed"], ["Digest", "SHA-256 · file hash <span class='mono'>4F9C2A…E81B</span>"], ["Thumbprint", "<span class='mono'>B1073E…214D</span>"]]))}
      <div class="fld"><span class="lbl">Certificate chain</span>${chain([["Fabrikam Software Ltd", "Valid", 0, "ok"], ["Contoso Code Signing CA 2024", "Trusted", 1, "ok"], ["Contoso Root Authority", "Trusted root", 2, "ok"]])}</div>`,
    foot: { left: btn("Open certificate", { k: "ghost", i: "Cert" }), note: "", primary: { label: "Close" }, cancel: "Copy details" } });
  const un = win({ title: "Who signed this?", icon: "Shield", file: "notes-tool.exe", meta: "program · 2.1 MB", w: 780,
    body: `${banner("warn", "This file is not signed", "Nobody vouches for who made it. That is common for small or older tools and is not proof of a problem. Check where you got it.")}${card(kv([["SHA-256", "<span class='mono'>7A31…C0D4</span>"], ["Size", "2.1 MB"], ["Modified", "2 Mar 2024"]]))}<div style="display:flex;gap:8px">${btn("Hash and verify", { k: "ghost", i: "Hash" })}${btn("Show in folder", { k: "ghost", i: "Folder" })}</div>`,
    foot: close });
  const bad = win({ title: "Who signed this?", icon: "Shield", file: "driver-pack.msi", meta: "installer · 31.0 MB", w: 780,
    body: `${banner("bad", "The signature is broken", "The file was changed after it was signed. Do not run it. Delete it and get a fresh copy from the publisher.")}${card(kv([["Claims to be from", "Fabrikam Software Ltd"], ["Signed on", "9 Jul 2025"], ["Problem", "Digest mismatch: the content no longer matches the signature"]]))}<div style="display:flex;gap:8px">${btn("Show in folder", { k: "ghost", i: "Folder" })}${btn("Delete file", { k: "danger", i: "Trash" })}</div>`,
    foot: close });
  const exp = win({ title: "Who signed this?", icon: "Shield", file: "legacy-tool.dll", meta: "library · 640 KB", w: 780,
    body: `${banner("warn", "Signed, but the root is not trusted on this computer", "The signature is intact and the certificate was valid when signed (timestamped 4 Feb 2022), but its issuer is not in your trusted list.")}<div class="fld"><span class="lbl">Certificate chain</span>${chain([["Northwind Traders", "Valid when signed", 0, "ok"], ["Northwind Internal CA", "Not trusted", 1, "warn"]])}</div>`,
    foot: { left: btn("Open certificate", { k: "ghost", i: "Cert" }), note: "", primary: { label: "Close" }, cancel: false } });
  return { cells: [cell("Signed and valid", main), cell("Not signed", un), cell("Broken signature", bad), cell("Untrusted issuer", exp)] };
}

// ---------------------------------------------------------------- 10 Certificate
const validity = (pct, k, rng = "from 15 Mar 2025 to 14 Mar 2026") => `<div class="fld"><div class="sl-top"><b>Valid</b><span>${rng}</span></div><div class="prog" style="height:10px"><i style="width:${pct}%;background:${k}"></i></div></div>`;
const usage = `<div style="display:flex;gap:6px;flex-wrap:wrap">${["Digital signature", "Key encipherment", "Server authentication", "Client authentication"].map((x) => `<span class="chip xs on">${x}</span>`).join("")}</div>`;
function cert() {
  const detail = (cd) => `<div style="display:flex;align-items:baseline;gap:12px;flex-wrap:wrap"><span class="big" style="${cd.c ? `color:${cd.c}` : ""}">${cd.big}</span><span class="sub">${cd.sub}</span></div>${validity(cd.pct, cd.col || "var(--mint)", cd.rng)}${card(kv([["Subject", `CN=${cd.cn || "api.example.org"}, O=Example Org`], ["Issuer", "CN=Example Issuing CA 1"], ["Alternative names", cd.cn ? "mail.example.org" : "api.example.org, www.example.org"], ["Key", "RSA 2048 bits · signature sha256RSA"], ["Serial", "<span class='mono'>04 7A 3C 91 E0 …</span>"], ["SHA-256", "<span class='mono'>C3 09 7B …  1F</span>"]]))}<div class="fld"><span class="lbl">Allowed uses</span>${usage}</div>`;
  const main = win({ title: "Certificate inspector", icon: "Cert", file: "api.example.org.cer", meta: "public certificate · 1.8 KB", w: 780,
    body: detail({ big: "214 days left", sub: "Expires 14 Mar 2026", pct: 41 }),
    foot: { left: btn("Convert…", { k: "ghost", i: "Refresh" }), note: "", primary: { label: "Close" }, cancel: "Copy details" } });
  const soon = win({ title: "Certificate inspector", icon: "Cert", file: "mail.example.org.cer", meta: "public certificate", w: 780,
    body: `${banner("warn", "Expires in 12 days", "Renew it before 26 Aug 2025 so mail clients keep trusting it.")}${detail({ big: "12 days left", sub: "Expires 26 Aug 2025", pct: 97, col: "var(--amber)", c: "var(--amber)", rng: "from 27 Aug 2024 to 26 Aug 2025", cn: "mail.example.org" })}`, foot: { left: btn("Convert…", { k: "ghost", i: "Refresh" }), note: "", primary: { label: "Close" }, cancel: "Copy details" } });
  const pfx = win({ title: "Certificate inspector", icon: "Cert", file: "client-auth.pfx", meta: "certificate with private key", w: 780,
    body: `${banner("info", "This file is protected with a password", "It holds a private key. The password is used once to read the certificate and is never stored or written to the log.", "", "Lock")}${field("Password", "••••••••••", { i: "Lock", focus: 1, after: ic("Eye", "s") })}<span class="hint">Only the public details are shown. The private key is never displayed.</span>`,
    foot: { note: "", primary: { label: "Open", i: "Check" } } });
  const conv = win({ title: "Convert the certificate", icon: "Cert", file: "api.example.org.cer", meta: "to another format", w: 780,
    body: `${sect("CONVERT TO")}${seg(["PEM (.pem)", "DER (.cer)", "PKCS#12 (.pfx)"], 0)}${card(`${sw("Include the private key", "Only possible when the source has one. Needs a new password to protect the result.", false)}<span class="hint">This file holds only the public certificate, so a PEM or DER copy is safe to share.</span>`)}${field("Save as", "api.example.org.pem", { i: "File" })}`,
    foot: { note: "Public data only.", primary: { label: "Convert", i: "Refresh" } } });
  return { cells: [cell("Ready, valid", main), cell("Expiring soon", soon), cell("Password needed", pfx), cell("Convert format", conv)] };
}

export const tools2 = [
  { file: "MakeIcon", num: 6, title: "Make an icon", tags: ["Opens for .png .jpg .bmp .gif .webp .svg", "Engine: .NET imaging, no external tool"], build: icon },
  { file: "ExtractArchive", num: 7, title: "Extract an archive", tags: ["Opens for .zip .7z .rar .tar .gz .tgz", "Engine: bundled 7-Zip, .NET for zip"], build: archive },
  { file: "HashVerify", num: 8, title: "Hash and verify", tags: ["Opens for any file", "Engine: .NET SHA-256, SHA-1, MD5, SHA-512"], build: hash },
  { file: "WhoSigned", num: 9, title: "Who signed this?", tags: ["Opens for .exe .msi .dll .sys .cab .msix", "Engine: WinVerifyTrust, X509Chain"], build: signed },
  { file: "CertInspector", num: 10, title: "Certificate inspector", tags: ["Opens for .cer .crt .pem .pfx .p12 .der", "Engine: X509Certificate2"], build: cert },
];
