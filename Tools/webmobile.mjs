// Phones and tablets: loads a browser build in headless WebKit as an iPhone 15 and an iPad Pro 11 and in headless Chromium
// as a Pixel 7, and plays a short session with touch events only: the title, NEW GAME, the login tap, SHIP CODE, a hire,
// the office view and back, the pause menu. Along the way it measures what decides whether a phone keeps the tab alive:
// the WebAssembly heap, the GPU memory the page allocates through WebGL (textures, render buffers, vertex and index
// buffers and the canvas, added up from the WebGL calls themselves), the browser's processes' resident memory, the
// download, and the frame rate. It also checks the on-screen touch controls (shown on these devices, out of the way of
// the notch and big enough for a thumb), the rotate prompt for a phone held upright, and that nothing scrolls or zooms
// the page.
//
//   node Tools/webmobile.mjs [site dir] [out dir]      default Builds/Pages, Logs/web-mobile
//   WEBMOBILE_ONLY=iphone,ipad,pixel                   a subset of the profiles
//
// WebKit is Playwright 1.63's (WEBKIT_PATH, default ~/.cache/webkit-libs/webkit-2359/pw_run.sh); it reports a coarse
// pointer and WebGL 2 but doesn't enforce iOS's memory limit, which is why the memory is measured here. Taps are
// Playwright's touchscreen (real touch events in both engines); WebKit has no multi-touch API, so the two-finger check
// there dispatches the page's own TouchEvents. Writes <profile>_*.png, <profile>_console.log and summary.json to the
// out dir.
import { createRequire } from "node:module";
import { spawn } from "node:child_process";
import { existsSync, mkdirSync, readdirSync, readFileSync, writeFileSync } from "node:fs";
import net from "node:net";
import os from "node:os";
import path from "node:path";

const require = createRequire(import.meta.url);
const root = path.resolve(path.dirname(new URL(import.meta.url).pathname), "..");
const site = path.resolve(process.argv[2] || path.join(root, "Builds", "Pages"));
const out = path.resolve(process.argv[3] || path.join(root, "Logs", "web-mobile"));
mkdirSync(out, { recursive: true });

function playwright() {
  for (const p of [process.env.PLAYWRIGHT_CORE, "playwright-core", path.join(os.homedir(), "Sites/blog/node_modules/playwright-core")]) {
    if (!p) continue;
    try { return require(p); } catch (e) { /* next */ }
  }
  throw new Error("playwright-core not found: set PLAYWRIGHT_CORE=/path/to/node_modules/playwright-core");
}
const pw = playwright();

function cachedHeadlessShell() {
  const cache = path.join(os.homedir(), ".cache/ms-playwright");
  if (!existsSync(cache)) return undefined;
  const dirs = readdirSync(cache).filter((d) => /^chromium_headless_shell-\d+$/.test(d))
    .sort((a, b) => Number(b.split("-")[1]) - Number(a.split("-")[1]));
  for (const d of dirs) {
    const exe = path.join(cache, d, "chrome-headless-shell-linux64/chrome-headless-shell");
    if (existsSync(exe)) return exe;
  }
  return undefined;
}

const results = [];
function check(ok, what) {
  results.push({ ok, what });
  console.log(`[Mobile] ${ok ? "PASS" : "FAIL"} ${what}`);
}
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const MB = (b) => (b / 1048576).toFixed(1);

// the save as the game last flushed it to IndexedDB
const readSave = (page) => page.evaluate(() => new Promise((resolve) => {
  const open = indexedDB.open("/idbfs");
  open.onerror = () => resolve(null);
  open.onsuccess = () => {
    const db = open.result;
    if (!db.objectStoreNames.contains("FILE_DATA")) return resolve(null);
    const req = db.transaction("FILE_DATA").objectStore("FILE_DATA").openCursor();
    req.onsuccess = () => {
      const c = req.result;
      if (!c) return resolve(null);
      if (String(c.key).endsWith("/agentclicker_save.json") && c.value.contents) {
        try { return resolve(JSON.parse(new TextDecoder().decode(c.value.contents))); } catch (e) { return resolve(null); }
      }
      c.continue();
    };
    req.onerror = () => resolve(null);
  };
}));

function freePort() {
  return new Promise((resolve) => {
    const s = net.createServer().listen(0, "127.0.0.1", () => { const p = s.address().port; s.close(() => resolve(p)); });
  });
}

// ---- the browser's processes: resident memory of the launched process and everything under it, from /proc ----
function children() {
  const kids = new Map();
  for (const d of readdirSync("/proc")) {
    if (!/^\d+$/.test(d)) continue;
    try {
      const stat = readFileSync(`/proc/${d}/stat`, "utf8");
      const ppid = Number(stat.slice(stat.lastIndexOf(")") + 2).split(" ")[1]);
      if (!kids.has(ppid)) kids.set(ppid, []);
      kids.get(ppid).push(Number(d));
    } catch (e) { /* gone */ }
  }
  return kids;
}
function treeRss(pid) {
  const kids = children();
  const byName = {};
  let total = 0;
  const stack = [pid];
  while (stack.length) {
    const p = stack.pop();
    try {
      const status = readFileSync(`/proc/${p}/status`, "utf8");
      const rss = Number(/VmRSS:\s+(\d+)/.exec(status)?.[1] ?? 0) * 1024;
      let name = /Name:\s+(\S+)/.exec(status)?.[1] ?? "?";
      try { const type = /--type=(\S+)/.exec(readFileSync(`/proc/${p}/cmdline`, "utf8").replace(/\0/g, " ")); if (type) name += ":" + type[1]; } catch (e) { /* gone */ }
      total += rss;
      byName[name] = (byName[name] ?? 0) + rss;
    } catch (e) { /* gone */ }
    stack.push(...(kids.get(p) ?? []));
  }
  return { total, byName };
}

// ---- in the page, before anything else: count what the page allocates through WebGL and find the wasm heap ----
const instrument = () => {
  const mem = { gpu: 0, peakGpu: 0, tex: 0, rb: 0, buf: 0, unknownFormats: {}, memories: [], attrs: null, glVersion: "" };
  window.__acMem = mem;
  const OrigMemory = WebAssembly.Memory;
  WebAssembly.Memory = function (d) { const m = new OrigMemory(d); mem.memories.push(m); return m; };
  WebAssembly.Memory.prototype = OrigMemory.prototype;
  const grab = (r) => {
    const inst = r && (r.instance || r);
    for (const v of Object.values((inst && inst.exports) || {})) if (v instanceof OrigMemory && !mem.memories.includes(v)) mem.memories.push(v);
    return r;
  };
  const inst = WebAssembly.instantiate, instS = WebAssembly.instantiateStreaming;
  WebAssembly.instantiate = function () { return inst.apply(this, arguments).then(grab); };
  if (instS) WebAssembly.instantiateStreaming = function () { return instS.apply(this, arguments).then(grab); };
  mem.heap = () => mem.memories.reduce((n, m) => Math.max(n, m.buffer.byteLength), 0);
  // each time the heap grows, a console line, so the game's own log shows what it was doing
  const grow = OrigMemory.prototype.grow;
  OrigMemory.prototype.grow = function (pages) {
    const r = grow.apply(this, arguments);
    console.log(`[Heap] grew by ${(pages * 65536 / 1048576).toFixed(1)} MB to ${(this.buffer.byteLength / 1048576).toFixed(1)} MB`);
    return r;
  };

  const G = window.WebGL2RenderingContext;
  if (!G) return;
  const P = G.prototype;
  // bytes per pixel of the sized formats the engine uses (unsized RGBA/RGB: 4)
  const bpp = { 0x8058: 4, 0x8C43: 4, 0x881A: 8, 0x8814: 16, 0x8C3A: 4, 0x8059: 4, 0x88F0: 4, 0x81A6: 4, 0x8CAC: 4,
                0x8CAD: 8, 0x81A5: 2, 0x8229: 1, 0x822B: 2, 0x822D: 2, 0x822F: 4, 0x822E: 4, 0x8230: 8, 0x8051: 4,
                0x1908: 4, 0x1907: 4, 0x1906: 1, 0x1909: 1, 0x190A: 2, 0x8D62: 2, 0x8056: 2, 0x8057: 2, 0x8232: 1,
                0x8233: 1, 0x881B: 6, 0x8815: 12, 0x8D7C: 4, 0x8D8E: 4, 0x8D70: 16, 0x8D82: 16, 0x8C41: 4, 0x8D48: 1,
                0x8D9F: 4, 0x8C3D: 4, 0x823A: 2, 0x823C: 4, 0x8234: 2, 0x8236: 4, 0x8231: 1, 0x8238: 2 };
  const size = (fmt) => { if (bpp[fmt]) return bpp[fmt]; mem.unknownFormats[fmt] = 1; return 4; };
  const add = (kind, d) => { mem[kind] += d; mem.gpu += d; if (mem.gpu > mem.peakGpu) mem.peakGpu = mem.gpu; };
  const texOf = new WeakMap(), rbOf = new WeakMap(), bufOf = new WeakMap(); // object → { key: bytes }
  const state = (gl) => gl.__acState || (gl.__acState = { unit: 0, tex: {}, rb: null, buf: {} });
  const boundTex = (gl, target) => {
    const s = state(gl);
    const t = target >= 0x8515 && target <= 0x851A ? 0x8513 : target; // a cube map face → the cube map
    return s.tex[s.unit + ":" + t];
  };
  const setBytes = (map, obj, key, bytes, kind) => {
    if (!obj) return;
    let e = map.get(obj);
    if (!e) map.set(obj, e = {});
    add(kind, bytes - (e[key] || 0));
    e[key] = bytes;
  };
  const free = (map, obj, kind) => {
    const e = obj && map.get(obj);
    if (!e) return;
    for (const k in e) add(kind, -e[k]);
    map.delete(obj);
  };
  const wrap = (name, fn) => { const o = P[name]; if (o) P[name] = function () { fn.call(this, arguments); return o.apply(this, arguments); }; };
  wrap("activeTexture", function (a) { state(this).unit = a[0] - 0x84C0; });
  wrap("bindTexture", function (a) { state(this).tex[state(this).unit + ":" + a[0]] = a[1]; });
  wrap("deleteTexture", function (a) { free(texOf, a[0], "tex"); });
  wrap("texStorage2D", function (a) {
    const [target, levels, fmt, w, h] = a;
    let b = 0;
    for (let l = 0; l < levels; l++) b += Math.max(1, w >> l) * Math.max(1, h >> l) * size(fmt);
    setBytes(texOf, boundTex(this, target), "s", b * (target === 0x8513 ? 6 : 1), "tex");
  });
  wrap("texStorage3D", function (a) {
    const [target, levels, fmt, w, h, d] = a;
    let b = 0;
    for (let l = 0; l < levels; l++) b += Math.max(1, w >> l) * Math.max(1, h >> l) * (target === 0x806F ? Math.max(1, d >> l) : d) * size(fmt);
    setBytes(texOf, boundTex(this, target), "s", b, "tex");
  });
  wrap("texImage2D", function (a) {
    let w, h, fmt = a[2];
    if (a.length >= 8) { w = a[3]; h = a[4]; } else { const src = a[5]; w = src.videoWidth || src.naturalWidth || src.width; h = src.videoHeight || src.naturalHeight || src.height; }
    setBytes(texOf, boundTex(this, a[0]), a[0] + ":" + a[1], w * h * size(fmt), "tex");
  });
  wrap("texImage3D", function (a) { setBytes(texOf, boundTex(this, a[0]), "l" + a[1], a[3] * a[4] * a[5] * size(a[2]), "tex"); });
  wrap("compressedTexImage2D", function (a) {
    const data = a[6];
    const b = typeof data === "number" ? data : (a.length > 8 && a[8] ? a[8] : data ? data.byteLength - (a[7] || 0) : 0);
    setBytes(texOf, boundTex(this, a[0]), a[0] + ":" + a[1], b, "tex");
  });
  wrap("bindRenderbuffer", function (a) { state(this).rb = a[1]; });
  wrap("deleteRenderbuffer", function (a) { free(rbOf, a[0], "rb"); });
  wrap("renderbufferStorage", function (a) { setBytes(rbOf, state(this).rb, "s", a[2] * a[3] * size(a[1]), "rb"); });
  wrap("renderbufferStorageMultisample", function (a) { setBytes(rbOf, state(this).rb, "s", a[3] * a[4] * size(a[2]) * Math.max(1, a[1]), "rb"); });
  wrap("bindBuffer", function (a) { state(this).buf[a[0]] = a[1]; });
  wrap("bindBufferBase", function (a) { state(this).buf[a[0]] = a[2]; });
  wrap("bindBufferRange", function (a) { state(this).buf[a[0]] = a[2]; });
  wrap("deleteBuffer", function (a) { free(bufOf, a[0], "buf"); });
  wrap("bufferData", function (a) {
    const d = a[1];
    const b = typeof d === "number" ? d : !d ? 0 : a.length >= 5 && a[4] ? a[4] * (d.BYTES_PER_ELEMENT || 1) : d.byteLength - (a[3] || 0) * (d.BYTES_PER_ELEMENT || 1);
    setBytes(bufOf, state(this).buf[a[0]], "s", b, "buf");
  });
  const getContext = HTMLCanvasElement.prototype.getContext;
  HTMLCanvasElement.prototype.getContext = function (type, attrs) {
    const ctx = getContext.apply(this, arguments);
    if (ctx && this.id === "unity-canvas" && /webgl/.test(type) && !mem.attrs) {
      mem.attrs = ctx.getContextAttributes ? ctx.getContextAttributes() : attrs;
      mem.glVersion = String(ctx.getParameter(ctx.VERSION));
      mem.canvas = this;
    }
    return ctx;
  };
  // the canvas's own buffers: colour, depth and stencil, and the multisampled copy when the context antialiases
  mem.canvasBytes = () => {
    const c = mem.canvas, a = mem.attrs;
    if (!c || !a) return 0;
    const px = c.width * c.height;
    return px * (4 + (a.depth || a.stencil ? 4 : 0)) * (a.antialias ? 1 + 4 : 1) + px * 4; // + the composited copy
  };
};

// ---- the session ----
const profiles = [
  { name: "iphone", engine: "webkit", device: "iPhone 15 landscape", portrait: "iPhone 15", label: "iPhone 15 (WebKit)" },
  { name: "ipad", engine: "webkit", device: "iPad Pro 11 landscape", portrait: "iPad Pro 11", label: "iPad Pro 11 (WebKit)" },
  { name: "pixel", engine: "chromium", device: "Pixel 7 landscape", portrait: "Pixel 7", label: "Pixel 7 (Chromium)" },
].filter((p) => !process.env.WEBMOBILE_ONLY || process.env.WEBMOBILE_ONLY.split(",").includes(p.name));

// launched as a server so its process (and the processes under it) can be measured
async function launch(engine) {
  const opts = engine === "webkit"
    ? { headless: true, executablePath: process.env.WEBKIT_PATH || path.join(os.homedir(), ".cache/webkit-libs/webkit-2359/pw_run.sh") }
    : { headless: true, executablePath: process.env.CHROMIUM_PATH || cachedHeadlessShell(),
        args: ["--use-angle=vulkan", "--enable-features=Vulkan", "--ignore-gpu-blocklist"] };
  const server = await pw[engine].launchServer(opts);
  const browser = await pw[engine].connect(server.wsEndpoint());
  return { browser, server };
}

async function session(prof, url) {
  const { browser, server } = await launch(prof.engine);
  try { return await run(prof, url, browser, server); }
  finally { await server.close().catch(() => {}); }
}

async function run(prof, url, browser, server) {
  const dev = pw.devices[prof.device];
  const summary = { profile: prof.label, device: prof.device, viewport: dev.viewport, dpr: dev.deviceScaleFactor, browser: browser.version() };
  let peakRss = 0, peakRssAt = "", rssByName = {};
  const browserPid = server.process().pid;
  let phase = "loading";
  const sampler = setInterval(() => {
    if (!browserPid) return;
    const r = treeRss(browserPid);
    if (r.total > peakRss) { peakRss = r.total; peakRssAt = phase; rssByName = r.byName; }
  }, 500);
  const context = await browser.newContext({ ...dev });
  await context.addInitScript(instrument);
  const page = await context.newPage();
  const log = [];
  const errors = [];
  page.on("console", (m) => log.push(m.text()));
  page.on("pageerror", (e) => { errors.push(String(e)); log.push("[pageerror] " + e); });
  page.on("crash", () => { errors.push("the tab crashed"); log.push("[crash]"); });
  const shot = (name) => page.screenshot({ path: path.join(out, `${prof.name}_${name}.png`) });
  const vw = dev.viewport.width, vh = dev.viewport.height;
  // where an element is, in CSS pixels, and how tall the game draws it (and how wide, on builds that say)
  const find = async (name) => {
    const from = log.length;
    await page.evaluate((n) => window.unityInstance.SendMessage("Game", "LogScreenPoint", n), name);
    for (let i = 0; i < 30; i++) {
      await sleep(100);
      const line = log.slice(from).find((l) => l.startsWith(`[Probe] ${name} `));
      if (!line) continue;
      const v = line.slice(`[Probe] ${name} `.length).split(" ").map(Number);
      if (v.length < 4 || v.some(isNaN)) return null;
      const [x, y, w, h, size, width] = v;
      return { x: x * vw / w, y: (h - y) * vh / h, size: (size ?? 0) * vh / h, width: width === undefined ? undefined : width * vw / w };
    }
    return null;
  };
  const tap = async (name) => {
    const p = await find(name);
    if (!p) { console.log(`[Mobile] ${prof.name}: ${name} not found`); return null; }
    await page.touchscreen.tap(p.x, p.y);
    return p;
  };
  const memory = async (when) => {
    const from = log.length;
    await page.evaluate(() => window.unityInstance.SendMessage("Game", "LogMemory", ""));
    await sleep(300);
    const unity = log.slice(from).find((l) => l.startsWith("[Probe] memory "));
    if (unity) console.log(`[Mobile] ${prof.name} ${unity.slice(8)}`);
    const m = await page.evaluate(() => {
      const a = window.__acMem;
      if (!a) return null;
      return { heap: a.heap(), gpu: a.gpu, peakGpu: a.peakGpu, tex: a.tex, rb: a.rb, buf: a.buf, canvas: a.canvasBytes(),
               canvasPx: a.canvas ? [a.canvas.width, a.canvas.height] : null, attrs: a.attrs, unknown: Object.keys(a.unknownFormats),
               js: performance.memory ? performance.memory.usedJSHeapSize : null };
    });
    if (m) console.log(`[Mobile] ${prof.name} memory ${when}: wasm heap ${MB(m.heap)} MB, WebGL ${MB(m.gpu + m.canvas)} MB ` +
                       `(textures ${MB(m.tex)}, render buffers ${MB(m.rb)}, vertex/index ${MB(m.buf)}, canvas ${MB(m.canvas)} at ` +
                       `${m.canvasPx?.join("x")}, peak counted ${MB(m.peakGpu + m.canvas)}), browser RSS peak so far ${MB(peakRss)} MB` +
                       (m.js ? `, JS heap ${MB(m.js)} MB` : ""));
    if (m) m.unity = unity;
    return m;
  };
  // the game saves when the tab is hidden: pretend it was, then read the save
  const forceSave = async () => {
    await page.evaluate(() => {
      Object.defineProperty(document, "visibilityState", { configurable: true, get: () => "hidden" });
      document.dispatchEvent(new Event("visibilitychange"));
    });
    await sleep(1500);
    await page.evaluate(() => { delete document.visibilityState; document.dispatchEvent(new Event("visibilitychange")); });
    return readSave(page);
  };
  const touchState = () => page.evaluate(() => ({ ...document.documentElement.dataset, ui: document.documentElement.classList.contains("touch-ui") }));
  const waitState = async (key, value, ms = 4000) => {
    for (const end = Date.now() + ms; Date.now() < end; await sleep(100)) if ((await touchState())[key] === value) return true;
    return false;
  };
  const button = async (id) => {
    const r = await page.evaluate((i) => {
      const b = document.getElementById(i);
      if (!b || getComputedStyle(b).display === "none" || !b.offsetParent && getComputedStyle(b).position !== "fixed") return null;
      const q = b.getBoundingClientRect();
      return q.width ? { x: q.x + q.width / 2, y: q.y + q.height / 2, w: q.width, h: q.height } : null;
    }, id);
    return r;
  };
  const press = async (id) => {
    const b = await button(id);
    if (!b) { console.log(`[Mobile] ${prof.name}: #${id} not shown`); return false; }
    await page.touchscreen.tap(b.x, b.y);
    return true;
  };
  const fps = async (seconds) => page.evaluate((s) => new Promise((resolve) => {
    let n = 0;
    const t0 = performance.now();
    const f = () => { n++; if (performance.now() - t0 < s * 1000) requestAnimationFrame(f); else resolve(n / ((performance.now() - t0) / 1000)); };
    requestAnimationFrame(f);
  }), seconds);
  const controls = () => page.evaluate(() => {
    const el = document.querySelector("#touch-controls");
    if (!el) return null;
    const cs = getComputedStyle(el);
    const buttons = [...el.querySelectorAll("button")].filter((b) => getComputedStyle(b).display !== "none" && b.offsetParent);
    return { shown: cs.display !== "none" && cs.visibility !== "hidden",
             buttons: buttons.map((b) => { const r = b.getBoundingClientRect(); return { id: b.id, x: r.x, y: r.y, w: r.width, h: r.height }; }) };
  });

  // the page's on-screen touch controls, used by touch alone
  const touchControls = async () => {
    phase = "touch controls";
    await waitState("touchScreen", "monitor");
    const st = await touchState();
    const c = await controls();
    summary.controlsInGame = c;
    await shot("03a_controls");
    const ids = c?.buttons?.map((b) => b.id).sort().join(" ");
    check(!!c?.shown && st.ui && ids === "tc-menu tc-mute tc-ship tc-view tc-zoom",
          `${prof.name}: at the monitor the touch controls are shown (${ids || "none"})`);
    const small = (c?.buttons ?? []).filter((b) => b.w < 44 || b.h < 44);
    const outside = (c?.buttons ?? []).filter((b) => b.x < 0 || b.y < 0 || b.x + b.w > vw || b.y + b.h > vh);
    const overlap = (c?.buttons ?? []).some((a, i, all) => all.some((b, j) => j > i && a.x < b.x + b.w && b.x < a.x + a.w && a.y < b.y + b.h && b.y < a.y + a.h));
    check(c?.buttons?.length && !small.length && !outside.length && !overlap,
          `${prof.name}: every touch button is at least 44 CSS px, on screen and apart (` +
          (c?.buttons ?? []).map((b) => `${b.id.slice(3)} ${Math.round(b.w)}x${Math.round(b.h)}`).join(", ") + ")");
    const audio = await page.evaluate(() => window.agentClickerAudio ? window.agentClickerAudio() : "n/a");
    console.log(`[Mobile] ${prof.name}: audio contexts after the first taps: ${audio}`);
    summary.audio = audio;

    // zoom steps: SHIP CODE's column, the store, the whole monitor
    const zooms = [];
    for (let i = 0; i < 3; i++) {
      await press("tc-zoom");
      await sleep(900);
      zooms.push((await touchState()).touchZoom);
      if (i < 2) await shot(`03b_zoom${i + 1}`);
    }
    check(zooms.join(",") === "1,2,0", `${prof.name}: the Zoom button stepped the monitor through SHIP CODE's column, the store and back (${zooms.join(" → ")})`);

    // SHIP: one ship per tap, counted in the save
    const before = (await forceSave())?.clicks ?? -1;
    const taps = 10;
    for (let i = 0; i < taps; i++) { await press("tc-ship"); await sleep(150); }
    let after = (await forceSave())?.clicks ?? -1;
    check(after - before === taps, `${prof.name}: ${taps} taps on the SHIP button shipped ${after - before} times`);
    if (prof.engine === "chromium") {
      // two fingers come down on SHIP together (and the menu button isn't pressed by it): two ships
      const b = await button("tc-ship");
      const cdp = await page.context().newCDPSession(page);
      const pts = [{ x: b.x - 12, y: b.y, id: 1 }, { x: b.x + 12, y: b.y, id: 2 }];
      for (let i = 0; i < 3; i++) {
        await cdp.send("Input.dispatchTouchEvent", { type: "touchStart", touchPoints: pts });
        await sleep(80);
        await cdp.send("Input.dispatchTouchEvent", { type: "touchEnd", touchPoints: [] });
        await sleep(200);
      }
      const two = (await forceSave())?.clicks ?? -1;
      check(two - after === 6, `${prof.name}: two fingers on SHIP at once, three times, shipped ${two - after} times (multi-touch)`);
      after = two;
    }

    // office and back, sound off and on, the menu
    await press("tc-view");
    const office = await waitState("touchScreen", "office");
    await sleep(1500);
    await shot("03c_office_controls");
    await press("tc-view");
    const desk = await waitState("touchScreen", "monitor");
    check(office && desk, `${prof.name}: the Office button looked around the office and Desk sat back down`);
    await press("tc-mute");
    const muted = await waitState("touchMuted", "true");
    await press("tc-mute");
    const unmuted = await waitState("touchMuted", "false");
    check(muted && unmuted, `${prof.name}: the Sound button turned the sound off and on`);
    await sleep(1500);
    await press("tc-menu");
    const menu = await waitState("touchScreen", "menu");
    const cm = await controls();
    await shot("03d_menu");
    const resume = await find("RESUME");
    if (resume) await page.touchscreen.tap(resume.x, resume.y);
    const back = await waitState("touchScreen", "monitor");
    check(menu && !cm?.buttons?.some((b) => b.id !== "tc-mute") && back,
          `${prof.name}: the Menu button opened the pause menu (touch buttons there: ${cm?.buttons?.map((b) => b.id.slice(3)).join(", ") || "none"}) and RESUME closed it`);

    // a key or a mouse hides them; a finger brings them back
    await page.keyboard.press("Shift");
    await sleep(300);
    const afterKey = (await controls())?.shown;
    await page.touchscreen.tap(vw * 0.5, vh * 0.04);
    await sleep(300);
    const afterTouch = (await controls())?.shown;
    await sleep(1000);
    await page.mouse.move(vw * 0.3, vh * 0.3);
    await page.mouse.move(vw * 0.35, vh * 0.35, { steps: 4 });
    await sleep(300);
    const afterMouse = (await controls())?.shown;
    await page.touchscreen.tap(vw * 0.5, vh * 0.04);
    await sleep(300);
    check(afterKey === false && afterTouch === true && afterMouse === false && (await controls())?.shown === true,
          `${prof.name}: a key hid the touch controls (${!afterKey}), a touch brought them back (${afterTouch}), a mouse hid them (${!afterMouse})`);
    summary.shipTaps = after - before;
  };

  try {
    const t0 = Date.now();
    await page.goto(url, { waitUntil: "domcontentloaded" });
    const outcome = await page.waitForFunction(() => {
      if (document.documentElement.dataset.screen === "title") return "title";
      const failed = document.querySelector("#loading.failed");
      return failed ? "failed: " + failed.innerText.replace(/\s+/g, " ").trim() : false;
    }, null, { timeout: 300000, polling: 250 }).then((h) => h.jsonValue(), (e) => "no title: " + String(e).split("\n")[0]);
    summary.loadSeconds = (Date.now() - t0) / 1000;
    summary.reachedTitle = outcome === "title";
    phase = "title";
    await sleep(4000);
    await shot("01_title");
    summary.download = await page.evaluate(() => [...performance.getEntriesByType("navigation"), ...performance.getEntriesByType("resource")]
      .reduce((n, e) => n + (e.encodedBodySize || e.transferSize || 0), 0));
    const env = await page.evaluate(() => ({
      coarse: matchMedia("(pointer: coarse)").matches, fine: matchMedia("(any-pointer: fine)").matches,
      hover: matchMedia("(any-hover: hover)").matches, webgpu: !!navigator.gpu,
      webgl2: !!document.createElement("canvas").getContext("webgl2"),
    }));
    summary.env = env;
    check(summary.reachedTitle, `${prof.name}: reached the title screen (${outcome}) in ${summary.loadSeconds.toFixed(1)} s, ` +
          `${MB(summary.download)} MB downloaded (coarse pointer ${env.coarse}, fine pointer ${env.fine}, WebGL 2 ${env.webgl2}, WebGPU ${env.webgpu})`);
    if (!summary.reachedTitle) return summary;
    summary.memTitle = await memory("at the title");
    summary.fpsTitle = await fps(5);
    console.log(`[Mobile] ${prof.name}: ${summary.fpsTitle.toFixed(1)} fps at the title (page animation frames over 5 s)`);
    const c0 = await controls();
    summary.controlsAtTitle = c0;

    phase = "new game";
    await tap("NEW GAME");
    await sleep(1500);
    await tap("Skip");
    await sleep(3000);
    await shot("02_morning");
    phase = "login";
    const login = await tap("Login");
    await sleep(4000);
    const ship = await find("ship");
    check(!!login && !!ship, `${prof.name}: a tap on the monitor logged in (SHIP CODE ${ship ? "on screen" : "not found"})`);
    summary.shipButton = ship;
    summary.memLoggedIn = await memory("logged in");
    if (await page.evaluate(() => !!document.querySelector("#touch-controls"))) await touchControls();
    phase = "ship";
    let shipped = 0;
    if (ship) for (let i = 0; i < 12; i++) { await page.touchscreen.tap(ship.x, ship.y); shipped++; await sleep(120); }
    await sleep(2500);
    const mail = await find("Close");
    if (mail) { await page.touchscreen.tap(mail.x, mail.y); await sleep(800); }
    if (ship) for (let i = 0; i < 12; i++) { await page.touchscreen.tap(ship.x, ship.y); shipped++; await sleep(120); }
    await shot("03_shipping");
    const c1 = await controls();
    summary.controlsInGame = c1;
    summary.fpsGame = await fps(5);
    console.log(`[Mobile] ${prof.name}: ${summary.fpsGame.toFixed(1)} fps at the monitor (page animation frames over 5 s)`);
    phase = "hire";
    const hired = await tap("agent0");
    await sleep(1000);
    await shot("04_hired");
    check(!!hired, `${prof.name}: tapped the store's first row`);
    phase = "office";
    const view = await tap("View");
    await sleep(2500);
    await shot("05_office");
    summary.memOffice = await memory("in the office view");
    summary.fpsOffice = await fps(5);
    console.log(`[Mobile] ${prof.name}: ${summary.fpsOffice.toFixed(1)} fps in the office view (page animation frames over 5 s)`);
    const back = await tap("View");
    await sleep(2500);
    check(!!view && !!back, `${prof.name}: VIEW went to the office and back to the desk by tap`);
    phase = "pause";
    const menu = await tap("Menu");
    await sleep(1000);
    await shot("06_pause");
    const resume = await find("RESUME");
    if (resume) await page.touchscreen.tap(resume.x, resume.y);
    await sleep(1000);
    check(!!menu && !!resume && !(await find("RESUME")), `${prof.name}: ⚙ opened the pause menu and RESUME closed it, by tap`);
    const scroll = await page.evaluate(() => ({ x: scrollX, y: scrollY, zoom: visualViewport ? visualViewport.scale : 1,
                                                 overflow: document.documentElement.scrollHeight > innerHeight + 1 || document.documentElement.scrollWidth > innerWidth + 1 }));
    check(scroll.x === 0 && scroll.y === 0 && scroll.zoom === 1 && !scroll.overflow,
          `${prof.name}: the page never scrolled or zoomed (scroll ${scroll.x},${scroll.y}, zoom ${scroll.zoom}, overflow ${scroll.overflow})`);
    summary.memEnd = await memory("at the end");
    summary.errors = errors;
    check(errors.length === 0, `${prof.name}: no page errors or crashes (${errors.join("; ") || "none"})`);
    summary.taps = shipped;
    summary.log = log;
  } finally {
    clearInterval(sampler);
    summary.peakRss = peakRss;
    summary.peakRssAt = peakRssAt;
    summary.rssByName = Object.fromEntries(Object.entries(rssByName).map(([k, v]) => [k, Number(MB(v))]));
    writeFileSync(path.join(out, `${prof.name}_console.log`), log.join("\n").slice(-300000) + "\n");
    await context.close();
  }

  // held upright: a phone or tablet is asked to turn sideways
  const pdev = pw.devices[prof.portrait];
  const pctx = await browser.newContext({ ...pdev });
  const ppage = await pctx.newPage();
  await ppage.goto(url, { waitUntil: "domcontentloaded" });
  await sleep(1500);
  summary.rotatePrompt = await ppage.evaluate(() => getComputedStyle(document.querySelector("#rotate")).display !== "none");
  await ppage.screenshot({ path: path.join(out, `${prof.name}_portrait.png`) });
  check(summary.rotatePrompt, `${prof.name}: held upright, the page asks to turn the device sideways`);
  await pctx.close();
  console.log(`[Mobile] ${prof.name}: browser processes peaked at ${MB(peakRss)} MB resident (during ${peakRssAt}: ` +
              Object.entries(summary.rssByName).map(([k, v]) => `${k} ${v}`).join(", ") + ")");
  return summary;
}

async function main() {
  if (!existsSync(path.join(site, "index.html"))) throw new Error("no site at " + site);
  const port = await freePort();
  const server = spawn("python3", ["-m", "http.server", String(port), "--bind", "127.0.0.1", "-d", site], { stdio: "ignore" });
  console.log(`[Mobile] serving ${site} on port ${port} (pid ${server.pid}); load average ${os.loadavg().map((l) => l.toFixed(1)).join(" ")}`);
  const all = [];
  try {
    await sleep(500);
    for (const prof of profiles) {
      try { all.push(await session(prof, `http://127.0.0.1:${port}/`)); }
      catch (e) { check(false, `${prof.name}: ${String(e?.stack || e).split("\n").slice(0, 3).join(" ")}`); }
    }
  } finally {
    server.kill();
  }
  writeFileSync(path.join(out, "summary.json"), JSON.stringify(all.map(({ log, ...s }) => s), null, 1));
  const failed = results.filter((r) => !r.ok).length;
  console.log(`[Mobile] ${results.length - failed}/${results.length} checks passed (summary: ${path.join(out, "summary.json")})`);
  process.exitCode = failed ? 1 : 0;
}

main().catch((e) => { console.error(e); process.exitCode = 2; });
