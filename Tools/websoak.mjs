// Long-session check for the browser build: the -soak mode (see Tools/soak.sh) in headless Chromium or Firefox.
// The page passes the player arguments from the URL; the game logs a "[Soak]" CSV line every 30 s and this script
// adds the page's own numbers (wasm heap, JS heap where the browser reports it) next to each one.
//
//   PLAYWRIGHT_CORE=/path/to/node_modules/playwright-core node Tools/websoak.mjs chromium|firefox [minutes] [out dir]
//
// Same browser setup as Tools/webtest.mjs (CHROMIUM_PATH, FIREFOX_PATH). Writes soak.csv and console.log.
import { createRequire } from "node:module";
import { spawn } from "node:child_process";
import { mkdirSync, writeFileSync } from "node:fs";
import net from "node:net";
import path from "node:path";

const require = createRequire(import.meta.url);
const root = path.resolve(path.dirname(new URL(import.meta.url).pathname), "..");
const engine = process.argv[2] || "chromium";
const minutes = Number(process.argv[3] || 30);
const out = path.resolve(process.argv[4] || path.join(root, "Logs", "websoak-" + engine));
mkdirSync(out, { recursive: true });
if (engine === "firefox") {
  process.env.TMPDIR = path.join(root, "Logs", "tmp");
  mkdirSync(process.env.TMPDIR, { recursive: true });
}
const pw = require(process.env.PLAYWRIGHT_CORE || "playwright-core");
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

function freePort() {
  return new Promise((resolve) => {
    const s = net.createServer().listen(0, "127.0.0.1", () => { const p = s.address().port; s.close(() => resolve(p)); });
  });
}

async function main() {
  const port = await freePort();
  const server = spawn("python3", ["-m", "http.server", String(port), "--bind", "127.0.0.1", "-d", path.join(root, "Builds", "WebGL")],
                       { stdio: "ignore" });
  const launch = { headless: true };
  if (engine === "chromium") {
    if (process.env.CHROMIUM_PATH) launch.executablePath = process.env.CHROMIUM_PATH;
    launch.args = ["--use-angle=vulkan", "--enable-features=Vulkan", "--ignore-gpu-blocklist", "--enable-precise-memory-info"];
  } else if (engine === "firefox") {
    launch.channel = "moz-firefox";
    launch.executablePath = process.env.FIREFOX_PATH || "/usr/bin/firefox";
  }
  const log = [];
  const rows = [];
  let browser, done = false, errors = 0;
  try {
    browser = await pw[engine].launch(launch);
    const page = await (await browser.newContext({ viewport: { width: 1600, height: 900 } })).newPage();
    page.on("pageerror", (e) => { errors++; log.push("[pageerror] " + e); });
    page.on("console", async (m) => {
      const text = m.text();
      log.push(`[${m.type()}] ${text}`);
      if (text.includes("[Soak] done")) done = true;
      const csv = /^\[Soak\] (\d+,[^\n]*)/.exec(text);
      if (!csv) return;
      const extra = await page.evaluate(() => ({
        wasm: (() => { const m = window.unityInstance?.Module; const b = m?.wasmMemory?.buffer ?? m?.HEAPU8?.buffer ?? m?.HEAP8?.buffer;
                        return b ? b.byteLength : -1; })(),
        js: performance.memory ? performance.memory.usedJSHeapSize : -1,
      })).catch(() => ({ wasm: -1, js: -1 }));
      const row = `${csv[1]},${(extra.wasm / 1048576).toFixed(1)},${(extra.js / 1048576).toFixed(1)}`;
      rows.push(row);
      console.log("[WebSoak] " + row);
      writeFileSync(path.join(out, "soak.csv"),
        "seconds,day,phase,cps,frames,avg_ms,max_ms,gc_mb,mono_used_mb,mono_heap_mb,unity_alloc_mb,unity_reserved_mb,rss_mb," +
        "gameobjects,textures,materials,meshes,toasts,wasm_heap_mb,js_heap_mb\n" + rows.join("\n") + "\n");
    });
    const args = ["-soak", "", String(minutes), "-daylength", "30"].map((a) => "arg=" + encodeURIComponent(a)).join("&");
    await page.goto(`http://127.0.0.1:${port}/?${args}`);
    const end = Date.now() + (minutes + 5) * 60000;
    while (!done && Date.now() < end) await sleep(2000);
    await page.screenshot({ path: path.join(out, "last.png") });
    console.log(`[WebSoak] ${engine}: ${done ? "finished" : "TIMED OUT"}, ${rows.length} samples, ${errors} page errors, ` +
                `${log.filter((l) => l.includes("[Autopilot] log in")).length} days logged in by the autopilot`);
  } finally {
    writeFileSync(path.join(out, "console.log"), log.join("\n").slice(-400000));
    if (browser) await browser.close();
    server.kill();
  }
  process.exitCode = done && errors === 0 ? 0 : 1;
}

main().catch((e) => { console.error(e); process.exitCode = 2; });
