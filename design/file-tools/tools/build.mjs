// Generates the artboards (*.dc.html) and canvas.json. Run: node tools/build.mjs   (from design/file-tools)
import { writeFileSync, existsSync, readFileSync } from "node:fs";
import { board } from "./lib.mjs";
import { tools1 } from "./tools1.mjs";
const all = [...tools1];
for (const m of ["tools2.mjs", "tools3.mjs"]) if (existsSync(new URL(m, import.meta.url))) { const x = await import("./" + m); all.push(...(x.tools2 || x.tools3)); }

const heights = existsSync("heights.json") ? JSON.parse(readFileSync("heights.json", "utf8")) : {};
const boards = {}; const order = [];
let y = 0; let col = 0; let rowH = 0;
for (const t of all) {
  const r = t.build();
  const html = board({ num: t.num, title: t.title, tags: t.tags, cells: r.cells, one: r.one });
  const name = `${t.file}.dc.html`;
  writeFileSync(name, html);
  const h = heights[name]?.h || 1400; const bw = Math.max(1680, heights[name]?.w || 1680);
  boards[name] = { x: col * 2060, y, w: bw, h, title: `${t.num} · ${t.title}`, expand: "fill", is_interactive: true };
  order.push(name);
  rowH = Math.max(rowH, h);
  if (col === 1) { y += rowH + 120; col = 0; rowH = 0; } else col = 1;
}
const canvas = { v: 3, createdOnFiles: { v: 1, at: "2026-11-05T09:00:00Z" }, title: "Monitoring Tools file tools", launch: { view: "canvas" }, pages: [], boards, order,
  notes: { t1: { x: 0, y: -300, text: "File tools: one dialog per format, opened from the clipboard notice", kind: "title1", maxW: 3400 } }, designSystems: [] };
writeFileSync("canvas.json", JSON.stringify(canvas, null, 2) + "\n");
console.log("boards:", order.length);
