// Renders every artboard to frames/<File>-dark.png and -light.png with headless Edge (no window is created).
// Usage: node render.mjs <path-to-playwright-core> [BoardName ...]   (run from design/file-tools)
import { readdirSync, writeFileSync, existsSync, readFileSync } from "node:fs";
import { pathToFileURL } from "node:url";
import { resolve } from "node:path";
const pw = await import(pathToFileURL(resolve(process.argv[2], "index.js")).href).then((m) => m.default || m);
const only = process.argv.slice(3);
const files = readdirSync(".").filter((f) => f.endsWith(".dc.html") && (!only.length || only.some((o) => f.startsWith(o))));
const browser = await pw.chromium.launch({ executablePath: "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe", headless: true });
const heights = existsSync("heights.json") ? JSON.parse(readFileSync("heights.json", "utf8")) : {};
const problems = [];
for (const f of files) {
  for (const theme of ["dark", "light"]) {
    const page = await browser.newPage({ viewport: { width: 1680, height: 900 } });
    await page.goto(pathToFileURL(resolve(f)).href + "?theme=" + theme);
    await page.evaluate(() => document.fonts.ready);
    const info = await page.evaluate(() => {
      const bad = [];
      document.querySelectorAll(".win *").forEach((e) => {
        if (e.closest("svg")) return;
        const r = e.getBoundingClientRect(); if (!r.width) return;
        if (e.scrollWidth > e.clientWidth + 1 && getComputedStyle(e).overflow === "visible" && !["svg"].includes(e.tagName.toLowerCase()) && e.children.length === 0) bad.push("overflow:" + e.className + ":" + e.textContent.slice(0, 40));
      });
      const fontsOk = [...document.fonts].filter((x) => x.status === "loaded").map((x) => x.family);
      return { h: Math.ceil(document.documentElement.scrollHeight), bad, fontsOk, w: document.documentElement.scrollWidth };
    });
    heights[f] = { h: info.h, w: info.w };
    if (info.bad.length || info.w > 1950) problems.push(`${f} ${theme}: ${info.bad.join(" | ")} width=${info.w}`);
    await page.screenshot({ path: `frames/${f.replace(".dc.html", "")}-${theme}.png`, fullPage: true });
    if (theme === "dark") console.log(f, "h=" + info.h, "fonts:", [...new Set(info.fontsOk)].join(","));
    await page.close();
  }
}
writeFileSync("heights.json", JSON.stringify(heights, null, 1));
console.log(problems.length ? "PROBLEMS\n" + problems.join("\n") : "no overflow problems");
await browser.close();
