# AGENT CLICKER — Game Design Document

> *"Automate yourself out of a job. Keep the paycheck."*

## 1. Pitch

A Cookie Clicker–style incremental game set in a 3D office. You play a software
developer at **Synergex Corp**, a gloriously boring enterprise. Every morning you
clock in, sit at your desk and log into **CorpOS**. The 2D game lives on your
in-world monitor: you ship code by hand, earn **Compute Credits**, and spend
them on AI agents from a market of (entirely fictional) frontier AI labs.

The goal is to build a fully autonomous **Software Factory**. Once it's online
your agents do the whole job and your character can put their feet up on the desk.

## 2. Core Loop

```
 ship code (click) ──► credits ──► hire agents ──► credits/sec ──► upgrades
        ▲                                                         │
        └────────────── office gadgets / desk upgrades ◄──────────┘
                              │
                      visible in 3D office
```

* **Click**: the *SHIP CODE* terminal. Each click types a line of fake code and
  earns `clickPower` credits.
* **Agents** (the "buildings"): produce credits per second. Cost grows `x1.15`
  per agent owned.
* **Upgrades**: multiply agent output, click power, or lab output.
* **Office**: physical gadgets bought with credits. They appear on your desk in 3D
  and grant bonuses (more monitors, a better keyboard, a rubber duck, and so on).
* **Days**: a work day runs 09:00–17:00 (5 real minutes by default). At 17:00 you
  clock out, get a performance review and a bonus, and your agents work the
  night shift. The next morning, the office has changed.

## 3. Fictional Frontier Labs

| Lab | Tagline | Brand colour |
|---|---|---|
| **Hallucin8 Labs** | "Move fast and make things up." | Magenta |
| **Paperclip Dynamics** | "Optimizing for your objectives. All of them." | Steel blue |
| **Gradient Ascent** | "Always climbing. Never converging." | Green |
| **Nimbus Cognition** | "Your thoughts, in the cloud." | Sky blue |
| **Deep Pocket AI** | "Infinite runway. Finite patience." | Gold |
| **Lumen Frontier** | "Illuminating the latent space." | Amber |
| **Singularity & Sons** | "A family business since the end of history." | Purple |
| **OmniSapient** | "We already know what you'll buy." | Red |

## 4. Agents

| # | Agent | Lab / Model | Base cost | Base credits/sec |
|---|---|---|---|---|
| 1 | Autocomplete | Hallucin8 · Fib-Mini | 15 | 0.1 |
| 2 | Chat Assistant | Hallucin8 · Fib-2 Chat | 100 | 1 |
| 3 | Junior Coding Agent | Paperclip · Maximizer Jr. | 1.1K | 8 |
| 4 | Test Writer | Gradient Ascent · Summit-T | 12K | 47 |
| 5 | Code Reviewer | Nimbus · Cumulus Review | 130K | 260 |
| 6 | Bug Triage Swarm | Paperclip · Clipswarm | 1.4M | 1.4K |
| 7 | DevOps Agent | Deep Pocket · Pipeline Baron | 20M | 7.8K |
| 8 | Architect Agent | Lumen · Prism Architect | 200M | 44K |
| 9 | Product Manager Agent | Singularity & Sons · Heirloom PM | 2.4B | 260K |
| 10 | Orchestrator Cluster | OmniSapient · Omni Conductor | 26B | 1.6M |

**Frontier agents** unlock with your first Software Factory and stay unlocked in every later division:

| # | Agent | Lab / Model | Base cost | Base credits/sec |
|---|---|---|---|---|
| 11 | Agent Foundry | OmniSapient · Omni Foundry | 330B | 10M |
| 12 | Hyperscale Datacenter | Deep Pocket · Baron Campus | 5T | 65M |
| 13 | Digital Twin | Hallucin8 · Fib-You | 70T | 430M |
| 14 | AGI Intern | Singularity & Sons · Heirloom Prodigy | 1Qa | 2.9B |
| 15 | Orbital Compute Ring | Nimbus · Stratus Orbital | 14Qa | 21B |
| 16 | Dyson Swarm | Lumen · Prism Swarm | 170Qa | 150B |
| 17 | Simulation Farm | Gradient Ascent · Summit-Ω | 40Qi | 1.1T |
| 18 | Retrocausal Deployer | Paperclip · Maximizer ∞ | 7Sx | 8.3T |
| 19 | Galaxy-Brain Cluster | OmniSapient · Omni Prime | 1.1Sp | 64T |
| 20 | The Singularity | Singularity & Sons · Patriarch | 300Sp | 510T |

Cost of the *n*-th copy: `baseCost × 1.15^n`. Bulk buys: x1, x10, x100 and MAX.

## 5. Upgrades

* **Agent tiers**: fifteen per agent, at 1 / 5 / 25 / 50 / 100 / 150 / 200 … 600 owned, each doubling
  its output (*Longer Context Window* … *Recursive Self-Improvement*, then *Multi-Agent Debate*, *Persistent
  Memory* … *Omniscience (Beta)*). Cost is `baseCost × {10, 50, 500, 5e3, 5e4, 5e6, 5e8, 5e11 … 5e32}`.
* **Research** (40 of them, *Quantized Weights* … *The Final Commit*): global +5% / +7.5% / +10% each,
  costs from 5B rising x12 per step, revealed at a quarter of their price in earnings.
* **Influence** (15, *LinkedIn Post* … *Your Face on Currency*): unlock at 25 … 500 trophies; each multiplies
  production by `1 + Clout × factor` (factors 0.10–0.20), the Cookie Clicker kitten/milk idea.
* **Click upgrades**: Vim Keybindings, Muscle Memory, 10x Engineer Mindset (each
  x2 clicks), plus Prompt Engineering, Prompt Chaining and Meta-Prompting (each adds
  +1% of credits/sec to every click).
* **Enterprise contracts** (one per lab): x1.5 output for that lab's agents. They
  unlock once you own 10 agents from that lab. **Exclusive partnerships** (x2) follow at 100 lab agents
  plus one of the lab's frontier agents.
* More click upgrades keep the hand-shipped path alive (each +1% of credits/sec per click, up to
  *The Click Heard Round the World*). **Buy All** buys every affordable upgrade, cheapest first.

## 6. Office (3D, visible on your desk)

| Item | Effect |
|---|---|
| Company Mug | +1 credit per click |
| Rubber Duck | 10% of clicks *crit* for x10 |
| Desk Succulent | +2% credits/sec, API outages are 50% shorter |
| Mechanical Keyboard | x2 click power (replaces the stock keyboard) |
| Second Monitor | +10% credits/sec (shows the live agent log) |
| Noise-Cancelling Headphones | clicks gain +1% of credits/sec |
| Architecture Whiteboard | Architect agents x2 |
| Lava Lamp | +5% credits/sec, model drops appear 25% more often |
| Espresso Machine | +5% credits/sec, Caffeine Rush lasts twice as long |
| Third Monitor | +15% credits/sec (shows the production graph) |
| Macro Pad | auto-ships code twice a second |
| Ergonomic Gaming Chair | +10% credits/sec |
| Homelab Server Rack | +20% credits/sec |
| Mini Fridge | +50% night-shift earnings |
| Executive Desk | +15% credits/sec, required for the monitor wall |
| Monitor Wall | +25% credits/sec (shows the lab status page and kanban board) |
| "SHIP IT" Neon Sign | +10% credits/sec |
| Zero-Gravity Recliner | +10% credits/sec, **required for the Software Factory** |

## 7. The Day

* 09:00 clock in. The CorpOS login screen appears on your monitor.
* 17:00 clock out. The **performance review** compares today's earnings with
  your quota (`max(150 × day, 0.9 × yesterday)`). Meeting the quota earns a
  ⭐ and a 10% bonus.
* **Night shift**: agents earn 60 seconds of production at night (x1.5 with the Mini Fridge).
* **Away from the keyboard** (`DayAutopilot`, on by default): agents only produce during the work day, so
  an idle game would otherwise stop at the review. With no input, the game clocks out 30 s after 17:00,
  goes home after 15 s on the review, clocks in after 8 s of night and logs in after 10 s, then toasts
  what it did. Any input restarts the countdown; calls, menus, story cards and the ending hold it, and
  choosing WORK LATE keeps that day's overtime.
* **Promotions** come from lifetime earnings: Junior Developer → Developer →
  Senior Developer → Staff Engineer → Principal Engineer → AI Whisperer →
  Chief Agent Officer. Promotions change the office: the cubicle walls disappear,
  and a rug, bookshelf, sofa, plaques and trophies arrive.
* **Office drift**: sticky notes, pizza boxes, posters and a calendar all
  accumulate over the days.

## 8. Random Events

* **Model Drop** (the golden cookie): *"Nimbus Cognition just released
  Cumulus-7!"* Click it within 12 s. Outcomes:
  * 50% **Benchmark Hype**: x7 credits/sec for 60 s
  * 35% **Funding Round**: `min(15% bank, 15 min of production)`
  * 15% **Caffeine Rush**: x77 click power for 10 s
* **API Outage**: credits/sec x0.5 for 45 s. Click the banner 5 times to fail
  over to another provider and clear it early.

## 9. Win Condition — The Software Factory

Requirements: own at least 1 of every agent, at least 5 Orchestrator Clusters,
and the Zero-Gravity Recliner. The Factory costs **3 trillion** credits.

**Pacing.** `BalanceSimulator` plays greedily, catches every model drop and clicks 4/s
for the first 30 minutes, then 1/s. It finishes in 2h 09m (day 27 with five-minute days; `Tools/unity.sh tests`
prints the run), and an EditMode test fails if that drifts outside 1.5 to 5 hours. Real players should take
roughly 3 hours.

**Next goal.** A card at the top of the ACTIVITY panel always names one thing to save for, with a progress bar and an
estimate at the current rate (`NextGoal`): the cheapest story agent type you don't own yet, then the Orchestrator
Clusters the Factory needs, then the recliner, then the Factory's price. After the Factory it's the next frontier agent
type, and once you own every type, a reorg that at least doubles your Stock Options. Clicking the card opens the store
tab where the goal is bought. It is display only. Along the bot's run the longest goals are the Product Manager Agent
(about 30 minutes) and the Factory's price (about 40 minutes); the store's hover info also says roughly how long until
you can afford any agent, upgrade or gadget.

When you buy it, the monitor shows the factory pipeline booting up. The camera
pulls back, and your character reclines with their feet on the desk and hands behind their head.
*"100% automated. You never have to work again."* After the epilogue the game keeps going: see §17.
Later divisions get a short victory lap instead of the full ending.

## 10. Presentation

* **Office view**: an over-the-shoulder 3D camera. The monitor UI is live and still clickable.
* **Monitor view**: the camera dollies into the main monitor, so the 2D game fills the screen.
* Toggle views with `Tab`, the mouse wheel, or the on-screen button. Right-drag in
  office view to look around.
* Extra monitors run live world-space dashboards.
* Time of day drives the sunlight through the window and the wall clock's hands.
* Employee animations: Idle, Typing, Sip, Relax (hands behind head), FeetUp.
* Procedurally synthesised sound effects: key clicks, purchase chimes, alerts.

## 11. Tech

* **Blender 4.5**: every model is generated by Python scripts in `Blender/scripts`
  and exported as FBX into `Unity/Assets/Art/Models`.
* **Unity 6 (6000.6) + URP**: data-driven economy (`GameDatabase`), pure C#
  simulation (`GameModel`) covered by EditMode tests, a runtime-built uGUI/TMP
  interface on world-space canvases, and an editor bootstrap that builds the scene.

## 12. Story

**Cast:** Rex Halvorsen (CEO), Dana Whitfield (your manager), Priya Raman (desk neighbour), Gary Okonkwo
(IT), Brenda Lowe (HR), Facilities Bot, the ModelMart newsletter, and later two agents: Heirloom PM and
Omni Conductor.

| Chapter | Starts when | Beats |
|---|---|---|
| 1. The Mandate | New game | The CEO's "AI-First, 10x or be re-imagined" memo. Dana suggests trying an agent. |
| 2. The Pilot Program | First agent | IT provisions API keys. Priya notices your terminal typing by itself. Quota and stars are explained. |
| 3. Scale Out | Senior Developer | The cubicle is removed ("open concept"). Priya is re-orged into "Prompt Operations". Agents file PTO. |
| 4. Who Manages Whom | Principal Engineer | The Product Manager agent replaces Dana, who opens a pottery studio. Heirloom PM now writes your reviews. |
| 5. The Factory | First Orchestrator | Omni Conductor pitches the Software Factory and insists you buy a recliner. |
| 6. Epilogue | Factory built | Record quarter, lunch with Priya, and the CEO admits he has been an Orchestrator since Q2. |

Emails are delivered while you're working, at most one every 8 seconds. Chapter-opening emails open the
inbox automatically once you've shipped your first 10 lines of code, so day 1 starts at SHIP CODE (this can be
turned off in settings). Chapter banners wait until no modal, model drop or call is on screen, and sit in the
upper part of the screen, clear of SHIP CODE. The morning day card shows the current chapter.
New games start with three intro cards, and the ending has five epilogue cards followed by a credits roll.

## 13. Menus & settings

The title screen has Continue, New Game, Settings, How to Play, Credits and Quit, over a slow cinematic
camera at golden hour. The pause menu (Esc) pauses time. The settings categories are Graphics, Audio,
Gameplay and Controls. Settings are saved in PlayerPrefs, separately from the save file. Gameplay → Save file
downloads and loads the save in the browser (checked by `SaveSystem.Validate`, confirmed before it replaces a career)
and opens the save folder on the desktop; the file is the same on every platform.

## 14. Performance budget

The target is under 2 ms of main-thread CPU at 60 fps on the High preset, no GC hitches while clicking,
and graceful scaling via presets on integrated GPUs. Track it with `Tools/benchmark.sh`.

## 15. Interruptions: phone calls

Calls start once you own an agent, ringing every 90–170 s (never in the last half hour of the day). A
ringing call lasts 14 s; answering halves your Focus. Each call has 2–3 replies with effects:

| Effect | Example |
|---|---|
| Rapport ±N | Dana, Priya, Gary, Rex (range −5..+5) |
| Credits | a recruiter's "signing bonus", a pizza order your agents fulfil |
| Output buff | press coverage, joining Rex's board call |
| Meeting | standups and syncs skip 10–45 game minutes, while your agents keep earning |
| Discount | sales reps: 25% off your next agent order |
| Outage | lying to IT gets your API keys revoked |

**Story calls** ring once, gated by progress (Dana's demo, Gary's password incident, Priya's borrowed
config, HR's org chart, the recruiter, Rex's board vision, Dana's farewell, Heirloom's sync, Omni's
question, Priya's partnership pitch). Ignoring one costs rapport. **Random calls** are weighted and gated
by chapter (sales, standups, coffee runs, IT surveys, spam, a journalist, your own agents, a status bot, Mom).

**Perks at rapport 3+:** Dana makes quotas 15% lower (−3 or below makes them 15% higher). Priya makes
outages end 30% sooner. Gary makes outages 40% rarer. Rex raises the review bonus to 15%. The epilogue
has rapport-dependent cards for each character.

## 16. Focus and daily asks

* **Focus** gains +3% per hand click (max 100%) and decays at 22% per second after 0.8 s idle. It
  multiplies click power by up to x3. It rewards active play and makes the phone a real interruption.
* **Asks:** two per day. Day 1 is always "ship by hand" and "hire agents"; later days pick from hire,
  ship, upgrade, model drop, production level, over-deliver, answer calls and install a gadget. Each pays
  30 s of production (at least 50 × day), and finishing both earns Dana +1.

Balance with all of this: the greedy bot finishes in 2h 09m. It always takes the first reply, catches
every drop and clicks 4/s early on, so human players should take roughly 3 hours.

## 17. The endless game

The Factory ends the story, not the game. Like Cookie Clicker, there is always another order of magnitude.

* **Frontier agents** (§4) and fifteen upgrade tiers keep a single division growing for hours.
* **Reorg (prestige).** Once a division's Factory is online, the board wants it everywhere. REORG moves
  Sam to the next division (Engineering → Marketing → Sales → Legal → Finance → People Ops → Customer
  Success → Facilities → The C-Suite → The Board → Synergex Holdings / Orbital / Lunar / Interplanetary /
  Multiverse → "Timeline N" forever). Credits, agents, upgrades, office gadgets, the day and the title reset.
  Stock Options, perks, trophies, the story and relationships carry over. Each division gets a memo email.
* **Stock Options** vest as `floor(cbrt(all-time credits / 1e9))`: 10 at 1T, 100 at 1Qa, 1,000 at 1Qi.
  Each option earned is worth +1% production forever (1.5% / 2% with vesting perks). Spending them never
  lowers that bonus.
* **Board Room perks** cost options: Starter Kit, Remote Work, Deep Work, Early Access Program, Preferred
  Vendor, Expense Account, Pack Your Desk, Night Owl Agents, Personal Brand, Factory Blueprints, Hype Machine,
  Unlimited PTO, Chief of Staff (auto-claims drops and fixes outages), Accelerated Vesting and Founder Shares,
  plus a repeatable **Board Seat** (x1.1 production each, doubling in price), an infinite options sink.
* **Bigger factories.** After the first, a division's Factory costs 0.1% of everything you've ever earned
  (at least 3T), so it stays a real goal in every division instead of an afterthought.
* **Trophies.** 512 of them, mostly generated ladders: earn a thousand … a centillion credits, produce
  1/s … a centillion/s, ten milestones per agent type, fleet size, clicks, days, stars, factories,
  reorgs, options, drops, outages, calls, asks and story beats. Each adds 4% **Clout**.
* **Big numbers.** Every power of a thousand up to a centillion (1e303) has a short name (K, M, B, T, Qa, Qi,
  Sx, Sp, Oc, No, Dc, UDc … Vg … Ce); beyond that, or by choice in Settings, numbers switch to scientific
  notation. All arithmetic saturates at the largest double, so nothing ever becomes NaN or breaks a save.

**Pacing.** `BalanceSimulator.RunCareer` plays division after division with a fixed lap after each Factory.
With one-hour laps (`Tools/unity.sh exec AgentClicker.EditorTools.BalanceReport.Career`), the bot gets:

| Division | Factory after | Earned in division | Options gained |
|---|---|---|---|
| Engineering | 2h 09m | 550T | 81 |
| Marketing | 26m | 10.8Qa | 143 |
| Sales | 8m | 2.77Qi | 1.18K |
| Legal | 4m 30s | 105Sx | 45.7K |
| Finance | 3m | 1.25Oc | 1.03M |
| … The Board (10th) | 3m 46s | 9.5UDc | 1.05B |

(Re-run on 2026-10-06 with the same results.) The EditMode test `Tools/unity.sh tests` runs a shorter career
of four divisions with 20-minute laps, so it prints slower factories (Marketing about 59m, Sales and Legal
about 47m); both are correct for their lap length.

Growth keeps compounding (still about x6–7 per division by the tenth), while later factories stay at
a few minutes. At that rate the trophy ladders that end at a centillion (1e303) are hundreds of hours away.

