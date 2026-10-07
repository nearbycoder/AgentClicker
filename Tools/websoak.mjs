// Long-session check for the browser build: the -soak mode (see Tools/soak.sh) in headless Chromium or Firefox.
// The page passes the player arguments from the URL; the game logs a "[Soak]" CSV line every 30 s and this script
// adds the page's own numbers (wasm heap, JS heap where the browser reports it) next to each one.
//
//   PLAYWRIGHT_CORE=/path/to/node_modules/playwright-core node Tools/websoak.mjs chromium|firefox [minutes] [out dir]
//   CHROMIUM_PATH=/path/to/chrome-headless-shell node Tools/websoak.mjs detached [minutes] [out dir]
//
// Same browser setup as Tools/webtest.mjs (CHROMIUM_PATH, FIREFOX_PATH). Writes soak.csv and console.log.
// WEBGL_DIR serves another copy of the build (default Builds/WebGL), so a long run can go on while the build is remade.
// WEBSOAK_QUERY adds to the page's URL (for example "glids=engine": the engine's own WebGL id allocator, for an A/B).
// The gl_* columns are the WebGL glue's id counter, its largest object table, the live objects in all of them, and the
// live buffers and textures on their own (buffers are the type that comes and goes).
//
// "detached" runs Chromium with no DevTools connection at all (an attached client makes the browser keep every console
// message, which could itself look like a leak). This script serves the build and starts the browser itself; the
// page's test-only ?report= option posts each [Soak] sample with the JS heap back here. No console.log in that mode.
import { createRequire } from "node:module";
import { spawn } from "node:child_process";
import { createReadStream, existsSync, mkdirSync, statSync, writeFileSync } from "node:fs";
import http from "node:http";
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
const build = path.resolve(process.env.WEBGL_DIR || path.join(root, "Builds", "WebGL"));
const query = process.env.WEBSOAK_QUERY ? "&" + process.env.WEBSOAK_QUERY : "";
const pw = engine === "detached" ? null : require(process.env.PLAYWRIGHT_CORE || "playwright-core");
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

function freePort() {
  return new Promise((resolve) => {
    const s = net.createServer().listen(0, "127.0.0.1", () => { const p = s.address().port; s.close(() => resolve(p)); });
  });
}

const header = "seconds,day,phase,cps,frames,avg_ms,max_ms,gc_mb,mono_used_mb,mono_heap_mb,unity_alloc_mb,unity_reserved_mb,rss_mb," +
               "gameobjects,textures,materials,meshes,toasts,wasm_heap_mb,js_heap_mb,gl_counter,gl_largest,gl_live,gl_buffers_live,gl_textures_live";
// the page reports each table as "length/live"
const liveIn = (t) => (typeof t === "string" ? t.split("/")[1] : "") ?? "";
const glColumns = (gl) => gl ? `${gl.counter},${gl.largest},${gl.live},${liveIn(gl.buffers)},${liveIn(gl.textures)}` : ",,,,";

async function detached() {
  const exe = process.env.CHROMIUM_PATH;
  if (!exe) throw new Error("detached mode needs CHROMIUM_PATH (a chrome-headless-shell or chrome binary)");
  const types = { ".html": "text/html", ".js": "text/javascript", ".json": "application/json" };
  const rows = [];
  let done = false, samples = 0, last = Date.now();
  const server = http.createServer((req, res) => {
    if (req.method === "POST" && req.url === "/soak-report") {
      let body = "";
      req.on("data", (c) => { body += c; });
      req.on("end", () => {
        res.end("ok");
        try {
          const r = JSON.parse(body);
          last = Date.now();
          if (r.text.includes("[Soak] done")) done = true;
          const csv = /^\[Soak\] (\d+,[^\n]*)/.exec(r.text);
          if (!csv) return;
          samples++;
          rows.push(`${csv[1]},,${r.js >= 0 ? (r.js / 1048576).toFixed(1) : ""},${glColumns(r.gl)}`);
          writeFileSync(path.join(out, "soak.csv"), header + "\n" + rows.join("\n") + "\n");
        } catch (e) { /* a partial post: skip it */ }
      });
      return;
    }
    const file = path.join(build, decodeURIComponent(new URL(req.url, "http://x").pathname).replace(/\/$/, "/index.html"));
    if (!file.startsWith(build) || !existsSync(file) || !statSync(file).isFile()) { res.statusCode = 404; res.end(); return; }
    res.setHeader("Content-Type", types[path.extname(file)] || "application/octet-stream");
    createReadStream(file).pipe(res);
  });
  const port = await freePort();
  await new Promise((r) => server.listen(port, "127.0.0.1", r));
  const args = ["-soak", "", String(minutes), "-daylength", "30"].map((a) => "arg=" + encodeURIComponent(a)).join("&");
  const url = `http://127.0.0.1:${port}/?report=/soak-report&${args}${query}`;
  const profile = path.join(out, "profile");
  mkdirSync(profile, { recursive: true });
  const browser = spawn(exe, ["--headless", "--use-angle=vulkan", "--enable-features=Vulkan", "--ignore-gpu-blocklist",
                              "--enable-precise-memory-info", "--window-size=1600,900", "--autoplay-policy=no-user-gesture-required",
                              "--no-first-run", `--user-data-dir=${profile}`, url], { stdio: ["ignore", "ignore", "pipe"] });
  let stderr = "";
  browser.stderr.on("data", (d) => { stderr = (stderr + d).slice(-20000); });
  writeFileSync(path.join(out, "browser.pid"), String(browser.pid));
  console.log(`[WebSoak] detached: browser pid ${browser.pid}, ${minutes} min, ${url}`);
  const end = Date.now() + (minutes + 5) * 60000;
  while (!done && Date.now() < end && browser.exitCode === null) {
    await sleep(5000);
    if (Date.now() - last > 180000) { console.log("[WebSoak] no sample for 3 minutes"); last = Date.now(); }
  }
  console.log(`[WebSoak] detached: ${done ? "finished" : browser.exitCode !== null ? "browser exited " + browser.exitCode : "TIMED OUT"}, ${samples} samples`);
  if (browser.exitCode === null) browser.kill();
  writeFileSync(path.join(out, "browser-stderr.log"), stderr);
  server.close();
  process.exitCode = done ? 0 : 1;
}

async function main() {
  if (engine === "detached") return detached();
  const port = await freePort();
  const server = spawn("python3", ["-m", "http.server", String(port), "--bind", "127.0.0.1", "-d", build],
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
        gl: window.agentClickerGL ? window.agentClickerGL() : null,
      })).catch(() => ({ wasm: -1, js: -1, gl: null }));
      const row = `${csv[1]},${(extra.wasm / 1048576).toFixed(1)},${(extra.js / 1048576).toFixed(1)},${glColumns(extra.gl)}`;
      rows.push(row);
      if (extra.gl) log.push("[GL tables] " + JSON.stringify(extra.gl)); // every table, as length/live
      console.log("[WebSoak] " + row);
      writeFileSync(path.join(out, "soak.csv"), header + "\n" + rows.join("\n") + "\n");
    });
    const args = ["-soak", "", String(minutes), "-daylength", "30"].map((a) => "arg=" + encodeURIComponent(a)).join("&");
    await page.goto(`http://127.0.0.1:${port}/?${args}${query}`);
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
