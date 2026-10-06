# Agent Clicker: improvement plan

Written on 2026-10-06, after v0.1.0 (published 2026-10-04). This document ranks what would most raise the
game's quality for a real player, then records each round's scope and results. Rounds 1 and 2 (branches
`improvements` and `improvements-2`) are merged into `main`; round 3 is on `improvements-3`.

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
| 9 | **Mid-game pacing goals.** *(Done in round 3 as the next goal card.)* Make the two long stretches visible goals (for example a "next unlock" line with progress), rather than rebalancing. | Medium | M | Medium: balance tests constrain it |
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
* ~~**Post-processing is off.**~~ *Corrected in round 1:* the console warning about the FSR upscaling shader
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

## Round 1 scope

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

## Round 1 results (2026-10-06)

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

## Round 2 scope

Picked from the ranked list (#8, plus small parts of #9 and #11) and from what round 1 turned up. Balance stays
as it is (the offline cap of 10% for 1 hour is an owner decision), and so do Windows, hosting and gamepad
support (L effort).

### 1. "While you were away" report

Round 1 made the day run itself, but a returning player only sees the last toast ("Day 24"). They can't tell
how many days passed, what was earned, whether the quota was met, or which calls and model drops were missed.

* A pure C# `AwayReport` (in `Core`) starts with the autopilot's first action and collects: days worked, credits
  earned, quotas met and missed, stars, missed calls and expired model drops.
* On the first input after the autopilot has run at least one full day, CorpOS shows a dismissable
  "WHILE YOU WERE AWAY" card with those numbers. Shorter absences keep today's toast.

**Acceptance**: after an idle stretch of several days, the card lists the right day count and earnings; it
appears once, on return, and never for an active player. **Verify**: EditMode tests (an idle hour driven by the
autopilot, then the report's numbers checked against the model), plus a tour segment that idles through a
day, simulates input, and logs PASS when the card shows (screenshot).

### 2. Reduce motion (#8)

* Settings → Gameplay → "Reduce motion" (off by default). Camera moves cut instead of flying (log-in dolly,
  calls, view toggle, ending); gadget showcases are skipped; the title camera holds still; button punches,
  pulses and the rising floating numbers stop moving (the numbers fade in place).

**Acceptance**: with the option on, a camera mode change lands on its target in the next frame, and no pulse or
punch changes scale. With it off, nothing changes. **Verify**: a tour segment that switches the camera with
reduce motion on and logs PASS if it has reached the target one frame later, a screenshot of the setting, and
the existing tour unchanged with the option off.

### 3. A hosting-ready browser page

Round 1's browser build uses Unity's default page: a fixed 1280×720 box with a "Unity Web Player" title.

* A project WebGL template (`Assets/WebGLTemplates/AgentClicker`): the canvas fills the window, the page title
  and colours match the game, a progress bar while loading, a fullscreen button, and a short notice on touch
  devices ("needs a mouse and keyboard").

**Acceptance**: at 1280×720, 1920×1080 and a phone-sized viewport the canvas fills the page with no scrollbars;
the title reads "Agent Clicker"; the touch notice appears only on touch devices; the game still saves and
continues across a reload. **Verify**: Playwright and headless Chrome against a local server (port chosen
free, output in `Logs/`), screenshots at each size.

### 4. Polish: model drop placement and number drift (#11)

* Model drop cards spawn in a band that can't overlap the chapter banner or the toast stack.
* GDD §9 and the career table quote today's numbers (2h 09m, day 27) and say which lap length they use.

**Acceptance**: drop positions are always inside the band (an EditMode test over many spawns via a pure helper);
the docs agree with `Tools/unity.sh tests` output.

### 5. Hunt the Linux segfault (time-boxed)

Round 1 saw one native crash in five tour runs under heavy load. Run the tour repeatedly on a quiet machine
(release build), then on the development build if it reproduces, and record the outcome honestly. If it doesn't
reproduce, the README keeps the note with the new run count.

**Acceptance**: a recorded run count and outcome; a fix only if the cause is found.

Whole round: `Tools/unity.sh tests` (including the balance simulation) passes, the tour passes, and screenshots
go to `docs/media/improvements/round2/`.

## Round 2 results (2026-10-06)

All five items landed on `improvements-2`, one commit each after the plan (`e4f2936`). Tests: **110/110**
(106 after round 1), balance bot unchanged at 2h 09m. Screenshots are in
[`docs/media/improvements/round2/`](media/improvements/round2).

| Item | Commit | Verified by |
|---|---|---|
| 1. "While you were away" report | `144f7c2` | 3 EditMode tests (an idle hour driven by the autopilot is reported with the right days, quotas, missed calls and earnings, and only once; an active player and a break without an end of day get nothing); the tour shows the card on return (`away_report.jpg`, with the threshold lowered to 3 s for the tour) |
| 2. Reduce motion | `79d05ed` | Tour check against a control run: normal motion leaves the camera 2.3 m off two frames into a move and 3 pulsing elements moving; with reduce motion it's 0 m and none move. Setting screenshot. With the option off the rest of the tour is unchanged |
| 3. Hosting-ready browser page | `7c1b260` | Headless Chrome at 1280×720, 1920×1080 and a 390×844 touch phone: canvas = viewport, no scrollbars, title "Agent Clicker", touch notice only on the phone, no page errors; save → reload → CONTINUE restored the career; 52 fps at load ~20 |
| 4. Drop placement + GDD numbers | `1320d02` | EditMode test over 2,000 spawns (below the banner, above the toasts, right of SHIP CODE); career table re-run with `BalanceReport.Career`, still matching |
| 5. Linux segfault hunt | — (no code change) | 7 tours of the round-1 release build, 2 of the round-2 release build and 6 of a round-2 development build (with symbols): **15 runs, 0 crashes**, every check passing, at load averages 15–73. Not reproduced, so no fix; the README note now gives the run count |

Also measured: `Tools/benchmark.sh` A/B against the published v0.1.0 build in the same session. Both read
3.3–4.6 ms main-thread CPU at 60 fps and swing widely in allocations (276–1,638 B/frame for v0.1.0, 729–1,295 for
this build) as the machine's load changes. No measurable regression, but the README's 1.7 ms can't be reproduced
on the busy machine, so it now says so.

Also: `222e25d` moves the tour, benchmark and record scripts' default output from the shared `/tmp` to the
gitignored `Logs/`, and makes their paths absolute (the player resolves relative paths against its own folder).
Older `/tmp/agentclicker-*` files from October 4 are still on the machine; they predate this work and were left
alone.

Deferred, and why:
* Gamepad / Steam Deck (#10) and localization (#13): large jobs, not started.
* Mid-game pacing goals (#9): only the doc part was done; a "next unlock" tracker needs design and play testing
  against the balance band.
* Firefox, Safari, real phones, and audible browser audio still unverified (headless Chrome only).

Owner decisions (unchanged): Windows Build Support, browser hosting, the offline-earnings cap (kept at 10% for
1 hour), license, signing, releases and tags.

## Round 3 scope

Picked from what is still open after round 2: the deferred "next goal" tracker (#9, designed below), the
browser build's untested engines and audio, and two things a browser player needs before the build is hosted
anywhere. Gamepad (#10) and localization (#13) stay deferred (large), and so do the owner's decisions (Windows
Build Support, hosting, the offline cap of 10% for 1 hour, license, releases).

Supporting work, not a player item: `Tools/tour.sh` and `Tools/benchmark.sh` run the player with a throwaway
`XDG_CONFIG_HOME` under `Logs/`, so a tool run can't write Unity's own window and session prefs into the real
`~/.config/unity3d/Nearby Games/Agent Clicker/` (the game's save and settings were already kept out). Checked by
hashing that folder before and after the round.

### 1. A "next goal" card

Two stretches of the first Factory run go long without a new agent type (Architect → Product Manager about 30
minutes for the bot, the first Orchestrator → the Factory about an hour), and the Factory's checklist is only
visible on its own tab. The design:

* A pure C# `NextGoal` (in `Core`) picks one goal from the model, in this order. Before this division's Factory:
  the cheapest story agent type you don't own yet (each is revealed once the previous one is hired), then the
  Orchestrator Clusters the Factory needs, then the Zero-Gravity Recliner, then the Factory's price. After it:
  the next frontier agent type you don't own, then the next vested Stock Option (with the reorg). Each goal has
  a title, what you have, what you need, and an estimate at the current rate.
* CorpOS shows it as a small card at the top of the ACTIVITY panel (which is empty above the log): "NEXT GOAL",
  the title, a progress bar and "1.1B / 2.4B · about 6m". Clicking it opens the store tab where the goal is
  bought.
* No balance change: the goal only displays what the store and Factory tab already know.
* The same "about 6m at your current rate" estimate is added to the store's hover info for agents, upgrades and
  gadgets you can't afford yet.

**Acceptance**: the goal follows the order above at every stage (fresh game, mid game, every Factory
requirement, after the Factory, after all frontier agents); progress is always between 0 and 1; along a full
bot run to the first Factory there is always a goal, and every goal the bot sees is eventually met. The balance
test is unchanged. **Verify**: EditMode tests for each stage and one that walks the `BalanceSimulator` run;
tour screenshots of the card early, mid, late and after the Factory, and a tour check that clicking it opens
the right tab.

### 2. The browser build in WebKit, with audio and save-on-hide checks

Rounds 1 and 2 only ran the browser build in headless Chrome, never checked that audio actually plays, and a
browser player who closes the tab can lose up to 15 seconds since the last autosave (including a big purchase).

* Run the build in Playwright's WebKit (the engine behind Safari; a Linux build of it, not Safari itself) as
  well as Chromium: load, new game, autopilot login, SHIP CODE, hire, save, reload, CONTINUE. Fix whatever
  breaks.
* An audio probe in the test page (an analyser on the page's `AudioContext`) measures whether the game's
  output is non-silent after the first click, in both engines.
* The page saves when it is hidden or closed (`visibilitychange` / `pagehide` → the game saves and flushes
  IndexedDB), so closing the tab right after buying keeps the purchase.

**Acceptance**: the full flow passes in both engines with no page errors; the audio probe reads a non-silent
signal after the first click in both (or the result is reported honestly if an engine can't be probed);
credits earned after the last autosave survive "hide the tab, reload". **Verify**: one Playwright script per
engine (output in `Logs/`), screenshots in `docs/media/improvements/round3/`.

### 3. Move a career between browsers and computers

A browser save lives in that browser's site storage: clearing site data, switching browsers or moving to the
desktop build loses the career, and there's no way to keep a copy.

* Settings → Gameplay gets a SAVE FILE row. In the browser: DOWNLOAD SAVE (the save JSON as
  `agentclicker_save.json`) and LOAD SAVE FILE (a file picker). On the desktop: OPEN SAVE FOLDER, since the
  same file can be copied in or out there. One format both ways, so a browser download works on the desktop and
  the other way round.
* A loaded file is checked (`SaveSystem.Validate`: parses, has a started career, day ≥ 1) and replaces the
  current career only after a confirmation that names both careers (division, day, credits).

**Acceptance**: a downloaded save loads into a fresh browser profile and continues with the same credits and
day; garbage, an empty file and a non-save JSON are rejected with a message and change nothing.
**Verify**: EditMode tests for `Validate`; Playwright: download, then a new browser context with empty
storage, LOAD SAVE FILE through the file chooser, CONTINUE, compare credits and day.

### 4. Help that matches the game

The How to Play card predates rounds 1–2: it doesn't mention that the day runs itself when you're away, the
"While you were away" card, daily asks, Focus draining, or the phone keys.

* Rewrite How to Play to cover those in the same space, and add the Settings → Gameplay options it refers to.

**Acceptance**: every mechanic in the README's "How to play" section is in the card, and it fits the card
without the auto-sizer shrinking it below its minimum. **Verify**: tour screenshot of the card.

Whole round: `Tools/unity.sh tests` (including the balance simulation) passes, the tour passes, the benchmark is
re-run and compared within the same session, screenshots go to `docs/media/improvements/round3/`.

## Round 3 results (2026-10-06)

All four items landed on `improvements-3`, one commit each after the plan (`077bd6a`) and the tool change
(`7486493`). Tests: **127/127** (110 after round 2), balance bot unchanged at 2h 09m. Screenshots are in
[`docs/media/improvements/round3/`](media/improvements/round3).

| Item | Commit | Verified by |
|---|---|---|
| 1. Next goal card + affordability estimates | `2e71b0a` | 7 EditMode tests, one walking the whole bot run (goals only move forward, one per story agent in order, the last is the Factory; never NaN at 1e308). Tour: the goal at day 1, mid game, late game, after the Factory and with every agent owned; a real mouse click on the card opens the Factory tab; a real hover over an unaffordable agent shows "about 3m 20s" |
| 2. Browser: save on hide, one IndexedDB sync at a time, audio check | `e33b81f` | New `Tools/webtest.mjs` in headless Chromium on the real GPU (ANGLE/Vulkan). The round 2 build passes 7/8: hiding the tab lost the 20 clicks made since the last autosave. This build: 8/8, the clicks are saved on hide. Audio peak 0.11–0.22 after the first clicks (context running) |
| 3. Save file: download / load in the browser, save folder on the desktop | `f15b34c` | 10 EditMode test cases for `SaveSystem.Validate`. Web test: DOWNLOAD gives `agentclicker_save.json`; in a fresh browser profile a JSON file that isn't a save is rejected and changes nothing; the real file loads after a confirmation, CONTINUE has the same clicks, day and agents (12/12 checks) |
| 4. How to Play | `98b1c96` | Tour screenshot; it fits the card at its full 19 pt |

Supporting work: `7486493` runs tour and benchmark players with `XDG_CONFIG_HOME` under `Logs/player-home`. The
real `~/.config/unity3d/Nearby Games/Agent Clicker/` was hashed before and after the round: `prefs` (Unity's window
and session keys) and the editor's analytics files are unchanged, there is no save, and the game's settings key was
never written there. One file did change: `TestResults.xml`, which Unity's performance-testing package (a dependency
of a dependency) writes into the editor's `persistentDataPath` after every EditMode run, as it did in earlier rounds.
It isn't a save or settings file. Redirecting the editor's config folder would also move its license, so it was left.

The tour also stopped passing its real-click checks mid-round when a new display appeared on the shared desktop: the
Input System drops synthetic mouse events while the window isn't focused. The tour now sets the Input System to ignore
focus (in tour mode only), and logs whether the window had focus. With that, all 12 tour checks pass.

Changes from the plan:
* The goal after every agent type is owned is "double your Stock Options" (a reorg worth taking), not "the next
  option": deep in the endless game options vest many times a second, so that goal read "about 0s".
* Along the bot's run the Office goal (the recliner) never shows: the bot buys the recliner before its fifth
  Orchestrator. The tests cover it directly.
* WebKit could not be tested (below), so item 2 is Chromium only.
* The plan said the overlapping syncs would be checked. They only appeared in a slow software-rendered run of the
  round 2 build (up to 6 at once); at GPU speed neither build overlaps, so the check passes for both. The new code
  can't overlap by construction (Unity's per-write auto-sync is off, and the game runs one sync at a time), but it
  wasn't re-measured under software rendering.

Also measured: `Tools/benchmark.sh` A/B against the published v0.1.0 (downloaded to `Builds/`, deleted afterwards),
alternating two runs each at load average about 50. With a 60 fps cap while clicking, main-thread CPU read 5.56 and
3.87 ms for v0.1.0 and 2.97 and 1.99 ms for this build; uncapped figures swung 2–3x between runs of the same build.
No sign of a regression; the difference is within the noise. Seven tour runs this round, no crashes (22 since
the segfault, which is still unexplained).

Deferred, and why:
* **WebKit (Safari's engine)**: Playwright's WebKit build is linked against Ubuntu 24.04 libraries (ICU 74, flite,
  libjxl 0.8, libbacktrace). CachyOS ships ICU 78, so it doesn't start. Getting it would mean installing old
  libraries or a container image outside the repo, which is the owner's call. Real Safari still needs a Mac.
* Firefox, real phones, audible audio through speakers: no Firefox build is cached, and headless only shows that
  the game produces a signal.
* Gamepad / Steam Deck (#10) and localization (#13): large, not started.
* The desktop OPEN SAVE FOLDER button wasn't clicked in the tour (it would open a file manager on the shared
  desktop). A browser-downloaded save wasn't loaded into the desktop build end to end; both use the same
  `SaveSystem` and the round-trip is covered by tests.

Owner decisions: unchanged (Windows Build Support, browser hosting, the offline cap of 10% for 1 hour, license,
signing, releases and tags), plus whether WebKit testing is worth an Ubuntu container or a Mac.
