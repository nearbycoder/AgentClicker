// Loads the hosted browser build and checks that it reaches the title screen cleanly. Exit 0 only if it does:
// every file downloads, the game signals its title screen, the tab is titled "Agent Clicker", and there are no page
// errors or console errors, and the phone-and-tablet touch controls stay hidden. Prints the download size and load time.
//
//   node Tools/check-pages.mjs <url> [chromium|firefox] [out dir]
//   node Tools/check-pages.mjs https://nearbycoder.github.io/AgentClicker/ firefox
//
// The page sets <html data-screen="title"> when the game reports its title screen (Platform.ShowFullscreenButton).
// Browsers: Chromium is the newest cached Playwright headless shell (or CHROMIUM_PATH), on the GPU through ANGLE/Vulkan;
// Firefox is the system Firefox (FIREFOX_PATH, default /usr/bin/firefox) over WebDriver BiDi. playwright-core comes from
// PLAYWRIGHT_CORE, the repo's node_modules, or ~/Sites/blog/node_modules. Writes console.log and title.png to the out
// dir (default Logs/check-pages-<browser>).
import { createRequire } from "node:module";
import { existsSync, mkdirSync, readdirSync, writeFileSync } from "node:fs";
import os from "node:os";
import path from "node:path";

const require = createRequire(import.meta.url);
const root = path.resolve(path.dirname(new URL(import.meta.url).pathname), "..");
const [url, engine = "chromium", outArg] = process.argv.slice(2);
if (!url || !["chromium", "firefox"].includes(engine)) {
  console.error("usage: node Tools/check-pages.mjs <url> [chromium|firefox] [out dir]");
  process.exit(2);
}
const out = path.resolve(outArg || path.join(root, "Logs", "check-pages-" + engine));
mkdirSync(out, { recursive: true });
if (engine === "firefox") { // its temporary profile goes here instead of the shared /tmp
  process.env.TMPDIR = path.join(root, "Logs", "tmp");
  mkdirSync(process.env.TMPDIR, { recursive: true });
}

function playwright() {
  for (const p of [process.env.PLAYWRIGHT_CORE, "playwright-core", path.join(os.homedir(), "Sites/blog/node_modules/playwright-core")]) {
    if (!p) continue;
    try { return require(p); } catch (e) { /* next */ }
  }
  throw new Error("playwright-core not found: set PLAYWRIGHT_CORE=/path/to/node_modules/playwright-core");
}

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

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const mb = (b) => (b / 1048576).toFixed(1) + " MB";

async function main() {
  const pw = playwright();
  const launch = { headless: true };
  if (engine === "chromium") {
    launch.executablePath = process.env.CHROMIUM_PATH || cachedHeadlessShell();
    launch.args = ["--use-angle=vulkan", "--enable-features=Vulkan", "--ignore-gpu-blocklist"];
  } else {
    launch.channel = "moz-firefox";
    launch.executablePath = process.env.FIREFOX_PATH || "/usr/bin/firefox";
  }

  const problems = [];
  const log = [];
  const browser = await pw[engine].launch(launch);
  try {
    const context = await browser.newContext({ viewport: { width: 1600, height: 900 } });
    const page = await context.newPage();
    page.on("console", (m) => {
      log.push(`[${m.type()}] ${m.text()}`);
      if (m.type() === "error") problems.push("console error: " + m.text());
    });
    page.on("pageerror", (e) => { log.push("[pageerror] " + e); problems.push("page error: " + e); });
    page.on("requestfailed", (r) => problems.push(`request failed: ${r.url()} (${r.failure()?.errorText})`));
    page.on("response", (r) => { if (r.status() >= 400) problems.push(`HTTP ${r.status()}: ${r.url()}`); });

    const t0 = Date.now();
    const response = await page.goto(url, { waitUntil: "domcontentloaded", timeout: 60000 });
    if (!response || !response.ok()) problems.push(`the page itself: HTTP ${response?.status()}`);
    // the title screen, or the page's own failure panel, whichever comes first
    const outcome = !response?.ok() ? "the page didn't load" : await page.waitForFunction(() => {
      if (document.documentElement.dataset.screen === "title") return "title";
      const failed = document.querySelector("#loading.failed");
      return failed ? "failed: " + failed.innerText.replace(/\s+/g, " ").trim() : false;
    }, null, { timeout: 300000, polling: 250 }).then((h) => h.jsonValue(), () => "timed out after 300 s");
    const seconds = (Date.now() - t0) / 1000;
    await sleep(3000); // a few frames of the title screen, for late errors and the screenshot
    await page.screenshot({ path: path.join(out, "title.png") });
    const title = await page.title();
    // what came over the network, from the page's own Resource Timing (the same in both browsers)
    const { bytes, files } = await page.evaluate(() => {
      const entries = [...performance.getEntriesByType("navigation"), ...performance.getEntriesByType("resource")]
        .filter((e) => !e.name.startsWith("data:"));
      return { bytes: entries.reduce((n, e) => n + (e.encodedBodySize || 0), 0), files: entries.length };
    });
    const renderer = await page.evaluate(() => {
      const gl = document.createElement("canvas").getContext("webgl2");
      if (!gl) return "no WebGL 2";
      const ext = gl.getExtension("WEBGL_debug_renderer_info");
      return ext ? gl.getParameter(ext.UNMASKED_RENDERER_WEBGL) : gl.getParameter(gl.RENDERER);
    });

    // the on-screen touch controls are for phones and tablets: a desktop browser must not show them, before or after
    // the mouse moves and clicks
    const touchControls = () => page.evaluate(() => {
      const el = document.querySelector("#touch-controls");
      return el ? getComputedStyle(el).display : "absent";
    });
    const touchAtTitle = await touchControls();
    await page.mouse.move(800, 450);
    await page.mouse.move(820, 470, { steps: 4 });
    await page.mouse.click(1400, 80);
    await sleep(500);
    const touchAfterMouse = await touchControls();
    console.log(`[Pages] ${engine} ${browser.version()}, renderer: ${renderer}`);
    console.log(`[Pages] touch controls: ${touchAtTitle} at the title, ${touchAfterMouse} after the mouse moved and clicked`);
    if (touchAtTitle === "block" || touchAfterMouse === "block") problems.push("the touch controls showed in a desktop browser");
    console.log(`[Pages] ${url}: ${outcome} in ${seconds.toFixed(1)} s, ${files} files, ${mb(bytes)} downloaded, tab title "${title}"`);
    if (outcome !== "title") problems.push("did not reach the title screen: " + outcome);
    if (title !== "Agent Clicker") problems.push(`tab title "${title}"`);
  } finally {
    writeFileSync(path.join(out, "console.log"), log.join("\n").slice(-200000) + "\n");
    await browser.close();
  }
  for (const p of problems) console.log("[Pages] FAIL " + p);
  console.log(`[Pages] ${problems.length ? "FAIL" : "PASS"} (log: ${path.join(out, "console.log")})`);
  process.exitCode = problems.length ? 1 : 0;
}

main().catch((e) => { console.error("[Pages] FAIL " + (e?.stack || e)); process.exitCode = 1; });
