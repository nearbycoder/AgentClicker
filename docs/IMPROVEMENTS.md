# Agent Clicker: improvement plan (round 2)

Written on 2026-10-06 on the `improvements` branch, after v0.1.0 (published 2026-10-04). This document
ranks what would most raise the game's quality for a real player and proposes the scope for the next
round. Nothing here is implemented yet.

## Baseline (what was run, and what it showed)

| Check | Result |
|---|---|
| `Tools/unity.sh tests` (EditMode) | **96/96 passed.** Balance bot: first Factory in **2h 09m** (day 27), inside the 1.5–5 h band. Career check: Marketing 59m, Sales 47m, Legal 47m. |
| `Tools/unity.sh build` (Linux, release) | **Succeeded**, 109 MB, 0 errors. |
| `Tools/tour.sh` (built player, `-force-wayland`) | **42 screenshots, no exceptions** in the player log. Every phase renders: title, settings, intro, login, day 1, calls, mid/late game, outage, review, night, Factory, ending, epilogue, endless, reorg, Board Room, trophies. |
| WebGL feasibility build (throwaway copy of the project in `~/.cache`, repo untouched) | **Builds with no code changes**: 7 min, 0 errors, **15 MB** download (Brotli). In headless Chrome on this iGPU it reaches the title screen and the day 1 office at **60 fps** (vsync-capped). Two problems: post-processing is off in the browser, and **the save did not survive a page reload**. Details below. |
| `Tools/benchmark.sh` | Not re-run. The machine is shared (load average 25–35 during this session), so frame times wouldn't be comparable with the README's 1.7 ms figure. |
| Windows build | Not possible here: Windows Build Support isn't installed. |
| macOS build | Not rebuilt; it has never been run on a Mac and can't be verified on this machine. |

The game is complete, stable and well tested at the model level. The gaps below are about how it
behaves for a real player (especially an *idle* player), where it can be played, and a few rough edges.

## Findings that drive the ranking

1. **The idle game stops when you go idle.** `GameModel.Tick` only produces while
   `Phase == Working`. At 17:00 the game asks you to clock out; if nobody answers, Sam works overtime
   until 23:30 and is then clocked out automatically into the performance review, a modal with a single
   GO HOME button (`ComputerUI.ShowReview`, `dismissable: false`). From then on **nothing is produced**
   until the player clicks GO HOME, then CLOCK IN on the night screen, then the monitor to log in. With
   the default 5-minute day that is about **9 minutes of production per check-in**: a player who leaves
   the game open and checks back every 30 minutes earns roughly 30% of what the game promises, every
   hour about 15%. The night shift is worth 60 s of production and the offline rate is 10% for at most
   one hour. The pitch is "your agents do the whole job"; the agents currently need Sam to press three
   buttons every nine minutes. The balance bot never sees this: it clocks out and back in instantly.
2. **Platform reach is Linux plus an untested Mac build.** The blog lists Windows · macOS · Linux.
   Windows needs a Unity module the owner has to install. A browser build is the cheapest way to reach
   everyone (see the WebGL notes below), and idle clickers are traditionally browser games.
3. **The Linux build hangs at startup under XWayland** unless it's started with `-force-wayland`.
   `Tools/play.sh` handles that for developers, but a player who unzips the release and double-clicks
   `AgentClicker.x86_64` on a Wayland desktop gets a hang: the published v0.1.0 zip contains only the
   player files, no launcher. Release zips are also packed by hand (the machine has no `zip`; `bsdtar`
   was used), so there's no reproducible packaging step.
4. **Saving is not crash-safe.** `SaveSystem.Save` writes `save.json.tmp`, then *deletes* `save.json`,
   then moves the temp file into place. A crash or power loss between the delete and the move leaves
   no save, and `Load` never looks at the `.tmp` file, so a 20-hour career would silently restart.
   There's also no backup copy if the JSON is ever corrupted.
5. **Screen traffic at busy moments.** In the tour's mid-game shot (day 4) a chapter banner, a model
   drop card and three toasts are on screen at once: the banner covers SHIP CODE, the drop card covers
   the store's buy-mode row, and the toast stack covers the store's info panel and the quota panel.
6. **Small onboarding and help gaps.** On day 1 the player dismisses three intro cards and a day card,
   clicks the monitor, and then the CEO's mandate email auto-opens over the desktop before the first
   SHIP CODE click, repeating what the intro cards just said. The in-game Controls tab omits the phone
   keys (`E`/`Q`, `1`/`2`/`3`) that the README lists.
7. **Pacing has two long stretches without a new agent type** in the bot's run: Architect (24m) →
   Product Manager (54m), and first Orchestrator (1h 10m) → Factory (2h 09m). Upgrades, gadgets and
   promotions land in between, so this is a watch item rather than a defect.
8. **The game reports the wrong version.** `bundleVersion` is `1.0`, so the title screen says "v1.0"
   while the release is v0.1.0.
9. **Docs drift.** GDD §9 says the bot takes "about 2.5 hours (around 31 in-game days)"; §16, the
   README and today's test run say 2h 09m / day 27. The GDD's career table (Marketing 26m) uses
   one-hour laps; the test's report uses shorter laps (Marketing 59m). Both are fine, but unlabeled.

## Ranked improvements

Impact is for a real player. Effort: S (≤ half a day), M (about a day), L (several days).

| # | Improvement | Impact | Effort | Risk |
|---|---|---|---|---|
| 1 | **Idle-friendly day cycle.** When nobody is at the keyboard, the day runs itself: clock out at 17:00, file the review, go home and clock back in the next morning, with a short toast so the player sees what happened. A Gameplay setting turns it off. | High: fixes the core idle promise | M | Low–medium: must not fight active players or story moments |
| 2 | **Browser (WebGL) build.** A `build-webgl` target, browser-safe code paths (music synthesis without a worker thread, saves flushed to IndexedDB, desktop-only settings hidden, no Quit button), and a page that can go on itch.io. | High: reach | M | Medium: browser performance on integrated GPUs, URP/WebGL2 limits |
| 3 | **Reproducible release packaging with a Linux launcher.** `Tools/package.sh` builds the zips (Linux, Mac, WebGL) with `bsdtar`, leaves out the `_BackUpThisFolder` debug folder (as the hand-made v0.1.0 zip did), and adds an `AgentClicker.sh` launcher that passes `-force-wayland` on Wayland. | Medium–high: first launch on Linux | S | Low |
| 4 | **Crash-safe saves.** Atomic replace, a `.bak` of the previous save, and a load fallback to `.bak`/`.tmp` when the main file is missing or unreadable. | Medium (rare, but catastrophic for an idle game) | S | Low |
| 5 | **Busy-screen polish.** Hold chapter banners while a modal, drop card or call is up; move the toast stack off the store and quota panels; complete the Controls tab; on day 1, let the player ship a few lines of code before the CEO's mail opens. | Medium | S–M | Low |
| 6 | **Windows build.** The build script already has a Windows target; it needs Windows Build Support (Mono) installed in Unity Hub. It could be built here but not tested on Windows. | High: reach | S once the module exists | Medium: untested platform. **Blocked on the owner.** |
| 7 | **Offline earnings review.** 10% for at most 1 hour is stingy for the genre once #1 lands; consider 2–4 hours, and show what the cap is earlier. A balance decision for the owner. | Medium | S | Medium: interacts with the Remote Work / Unlimited PTO perks |
| 8 | **Reduce motion option.** One toggle for the camera dollies, button punches and floating numbers (the showcase camera already has its own toggle). | Low–medium (accessibility) | S–M | Low |
| 9 | **Mid-game pacing goals.** Make the two long stretches visible goals (for example a "next unlock" line with progress), rather than rebalancing. | Medium | M | Medium: balance tests constrain it |
| 10 | **Gamepad / Steam Deck support.** The UI is built from code with uGUI buttons; it would need explicit navigation and focus visuals. | Medium | L | Medium |
| 11 | **Version and docs cleanup**: set `bundleVersion` to match the release (findings 8, 9). | Low | S | None |
| 12 | **macOS verification.** Needs someone with a Mac (Apple Silicon and Intel) to run the build; signing and notarisation need an Apple Developer account. **Blocked on the owner.** | Medium | S–M | — |
| 13 | **Localization.** All text is in code as C# strings; extracting it is a large job. | Medium | L | Medium |

## WebGL assessment

**Viable and worth it.** The unmodified project builds for WebGL (the module is installed), the
download is 15 MB against 109 MB for the Linux player, and the first minutes run at 60 fps in Chrome on
this machine's Radeon 8060S (ANGLE on Vulkan). An idle clicker is the kind of game people play in a
browser tab, so this is the cheapest platform reach available. It needs these fixes:

* **Saves.** After a new game, 20 s at the login screen (autosave runs every 15 s) and a reload, the
  title screen offered no CONTINUE, so the save did not persist. The likely cause is that
  `persistentDataPath` lives in IndexedDB and isn't flushed after `File.WriteAllText`; this still needs
  confirming. A browser player who loses their career on reload would never come back, so this blocks
  shipping.
* ~~**Post-processing is off.**~~ *Corrected in round 2:* the console warning about the FSR upscaling shader
  is generic. In this URP version an unsupported FSR shader only disables the FSR pass itself
  (`Fsr1UpscalePostProcessPass`), and a side-by-side of the title screen shows the same grading and vignette
  in the browser as on desktop. No fix was needed.
* **Music.** `Sfx` renders the lo-fi loop with `Task.Run`. WebGL has no worker threads, so the loop
  probably never finishes or blocks the main thread. Unverified: headless Chrome can't tell us what
  was audible. The fix is to render it in slices from a coroutine on WebGL.
* **Desktop-only settings and buttons**: display mode, resolution list, V-Sync, frame cap, QUIT and the
  F12 screenshot do nothing useful in a browser and should be hidden there.
* **Not tested**: Firefox and Safari, phones and tablets (the game has no touch support, so mobile would
  be marked unsupported), lower-end GPUs, and long sessions (memory growth).

Windows, by contrast, is a one-line build once the module is installed, but this machine can't verify
it. The browser build can be verified end to end here.

## Proposed scope for this round

Five items, in order. Windows (#6) and macOS (#12) wait for the owner.

### 1. Idle-friendly day cycle (#1)

* A pure C# policy (`Core/DayAutopilot.cs`) decides what to do from the phase, seconds since the last
  player input and whether a modal, call or story card is up. `GameManager` feeds it real input times
  (any key, click, wheel or mouse movement) and performs the action through the same methods the
  buttons call.
* Defaults: at 17:00 with no input for 30 s, clock out; review shown and idle for 15 s, go home; night
  screen idle for 8 s, clock in; login idle for 10 s, log in. Never during the intro, the ending, the
  reorg memo, a ringing or active call, the pause menu or settings. Any input cancels the countdown.
* Setting: Gameplay → "Run the work day when I'm away" (on by default). A toast tells the player what
  happened ("Your agents clocked you out and back in. Day 12 is underway.").

**Acceptance criteria**
* With the setting on and no input, a game left running for one hour produces in every day (no phase
  other than Working lasts longer than about 20 s).
* With the setting off, behaviour is identical to v0.1.0.
* An active player who keeps clicking never sees an automatic action.
* The balance tests still pass unchanged.

**Verification**: new EditMode tests for the policy (each phase, each blocker, input resets the
timer) and a simulated hour with no input that checks earnings in every day; a run of the built player
with `-daylength 30` left alone for a few minutes, checking the log and screenshots for automatic
clock-outs and clock-ins.

### 2. Crash-safe saves (#4)

* Save writes the temp file, then `File.Replace` (keeping `agentclicker_save.json.bak`), with a
  fallback to copy-then-delete where `Replace` isn't available (WebGL).
* Load tries the main file, then `.bak`, then `.tmp`, and logs which one it used.

**Acceptance criteria**: a missing main file with a valid `.bak` or `.tmp` loads; a corrupt main file
falls back to `.bak`; a normal save leaves the main file and one backup.
**Verification**: EditMode tests against a temporary save folder (the save folder becomes
overridable for tests).

### 3. Browser build (#2)

* `Tools/unity.sh build-webgl` → `Builds/WebGL`, Brotli with decompression fallback so it works on
  any static host.
* Browser-safe paths behind `#if UNITY_WEBGL`: music synthesised incrementally on the main thread
  instead of `Task.Run`; save writes followed by an IndexedDB flush; display mode, resolution, V-Sync,
  frame cap and Quit hidden; F12 screenshots disabled; a lighter default quality preset.
* Bilinear upscaling instead of FSR on WebGL, so post-processing runs in the browser.
* README: a "Play in the browser" section with honest caveats (tested browsers, performance).

**Acceptance criteria**: the build loads in a browser served from `localhost`, reaches the title
screen, starts a new game, logs in, clicks SHIP CODE and hires an agent; the save survives a page
reload (CONTINUE appears and restores the credits); the console has no exceptions and no "post
processing will not execute" warning; the music clip is created (logged), even though headless Chrome
can't confirm that it's audible.
**Verification**: serve `Builds/WebGL` with `python3 -m http.server` and drive it with Playwright and
headless Chrome (as in this phase's spike), capturing screenshots and console logs. Frame rate is
measured in that browser on this iGPU and reported as-is.

### 4. Release packaging with a Linux launcher (#3)

* `Tools/package.sh [version]` zips Linux, Mac and (if built) WebGL into `Builds/Release/` with
  `bsdtar`, excluding `*_BackUpThisFolder_ButDontShipItWithYourGame`.
* The Linux zip gets `AgentClicker.sh`, which adds `-force-wayland` when `WAYLAND_DISPLAY` is set and
  passes other arguments through. README "Play it" points to it.

**Acceptance criteria**: zips contain no backup folders; `AgentClicker.sh` from the unpacked zip starts
the game on this Wayland desktop without extra flags.
**Verification**: list the zip contents; unpack to a temp folder and run the launcher with a tour
argument (`-tour`) to confirm it reaches the title screen and writes screenshots.

### 5. Busy-screen polish (#5)

* Chapter banners wait until no modal, drop card or call is showing.
* The toast stack moves to the left of the store column so it no longer covers the store info panel.
* The Controls tab lists the phone keys.
* `bundleVersion` matches the release, so the title screen stops saying "v1.0".
* Day 1: the CEO's mandate email is delivered after the first 10 clicks of SHIP CODE (the intro cards
  already carry the mandate), so the player's first action is shipping code. Dana's follow-up, which
  arrives at 15 clicks today, moves back accordingly.

**Acceptance criteria**: in the tour's mid-game shot, banner, drop card and toasts don't overlap each
other or the SHIP CODE button; the Controls tab shows `E`/`Q` and `1`/`2`/`3`; on a fresh game the
first modal after login appears only after 10 clicks.
**Verification**: re-run `Tools/tour.sh` and compare the affected screenshots with the baseline; an
EditMode test for the mail trigger.

## Needs a decision from the owner

* **Windows**: install Windows Build Support (Mono) for 6000.6.2f1 in Unity Hub
  (`unityhub --headless install-modules --version 6000.6.2f1 -m windows-mono`) if a Windows zip is
  wanted. It would ship as untested, like the Mac build.
* **Where the browser build lives**: itch.io, GitHub Pages or the nearbycoder.com page. This round only
  produces the build; publishing is the owner's call.
* **Offline earnings (#7)**: keep 10% / 1 hour, or raise the cap once the day runs itself.
* **License**: still none, which matters more once the game is on a public web page.

## Round 2 results (2026-10-06)

All five scoped items landed on `improvements`, one commit each. Tests: **106/106** (96 at baseline). Screenshots
are in [`docs/media/improvements/`](media/improvements).

| Item | Commit | Verified by |
|---|---|---|
| 1. Idle-friendly day cycle | `0500405` | 5 EditMode tests (an idle hour produces in every day; input, blockers and WORK LATE hold it); a new autopilot segment in `Tools/tour.sh` logs PASS for clock-out, go home, clock-in and log-in in the built player |
| 2. Crash-safe saves | `6ac68df` | 4 EditMode tests against a temp folder (backup kept, corrupt and missing main file recover, delete removes every copy) |
| 3. Browser build | `7d72a4e` | `Tools/unity.sh build-webgl` (14 MB). Headless Chrome + Playwright on a local server: title without QUIT, new game, autopilot login, music ready after ~5–8 s, SHIP CODE, the save in IndexedDB before and after a reload (credits 61.46), CONTINUE restores it. 60 fps on this iGPU when the machine was quiet, 5–30 fps at load average 60–90 |
| 4. Packaging + Linux launcher | `bd80297` | `Tools/package.sh` zip listings (no do-not-ship folders, exec bits kept); the launcher from an unpacked zip chose the Wayland backend and ran the tour |
| 5. Busy-screen polish | `099f157` | New EditMode test for the day-1 mail rule; tour before/after shots; the browser run shows no inbox after login and the CEO's mail after the first clicks |

Changes from the plan:
* Day 1 mail: the CEO's email is still *delivered* first (tests and the trailer depend on that); what waits
  for 10 clicks is the inbox *auto-opening*. Dana's email didn't need to move.
* The chapter banner also moved to the upper part of the screen, so it no longer covers SHIP CODE even when it
  starts before a model drop appears (as in the tour's mid-game shot).
* A frame gap of over a minute (hidden browser tab, laptop sleep) is now credited like time away with the game
  closed, on every platform. Browsers stop running hidden tabs, so without this a browser idle game would lose
  that time.

Open:
* **One native crash.** A SIGSEGV on a Unity worker thread (no managed frames) ended one of five tour runs
  of the item-1 build while the machine was at load average 70–90. The other four runs were clean, and it
  hasn't been reproduced or explained. Worth running the tour a few more times on the development build
  (`Builds/LinuxDev`, with symbols) on a quiet machine.
* Not verified: Firefox and Safari, audible audio in the browser (headless), the macOS build (no Mac), Windows
  (module not installed), and save crash-safety in a real power cut (only simulated in tests).
* Still owner decisions: Windows Build Support, hosting the browser build, the offline-earnings cap, a license,
  and release versioning (the project now says 0.2.0; nothing is tagged or released).
