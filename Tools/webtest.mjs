// Drives the browser build (Builds/WebGL) in headless Chromium, Firefox or WebKit and checks the things a browser player
// depends on: it loads, a new game starts and logs in, SHIP CODE and hiring work, audio is actually produced, the
// save reaches IndexedDB when the tab is hidden, and CONTINUE restores it after a reload.
//
//   PLAYWRIGHT_CORE=/path/to/node_modules/playwright-core node Tools/webtest.mjs chromium|firefox|webkit [out dir]
//
// Firefox is the system Firefox (FIREFOX_PATH, default /usr/bin/firefox) driven over WebDriver BiDi, which
// playwright-core 1.63 supports with channel "moz-firefox"; no Playwright Firefox build is needed. Its temporary
// profile goes under Logs/tmp instead of the shared /tmp.
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
if (engine === "firefox") {
  process.env.TMPDIR = path.join(root, "Logs", "tmp");
  mkdirSync(process.env.TMPDIR, { recursive: true });
}
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
// Test-only: a page loaded after sessionStorage.acTimeShift is set sees the clock that many ms ahead, as if the tab had
// been closed that long (the game times offline earnings with Date.now). The page already open keeps the real clock.
const timeShift = () => {
  let shift = 0;
  try { shift = Number(sessionStorage.getItem("acTimeShift") || 0); } catch (e) { } // about:blank has no storage in Firefox
  if (shift) { const now = Date.now.bind(Date); Date.now = () => now() + shift; }
};

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
  if (engine === "firefox") {
    launch.channel = "moz-firefox";
    launch.executablePath = process.env.FIREFOX_PATH || "/usr/bin/firefox";
    launch.firefoxUserPrefs = { "media.autoplay.default": 0, "media.autoplay.blocking_policy": 0 };
  }
  const log = [];
  let browser;
  try {
    browser = await pw[engine].launch(launch);
    await sleep(500);
    const context = await browser.newContext({ viewport: { width: 1600, height: 900 } });
    await context.addInitScript(audioProbe);
    await context.addInitScript(timeShift);
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
    const titleScreen = await page.title();

    // home-screen play: the web app manifest parses and its icons load (Chromium can read the parsed manifest)
    if (engine === "chromium") {
      const cdp = await context.newCDPSession(page);
      const m = await cdp.send("Page.getAppManifest");
      const parsed = m.data ? JSON.parse(m.data) : null;
      const icons = [...(parsed?.icons ?? []).map((i) => i.src), "icon-180.png"];
      const loadedIcons = await page.evaluate((srcs) => Promise.all(srcs.map((src) => new Promise((resolve) => {
        const img = new Image();
        img.onload = () => resolve(`${src} ${img.naturalWidth}x${img.naturalHeight}`);
        img.onerror = () => resolve(null);
        img.src = src;
      }))), icons);
      const meta = await page.evaluate(() => ({
        ios: document.querySelector('meta[name="apple-mobile-web-app-capable"]')?.content,
        touchIcon: document.querySelector('link[rel="apple-touch-icon"]')?.getAttribute("href"),
      }));
      check(!!parsed && (m.errors ?? []).length === 0 && parsed.display === "fullscreen" && parsed.orientation === "landscape" &&
            loadedIcons.every(Boolean) && meta.ios === "yes" && meta.touchIcon === "icon-180.png",
            `the web app manifest parses (${(m.errors ?? []).length} errors, display ${parsed?.display}, ${parsed?.orientation}) and its icons ` +
            `load (${loadedIcons.join(", ")}); iOS home-screen tags present`);
    }

    // settings are kept in PlayerPrefs, in the same IndexedDB file system: switch numbers to scientific
    await page.mouse.click(250, 579); // SETTINGS
    await sleep(1000);
    await page.mouse.click(735, 182); // GAMEPLAY
    await sleep(800);
    await page.mouse.click(1220, 661); // Number format ▶
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
    const fps = await page.evaluate(() => new Promise((resolve) => {
      let n = 0; const t0 = performance.now();
      const step = () => { n++; if (performance.now() - t0 < 5000) requestAnimationFrame(step); else resolve(n / ((performance.now() - t0) / 1000)); };
      requestAnimationFrame(step);
    }));
    console.log(`[Web] frame rate in the monitor view: ${fps.toFixed(0)} fps (browser frames over 5 s)`);

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

    // the CEO's email opens by itself once the clicking pauses; read it and close it with Esc
    const modalOpen = async () => {
      const from = log.length;
      await page.evaluate(() => window.unityInstance.SendMessage("Game", "LogScreenPoint", "Modal"));
      for (let i = 0; i < 20; i++) {
        await sleep(100);
        const line = log.slice(from).find((l) => l.includes("[Probe] Modal"));
        if (line) return !line.includes("none");
      }
      return false;
    };
    let mailOpen = false;
    for (let i = 0; i < 12 && !mailOpen; i++) { await sleep(500); mailOpen = await modalOpen(); }
    await shot("04b_ceo_email");
    if (mailOpen) { await page.keyboard.press("Escape"); await sleep(600); }
    check(mailOpen && !(await modalOpen()), "the CEO's email opened when the clicking paused, and Esc closed it");

    // M mutes everything (music and ambience keep playing without clicks) and turns it back on
    const loudest = async (ms) => {
      let peak = 0;
      for (let t = 0; t < ms; t += 150) {
        const a = await page.evaluate(() => window.__audio ? window.__audio() : null);
        if (a) peak = Math.max(peak, a.peak);
        await sleep(150);
      }
      return peak;
    };
    const playing = await loudest(1500);
    await page.keyboard.press("m");
    await sleep(400);
    const muted = await loudest(2000);
    await page.keyboard.press("m");
    await sleep(400);
    const unmuted = await loudest(2000);
    console.log(`[Web] mute: peak ${playing.toFixed(4)} playing, ${muted.toFixed(5)} muted, ${unmuted.toFixed(4)} after M again`);
    check(playing > 0.001 && muted < 0.0005 && unmuted > 0.001 && log.some((l) => l.includes("[Audio] muted")),
          `M mutes all sound and M again brings it back (peak ${playing.toFixed(4)} → ${muted.toFixed(5)} → ${unmuted.toFixed(4)})`);

    // hire an Autocomplete (top row of the store)
    await page.mouse.click(1290, 235);
    await sleep(600);
    await shot("05_hired");

    // ---- the tab's title: the credits, and an alert in front while a model drop is up or the phone rings ----------
    const titleWhen = async (test, ms = 4000) => {
      let t = await page.title();
      for (let waited = 0; waited < ms && !test(t); waited += 250) { await sleep(250); t = await page.title(); }
      return t;
    };
    const event = (what) => page.evaluate((w) => window.unityInstance.SendMessage("Game", "ForceEvent", w), what);
    const plain = await titleWhen((t) => / credits · Agent Clicker$/.test(t));
    await event("drop");
    const dropTitle = await titleWhen((t) => t.startsWith("★ Model drop! · "));
    await event("claim");
    const afterDrop = await titleWhen((t) => !t.startsWith("★"));
    await event("ring");
    const ringTitle = await titleWhen((t) => t.startsWith("☎ Phone ringing · "));
    await event("decline");
    const afterRing = await titleWhen((t) => !t.startsWith("☎"));
    console.log(`[Web] tab titles: "${titleScreen}" · "${plain}" · "${dropTitle}" · "${afterDrop}" · "${ringTitle}" · "${afterRing}"`);
    check(titleScreen === "Agent Clicker" && /^[\d.]+\S* credits · Agent Clicker$/.test(plain) &&
          dropTitle.startsWith("★ Model drop! · ") && /^[\d.]+\S* credits · /.test(afterDrop) &&
          ringTitle.startsWith("☎ Phone ringing · ") && /^[\d.]+\S* credits · /.test(afterRing),
          `the tab's title shows the credits and puts a model drop and a ringing phone in front ("${plain}", "${dropTitle}", "${ringTitle}")`);

    // ---- a page behind another window saves power, and the WebGL id tables don't grow with every frame ----------
    // The game's own frame rate (the page's animation frames keep their pace when the game caps its own). Focus is
    // moved with the window's blur and focus events, which is what the engine listens to.
    const gameFps = async (secs) => {
      const from = log.length;
      await page.evaluate((s) => window.unityInstance.SendMessage("Game", "LogFrameRate", String(s)), secs);
      for (let i = 0; i < (secs + 10) * 4; i++) {
        await sleep(250);
        const line = log.slice(from).find((l) => l.includes("[Probe] fps"));
        if (line) return { fps: parseFloat(/fps ([\d.]+)/.exec(line)[1]), line: line.replace(/^.*\[Probe\] /, "") };
      }
      return { fps: -1, line: "no answer" };
    };
    const capOf = (r) => { const m = /cap (-?\d+)/.exec(r.line); return m ? parseInt(m[1], 10) : NaN; };
    const glStats = () => page.evaluate(() => window.agentClickerGL ? window.agentClickerGL() : null);
    const gl0 = await glStats(), glT0 = Date.now();
    // The game's own cap (15 behind another window, none in front) is the setting at work; the measured rate confirms it.
    // On an overloaded machine the page can render under 30 fps even in front (4 fps at load 27 in round 10), so the rates
    // can't show a drop: then it measures again, up to three times, and says so.
    let front, behind, back, rateOk = false, capsOk = false;
    for (let attempt = 1; attempt <= 3; attempt++) {
      front = await gameFps(3);
      await page.evaluate(() => window.dispatchEvent(new FocusEvent("blur")));
      await sleep(500);
      behind = await gameFps(3);
      await page.evaluate(() => window.dispatchEvent(new FocusEvent("focus")));
      await sleep(500);
      back = await gameFps(3);
      console.log(`[Web] game frame rate (try ${attempt}): in front ${front.line} · behind another window ${behind.line} · in front again ${back.line}`);
      capsOk = capOf(behind) === 15 && capOf(front) !== 15 && capOf(back) !== 15;
      rateOk = behind.fps > 0 && behind.fps <= 17 && back.fps >= Math.max(20, behind.fps * 1.5);
      if (rateOk || !capsOk) break;
      if (front.fps >= 30 && back.fps >= 30) break; // a fast page that didn't slow down: a real failure, no retry
      console.log(`[Web] the page renders under 30 fps in front (${front.fps}, ${back.fps}): the machine is busy, measuring again`);
    }
    check(capsOk && rateOk,
          `the game drops to about 15 fps behind another window and comes back (${front.fps} → ${behind.fps} → ${back.fps} fps, ` +
          `cap ${capOf(front)} → ${capOf(behind)} → ${capOf(back)})`);
    const gl1 = await glStats(), glSecs = (Date.now() - glT0) / 1000;
    console.log(`[Web] WebGL ids: ${JSON.stringify(gl0)} → ${JSON.stringify(gl1)}`);
    check(!!gl0 && !!gl1 && gl1.reuse && gl1.counter - gl0.counter < 3 * glSecs,
          `WebGL ids are reused: the shared id counter went ${gl0?.counter} → ${gl1?.counter} in ${glSecs.toFixed(0)} s ` +
          `(the engine's own allocator adds about one a frame), largest table ${gl0?.largest} → ${gl1?.largest}`);

    // ---- the long music piece takes over from the 25 s loop, and the music keeps playing ----------------------
    const songLine = await waitLog("[Sfx] the long piece is playing", 240000);
    const songPeak = songLine ? await loudest(2000) : 0;
    console.log("[Web] music: " + log.filter((l) => l.includes("[Sfx]")).map((l) => l.replace(/^.*\[Sfx\] /, "")).join(" · "));
    check(songLine && log.some((l) => l.includes("[Sfx] the long piece is playing (playing")) && songPeak > 0.001,
          `the long music piece took over from the loop and plays (peak ${songPeak.toFixed(4)} after the switch)`);

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
    // the reloaded page's clock runs two hours ahead: the game was "closed" for two hours
    await page.evaluate(() => sessionStorage.setItem("acTimeShift", String(2 * 3600 * 1000)));
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

    // ---- two hours closed: after logging in, the away card says what the agents earned ------------------------
    const closedLine = log.find((l) => l.includes("[Away] game closed for")) ?? "";
    const loginFrom = log.length;
    for (let i = 0; i < 100 && !log.slice(loginFrom).some((l) => l.includes("[Autopilot] log in")); i++) await sleep(250); // ~10 s idle
    await sleep(2500);
    const awayCard = await modalOpen();
    await shot("07b_offline_card");
    check(/game closed for 2h 0[01]m/.test(closedLine) && awayCard,
          `reopened two hours later: ${closedLine.replace(/^.*\[Away\] /, "") || "no offline credit"}, and the away card shows after logging in (${awayCard})`);
    if (awayCard) { await page.keyboard.press("Escape"); await sleep(600); }
    await page.evaluate(() => sessionStorage.removeItem("acTimeShift"));

    // ---- the pause menu's FULLSCREEN works with one click ----------------------------------------------------
    const find = async (name) => {
      const from = log.length;
      await page.evaluate((n) => window.unityInstance.SendMessage("Game", "LogScreenPoint", n), name);
      for (let i = 0; i < 25; i++) {
        await sleep(100);
        const line = log.slice(from).find((l) => l.includes(`[Probe] ${name} `));
        if (!line) continue;
        const v = line.slice(line.indexOf(`[Probe] ${name} `) + `[Probe] ${name} `.length).split(" ").map(Number);
        return v.length < 4 || v.some(isNaN) ? null : { x: v[0] * 1600 / v[2], y: (v[3] - v[1]) * 900 / v[3] };
      }
      return null;
    };
    const canFull = await page.evaluate(() => document.fullscreenEnabled || document.webkitFullscreenEnabled);
    await page.keyboard.press("Escape"); // pause
    await sleep(800);
    const fsButton = await find("FULLSCREEN");
    if (fsButton) { await page.mouse.click(fsButton.x, fsButton.y); await sleep(800); }
    const isFull = await page.evaluate(() => !!(document.fullscreenElement || document.webkitFullscreenElement));
    await shot("07c_pause_fullscreen");
    check(!canFull || (!!fsButton && isFull), `one click on the pause menu's FULLSCREEN makes the page fullscreen (${fsButton ? "button shown" : "no button"}, fullscreen ${isFull})`);
    if (isFull) { await page.evaluate(() => document.exitFullscreen()); await sleep(600); }
    await page.keyboard.press("Escape"); // resume
    await sleep(600);
    // ---- save files: download here, load into a fresh browser profile ---------------------------------------
    const openGameplaySettings = async (p) => {
      await p.mouse.click(250, 579); // SETTINGS
      await sleep(1000);
      await p.mouse.click(735, 182); // GAMEPLAY
      await sleep(800);
    };
    await page.reload();
    await loaded();
    await openGameplaySettings(page);
    await shot("08_settings_save_file");
    const [download] = await Promise.all([page.waitForEvent("download", { timeout: 15000 }), page.mouse.click(1010, 713)]);
    const file = path.join(out, "agentclicker_save.json");
    await download.saveAs(file);
    const downloaded = JSON.parse((await import("node:fs")).readFileSync(file, "utf8"));
    const stored = await readSave(page);
    check(download.suggestedFilename() === "agentclicker_save.json" && downloaded.clicks === stored.clicks && downloaded.day === stored.day,
          `DOWNLOAD gives the career as ${download.suggestedFilename()} (clicks ${downloaded.clicks}, day ${downloaded.day})`);

    const fresh = await browser.newContext({ viewport: { width: 1600, height: 900 } });
    const page2 = await fresh.newPage();
    const log2 = [];
    page2.on("console", (m) => log2.push(m.text()));
    page2.on("pageerror", (e) => pageErrors.push(String(e)));
    await page2.goto(url);
    await page2.waitForFunction(() => document.querySelector("#loading")?.style.display === "none", null, { timeout: 240000 });
    await sleep(4000);
    check(await readSave(page2) === null, "a fresh browser profile starts without a career");
    await openGameplaySettings(page2);
    // a zero-length click: it only reaches the button if no frame is long (the music is still being synthesised on the
    // page's only thread for the first half minute, in slices that must stay short)
    const pick = async (f) => {
      const [chooser] = await Promise.all([page2.waitForEvent("filechooser", { timeout: 15000 }), page2.mouse.click(1165, 713)]);
      await chooser.setFiles(f);
      await sleep(2000);
    };
    const junk = path.join(out, "not_a_save.json");
    writeFileSync(junk, JSON.stringify({ name: "some other game", level: 4 }));
    await pick(junk);
    await page2.screenshot({ path: path.join(out, "09_load_rejected.png") });
    check(log2.some((l) => l.includes("[SaveFile] rejected")) && await readSave(page2) === null, "a file that isn't a save is rejected and changes nothing");
    await page2.mouse.click(1110, 749); // BACK
    await sleep(800);
    await pick(file);
    await page2.screenshot({ path: path.join(out, "10_load_confirm.png") });
    await page2.mouse.click(925, 518); // LOAD
    await sleep(2500);
    await page2.screenshot({ path: path.join(out, "11_loaded_title.png") });
    await page2.mouse.click(250, 439); // CONTINUE
    await sleep(3000);
    await page2.screenshot({ path: path.join(out, "12_loaded_continued.png") });
    const imported = await readSave(page2);
    check(!!imported && imported.clicks === downloaded.clicks && imported.day === downloaded.day &&
          Math.abs(imported.credits - downloaded.credits) <= Math.max(1, downloaded.credits * 0.5) && imported.agentCounts[0] === downloaded.agentCounts[0],
          `LOAD FILE moves the career into the fresh profile (clicks ${imported?.clicks}, day ${imported?.day}, Autocomplete x${imported?.agentCounts?.[0]})`);
    await fresh.close();

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
