// Draws the soak results (Tools/soak.sh, Tools/websoak.mjs) as small multiples and saves a PNG:
//   PLAYWRIGHT_CORE=... CHROMIUM_PATH=... node Tools/soak_chart.mjs desktop.csv browser.csv out.png
// One unit per panel (MB, or a count), no second axis. Each panel's numbers are in the CSVs.
import { createRequire } from "node:module";
import { readFileSync, writeFileSync } from "node:fs";
import path from "node:path";

const require = createRequire(import.meta.url);
const [desktopCsv, browserCsv, outPng] = process.argv.slice(2);

function read(file) {
  const [head, ...lines] = readFileSync(file, "utf8").trim().split("\n");
  const cols = head.split(",");
  return lines.map((l) => Object.fromEntries(l.split(",").map((v, i) => [cols[i], isNaN(+v) ? v : +v])));
}
const desk = read(desktopCsv), web = read(browserCsv);

// reference categorical slots 1-3 (light mode), text in ink tokens, recessive grid
const C = { s1: "#2a78d6", s2: "#eb6834", s3: "#1baf7a", ink: "#0b0b0b", ink2: "#52514e", grid: "#e4e3df", surface: "#fcfcfb" };

function panel(x0, y0, w, h, title, rows, series, unit) {
  const minutes = rows.map((r) => r.seconds / 60);
  const xMax = Math.ceil(Math.max(...minutes) / 10) * 10;
  const yMaxRaw = Math.max(...series.flatMap((s) => rows.map((r) => r[s.key])));
  const step = [10, 20, 50, 100, 200, 250, 500, 1000].find((s) => yMaxRaw * 1.15 / s <= 5) || 1000;
  const yMax = Math.ceil(yMaxRaw * 1.15 / step) * step;
  const L = 56, R = 200, T = 44, B = 36;
  const X = (m) => x0 + L + (m / xMax) * (w - L - R), Y = (v) => y0 + h - B - (v / yMax) * (h - T - B);
  let s = `<text x="${x0 + L}" y="${y0 + 22}" font-size="15" font-weight="600" fill="${C.ink}">${title}</text>`;
  for (let v = 0; v <= yMax; v += step) {
    s += `<line x1="${X(0)}" x2="${X(xMax)}" y1="${Y(v)}" y2="${Y(v)}" stroke="${C.grid}" stroke-width="1"/>`;
    s += `<text x="${X(0) - 8}" y="${Y(v) + 4}" font-size="11" text-anchor="end" fill="${C.ink2}">${v}</text>`;
  }
  for (let m = 0; m <= xMax; m += 10)
    s += `<text x="${X(m)}" y="${y0 + h - B + 18}" font-size="11" text-anchor="middle" fill="${C.ink2}">${m} min</text>`;
  s += `<text x="${x0 + 10}" y="${y0 + T - 8}" font-size="11" fill="${C.ink2}">${unit}</text>`;
  const ends = [];
  for (const se of series) {
    const pts = rows.map((r) => `${X(r.seconds / 60).toFixed(1)},${Y(r[se.key]).toFixed(1)}`).join(" ");
    s += `<polyline points="${pts}" fill="none" stroke="${se.color}" stroke-width="2" stroke-linejoin="round"/>`;
    const last = rows[rows.length - 1][se.key];
    ends.push({ y: Y(last), text: `${se.name} ${Math.round(last)}`, color: se.color });
  }
  // direct labels at the line ends, nudged apart so they never overlap
  ends.sort((a, b) => a.y - b.y);
  for (let i = 1; i < ends.length; i++) ends[i].y = Math.max(ends[i].y, ends[i - 1].y + 15);
  for (const e of ends) {
    s += `<rect x="${X(xMax) + 8}" y="${e.y - 5}" width="10" height="3" rx="1.5" fill="${e.color}"/>`;
    s += `<text x="${X(xMax) + 22}" y="${e.y + 4}" font-size="12" fill="${C.ink}">${e.text}</text>`;
  }
  return s;
}

const W = 1200, H = 300;
const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${W}" height="${H * 3 + 40}" font-family="Fira Sans, DejaVu Sans, sans-serif">
<rect width="100%" height="100%" fill="${C.surface}"/>
${panel(0, 0, W, H, `Desktop player: ${Math.round(desk.at(-1).seconds / 60)} minutes, ${desk.at(-1).day - desk[0].day} work days, nobody at the keyboard`, desk, [
  { key: "rss_mb", name: "Process RSS", color: C.s1 },
  { key: "unity_alloc_mb", name: "Unity allocated", color: C.s2 },
  { key: "gc_mb", name: "Managed heap", color: C.s3 },
], "MB")}
${panel(0, H, W, H, `Browser build (headless Chrome): ${Math.round(web.at(-1).seconds / 60)} minutes, ${web.at(-1).day - web[0].day} work days`, web, [
  { key: "unity_reserved_mb", name: "Unity reserved", color: C.s1 },
  { key: "unity_alloc_mb", name: "Unity allocated", color: C.s2 },
  { key: "js_heap_mb", name: "Page JS heap", color: C.s3 },
], "MB")}
${panel(0, H * 2, W, H, "Objects alive in the scene", desk, [
  { key: "gameobjects", name: "GameObjects (desktop)", color: C.s1 },
  { key: "meshes", name: "Meshes (desktop)", color: C.s2 },
  { key: "materials", name: "Materials (desktop)", color: C.s3 },
], "count")}
<text x="56" y="${H * 3 + 26}" font-size="11" fill="${C.ink2}">Sampled every 30 s. Data: docs/media/improvements/round4/soak-*.csv</text>
</svg>`;

const html = `<!doctype html><meta charset="utf-8"><body style="margin:0;background:${C.surface}">${svg}</body>`;
const htmlPath = outPng.replace(/\.\w+$/, ".html");
writeFileSync(htmlPath, html);
const pw = require(process.env.PLAYWRIGHT_CORE || "playwright-core");
const browser = await pw.chromium.launch({ headless: true, executablePath: process.env.CHROMIUM_PATH });
const page = await browser.newPage({ viewport: { width: W, height: H * 3 + 40 } });
await page.goto("file://" + path.resolve(htmlPath));
await page.screenshot({ path: outPng });
await browser.close();
console.log("wrote " + outPng);
