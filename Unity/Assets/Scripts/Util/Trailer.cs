using System.Collections;
using System.Collections.Generic;
using System.IO;
using AgentClicker.Core;
using AgentClicker.Office;
using AgentClicker.UI;
using AgentClicker.Util;
using UnityEngine;
using UnityEngine.UI;

namespace AgentClicker
{
    /// <summary>
    /// Shot list for the store trailer and screenshots:
    ///   AgentClicker.x86_64 -trailer out/          one mp4 per shot in out/clips (game SFX, no music) + the music loops as WAVs
    ///   AgentClicker.x86_64 -trailer-stills out/   the same script in real time, saving cursor-free PNGs to out/stills
    /// Tools/make_trailer.sh runs both and Tools/make_trailer.py edits the clips into docs/media.
    /// </summary>
    public class Trailer : Director
    {
        public string OutputDir;
        public bool StillsOnly;

        VideoCapture _video;
        Coroutine _camMove;

        IEnumerator Start()
        {
            InitDirector();
            LogClicks = true;
            Directory.CreateDirectory(Path.Combine(OutputDir, StillsOnly ? "stills" : "clips"));
            M.RandomEventsEnabled = false;
            _gm.SuppressShowcase = false;
            if (!StillsOnly) ExportMusic();
            yield return Wait(1.0f);
            Debug.Log("[Trailer] start" + (StillsOnly ? " (stills)" : ""));
            yield return Script();
            Debug.Log("[Trailer] done");
            Application.Quit();
        }

        // ================================================================== the shots
        IEnumerator Script()
        {
            // --- title: slow golden-hour drift behind the logo --------------------------------
            _gm.ShowTitle();
            CursorVisible = false;
            yield return Wait(2.5f);
            yield return Still("title");
            _gm.Menu.HideAll();
            yield return Wait(0.5f);
            yield return Rec("title_bg");
            yield return Wait(7f);
            Cut();

            // --- story: the CEO's memo ------------------------------------------------------------
            _gm.Menu.ShowStoryCards(StoryDatabase.Intro(), null);
            yield return Wait(0.3f);
            yield return Rec("story_intro");
            yield return Wait(3.6f);
            Cut();
            _gm.Menu.HideAll();

            // --- day 1: clock in ----------------------------------------------------------------
            Fresh();
            _gm.BeginMorning(false);
            CursorVisible = true;
            Place(_pos = new Vector2(Screen.width * 0.66f, Screen.height * 0.72f));
            yield return Wait(0.4f);
            yield return Rec("clockin");
            yield return Wait(1.6f);
            yield return Still("day1_morning");
            yield return Wait(0.6f);
            yield return ClickWorld(_gm.Refs.MainScreen.bounds.center);
            yield return Wait(2.6f);
            Cut();

            // --- the core loop: ship code by hand -----------------------------------------------------
            EarlyGame();
            yield return ToDesktop();
            yield return Wait(4.8f);                            // chapter 2 banner comes and goes off camera
            yield return MoveToName("ShipButton", 0.01f);
            yield return Zoom(new Vector2(0.31f, 0.52f), 1.6f, 0.01f);
            yield return Wait(0.6f);
            yield return Rec("ship");
            yield return MoveToName("ShipButton", 0.3f);
            for (int i = 0; i < 52; i++)
            {
                yield return Tap(0.075f, "ShipButton");
                if (i == 40) yield return Still("ship_code", keepCursor: true);
            }
            yield return Wait(0.8f);
            Cut();

            // --- hire agents ------------------------------------------------------------------------
            yield return Zoom(new Vector2(0.64f, 0.5f), 1.42f, 0.01f);
            M.State.credits = 26000;
            yield return Wait(0.5f);
            yield return Rec("hire");
            yield return ClickName("Amt10");
            yield return ClickName("Agent0");
            yield return ClickName("Agent1");
            yield return ClickName("Agent1");
            yield return Wait(0.3f);
            yield return ClickName("Agent2");
            yield return Wait(0.6f);
            yield return ClickName("Amt1");
            yield return ClickName("Agent3");
            yield return Wait(1.0f);
            Cut();

            // --- upgrades -------------------------------------------------------------------------------
            MidGameUpgrades();
            yield return MonitorView();
            yield return Wait(4.8f);                            // chapter 3 banner
            yield return ClickName("TabUPGRADES");
            yield return Zoom(new Vector2(0.69f, 0.5f), 1.6f, 0.01f);
            yield return Rec("upgrades");
            yield return MoveToName("BuyAll", 0.6f);
            yield return Wait(0.4f);
            yield return ClickName("BuyAll");
            yield return Wait(2.2f);
            Cut();

            // --- office gadgets -----------------------------------------------------------------------------
            yield return MonitorView();
            M.State.credits = 3e6;
            yield return ClickName("TabOFFICE");
            yield return Wait(0.5f);
            yield return Rec("gadget");
            yield return Wait(0.3f);
            yield return ClickName("monitor2");
            yield return Wait(1.3f);
            yield return Still("gadget_showcase");
            yield return Wait(2.6f);
            yield return ClickName("lava_lamp");
            yield return Wait(3.6f);
            Cut();
            yield return ClickName("TabAGENTS");

            // --- model drop -------------------------------------------------------------------------------
            yield return Zoom(new Vector2(0.45f, 0.6f), 1.4f, 0.01f);
            Place(_pos = new Vector2(Screen.width * 0.42f, Screen.height * 0.75f));
            yield return Rec("drop");
            yield return Wait(0.4f);
            M.SpawnDrop();
            yield return Wait(1.2f);
            yield return Still("model_drop", keepCursor: true);
            yield return ClickName("ModelDrop");
            yield return Wait(2.4f);
            Cut();

            // --- API outage -------------------------------------------------------------------------------
            yield return Wait(0.5f);
            yield return Rec("outage");
            yield return Wait(0.3f);
            M.StartOutage();
            yield return Wait(1.4f);
            yield return Still("outage", keepCursor: true);
            for (int i = 0; i < GameDatabase.OutageClicks; i++) yield return Tap(0.22f, "Outage");
            yield return Wait(1.8f);
            Cut();

            // --- the phone rings --------------------------------------------------------------------------
            _gm.Cam.SetMode(CamMode.Office, 0.01f);
            yield return Wait(1.0f);
            yield return Rec("call");
            yield return Wait(0.4f);
            M.RingPhone(CallDatabase.ById("gary_password"));
            yield return Wait(2.0f);
            yield return ClickName("Answer");
            yield return Wait(3.4f);
            yield return Still("phone_call");
            yield return ClickName("Choice1");
            yield return Wait(3.8f);
            Cut();
            yield return Wait(1.5f);

            // --- the inbox ----------------------------------------------------------------------------------
            yield return MonitorView();
            yield return Wait(0.6f);
            yield return Rec("inbox");
            yield return MoveToName("Inbox");
            yield return Press();
            _gm.Computer.ShowInbox("ceo_mandate");
            yield return Wait(3.2f);
            Cut();
            _gm.Computer.CloseModal();

            // --- 5 PM: review, night shift ----------------------------------------------------------------
            var s = M.State;
            s.earnedToday = s.quotaToday * 1.4 + 1000;
            s.dayMinutes = GameDatabase.WorkdayMinutes - 2.5f;
            yield return Rec("review");
            yield return Wait(2.6f);
            yield return ClickNameUnder("ClockOut", "Modal");
            yield return Wait(2.4f);
            yield return Still("review");
            yield return Wait(0.6f);
            yield return ClickName("GoHome");
            yield return Wait(2.8f);
            Cut();

            // --- the office grows with you: day 1 vs. day 22, same camera move ------------------------
            Fresh();
            _gm.BeginMorning(false);
            _gm.Overlay.HideDayCard();
            M.State.dayMinutes = 7 * 60;
            CursorVisible = false;
            yield return Wait(1.0f);
            yield return OfficeMove("office_day1", 4.5f);
            LateGame();
            _gm.Overlay.HideDayCard();
            M.State.dayMinutes = 7 * 60;
            yield return Wait(1.5f);
            yield return OfficeMove("office_late", 4.5f, still: "office_late");

            // --- promotion: the sofa arrives ------------------------------------------------------------
            s = M.State;
            s.Phase = GamePhase.Working;                       // agents only earn while Sam is clocked in
            yield return Wait(5f);                              // chapter 5 banner comes and goes off camera
            s.titleIndex = 3;
            // production pushes lifetime earnings over the Principal Engineer line about 1.4 s into the clip
            s.lifetimeEarned = GameDatabase.Titles[4].Threshold - M.Cps * 2.0;
            M.MarkDirty();
            _gm.Office.Refresh(false);
            _camMove = StartCoroutine(Dolly(new Vector3(-1.5f, 1.65f, -2.3f), new Vector3(-1.0f, 1.55f, -2.45f),
                                            new Vector3(1.3f, 0.75f, -0.3f), new Vector3(1.45f, 0.75f, -0.35f), 6.5f));
            yield return Wait(0.6f);
            yield return Rec("promotion");
            yield return Wait(4.6f);
            Cut();
            StopCam();
            s.titleIndex = 5;
            s.lifetimeEarned = 2.2e12;
            _gm.Office.Refresh(false);

            // --- the Software Factory ---------------------------------------------------------------------
            s.dayMinutes = 400;
            s.credits = 1e13;
            M.BuyOffice("recliner");
            s.credits = 3.4e12;
            yield return Wait(4.0f);                            // let the showcase finish
            _gm.Computer.CloseModal();
            yield return ToDesktop();
            CursorVisible = true;
            yield return Wait(4.8f);                            // chapter 5 banner
            yield return Still("corpos_late");
            yield return Rec("factory");
            yield return ClickName("TabFACTORY");
            yield return Wait(1.4f);
            yield return ClickName("Build");
            yield return Wait(0.5f);
            CursorVisible = false;                              // hands off from here on
            yield return Wait(1.1f);
            yield return Still("factory_boot");
            yield return Wait(9.4f);
            yield return Still("automated");
            yield return Wait(2.2f);
            Cut();
            _gm.KeepPlaying();
            yield return Wait(1.5f);

            // --- feet up: slow orbit for the end card ------------------------------------------------------
            _gm.Overlay.HideEnding();
            yield return OfficeMove("feet_up", 6f, yaw0: -64f, yaw1: -46f, pitch: 15f, dist: 2.35f, still: "feet_up");

            // --- endless: frontier agents --------------------------------------------------------------------
            Endless();
            yield return ToDesktop();
            CursorVisible = true;
            yield return Zoom(new Vector2(0.64f, 0.5f), 1.42f, 0.01f);
            _gm.Computer.SelectStoreTab(0);
            yield return MoveToName("AmtMax", 0.01f);
            yield return Wait(0.6f);
            yield return Rec("frontier");
            yield return ClickName("AmtMax");
            M.State.credits = 6e17;
            var list = AgentList();
            yield return ScrollTo(list, 0f, 1.6f);
            yield return ClickName("Agent15");
            yield return ClickName("Agent14");
            yield return Wait(0.3f);
            yield return Still("frontier_agents", keepCursor: true);
            yield return Wait(0.6f);
            Cut();

            // --- trophies ----------------------------------------------------------------------------------------
            _gm.Computer.SelectStoreTab(StorePanel.TrophiesTabIndex);
            yield return Wait(0.6f);
            yield return Rec("trophies");
            yield return MoveTo(new Vector2(Screen.width * 0.74f, Screen.height * 0.45f), 0.8f);
            M.State.lifetimeEarned = M.State.allTimeEarned = 4e18;
            M.CheckAchievements();
            yield return MoveTo(new Vector2(Screen.width * 0.8f, Screen.height * 0.5f), 1.2f);
            yield return Still("trophies", keepCursor: true);
            yield return Wait(1.2f);
            Cut();

            // --- reorg to the next division ----------------------------------------------------------------------
            yield return MonitorView();
            _gm.Computer.SelectStoreTab(3);
            yield return Wait(0.8f);
            yield return Rec("reorg");
            yield return ClickName("Build");
            yield return Wait(0.9f);
            yield return ClickName("Yes");
            yield return Wait(2.8f);
            yield return Still("reorg_memo");
            yield return Wait(0.4f);
            Cut();
            _gm.Menu.SkipCards();
            yield return Wait(2.4f);

            // --- the Board Room ---------------------------------------------------------------------------------------
            yield return ClickWorld(_gm.Refs.MainScreen.bounds.center);
            yield return Wait(2.0f);
            _gm.Computer.CloseModal();
            yield return Zoom(new Vector2(0.69f, 0.5f), 1.6f, 0.01f);
            yield return Wait(0.5f);
            yield return Rec("boardroom");
            yield return ClickName("starter_kit");
            yield return Wait(0.5f);
            yield return ClickName("deep_work");
            yield return Wait(0.5f);
            yield return ClickName("remote_work");
            yield return Still("board_room", keepCursor: true);
            yield return Wait(1.4f);
            Cut();

            // --- hundreds of hours in --------------------------------------------------------------------------------
            BigNumbers();
            yield return ToDesktop();
            yield return Zoom(new Vector2(0.31f, 0.52f), 1.6f, 0.01f);
            yield return MoveToName("ShipButton", 0.01f);
            yield return Wait(0.6f);
            yield return Rec("bignumbers");
            for (int i = 0; i < 30; i++) yield return Tap(0.08f, "ShipButton");
            yield return Still("big_numbers", keepCursor: true);
            yield return Wait(0.6f);
            Cut();

            // --- Sam, close up -------------------------------------------------------------------------------------------
            _gm.Cam.SetMode(CamMode.Office, 0.01f);
            CursorVisible = false;
            _gm.Cam.SetFixed(new Vector3(1.15f, 1.32f, -0.45f), new Vector3(0f, 1.0f, -0.05f));
            yield return Wait(1.0f);
            yield return Rec("sam");
            _gm.Employee.PlayOneShot(EmployeeController.Celebrate, 1.6f);
            yield return Wait(1.8f);
            _gm.Employee.PlayOneShot("Sip", 2.2f);
            yield return Wait(2.4f);
            Cut();
        }

        // ================================================================== game states
        void Fresh()
        {
            M.Load(new GameState());
            M.State.introSeen = true;
            M.RandomEventsEnabled = false;
            _gm.Computer.ResetSession();
            _gm.Office.Refresh(false);
        }

        /// <summary>Day 3: a mug, a duck and a clicky keyboard, an ask that is nearly done.</summary>
        void EarlyGame()
        {
            Fresh();
            var s = M.State;
            s.day = 3;
            s.lifetimeEarned = 4000;
            s.handmadeTotal = 2500;
            s.clicks = 900;
            s.stars = 2;
            s.titleIndex = 1;
            s.agentCounts[0] = 6;
            s.agentCounts[1] = 2;
            M.MarkDirty();
            foreach (var id in new[] { "mug", "duck", "mech_keyboard" }) { s.credits = 1e5; M.BuyOffice(id); }
            foreach (var id in new[] { "vim", "muscle_memory" }) { s.credits = 1e5; M.BuyUpgrade(id); }
            foreach (var id in new[] { "dana_welcome", "ceo_mandate", "gary_keys", "priya_hello" })
                if (!s.mail.Contains(id)) { s.mail.Add(id); s.mailRead.Add(id); }
            s.mailRead.Remove("ceo_mandate");
            s.credits = 180;
            s.Phase = GamePhase.Night;
            M.StartNextDay();
            s.dayMinutes = 95;
            s.quotaToday = 2400;
            M.GenerateAsks();
            if (s.asks.Count > 0 && s.asks[0].id == "ship") s.handmadeToday = s.asks[0].target * 0.8;
            _gm.Office.Refresh(false);
        }

        /// <summary>Mid game with a dozen affordable upgrades waiting.</summary>
        void MidGameUpgrades()
        {
            var s = M.State;
            int[] counts = { 30, 26, 14, 12, 4, 0, 0, 0, 0, 0 };
            for (int i = 0; i < counts.Length; i++) s.agentCounts[i] = counts[i];
            s.lifetimeEarned = 1.5e6;
            s.titleIndex = 2;
            M.MarkDirty();
            _gm.Office.Refresh(false);
            s.credits = 2.5e5;
        }

        void Endless()
        {
            var s = M.State;
            s.factoryBuilt = true;
            s.endingSeen = true;
            s.lifetimeEarned = s.allTimeEarned = 4e16;
            s.credits = 2.2e16;
            int[] frontier = { 400, 300, 220, 160, 140, 120, 100, 80, 60, 40, 30, 22, 12, 6, 2, 1, 0, 0, 0, 0 };
            for (int i = 0; i < frontier.Length; i++) s.agentCounts[i] = frontier[i];
            M.MarkDirty();
            M.CheckAchievements();
            s.Phase = GamePhase.Working;
            s.dayMinutes = 100;
        }

        void BigNumbers()
        {
            var s = M.State;
            s.factoryBuilt = true;
            s.endingSeen = true;
            s.lifetimeEarned = s.allTimeEarned = 3.7e45;
            s.credits = 1.9e44;
            s.optionsEarned = 1.5e11;
            s.reorgs = 23;
            s.titleIndex = 12;
            for (int i = 0; i < s.agentCounts.Length; i++) s.agentCounts[i] = 650 - i * 25;
            foreach (var u in GameDatabase.Upgrades)
                if (u.Kind != UpgradeKind.AgentTier || u.Tier <= 13) s.upgrades.Add(u.Id);
            foreach (var id in new[] { "mug", "duck", "mech_keyboard", "headphones" }) if (!s.office.Contains(id)) s.office.Add(id);
            s.dayMinutes = 100;
            M.Load(s);
            M.State.Phase = GamePhase.Working;
            M.CheckAchievements();
            _gm.Office.Refresh(false);
        }

        /// <summary>Straight to the CorpOS desktop in monitor view.</summary>
        IEnumerator ToDesktop()
        {
            _gm.Menu.HideAll();
            _gm.Overlay.HideDayCard();
            _gm.Overlay.HideNight();
            if (M.Phase != GamePhase.Working)
            {
                if (M.Phase != GamePhase.Login) M.State.Phase = GamePhase.Login;
                _gm.Login();
            }
            _gm.Computer.ShowDesktop();
            _gm.Cam.SetMode(CamMode.Monitor, 0.01f);
            yield return Wait(0.8f);
        }

        // ================================================================== camera
        IEnumerator MonitorView()
        {
            _gm.Cam.SetMode(CamMode.Monitor, 0.01f);
            yield return null;
            yield return null;
        }

        /// <summary>Glide toward a point of the CorpOS screen (viewport coords of the full monitor view).</summary>
        IEnumerator Zoom(Vector2 viewport, float zoom, float seconds)
        {
            var cam = _gm.Refs.MainCamera;
            _gm.Cam.SetMode(CamMode.Monitor, 0.01f);
            yield return null;
            yield return null;
            SceneRefs.ScreenFrame(_gm.Refs.MainScreen, out var center, out var frame, out _);
            Vector3 normal = frame * Vector3.back;
            var ray = cam.ViewportPointToRay(new Vector3(viewport.x, viewport.y, 0));
            new Plane(normal, center).Raycast(ray, out float enter);
            Vector3 focus = ray.GetPoint(enter);
            float dist = Vector3.Distance(cam.transform.position, center);
            Vector3 fromPos = cam.transform.position, fromLook = center;
            Vector3 toPos = focus + normal * (dist / zoom);
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0, 1, t / seconds);
                _gm.Cam.SetFixed(Vector3.Lerp(fromPos, toPos, k), Vector3.Lerp(fromLook, focus, k));
                yield return null;
            }
            // from Monitor mode SetFixed cuts straight there; from Fixed it eases in, so let it settle before clicking
            _gm.Cam.SetFixed(toPos, focus);
            yield return Wait(0.6f);
        }

        IEnumerator Orbit(float yaw0, float yaw1, float pitch0, float pitch1, float dist0, float dist1, float seconds, Vector3 offset)
        {
            Vector3 pivot = _gm.Refs.OfficeViewPivot.position + offset;
            for (float t = 0; ; t += Time.deltaTime)
            {
                float k = Mathf.Clamp01(t / seconds);
                var rot = Quaternion.Euler(Mathf.Lerp(pitch0, pitch1, k), Mathf.Lerp(yaw0, yaw1, k), 0f);
                _gm.Cam.SetFixed(pivot - rot * Vector3.forward * Mathf.Lerp(dist0, dist1, k), pivot);
                yield return null;
            }
        }

        IEnumerator Dolly(Vector3 p0, Vector3 p1, Vector3 look0, Vector3 look1, float seconds)
        {
            for (float t = 0; ; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / seconds));
                _gm.Cam.SetFixed(Vector3.Lerp(p0, p1, k), Vector3.Lerp(look0, look1, k));
                yield return null;
            }
        }

        /// <summary>A slow camera move through the office, recorded as one clip.</summary>
        IEnumerator OfficeMove(string clip, float seconds, float yaw0 = -42f, float yaw1 = -12f, float pitch = 20f, float dist = 3.0f, string still = null)
        {
            StopCam();
            var rot = Quaternion.Euler(pitch, yaw0, 0f);
            Vector3 pivot = _gm.Refs.OfficeViewPivot.position;
            _gm.Cam.SetFixed(pivot - rot * Vector3.forward * dist, pivot);
            yield return Wait(0.6f);
            _camMove = StartCoroutine(Orbit(yaw0, yaw1, pitch, pitch - 3f, dist, dist - 0.25f, seconds, Vector3.zero));
            yield return Rec(clip);
            yield return Wait(seconds * 0.5f);
            if (still != null) yield return Still(still);
            yield return Wait(seconds * 0.5f);
            Cut();
            StopCam();
        }

        void StopCam()
        {
            if (_camMove != null) StopCoroutine(_camMove);
            _camMove = null;
        }

        ScrollRect AgentList()
        {
            foreach (var rt in FindObjectsByType<RectTransform>())
                if (rt.name == "Agent0" && rt.gameObject.activeInHierarchy) return rt.GetComponentInParent<ScrollRect>();
            return null;
        }

        IEnumerator ScrollTo(ScrollRect sr, float target, float seconds)
        {
            if (sr == null) yield break;
            float from = sr.verticalNormalizedPosition;
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                sr.verticalNormalizedPosition = Mathf.Lerp(from, target, Mathf.SmoothStep(0, 1, t / seconds));
                yield return null;
            }
            sr.verticalNormalizedPosition = target;
        }

        // ================================================================== recording
        IEnumerator Rec(string clip)
        {
            if (StillsOnly) yield break;
            _video = gameObject.AddComponent<VideoCapture>();
            _video.OutputPath = Path.Combine(OutputDir, "clips", clip + ".mp4");
            _video.Begin();
            Debug.Log("[Trailer] rec " + clip);
            yield return null;
        }

        void Cut()
        {
            if (_video == null) return;
            _video.End();
            Destroy(_video);
            _video = null;
        }

        /// <summary>Saves a PNG in the stills pass (the trailer pass ignores it, so clips never lose the cursor).</summary>
        IEnumerator Still(string name, bool keepCursor = false)
        {
            if (!StillsOnly) yield break;
            bool cursor = CursorVisible;
            if (!keepCursor) CursorVisible = false;
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(OutputDir, "stills", name + ".png"));
            yield return null;
            yield return null;
            CursorVisible = cursor;
            Debug.Log("[Trailer] still " + name);
        }

        /// <summary>
        /// The game's own lo-fi loop, rendered with a few seeds so the trailer bed doesn't repeat, plus the
        /// synthesised stingers the edit uses on the title and end cards.
        /// </summary>
        void ExportMusic()
        {
            const int rate = 44100;
            foreach (int seed in new[] { 7, 11, 23, 42 })
                WriteWav(Path.Combine(OutputDir, $"music_{seed}.wav"), LofiMusic.Render(rate, seed), rate);
            foreach (var sound in new[] { Sound.Chapter, Sound.Whoosh, Sound.Promotion, Sound.Trophy, Sound.BigBuy })
            {
                var clip = _gm.Sfx.Clip(sound);
                if (clip == null) continue;
                var data = new float[clip.samples * clip.channels];
                clip.GetData(data, 0);
                WriteWav(Path.Combine(OutputDir, $"sfx_{sound.ToString().ToLowerInvariant()}.wav"), data, clip.frequency);
            }
            Debug.Log($"[Trailer] music bpm {LofiMusic.Bpm}");
        }

        static void WriteWav(string path, float[] data, int rate)
        {
            using var w = new BinaryWriter(File.Create(path));
            w.Write(new[] { 'R', 'I', 'F', 'F' });
            w.Write(36 + data.Length * 2);
            w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
            w.Write(16);
            w.Write((short)1);
            w.Write((short)1);
            w.Write(rate);
            w.Write(rate * 2);
            w.Write((short)2);
            w.Write((short)16);
            w.Write(new[] { 'd', 'a', 't', 'a' });
            w.Write(data.Length * 2);
            foreach (var v in data) w.Write((short)(Mathf.Clamp(v, -1f, 1f) * 32767));
        }
    }
}
