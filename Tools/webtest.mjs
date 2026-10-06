// Drives the browser build (Builds/WebGL) in headless Chromium or WebKit and checks the things a browser player
// depends on: it loads, a new game starts and logs in, SHIP CODE and hiring work, audio is actually produced, the
// save reaches IndexedDB when the tab is hidden, and CONTINUE restores it after a reload.
//
//   PLAYWRIGHT_CORE=/path/to/node_modules/playwright-core node Tools/webtest.mjs chromium|webkit [out dir]
//
// Needs a playwright-core whose browsers are in ~/.cache/ms-playwright (1.63 matches webkit-2359). For Chromium,
// CHROMIUM_PATH may point at a cached headless shell of another revision. Coordinates assume the 1600x900 viewport.
import { createRequire } from "node:module";
import { spawn } from "node:child_process";
import { mkdirSync, writeFileSync } from "node:fs";
import net from "node:net";
import path from "node:path";

const require = createRequire(import.meta.url);
const root = path.resolve(path.dirname(new URL(import.meta.url).pathname), "..");
const engine = process.argv[2] || "chromium";
const out = path.resolve(process.argv[3] || path.join(root, "Logs", "web-" + engine));
mkdirSync(out, { recursive: true });
const pw = require(process.env.PLAYWRIGHT_CORE || "playwright-core");

const results = [];
function check(ok, what) {
  results.push({ ok, what });
  console.log(`[Web] ${ok ? "PASS" : "FAIL"} ${what}`);
}
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

function freePort() {
  return new Promise((resolve) => {
    const s = net.createServer().listen(0, "127.0.0.1", () => { const p = s.address().port; s.close(() => resolve(p)); });
  });
}

// An analyser on every AudioContext that reaches the speakers: tells us whether the game is producing sound.
const audioProbe = () => {
  const analysers = [];
  const connect = AudioNode.prototype.connect;
  AudioNode.prototype.connect = function (dest, ...rest) {
    const r = connect.call(this, dest, ...rest);
    try {
      if (typeof AudioDestinationNode !== "undefined" && dest instanceof AudioDestinationNode) {
        const ctx = dest.context;
        if (!ctx.__probe) {
          ctx.__probe = ctx.createAnalyser();
          ctx.__probe.fftSize = 2048;
          const mute = ctx.createGain();
          mute.gain.value = 0;
          connect.call(ctx.__probe, mute);
          connect.call(mute, ctx.destination);
          analysers.push(ctx.__probe);
        }
        connect.call(this, ctx.__probe);
      }
    } catch (e) { /* never break the game's audio */ }
    return r;
  };
  window.__audio = () => {
    let peak = 0;
    const buf = new Float32Array(2048);
    for (const a of analysers) {
      a.getFloatTimeDomainData(buf);
      for (let i = 0; i < buf.length; i++) peak = Math.max(peak, Math.abs(buf[i]));
    }
    return { peak, contexts: analysers.length, states: analysers.map((a) => a.context.state) };
  };
};

// A file as the game last flushed it to IndexedDB (Unity's IDBFS), as text, or null.
const readIdb = (suffix) => new Promise((resolve) => {
  const open = indexedDB.open("/idbfs");
  open.onerror = () => resolve(null);
  open.onsuccess = () => {
    const db = open.result;
    if (!db.objectStoreNames.contains("FILE_DATA")) return resolve(null);
    const req = db.transaction("FILE_DATA").objectStore("FILE_DATA").openCursor();
    req.onsuccess = () => {
      const c = req.result;
      if (!c) return resolve(null);
      if (String(c.key).endsWith(suffix) && c.value.contents) return resolve(new TextDecoder().decode(c.value.contents));
      c.continue();
    };
    req.onerror = () => resolve(null);
  };
});
const readSave = async (page) => {
  const text = await page.evaluate(readIdb, "/agentclicker_save.json");
  try { return text ? JSON.parse(text) : null; } catch (e) { return null; }
};

async function main() {
  const port = await freePort();
  const server = spawn("python3", ["-m", "http.server", String(port), "--bind", "127.0.0.1", "-d", path.join(root, "Builds", "WebGL")],
                       { stdio: "ignore" });
  const launch = { headless: true };
  if (engine === "chromium") {
    if (process.env.CHROMIUM_PATH) launch.executablePath = process.env.CHROMIUM_PATH;
    // the real GPU through ANGLE/Vulkan instead of SwiftShader, which is far too slow for a 3D game
    launch.args = ["--use-angle=vulkan", "--enable-features=Vulkan", "--ignore-gpu-blocklist"];
  }
  const log = [];
  let browser;
  try {
    browser = await pw[engine].launch(launch);
    await sleep(500);
    const context = await browser.newContext({ viewport: { width: 1600, height: 900 } });
    await context.addInitScript(audioProbe);
    const page = await context.newPage();
    page.on("console", (m) => log.push(`[${m.type()}] ${m.text()}`));
    const pageErrors = [];
    page.on("pageerror", (e) => { pageErrors.push(String(e)); log.push("[pageerror] " + e); });
    const shot = (name) => page.screenshot({ path: path.join(out, name + ".png") });
    // waits for a line the game logs (Debug.Log goes to the browser console)
    const waitLog = async (text, ms) => {
      for (const end = Date.now() + ms; Date.now() < end; await sleep(250)) if (log.some((l) => l.includes(text))) return true;
      return false;
    };
    const url = `http://127.0.0.1:${port}/`;

    const loaded = async () => {
      await page.waitForFunction(() => document.querySelector("#loading")?.style.display === "none", null, { timeout: 240000 });
      await sleep(4000);
    };

    // ---- first load, new game, autopilot login ----------------------------------------------------------
    const t0 = Date.now();
    await page.goto(url);
    await loaded();
    console.log(`[Web] ${engine}: loaded in ${((Date.now() - t0) / 1000).toFixed(1)} s`);
    console.log("[Web] renderer: " + await page.evaluate(() => {
      const gl = document.createElement("canvas").getContext("webgl2");
      if (!gl) return "no WebGL2";
      const ext = gl.getExtension("WEBGL_debug_renderer_info");
      return ext ? gl.getParameter(ext.UNMASKED_RENDERER_WEBGL) : gl.getParameter(gl.RENDERER);
    }));
    await shot("01_title");
    check(pageErrors.length === 0, "loaded to the title screen without page errors");

    // settings are kept in PlayerPrefs, in the same IndexedDB file system: switch numbers to scientific
    await page.mouse.click(250, 579); // SETTINGS
    await sleep(1000);
    await page.mouse.click(735, 182); // GAMEPLAY
    await sleep(800);
    await page.mouse.click(1220, 609); // Number format ▶
    await sleep(500);
    await page.mouse.click(1160, 784); // DONE
    await sleep(2000);

    await page.mouse.click(250, 509); // NEW GAME
    await sleep(1500);
    await page.mouse.click(1510, 50); // SKIP the intro cards
    await sleep(1500);
    await shot("02_morning");
    // nobody touches anything: the autopilot logs in after 10 s
    const auto = await waitLog("[Autopilot] log in", 120000);
    check(auto, "autopilot logged in on day 1");
    if (!auto) await page.mouse.click(868, 360); // the monitor
    await sleep(3000); // the camera dollies in
    await shot("03_logged_in");

    // ---- SHIP CODE and audio ------------------------------------------------------------------------------
    for (let i = 0; i < 30; i++) { await page.mouse.click(234, 492); await sleep(70); }
    let audio = { peak: 0 };
    for (let i = 0; i < 10; i++) {
      const a = await page.evaluate(() => window.__audio ? window.__audio() : null);
      if (a && a.peak >= audio.peak) audio = a;
      await sleep(200);
    }
    console.log(`[Web] audio: ${JSON.stringify(audio)}`);
    check(audio.peak > 0.001, `audio is produced after the first clicks (peak ${audio.peak.toFixed(4)}, contexts ${audio.contexts}, ${audio.states})`);
    await shot("04_shipped");

    // hire an Autocomplete (top row of the store)
    await page.mouse.click(1290, 235);
    await sleep(600);
    await shot("05_hired");

    // ---- hiding the tab saves at once -------------------------------------------------------------------
    // wait for an autosave, then ship more code: only a save on hide can include those clicks
    const before = await readSave(page);
    for (let i = 0; i < 80; i++) {
      await sleep(250);
      const s = await readSave(page);
      if (s && before && s.lastSaveUnix !== before.lastSaveUnix) break;
    }
    const autosaved = await readSave(page);
    for (let i = 0; i < 20; i++) { await page.mouse.click(234, 492); await sleep(60); }
    await sleep(300);
    await page.evaluate(() => {
      Object.defineProperty(document, "visibilityState", { configurable: true, get: () => "hidden" });
      Object.defineProperty(document, "hidden", { configurable: true, get: () => true });
      document.dispatchEvent(new Event("visibilitychange"));
    });
    await sleep(1500);
    const hidden = await readSave(page);
    const extra = hidden && autosaved ? hidden.clicks - autosaved.clicks : -1;
    console.log(`[Web] clicks in the autosave ${autosaved?.clicks}, after hiding ${hidden?.clicks}`);
    check(extra >= 20, `hiding the tab saved the ${extra} clicks made since the last autosave`);

    // ---- reload and CONTINUE ------------------------------------------------------------------------------
    await page.evaluate(() => {
      delete document.visibilityState; delete document.hidden;
    });
    const saved = await readSave(page);
    await page.reload();
    await loaded();
    await shot("06_title_after_reload");
    await page.mouse.click(250, 439); // CONTINUE
    await sleep(3000);
    await shot("07_continued");
    const after = await readSave(page);
    check(!!saved && !!after && after.introSeen && after.clicks >= saved.clicks && after.agentCounts[0] >= 1,
          `the career survives a reload (clicks ${saved?.clicks} → ${after?.clicks}, Autocomplete x${after?.agentCounts?.[0]}, day ${after?.day})`);
    console.log("[Web] IndexedDB files: " + JSON.stringify(await page.evaluate(() => new Promise((resolve) => {
      const open = indexedDB.open("/idbfs");
      open.onsuccess = () => {
        const req = open.result.transaction("FILE_DATA").objectStore("FILE_DATA").getAllKeys();
        req.onsuccess = () => resolve(req.result.map(String));
        req.onerror = () => resolve([]);
      };
      open.onerror = () => resolve([]);
    }))));
    const prefs = await page.evaluate(readIdb, "/PlayerPrefs");
    check(!!prefs && prefs.includes('"numberStyle":1'), "a settings change survives the reload (PlayerPrefs in IndexedDB)");
    const overlaps = log.filter((l) => l.includes("syncfs operations in flight")).length;
    check(overlaps === 0, `IndexedDB syncs never overlap (${overlaps} warnings)`);
    check(pageErrors.length === 0, `no page errors (${pageErrors.length})`);
  } finally {
    writeFileSync(path.join(out, "console.log"), log.join("\n").slice(-400000));
    if (browser) await browser.close();
    server.kill();
  }
  const failed = results.filter((r) => !r.ok).length;
  console.log(`[Web] ${engine}: ${results.length - failed}/${results.length} checks passed`);
  process.exitCode = failed ? 1 : 0;
}

main().catch((e) => { console.error(e); process.exitCode = 2; });
