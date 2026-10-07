// Touch play in the browser build (Builds/WebGL): headless Chromium with touch emulation, no mouse or keyboard input.
// For a tablet (1180x820 at 2x) and a phone held sideways (844x390 at 3x) it taps through a new game, logs in by tapping
// the monitor, ships code (one click per tap), hires, reads the save, looks around the office with one finger, pinches
// to zoom and sit back down, and opens and closes the pause menu. Then it checks that a phone held upright is asked to
// turn sideways.
//
//   PLAYWRIGHT_CORE=/path/to/node_modules/playwright-core node Tools/webtouch.mjs [out dir]
//
// CHROMIUM_PATH may point at a cached headless shell of another revision (as for Tools/webtest.mjs). Elements are found
// with the game's LogScreenPoint test hook, so the taps work at any window size. Drags and pinches go through the
// DevTools touch events, which is Chromium only.
import { createRequire } from "node:module";
import { spawn } from "node:child_process";
import { mkdirSync, writeFileSync } from "node:fs";
import net from "node:net";
import path from "node:path";

const require = createRequire(import.meta.url);
const root = path.resolve(path.dirname(new URL(import.meta.url).pathname), "..");
const out = path.resolve(process.argv[2] || path.join(root, "Logs", "web-touch"));
mkdirSync(out, { recursive: true });
const pw = require(process.env.PLAYWRIGHT_CORE || "playwright-core");

const results = [];
function check(ok, what) {
  results.push({ ok, what });
  console.log(`[Touch] ${ok ? "PASS" : "FAIL"} ${what}`);
}
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

function freePort() {
  return new Promise((resolve) => {
    const s = net.createServer().listen(0, "127.0.0.1", () => { const p = s.address().port; s.close(() => resolve(p)); });
  });
}

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

const WORKING = 1; // GamePhase.Working in the save

const devices = [
  { name: "tablet", viewport: { width: 1180, height: 820 }, deviceScaleFactor: 2 },
  { name: "phone", viewport: { width: 844, height: 390 }, deviceScaleFactor: 3 },
];

async function play(browser, url, dev) {
  const context = await browser.newContext({ viewport: dev.viewport, deviceScaleFactor: dev.deviceScaleFactor, hasTouch: true, isMobile: true });
  const page = await context.newPage();
  const cdp = await context.newCDPSession(page);
  const log = [];
  const errors = [];
  page.on("console", (m) => log.push(m.text()));
  page.on("pageerror", (e) => { errors.push(String(e)); log.push("[pageerror] " + e); });
  const shot = (name) => page.screenshot({ path: path.join(out, `${dev.name}_${name}.png`) });
  const waitLog = async (text, ms, from = 0) => {
    for (const end = Date.now() + ms; Date.now() < end; await sleep(200))
      if (log.slice(from).some((l) => l.includes(text))) return true;
    return false;
  };
  // where an element is, in CSS pixels
  const find = async (name) => {
    const from = log.length;
    await page.evaluate((n) => window.unityInstance.SendMessage("Game", "LogScreenPoint", n), name);
    for (let i = 0; i < 25; i++) {
      await sleep(100);
      const line = log.slice(from).find((l) => l.startsWith(`[Probe] ${name} `));
      if (!line) continue;
      const v = line.slice(`[Probe] ${name} `.length).split(" ").map(Number);
      if (v.length < 4 || v.some(isNaN)) return null;
      const [x, y, w, h, size] = v;
      // size: how big the game draws it (a text's font size), in CSS pixels
      return { x: x * dev.viewport.width / w, y: (h - y) * dev.viewport.height / h, size: (size ?? 0) * dev.viewport.height / h };
    }
    return null;
  };
  const tap = async (name) => {
    const p = await find(name);
    if (!p) { console.log(`[Touch] ${dev.name}: ${name} not found`); return false; }
    await page.touchscreen.tap(p.x, p.y);
    return true;
  };
  // fingers move together from a to b (CSS pixels) in steps, through DevTools touch events
  const gesture = async (from, to, steps = 12) => {
    const pts = (k) => from.map((f, i) => ({ x: f.x + (to[i].x - f.x) * k, y: f.y + (to[i].y - f.y) * k, id: i + 1 }));
    await cdp.send("Input.dispatchTouchEvent", { type: "touchStart", touchPoints: pts(0) });
    for (let s = 1; s <= steps; s++) {
      await sleep(30);
      await cdp.send("Input.dispatchTouchEvent", { type: "touchMove", touchPoints: pts(s / steps) });
    }
    await sleep(30);
    await cdp.send("Input.dispatchTouchEvent", { type: "touchEnd", touchPoints: [] });
    await sleep(150);
  };
  // a finger held for holdMs: real taps last about 50-150 ms, Playwright's tap() is instantaneous
  const fingerTap = async (p, holdMs) => {
    await cdp.send("Input.dispatchTouchEvent", { type: "touchStart", touchPoints: [{ x: p.x, y: p.y, id: 1 }] });
    if (holdMs > 0) await sleep(holdMs);
    await cdp.send("Input.dispatchTouchEvent", { type: "touchEnd", touchPoints: [] });
  };
  const forceSave = async () => {
    await page.evaluate(() => {
      Object.defineProperty(document, "visibilityState", { configurable: true, get: () => "hidden" });
      document.dispatchEvent(new Event("visibilitychange"));
    });
    await sleep(1500);
    await page.evaluate(() => { delete document.visibilityState; });
    return readSave(page);
  };

  try {
    await page.goto(url);
    await page.waitForFunction(() => document.querySelector("#loading")?.style.display === "none", null, { timeout: 240000 });
    await sleep(4000);
    const fullscreenButton = () => page.evaluate(() => getComputedStyle(document.querySelector("#fullscreen")).display);
    const page0 = await page.evaluate(() => ({
      rotate: getComputedStyle(document.querySelector("#rotate")).display,
      oldCard: !!document.querySelector("#touch"),
      touchAction: getComputedStyle(document.querySelector("#unity-canvas")).touchAction,
      fullscreen: document.fullscreenEnabled || document.webkitFullscreenEnabled,
    }));
    const fsTitle = await fullscreenButton();
    await shot("01_title");
    check(page0.rotate === "none" && !page0.oldCard && page0.touchAction === "none",
          `${dev.name}: the game opens straight to the title, no mouse-and-keyboard card (canvas touch-action ${page0.touchAction})`);

    await tap("NEW GAME");
    await sleep(1500);
    await tap("Skip");
    await sleep(2500);
    await shot("02_morning");
    const loginFrom = log.length;
    await tap("Login");
    await sleep(3500);
    const loggedIn = await forceSave();
    const ship = await find("ship");
    check(!!loggedIn && loggedIn.phase === WORKING && !log.slice(loginFrom).some((l) => l.includes("[Autopilot] log in")) && !!ship,
          `${dev.name}: tapping the monitor logged in (phase ${loggedIn?.phase}, touch noticed: ${log.some((l) => l.includes("[Touch] on"))})`);
    const fsGame = await fullscreenButton();
    check(fsTitle === (page0.fullscreen ? "block" : "none") && fsGame === "none",
          `${dev.name}: the page's Fullscreen button shows on the title screen only (title ${fsTitle}, in the game ${fsGame}, ` +
          `fullscreen API ${page0.fullscreen ? "available" : "missing"})`);

    // on day 1 the CEO's email opens after the 10th line of code, but only once the player pauses, so a burst of taps
    // ships code instead of closing the email unread. Playwright's taps take no time at all, so under heavy load one
    // can start and end inside a single frame; the count of those is for information. Real taps last 50 ms or more.
    const quick = 24;
    for (let i = 0; i < quick; i++) { await fingerTap(ship, 0); await sleep(90); }
    const afterQuick = (await forceSave())?.clicks ?? -1;
    console.log(`[Touch] ${dev.name}: ${quick} instantaneous taps shipped ${afterQuick} times (information only)`);
    let mail = null;
    for (let i = 0; i < 12 && !mail; i++) { await sleep(500); mail = await find("Modal"); }
    await shot("03a_mail_after_pause");
    check(!!mail, `${dev.name}: the CEO's email opened once the tapping paused (${mail ? "open" : "not open"})`);
    await tap("Close");
    await sleep(600);
    const taps = 24;
    for (let i = 0; i < taps; i++) { await fingerTap(ship, 50); await sleep(60); }
    await sleep(500);
    await shot("03_shipped");
    await tap("agent0");
    await sleep(800);
    await shot("04_hired_info");
    const saved = await forceSave();
    check(!!saved && saved.clicks - afterQuick === taps && saved.agentCounts[0] >= 1,
          `${dev.name}: ${taps} taps (50 ms each) on SHIP CODE shipped ${saved?.clicks - afterQuick} times after closing the email; a tap hired (Autocomplete x${saved?.agentCounts?.[0]})`);

    // the office: drag the room, then pinch
    await tap("View");
    await sleep(2000);
    await shot("05_office");
    const w = dev.viewport.width, h = dev.viewport.height;
    let from = log.length;
    await gesture([{ x: w * 0.1, y: h * 0.5 }], [{ x: w * 0.1 + h * 0.3, y: h * 0.5 }]);
    const looked = log.slice(from).find((l) => l.includes("[Touch] looked around"));
    check(!!looked, `${dev.name}: one finger dragged across the room looked around (${looked ?? "no log"})`);
    from = log.length;
    const c = { x: w * 0.5, y: h * 0.5 }, d = h * 0.05;
    for (let i = 0; i < 6 && !log.slice(from).some((l) => l.includes("sitting down")); i++)
      await gesture([{ x: c.x - d, y: c.y }, { x: c.x + d, y: c.y }], [{ x: c.x - 4 * d, y: c.y }, { x: c.x + 4 * d, y: c.y }]);
    const zooms = log.slice(from).filter((l) => l.includes("[Touch] pinch")).length;
    await sleep(1500);
    check(log.slice(from).some((l) => l.includes("sitting down")), `${dev.name}: pinching in zoomed (${zooms} pinches) and sat back down at the computer`);
    await shot("06_back_at_desk");

    // the monitor: two fingers spread on the store zoom into the screen, where the text is small on a phone, and lifting
    // them buys nothing; a tip told the player about it
    await sleep(1500);
    const sub0 = await find("agent0sub"), row = await find("agent0");
    const owned = (await forceSave())?.agentCounts?.reduce((a, b) => a + b, 0);
    from = log.length;
    await gesture([{ x: row.x, y: row.y - d * 0.5 }, { x: row.x, y: row.y + d * 0.5 }],
                  [{ x: row.x, y: row.y - d * 3 }, { x: row.x, y: row.y + d * 3 }], 16);
    await sleep(1500);
    const sub1 = await find("agent0sub");
    await shot("06b_monitor_zoomed");
    const zoomed = log.slice(from).find((l) => l.includes("[Touch] monitor zoom")) ?? "no zoom";
    const ownedAfter = (await forceSave())?.agentCounts?.reduce((a, b) => a + b, 0);
    check(!!sub0 && !!sub1 && sub1.size >= Math.max(11, sub0.size * 2) && owned === ownedAfter,
          `${dev.name}: spreading two fingers on the store zoomed the monitor (${zoomed.replace("[Touch] ", "").trim()}): a 14 pt store ` +
          `line went from ${sub0?.size.toFixed(1)} to ${sub1?.size.toFixed(1)} CSS px, and nothing was bought (${owned} → ${ownedAfter} agents)`);
    check(log.filter((l) => l.includes("[Touch] zoom tip shown")).length === 1, `${dev.name}: a tip told the player about zooming, once`);
    from = log.length;
    for (let i = 0; i < 3 && !log.slice(from).some((l) => /monitor zoom: x[\d.]+ → x1\.00/.test(l)); i++)
      await gesture([{ x: c.x, y: c.y - 3 * d }, { x: c.x, y: c.y + 3 * d }], [{ x: c.x, y: c.y - 0.5 * d }, { x: c.x, y: c.y + 0.5 * d }], 12);
    await sleep(1200);
    const sub2 = await find("agent0sub");
    check(!!sub2 && Math.abs(sub2.size - sub0.size) < 0.5,
          `${dev.name}: pinching in went back to the whole monitor (${sub2?.size.toFixed(1)} CSS px)`);

    await tap("Menu");
    await sleep(800);
    await shot("07_pause");
    const paused = await find("RESUME");
    // one tap on FULLSCREEN makes the page fullscreen (the game asks while the browser still counts the tap)
    const fsItem = page0.fullscreen ? await find("FULLSCREEN") : null;
    if (fsItem) { await tap("FULLSCREEN"); await sleep(800); }
    const full = await page.evaluate(() => !!(document.fullscreenElement || document.webkitFullscreenElement));
    check(!page0.fullscreen || (!!fsItem && full),
          `${dev.name}: one tap on the pause menu's FULLSCREEN made the page fullscreen (${fsItem ? "button shown" : "no button"}, fullscreen ${full})`);
    await tap("RESUME");
    await sleep(800);
    check(!!paused && !(await find("RESUME")), `${dev.name}: ⚙ opened the pause menu and RESUME closed it`);
    check(errors.length === 0, `${dev.name}: no page errors (${errors.length})`);
  } finally {
    writeFileSync(path.join(out, `${dev.name}_console.log`), log.join("\n").slice(-300000));
    await context.close();
  }
}

async function portrait(browser, url) {
  const context = await browser.newContext({ viewport: { width: 390, height: 844 }, deviceScaleFactor: 3, hasTouch: true, isMobile: true });
  const page = await context.newPage();
  await page.goto(url);
  await sleep(2000);
  const shown = await page.evaluate(() => getComputedStyle(document.querySelector("#rotate")).display);
  await page.screenshot({ path: path.join(out, "phone_portrait.png") });
  await page.setViewportSize({ width: 844, height: 390 });
  await sleep(500);
  const turned = await page.evaluate(() => getComputedStyle(document.querySelector("#rotate")).display);
  check(shown === "flex" && turned === "none", `a phone held upright is asked to turn sideways (${shown}), and the note goes once it's turned (${turned})`);
  await context.close();
}

async function main() {
  const port = await freePort();
  const server = spawn("python3", ["-m", "http.server", String(port), "--bind", "127.0.0.1", "-d", path.join(root, "Builds", "WebGL")],
                       { stdio: "ignore" });
  const launch = { headless: true, args: ["--use-angle=vulkan", "--enable-features=Vulkan", "--ignore-gpu-blocklist"] };
  if (process.env.CHROMIUM_PATH) launch.executablePath = process.env.CHROMIUM_PATH;
  let browser;
  try {
    browser = await pw.chromium.launch(launch);
    const url = `http://127.0.0.1:${port}/`;
    for (const dev of devices) await play(browser, url, dev);
    await portrait(browser, url);
  } finally {
    if (browser) await browser.close();
    server.kill();
  }
  const failed = results.filter((r) => !r.ok).length;
  console.log(`[Touch] ${results.length - failed}/${results.length} checks passed`);
  process.exitCode = failed ? 1 : 0;
}

main().catch((e) => { console.error(e); process.exitCode = 2; });
