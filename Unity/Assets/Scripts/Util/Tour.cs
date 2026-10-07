using System.Collections;
using System.IO;
using AgentClicker.Core;
using AgentClicker.Office;
using AgentClicker.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace AgentClicker
{
    /// <summary>
    /// Automated screenshot tour for visual checks and store screenshots:
    ///   AgentClicker.x86_64 -tour /tmp/shots [-screen-width 1600 -screen-height 900]
    /// Plays through every phase of the game with a throwaway save and quits.
    /// </summary>
    public class Tour : MonoBehaviour
    {
        public string OutputDir;
        GameManager _gm;
        GameModel M => _gm.Model;

        IEnumerator Start()
        {
            _gm = GetComponent<GameManager>();
            Directory.CreateDirectory(OutputDir);
            _gm.Settings.autoOpenStoryMail = false;
            _gm.Settings.tutorialTips = false;
            // the tour's mouse events are synthetic: deliver them even if another window has the desktop's focus
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            yield return new WaitForSeconds(1.0f);
            Debug.Log($"[Tour] window focused: {Application.isFocused}");

            // ---- menus & intro ------------------------------------------------
            _gm.ShowTitle();
            yield return new WaitForSeconds(2.5f);
            yield return Shot("00a_title");
            _gm.Menu.OpenSettings(() => { });
            yield return new WaitForSeconds(0.6f);
            yield return Shot("00b_settings");
            CheckHintsFit("graphics");
            _gm.Menu.SetSettingsTab(MenuUI.SettingsTab.Gameplay);
            yield return new WaitForSeconds(0.4f);
            yield return Shot("00b2_settings_gameplay");
            CheckHintsFit("gameplay");
            _gm.Menu.SetSettingsTab(MenuUI.SettingsTab.Controls);
            yield return new WaitForSeconds(0.4f);
            yield return Shot("00b3_settings_controls");
            _gm.Menu.SetSettingsTab(MenuUI.SettingsTab.Graphics);
            _gm.Menu.HideAll();
            _gm.Menu.ShowHowToPlay();
            yield return new WaitForSeconds(0.5f);
            yield return Shot("00b4_how_to_play");
            var (size, overflowing) = _gm.Menu.InfoTextFit();
            Debug.Log(!overflowing && size >= 16f ? $"[Tour] PASS How to Play fits at {size:0.#} pt"
                                                  : $"[Tour] FAIL How to Play at {size:0.#} pt, overflowing {overflowing}");
            _gm.Menu.HideAll();
            _gm.Menu.ShowStoryCards(StoryDatabase.Intro(), null);
            yield return new WaitForSeconds(3.5f);
            yield return Shot("00c_intro_card");
            _gm.Menu.HideAll();
            _gm.BeginMorning(false);
            yield return new WaitForSeconds(2.0f);
            yield return Shot("01_morning_login");

            // Real input: click the monitor (login screen) with genuine Input System mouse events.
            var screenCenter = _gm.Refs.MainCamera.WorldToScreenPoint(_gm.Refs.MainScreen.bounds.center);
            yield return RealClick(screenCenter);
            yield return new WaitForSeconds(1.0f);
            Debug.Log(M.Phase == GamePhase.Working ? "[Tour] PASS real click on monitor logs in" : "[Tour] FAIL real click on monitor did not log in");
            if (M.Phase != GamePhase.Working) _gm.Login();
            yield return new WaitForSeconds(1.8f);

            // Real input: click SHIP CODE through the world-space canvas.
            long before = M.State.clicks;
            var rt = _gm.Computer.ShipButton;
            var shipScreen = _gm.Refs.MainCamera.WorldToScreenPoint(rt.TransformPoint(rt.rect.center));
            for (int i = 0; i < 5; i++) yield return RealClick(shipScreen);
            Debug.Log(M.State.clicks >= before + 5
                ? $"[Tour] PASS real clicks on SHIP CODE ({M.State.clicks - before})"
                : $"[Tour] FAIL real clicks on SHIP CODE ({M.State.clicks - before}/5)");
            for (int i = 0; i < 20; i++) { _gm.Ship(); yield return null; }
            yield return new WaitForSeconds(0.3f);
            yield return Shot("02_desktop_day1");
            LogGoal("day 1");
            yield return MuteSegment();
            M.DeliverNextMail();
            M.DeliverNextMail();
            _gm.Computer.ShowInbox(null);
            yield return new WaitForSeconds(0.6f);
            yield return Shot("02b_inbox");
            _gm.Computer.CloseModal();

            // ---- a phone call ----------------------------------------------------
            for (int i = 0; i < 30; i++) { _gm.Ship(); yield return null; }
            M.RingPhone(CallDatabase.ById("dana_demo"));
            yield return new WaitForSeconds(1.2f);
            yield return Shot("02c_call_incoming");
            M.AnswerCall();
            yield return new WaitForSeconds(2.5f);
            yield return Shot("02d_call_dialogue");
            M.ChooseCallOption(2);
            yield return new WaitForSeconds(2.0f);
            yield return Shot("02e_call_response");
            yield return new WaitForSeconds(3.5f);

            // ---- mid game -------------------------------------------------
            _gm.SuppressShowcase = true;
            var s = M.State;
            s.day = 4;
            s.credits = 2e7;
            s.lifetimeEarned = 3e7;
            s.handmadeTotal = 2e5;
            s.clicks = 4000;
            s.stars = 3;
            int[] counts = { 40, 30, 22, 12, 6, 3, 0, 0, 0, 0 };
            for (int i = 0; i < counts.Length; i++) s.agentCounts[i] = counts[i];
            M.MarkDirty();
            foreach (var id in new[] { "mug", "duck", "plant", "mech_keyboard", "monitor2", "headphones", "lava_lamp", "whiteboard" })
                M.BuyOffice(id);
            foreach (var u in GameDatabase.Upgrades)
                if (u.Cost < 2e5) M.BuyUpgrade(u.Id);
            s.credits = 6.3e6;
            s.dayMinutes = 150;
            s.earnedToday = 1.2e6;
            s.quotaToday = 2e6;
            _gm.Office.Refresh(false);
            yield return new WaitForSeconds(1.0f);
            M.SpawnDrop();
            yield return new WaitForSeconds(0.5f);
            yield return Shot("03_desktop_midgame");
            LogGoal("mid game");
            // the chapter banner waits for the drop; claiming it lets the banner through
            M.ClaimDrop();
            yield return new WaitForSeconds(1.2f);
            yield return Shot("03b_chapter_banner");
            // hovering an agent you can't afford yet says roughly when you can
            var row = _gm.Computer.Store.AgentRowRect(6);
            yield return RealMove(_gm.Refs.MainCamera.WorldToScreenPoint(row.TransformPoint(row.rect.center)));
            yield return new WaitForSeconds(0.5f);
            string foot = _gm.Computer.Store.InfoFoot;
            Debug.Log(foot.Contains("about ") ? $"[Tour] PASS store hover estimates when the DevOps Agent is affordable: {foot}"
                                              : $"[Tour] FAIL store hover has no estimate: {foot}");
            yield return Shot("03c_store_hover_eta");
            yield return new WaitForSeconds(3.5f);
            _gm.Computer.SelectStoreTab(2);
            yield return new WaitForSeconds(0.4f);
            yield return Shot("04_store_office");
            _gm.Computer.SelectStoreTab(0);

            _gm.Cam.SetMode(CamMode.Office, 0.2f);
            yield return new WaitForSeconds(1.5f);
            yield return Shot("05_office_midgame");
            // A/B: quality presets (High has SSAO, Medium doesn't)
            _gm.Settings.quality = 1;
            _gm.ApplySettings(false);
            yield return new WaitForSeconds(0.5f);
            yield return Shot("05b_office_medium");
            _gm.Settings.quality = 2;
            _gm.ApplySettings(false);
            yield return ReduceMotionSegment();

            // ---- late game, evening ---------------------------------------
            s.day = 22;
            s.lifetimeEarned = 2e11;
            s.credits = 5e11;
            int[] late = { 150, 120, 100, 100, 80, 60, 40, 25, 8, 2 };
            for (int i = 0; i < late.Length; i++) s.agentCounts[i] = late[i];
            M.MarkDirty();
            foreach (var o in GameDatabase.OfficeItems)
                if (o.Id != "recliner") { s.credits = 1e12; M.BuyOffice(o.Id); }
            s.titleIndex = 5;
            s.dayMinutes = 8.6f * 60;
            _gm.Office.Refresh(false);
            yield return new WaitForSeconds(1.5f);
            yield return Shot("06_office_lategame_evening");

            // ---- animation close-ups -------------------------------------------
            _gm.Cam.SetFixed(new Vector3(1.15f, 1.32f, -0.45f), new Vector3(0f, 1.0f, -0.05f));
            foreach (var (state, wait) in new[] { ("Typing", 0.5f), ("Sip", 1.6f), ("Think", 1.2f), ("Facepalm", 1.0f),
                                                  ("LookAround", 1.0f), ("Relax", 1.2f), ("Celebrate", 0.45f) })
            {
                _gm.Employee.PlayOneShot(state, 30f);
                yield return new WaitForSeconds(wait);
                yield return Shot("pose_" + state);
            }
            _gm.Employee.PlayOneShot("Idle", 0.01f);
            _gm.Menu.OpenPause();
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("06b_pause");
            _gm.Menu.ClosePause();
            _gm.Cam.SetMode(CamMode.Monitor, 0.2f);
            M.StartOutage();
            yield return new WaitForSeconds(1.0f);
            yield return Shot("07_desktop_lategame_outage");
            LogGoal("late game");

            // ---- review & night ----------------------------------------------
            M.ClockOut();
            yield return new WaitForSeconds(0.8f);
            yield return Shot("08_review");
            _gm.Computer.CloseModal();
            _gm.GoHome();
            yield return new WaitForSeconds(1.5f);
            yield return Shot("09_night");
            _gm.ClockIn();
            yield return new WaitForSeconds(2.5f);
            yield return Shot("10_morning_day23");

            // ---- autopilot: nobody touches the keyboard from 5 PM to the next morning ----------
            yield return AutopilotSegment();
            yield return OfflineSegment();

            // ---- the ending ---------------------------------------------------
            _gm.Login();
            s.credits = 1e13;
            s.agentCounts[GameDatabase.OrchestratorIndex] = 12;
            M.MarkDirty();
            M.BuyOffice("recliner");
            _gm.Office.Refresh(false);
            yield return GoalCardSegment();
            yield return new WaitForSeconds(1.5f);
            yield return Shot("11_factory_tab");
            _gm.BuildFactory();
            yield return new WaitForSeconds(1.2f);
            yield return Shot("12_factory_boot");
            yield return new WaitForSeconds(9.5f);
            yield return Shot("13_ending");
            _gm.PlayEpilogue();
            yield return new WaitForSeconds(3.0f);
            yield return Shot("13b_epilogue");
            _gm.Menu.HideAll();
            _gm.KeepPlaying();
            yield return new WaitForSeconds(2.5f);
            yield return Shot("14_endless_office");

            // ---- endless: frontier agents, trophies, reorg, board room -------
            _gm.Cam.SetMode(CamMode.Monitor, 0.2f);
            if (M.Phase != GamePhase.Working) { M.State.Phase = GamePhase.Working; _gm.Computer.ShowDesktop(); }
            s.lifetimeEarned = s.allTimeEarned = 4e16;
            s.credits = 2.2e16;
            int[] frontier = { 400, 300, 220, 160, 140, 120, 100, 80, 60, 40, 30, 22, 12, 6, 2, 1, 0, 0, 0, 0 };
            for (int i = 0; i < frontier.Length; i++) s.agentCounts[i] = frontier[i];
            M.MarkDirty();
            M.CheckAchievements();
            _gm.Computer.SelectStoreTab(0);
            yield return new WaitForSeconds(1.2f);
            yield return Shot("15_frontier_agents");
            LogGoal("after the Factory");
            // three toasts, two of them two lines long, stacked on a full activity feed
            _gm.Computer.Toast("Promoted to Board Member! You get a parking spot. You don't drive. The agents park there now.", Theme.Accent2, 8f);
            _gm.Computer.Toast("★ TROPHY  Millionaire  Earn 1 million credits, across every division.", Theme.Gold, 8f);
            _gm.Computer.Toast("✉ New email from Brenda Lowe: <b>PTO requests from your \"team\"</b>", Theme.Accent, 8f);
            yield return new WaitForSeconds(1.0f);
            yield return Shot("15c_toasts_over_feed");
            CheckToasts();
            _gm.Computer.SelectStoreTab(1);
            yield return new WaitForSeconds(0.6f);
            yield return Shot("15b_upgrades_endless");
            _gm.Computer.SelectStoreTab(StorePanel.TrophiesTabIndex);
            yield return new WaitForSeconds(0.6f);
            yield return Shot("16_trophies");
            _gm.Computer.SelectStoreTab(StorePanel.StatsTabIndex);
            yield return new WaitForSeconds(0.6f);
            yield return Shot("17_stats");
            _gm.Computer.ShowCareer(false);
            yield return new WaitForSeconds(0.6f);
            yield return Shot("18_reorg_tab");
            _gm.RequestReorg();
            yield return new WaitForSeconds(0.8f);
            yield return Shot("19_reorg_confirm");
            _gm.Menu.HideAll();
            _gm.ReorgNow();
            yield return new WaitForSeconds(3.5f);
            yield return Shot("20_reorg_memo");
            _gm.Menu.SkipCards();
            yield return new WaitForSeconds(2.2f);
            yield return Shot("21_division2_morning");
            _gm.Login();
            yield return new WaitForSeconds(1.6f);
            _gm.Computer.CloseModal();
            M.BuyPerk("starter_kit");
            M.BuyPerk("deep_work");
            _gm.Computer.ShowCareer(true);
            yield return new WaitForSeconds(0.8f);
            yield return Shot("22_board_room");

            // ---- hundreds of hours in: enormous numbers ---------------------------
            s.factoryBuilt = true;
            s.lifetimeEarned = s.allTimeEarned = 3.7e45;
            s.credits = 1.9e44;
            s.optionsEarned = 1.5e11;
            s.reorgs = 23;
            for (int i = 0; i < s.agentCounts.Length; i++) s.agentCounts[i] = 650 - i * 25;
            foreach (var u in GameDatabase.Upgrades)
                if (u.Kind != UpgradeKind.AgentTier || u.Tier <= 13) s.upgrades.Add(u.Id);
            M.Load(s);
            M.State.Phase = GamePhase.Working;
            M.CheckAchievements();
            _gm.Computer.ShowDesktop();
            _gm.Computer.SelectStoreTab(0);
            yield return new WaitForSeconds(1.5f);
            yield return Shot("23_endgame_numbers");
            LogGoal("every agent owned");
            _gm.Computer.SelectStoreTab(StorePanel.TrophiesTabIndex);
            yield return new WaitForSeconds(0.6f);
            yield return Shot("24_endgame_trophies");

            yield return GamepadSegment();
            yield return TouchSegment();

            Debug.Log(_screenMinAll >= MinScreenText
                ? $"[Tour] PASS menus and overlays never draw text below {MinScreenText} pt (smallest {_screenMinAll:0.#}: {_screenMinWhat})"
                : $"[Tour] FAIL menus and overlays draw {_screenMinAll:0.#} pt text: {_screenMinWhat}");
            Debug.Log(_monitorSmall.Count == 0
                ? $"[Tour] PASS the CorpOS monitor never draws text below {MinMonitorText} pt (smallest {_monitorMinAll:0.#}: {_monitorMinWhat})"
                : $"[Tour] FAIL the CorpOS monitor draws text below {MinMonitorText} pt: {string.Join(" | ", _monitorSmall.Values)}");
            Debug.Log("[Tour] done");
            Application.Quit();
        }

        /// <summary>Reduce motion: camera moves are cuts and pulsing UI holds still.</summary>
        IEnumerator ReduceMotionSegment()
        {
            // control run with normal motion: the camera is still flying two frames in, and the drop card pulses
            M.SpawnDrop();
            _gm.Cam.SetMode(CamMode.Monitor, 1.1f);
            yield return null;
            yield return null;
            float flying = _gm.Cam.DistanceToTarget();
            int movingNormally = 0;
            yield return CountMovingPulses(n => movingNormally = n);
            Debug.Log($"[Tour] control: normal motion leaves the camera {flying:0.000} m off after two frames, {movingNormally} pulsing elements moving");
            _gm.Cam.SetMode(CamMode.Office, 0.01f);
            yield return new WaitForSeconds(0.3f);

            _gm.Settings.reduceMotion = true;
            _gm.ApplySettings(false);
            _gm.Cam.SetMode(CamMode.Monitor, 1.1f);
            yield return null;
            yield return null;
            float off = _gm.Cam.DistanceToTarget();
            Debug.Log(off < 0.01f && flying > 0.05f ? $"[Tour] PASS reduce motion: camera cut to the monitor ({off:0.0000} m off)"
                                                    : $"[Tour] FAIL reduce motion: camera {off:0.000} m from the monitor (control {flying:0.000})");
            int moving = -1;
            yield return CountMovingPulses(n => moving = n);
            Debug.Log(moving == 0 && movingNormally > 0 ? $"[Tour] PASS reduce motion: pulsing elements hold still ({movingNormally} moved without it)"
                                                        : $"[Tour] FAIL reduce motion: {moving} pulsing elements moved (control {movingNormally})");
            yield return Shot("05c_reduce_motion_drop");
            M.ClaimDrop();
            _gm.Settings.reduceMotion = false;
            _gm.ApplySettings(false);
            _gm.Cam.SetMode(CamMode.Office, 0.2f);
            yield return new WaitForSeconds(0.6f);
        }

        static IEnumerator CountMovingPulses(System.Action<int> result)
        {
            var pulses = FindObjectsByType<Pulse>(FindObjectsSortMode.None);
            var before = new Vector3[pulses.Length];
            for (int i = 0; i < pulses.Length; i++) before[i] = pulses[i].transform.localScale;
            yield return new WaitForSeconds(0.4f);
            int moved = 0;
            for (int i = 0; i < pulses.Length; i++) if (pulses[i] && pulses[i].transform.localScale != before[i]) moved++;
            result(moved);
        }

        IEnumerator AutopilotSegment()
        {
            var ap = _gm.Autopilot;
            var defaults = new DayAutopilot();
            _gm.Settings.autopilotDay = true;
            _gm.AutopilotAllowed = true;
            M.RandomEventsEnabled = false; // no phone call in the middle of the check
            _gm.Away.MinSeconds = 3;       // the real threshold is 90 s; the tour can't wait that long
            ap.ClockOutAfter = 3f; ap.GoHomeAfter = 3f; ap.ClockInAfter = 2f; ap.LogInAfter = 2f;
            _gm.Login();
            yield return new WaitForSeconds(1.5f);
            M.State.dayMinutes = GameDatabase.WorkdayMinutes - 1f;
            int day = M.State.day;
            yield return WaitFor(() => _gm.Computer.ModalOpen, 5f);
            yield return Shot("10a_autopilot_five_pm");
            yield return WaitFor(() => M.Phase == GamePhase.Review, 8f);
            yield return new WaitForSeconds(0.8f);
            yield return Shot("10b_autopilot_review");
            Debug.Log(M.Phase == GamePhase.Review ? "[Tour] PASS autopilot clocked out at 5 PM" : $"[Tour] FAIL autopilot did not clock out ({M.Phase})");
            yield return WaitFor(() => M.Phase == GamePhase.Night, 8f);
            yield return new WaitForSeconds(1.0f);
            yield return Shot("10c_autopilot_night");
            Debug.Log(M.Phase == GamePhase.Night ? "[Tour] PASS autopilot went home" : $"[Tour] FAIL autopilot did not go home ({M.Phase})");
            yield return WaitFor(() => M.Phase == GamePhase.Working, 12f);
            yield return new WaitForSeconds(2.0f);
            yield return Shot("10d_autopilot_logged_in");
            Debug.Log(M.Phase == GamePhase.Working && M.State.day == day + 1
                ? $"[Tour] PASS autopilot clocked in and logged in (day {M.State.day})"
                : $"[Tour] FAIL autopilot did not start the next day ({M.Phase}, day {M.State.day})");
            _gm.PlayerReturned(); // as if the mouse moved
            yield return WaitFor(() => _gm.Computer.ModalOpen, 3f);
            yield return new WaitForSeconds(0.6f);
            yield return Shot("10e_away_report");
            Debug.Log(_gm.Computer.ModalOpen ? "[Tour] PASS away report shown on return" : "[Tour] FAIL no away report on return");
            _gm.Computer.CloseModal();
            _gm.Away.MinSeconds = 90;
            _gm.AutopilotAllowed = false;
            M.RandomEventsEnabled = true;
            ap.ClockOutAfter = defaults.ClockOutAfter; ap.GoHomeAfter = defaults.GoHomeAfter;
            ap.ClockInAfter = defaults.ClockInAfter; ap.LogInAfter = defaults.LogInAfter;
        }

        /// <summary>
        /// Time the game didn't run: two hours closed shows the card with what was credited and that the second hour didn't
        /// count, ten minutes paused doesn't mention the cap, and thirty seconds shows nothing.
        /// </summary>
        IEnumerator OfflineSegment()
        {
            M.RandomEventsEnabled = false;
            if (_gm.Computer.ModalOpen) _gm.Computer.CloseModal();
            yield return new WaitForSeconds(0.5f);

            double before = M.State.credits;
            double gain = _gm.CreditOffline(2 * 3600, paused: false), credited = M.State.credits - before;
            yield return WaitFor(() => _gm.Computer.ModalOpen, 3f);
            yield return new WaitForSeconds(0.6f);
            yield return Shot("10g_offline_report");
            string text = _gm.Computer.LastAwayReportText;
            bool ok = _gm.Computer.ModalOpen && gain > 0 && System.Math.Abs(credited - gain) < 1e-6 * gain
                      && text.Contains("Your agents kept working") && text.Contains("Game closed") && text.Contains("2h 00m")
                      && text.Contains("+" + NumberFormat.Credits(gain)) && text.Contains("Only the first 1 hour counted; the other 1h 00m didn't.");
            Debug.Log(ok ? $"[Tour] PASS offline: two hours closed shows the card (+{NumberFormat.Credits(gain)}, the second hour didn't count)"
                         : $"[Tour] FAIL offline: two hours closed (modal {_gm.Computer.ModalOpen}, gain {gain}, credited {credited}): {text.Replace("\n", " / ")}");
            _gm.Computer.CloseModal();
            yield return new WaitForSeconds(0.4f);

            gain = _gm.CreditOffline(600, paused: true);
            yield return WaitFor(() => _gm.Computer.ModalOpen, 3f);
            text = _gm.Computer.LastAwayReportText;
            ok = _gm.Computer.ModalOpen && text.Contains("Game paused") && text.Contains("10m 00s") && text.Contains("10% for up to 1 hour")
                 && !text.Contains("Only the first");
            Debug.Log(ok ? "[Tour] PASS offline: ten minutes paused shows the card without the cap note"
                         : $"[Tour] FAIL offline: ten minutes paused (modal {_gm.Computer.ModalOpen}): {text}");
            _gm.Computer.CloseModal();
            yield return new WaitForSeconds(0.4f);

            gain = _gm.CreditOffline(30, paused: false);
            yield return new WaitForSeconds(1.5f);
            Debug.Log(!_gm.Computer.ModalOpen && gain == 0
                ? "[Tour] PASS offline: thirty seconds shows no card (gaps under a minute earn nothing offline)"
                : $"[Tour] FAIL offline: thirty seconds (modal {_gm.Computer.ModalOpen}, credited {gain})");
            if (_gm.Computer.ModalOpen) _gm.Computer.CloseModal();

            // idle days and closed time in one card (the real thresholds are too long to reach in the tour)
            var both = new AwaySummary { Days = 3, Seconds = 900, Credits = 4.2e9, QuotasMet = 2, QuotasMissed = 1, CallsMissed = 2,
                                         FromDay = M.State.day - 3, ToDay = M.State.day };
            var closed = new AwaySummary();
            closed.AddOffline(3 * 3600 + 600, 6.4e9, M.OfflineEfficiency, M.OfflineCapSeconds, paused: false);
            both.MergeOffline(closed);
            _gm.Computer.ShowAwayReport(both);
            yield return new WaitForSeconds(0.6f);
            yield return Shot("10h_away_report_combined");
            _gm.Computer.CloseModal();
            M.RandomEventsEnabled = true;
        }

        void LogGoal(string when)
        {
            var g = NextGoal.Pick(M);
            Debug.Log($"[Tour] next goal, {when}: {g.Title} ({NumberFormat.Percent(g.Progress)})");
        }

        /// <summary>Everything the Factory needs is in place: the card says so, and a real click on it opens the Factory tab.</summary>
        /// <summary>A real click on the top bar's ♪ button mutes everything and keeps it in the settings; M turns it back on.</summary>
        IEnumerator MuteSegment()
        {
            var st = _gm.Settings;
            if (st.muted) _gm.ToggleMute(); // a throwaway prefs file left muted by an interrupted run
            float master = st.masterVolume;
            var b = _gm.Computer.SoundButton;
            yield return RealClick(_gm.Refs.MainCamera.WorldToScreenPoint(b.TransformPoint(b.rect.center)));
            bool saved = PlayerPrefs.GetString(GameSettings.PrefsKey, "").Contains("\"muted\":true");
            Debug.Log(st.muted && AudioListener.volume == 0f && saved && st.masterVolume == master
                ? $"[Tour] PASS a real click on ♪ mutes (listener 0, saved in the settings, master volume still {master:0.00})"
                : $"[Tour] FAIL ♪ click: muted {st.muted}, listener {AudioListener.volume:0.00}, saved {saved}, master {st.masterVolume:0.00}");
            yield return new WaitForSeconds(0.3f);
            yield return Shot("02a_muted");
            var kb = Keyboard.current ?? InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(kb, new KeyboardState(Key.M));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(kb, new KeyboardState());
            yield return null;
            Debug.Log(!st.muted && Mathf.Approximately(AudioListener.volume, master)
                ? $"[Tour] PASS M turns the sound back on (listener {AudioListener.volume:0.00})"
                : $"[Tour] FAIL M: muted {st.muted}, listener {AudioListener.volume:0.00}");
            if (st.muted) _gm.ToggleMute();
        }

        IEnumerator GoalCardSegment()
        {
            _gm.Computer.SelectStoreTab(0);
            yield return new WaitForSeconds(1.5f); // the camera settles on the monitor and a fleet refresh picks the goal
            var g = _gm.Computer.Goal;
            Debug.Log(g != null && g.Kind == GoalKind.Factory && g.Reached
                ? "[Tour] PASS next goal is the Factory, ready to build"
                : $"[Tour] FAIL next goal should be the ready Factory, was {g?.Title} ({g?.Kind}, reached {g?.Reached})");
            yield return Shot("10f_goal_factory_ready");
            var rt = _gm.Computer.GoalCard;
            yield return RealClick(_gm.Refs.MainCamera.WorldToScreenPoint(rt.TransformPoint(rt.rect.center)));
            yield return new WaitForSeconds(0.3f);
            Debug.Log(_gm.Computer.StoreTab == StorePanel.FactoryTabIndex
                ? "[Tour] PASS clicking the next goal card opens the Factory tab"
                : $"[Tour] FAIL clicking the next goal card left the store on tab {_gm.Computer.StoreTab}");
            if (_gm.Computer.StoreTab != StorePanel.FactoryTabIndex) _gm.Computer.SelectStoreTab(StorePanel.FactoryTabIndex);
        }

        /// <summary>
        /// A gamepad alone plays the game: a virtual Input System Gamepad (state events, as a real one would send) moves
        /// the cursor, ships code, hires, takes a call, opens and closes the pause menu, switches views and holds off the
        /// day autopilot; moving the real mouse hands control back.
        /// </summary>
        IEnumerator GamepadSegment()
        {
            M.RandomEventsEnabled = false;
            _gm.SuppressShowcase = true;
            if (M.Phase != GamePhase.Working) _gm.Login();
            _gm.Computer.CloseModal();
            _gm.Computer.SelectStoreTab(0);
            _gm.Cam.SetMode(CamMode.Monitor, 0.01f);
            M.State.credits = 1e300;
            yield return new WaitForSecondsRealtime(1.0f);

            var pad = InputSystem.AddDevice<Gamepad>("TourGamepad");
            var cam = _gm.Refs.MainCamera;
            System.Func<RectTransform, Vector2> screenOf = r => cam.WorldToScreenPoint(r.TransformPoint(r.rect.center));
            void Send(GamepadState st) => InputSystem.QueueStateEvent(pad, st);
            IEnumerator Button(GamepadButton b)
            {
                Send(new GamepadState().WithButton(b, true));
                yield return null; yield return null;
                Send(new GamepadState());
                yield return null; yield return null;
            }
            IEnumerator StickTo(Vector2 target)
            {
                for (float t = 0; t < 8f; t += Time.unscaledDeltaTime)
                {
                    Vector2 d = target - _gm.Pad.Position;
                    if (_gm.Pad.Active && d.magnitude < 6f) break;
                    // push harder the further away, like a thumb would
                    Send(new GamepadState { leftStick = d.normalized * Mathf.Clamp(d.magnitude / (Screen.height * 0.25f), 0.3f, 1f) });
                    yield return null;
                }
                Send(new GamepadState());
                yield return null; yield return null;
            }

            var ship = screenOf(_gm.Computer.ShipButton);
            yield return StickTo(ship);
            float off = Vector2.Distance(_gm.Pad.Position, ship);
            Debug.Log(_gm.Pad.Active && off < 10f ? $"[Tour] PASS gamepad: the left stick moved the cursor onto SHIP CODE ({off:0} px off)"
                                                  : $"[Tour] FAIL gamepad: cursor active {_gm.Pad.Active}, {off:0} px from SHIP CODE");
            long clicks = M.State.clicks;
            for (int i = 0; i < 5; i++) yield return Button(GamepadButton.South);
            Debug.Log(M.State.clicks >= clicks + 5 ? $"[Tour] PASS gamepad: A on SHIP CODE shipped code ({M.State.clicks - clicks})"
                                                   : $"[Tour] FAIL gamepad: A on SHIP CODE ({M.State.clicks - clicks}/5)");
            clicks = M.State.clicks;
            for (int i = 0; i < 5; i++)
            {
                Send(new GamepadState { rightTrigger = 1f });
                yield return null; yield return null;
                Send(new GamepadState());
                yield return null; yield return null;
            }
            Debug.Log(M.State.clicks >= clicks + 5 ? $"[Tour] PASS gamepad: RT shipped code ({M.State.clicks - clicks})"
                                                   : $"[Tour] FAIL gamepad: RT ({M.State.clicks - clicks}/5)");

            int owned = M.State.agentCounts[0];
            yield return StickTo(screenOf(_gm.Computer.Store.AgentRowRect(0)));
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Shot("25_gamepad_cursor");
            yield return Button(GamepadButton.South);
            Debug.Log(M.State.agentCounts[0] > owned ? $"[Tour] PASS gamepad: A on the store hired ({owned} → {M.State.agentCounts[0]} Autocomplete)"
                                                     : "[Tour] FAIL gamepad: A on the store hired nothing");

            M.RingPhone(CallDatabase.ById("dana_demo"));
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot("26a_gamepad_call_incoming");
            int answered = M.State.callsAnswered;
            yield return Button(GamepadButton.North);
            yield return WaitFor(() => false, 3f); // the line types out before the replies take input
            yield return Shot("26_gamepad_call");
            yield return Button(GamepadButton.DpadUp);
            yield return new WaitForSecondsRealtime(0.3f);
            Debug.Log(M.State.callsAnswered == answered + 1 && M.ActiveCall == null
                ? "[Tour] PASS gamepad: Y answered the phone and the d-pad picked a reply"
                : $"[Tour] FAIL gamepad: call answered {M.State.callsAnswered - answered}, still active {M.ActiveCall != null}");
            yield return WaitFor(() => !_gm.Calls.Busy, 8f);
            yield return new WaitForSecondsRealtime(1.0f);

            yield return Button(GamepadButton.Start);
            yield return new WaitForSecondsRealtime(0.3f);
            bool paused = _gm.Menu.PauseOpen;
            yield return Shot("27_gamepad_pause");
            yield return Button(GamepadButton.East);
            yield return new WaitForSecondsRealtime(0.3f);
            Debug.Log(paused && !_gm.Menu.PauseOpen ? "[Tour] PASS gamepad: Start opened the pause menu and B closed it"
                                                    : $"[Tour] FAIL gamepad: pause opened {paused}, still open {_gm.Menu.PauseOpen}");

            yield return Button(GamepadButton.Select);
            yield return new WaitForSecondsRealtime(1.5f);
            bool office = _gm.Cam.Mode == CamMode.Office;
            var rot = cam.transform.rotation;
            Send(new GamepadState { rightStick = new Vector2(1f, 0f) });
            yield return new WaitForSecondsRealtime(0.6f);
            Send(new GamepadState());
            yield return new WaitForSecondsRealtime(0.6f);
            float turned = Quaternion.Angle(rot, cam.transform.rotation);
            yield return Shot("28_gamepad_office");
            yield return Button(GamepadButton.Select);
            yield return new WaitForSecondsRealtime(1.5f);
            Debug.Log(office && turned > 5f && _gm.Cam.Mode == CamMode.Monitor
                ? $"[Tour] PASS gamepad: View switched to the office and back, the right stick turned the camera {turned:0}°"
                : $"[Tour] FAIL gamepad: office {office}, turned {turned:0}°, back to {_gm.Cam.Mode}");

            // the day autopilot leaves a gamepad player alone, then takes over once they stop
            var ap = _gm.Autopilot;
            _gm.Settings.autopilotDay = true;
            _gm.AutopilotAllowed = true;
            ap.ClockOutAfter = 1.5f;
            M.State.dayMinutes = GameDatabase.WorkdayMinutes + 1f;
            _gm.Computer.CloseModal();
            for (float t = 0; t < 3f; t += Time.unscaledDeltaTime)
            {
                Send(new GamepadState { leftStick = new Vector2(Mathf.Sin(t * 6f), Mathf.Cos(t * 6f)) * 0.4f });
                yield return null;
            }
            Send(new GamepadState());
            bool held = M.Phase == GamePhase.Working;
            yield return WaitFor(() => M.Phase == GamePhase.Review, 6f);
            Debug.Log(held && M.Phase == GamePhase.Review
                ? "[Tour] PASS gamepad: the autopilot waited while the stick moved, then clocked out"
                : $"[Tour] FAIL gamepad: held {held}, then {M.Phase}");
            ap.ClockOutAfter = new DayAutopilot().ClockOutAfter;
            _gm.AutopilotAllowed = false;

            // the real mouse takes over again
            Mouse real = null;
            foreach (var d in InputSystem.devices)
                if (d is Mouse m && m.name != "GamepadCursor") { real = m; break; }
            real ??= InputSystem.AddDevice<Mouse>();
            InputSystem.QueueStateEvent(real, new MouseState { position = new Vector2(200, 200), delta = new Vector2(40, 30) });
            yield return null; yield return null; yield return null;
            Debug.Log(!_gm.Pad.Active && Mouse.current == real ? "[Tour] PASS gamepad: moving the mouse hid the cursor and gave the mouse back"
                                                               : $"[Tour] FAIL gamepad: cursor still active {_gm.Pad.Active}, current mouse {Mouse.current?.name}");
            InputSystem.RemoveDevice(pad);
            M.RandomEventsEnabled = true;
        }

        /// <summary>
        /// Touch: taps reach the UI like clicks, one finger drags the office camera, two fingers pinch to zoom and sit
        /// down, the store keeps a tapped item's details, prompts drop the key names, and the autopilot waits.
        /// </summary>
        IEnumerator TouchSegment()
        {
            M.RandomEventsEnabled = false;
            _gm.SuppressShowcase = true;
            _gm.Computer.CloseModal();
            M.State.dayMinutes = 120;
            M.State.Phase = GamePhase.Working;
            _gm.Computer.ShowDesktop();
            _gm.Computer.SelectStoreTab(0);
            _gm.Cam.SetMode(CamMode.Monitor, 0.01f);
            M.State.credits = 1e300;
            yield return new WaitForSecondsRealtime(1.0f);

            var ts = InputSystem.AddDevice<Touchscreen>("TourTouchscreen");
            var cam = _gm.Refs.MainCamera;
            System.Func<RectTransform, Vector2> screenOf = r => cam.WorldToScreenPoint(r.TransformPoint(r.rect.center));
            System.Func<RectTransform, Vector2> overlayOf = r => RectTransformUtility.WorldToScreenPoint(null, r.TransformPoint(r.rect.center));
            int nextId = 1;
            void Send(int id, Vector2 p, UnityEngine.InputSystem.TouchPhase ph) =>
                InputSystem.QueueStateEvent(ts, new TouchState { touchId = id, position = p, phase = ph, pressure = 1f });
            IEnumerator Tap(Vector2 p)
            {
                int id = nextId++;
                Send(id, p, UnityEngine.InputSystem.TouchPhase.Began);
                yield return null; yield return null;
                Send(id, p, UnityEngine.InputSystem.TouchPhase.Ended);
                yield return null; yield return null;
            }
            // fingers move together from a to b over the given frames
            IEnumerator Gesture(Vector2[] from, Vector2[] to, int frames)
            {
                int[] ids = new int[from.Length];
                for (int f = 0; f < from.Length; f++) { ids[f] = nextId++; Send(ids[f], from[f], UnityEngine.InputSystem.TouchPhase.Began); }
                yield return null;
                for (int i = 1; i <= frames; i++)
                {
                    for (int f = 0; f < from.Length; f++)
                        Send(ids[f], Vector2.Lerp(from[f], to[f], i / (float)frames), UnityEngine.InputSystem.TouchPhase.Moved);
                    yield return null;
                }
                for (int f = 0; f < from.Length; f++) Send(ids[f], to[f], UnityEngine.InputSystem.TouchPhase.Ended);
                yield return null; yield return null;
            }

            // a touch player's morning: tap the monitor to log in, then the first tap on SHIP CODE
            M.State.Phase = GamePhase.Login;
            _gm.Computer.ShowLogin();
            _gm.Cam.SetMode(CamMode.Office, 0.01f);
            yield return new WaitForSecondsRealtime(1.0f);
            RectTransform login = null;
            foreach (var r in FindObjectsByType<RectTransform>())
                if (r.name == "Login" && r.gameObject.activeInHierarchy) login = r;
            if (login != null) yield return Tap(screenOf(login));
            yield return WaitFor(() => M.Phase == GamePhase.Working, 6f);
            yield return new WaitForSecondsRealtime(3.0f);
            var ship = screenOf(_gm.Computer.ShipButton);
            long first = M.State.clicks;
            yield return Tap(ship);
            Debug.Log(M.Phase == GamePhase.Working && M.State.clicks == first + 1
                ? "[Tour] PASS touch: tapped the monitor to log in, and the first tap on SHIP CODE shipped"
                : $"[Tour] FAIL touch: logged in {M.Phase == GamePhase.Working}, first tap on SHIP CODE shipped {M.State.clicks - first}");
            M.State.credits = 1e300;

            long clicks = M.State.clicks;
            for (int i = 0; i < 5; i++) yield return Tap(ship);
            Debug.Log(M.State.clicks == clicks + 5 && _gm.Touch.Active
                ? $"[Tour] PASS touch: five taps on SHIP CODE shipped code five times ({M.State.clicks - clicks}), touch prompts on"
                : $"[Tour] FAIL touch: SHIP CODE {M.State.clicks - clicks}/5, touch active {_gm.Touch.Active}");

            // two fingers at once, like drumming on a phone
            clicks = M.State.clicks;
            int a = nextId++, b = nextId++;
            Send(a, ship + new Vector2(-30, 0), UnityEngine.InputSystem.TouchPhase.Began);
            Send(b, ship + new Vector2(30, 0), UnityEngine.InputSystem.TouchPhase.Began);
            yield return null; yield return null;
            Send(a, ship + new Vector2(-30, 0), UnityEngine.InputSystem.TouchPhase.Ended);
            Send(b, ship + new Vector2(30, 0), UnityEngine.InputSystem.TouchPhase.Ended);
            yield return null; yield return null;
            Debug.Log(M.State.clicks == clicks + 2 ? "[Tour] PASS touch: two fingers on SHIP CODE shipped twice"
                                                   : $"[Tour] FAIL touch: two fingers shipped {M.State.clicks - clicks}");

            int owned = M.State.agentCounts[0];
            yield return Tap(screenOf(_gm.Computer.Store.AgentRowRect(0)));
            yield return new WaitForSecondsRealtime(0.3f);
            string info = _gm.Computer.Store.InfoTitle;
            yield return Shot("29_touch_store_info");
            Debug.Log(M.State.agentCounts[0] > owned && info.StartsWith(GameDatabase.Agents[0].Name)
                ? $"[Tour] PASS touch: a tap hired ({owned} → {M.State.agentCounts[0]}) and the info panel kept \"{info}\" after the finger lifted"
                : $"[Tour] FAIL touch: hired {M.State.agentCounts[0] - owned}, info panel \"{info}\"");

            M.RingPhone(CallDatabase.ById("dana_demo"));
            yield return new WaitForSecondsRealtime(1.2f);
            string answerText = _gm.Calls.AnswerText;
            yield return Shot("30_touch_call_incoming");
            int answered = M.State.callsAnswered;
            yield return Tap(overlayOf(_gm.Calls.AnswerButton));
            yield return WaitFor(() => false, 3f);
            string hint = _gm.Calls.HintText;
            yield return Tap(overlayOf(_gm.Calls.ChoiceButton(0)));
            yield return new WaitForSecondsRealtime(0.3f);
            Debug.Log(M.State.callsAnswered == answered + 1 && M.ActiveCall == null && answerText == "ANSWER" && hint == "Tap a reply"
                ? "[Tour] PASS touch: tapped ANSWER and a reply; the prompts name no keys"
                : $"[Tour] FAIL touch: answered {M.State.callsAnswered - answered}, active {M.ActiveCall != null}, \"{answerText}\", \"{hint}\"");
            yield return WaitFor(() => !_gm.Calls.Busy, 8f);
            yield return new WaitForSecondsRealtime(1.0f);

            // the office: one finger looks around, unless it starts on a control; two fingers pinch
            _gm.Cam.Toggle();
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot("31_touch_office");
            float yaw = _gm.Cam.Yaw;
            var ship3d = screenOf(_gm.Computer.ShipButton);
            yield return Gesture(new[] { ship3d }, new[] { ship3d + new Vector2(Screen.height * 0.3f, 0) }, 20);
            float onButton = _gm.Cam.Yaw - yaw;
            yaw = _gm.Cam.Yaw;
            Vector2 room = new Vector2(Screen.width * 0.12f, Screen.height * 0.55f);
            yield return Gesture(new[] { room }, new[] { room + new Vector2(Screen.height * 0.3f, 0) }, 20);
            float turned = _gm.Cam.Yaw - yaw;
            Debug.Log(Mathf.Abs(turned) > 10f && Mathf.Abs(onButton) < 0.01f && _gm.Cam.Mode == CamMode.Office
                ? $"[Tour] PASS touch: dragging the room turned the camera {turned:0}°; a drag starting on SHIP CODE didn't ({onButton:0.#}°)"
                : $"[Tour] FAIL touch: room drag {turned:0.#}°, drag on SHIP CODE {onButton:0.#}°, mode {_gm.Cam.Mode}");

            Vector2 c = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), h = new Vector2(Screen.height * 0.05f, 0);
            float dist = _gm.Cam.Distance;
            yield return Gesture(new[] { c - h * 3, c + h * 3 }, new[] { c - h, c + h }, 15); // fingers together: zoom out
            float outDist = _gm.Cam.Distance;
            for (int i = 0; i < 6 && _gm.Cam.Mode == CamMode.Office; i++)
                yield return Gesture(new[] { c - h, c + h }, new[] { c - h * 4, c + h * 4 }, 15); // apart: zoom in
            yield return new WaitForSecondsRealtime(1.5f);
            Debug.Log(outDist > dist + 0.05f && _gm.Cam.Mode == CamMode.Monitor
                ? $"[Tour] PASS touch: pinching out zoomed out ({dist:0.00} → {outDist:0.00} m) and pinching in sat back down"
                : $"[Tour] FAIL touch: pinch {dist:0.00} → {outDist:0.00} m, mode {_gm.Cam.Mode}");

            // the monitor: two fingers spread apart zoom into the screen (its text is small on a phone), and lifting them
            // doesn't press what they started on (here a store row, which buys on release; SHIP CODE ships on touch-down,
            // like drumming); a tap still works while zoomed; two fingers moved together pan; pinching in goes back
            yield return WaitFor(() => !_gm.Cam.InTransition, 3f);
            yield return new WaitForSecondsRealtime(1.2f);
            float ShipHeight()
            {
                var r = _gm.Computer.ShipButton;
                Vector2 lo = cam.WorldToScreenPoint(r.TransformPoint(new Vector3(r.rect.center.x, r.rect.yMin)));
                Vector2 hi = cam.WorldToScreenPoint(r.TransformPoint(new Vector3(r.rect.center.x, r.rect.yMax)));
                return (hi - lo).magnitude;
            }
            yield return Shot("32a_touch_monitor");
            float h0 = ShipHeight();
            var row = _gm.Computer.Store.AgentRowRect(0);
            var rowNow = screenOf(row);
            Vector2 v = new Vector2(0, Screen.height * 0.04f);
            int hired = M.TotalAgents;
            yield return Gesture(new[] { rowNow - v * 0.5f, rowNow + v * 0.5f }, new[] { rowNow - v * 3, rowNow + v * 3 }, 20);
            yield return new WaitForSecondsRealtime(1.0f); // the camera eases in
            float zoom = _gm.Cam.MonitorZoom, h1 = ShipHeight();
            yield return Shot("32b_touch_monitor_zoomed");
            Debug.Log(zoom >= 1.8f && Mathf.Abs(h1 / h0 / zoom - 1f) < 0.15f && M.TotalAgents == hired
                ? $"[Tour] PASS touch: spreading two fingers on a store row zoomed the monitor x{zoom:0.00} (SHIP CODE {h0:0} → {h1:0} px tall) and bought nothing"
                : $"[Tour] FAIL touch: monitor zoom x{zoom:0.00}, SHIP CODE {h0:0} → {h1:0} px, agents bought by the gesture {M.TotalAgents - hired}");

            yield return null; yield return null; yield return null;
            hired = M.TotalAgents;
            yield return Tap(screenOf(row));
            Debug.Log(M.TotalAgents == hired + 1
                ? "[Tour] PASS touch: a tap on the store row while zoomed in hired one agent"
                : $"[Tour] FAIL touch: a tap while zoomed hired {M.TotalAgents - hired}");

            Vector2 rowBefore = screenOf(row);
            Vector2 p0 = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), sep = new Vector2(Screen.height * 0.1f, 0);
            Vector2 right = new Vector2(Screen.height * 0.2f, 0); // the store is on the right: there's room to pan that way
            hired = M.TotalAgents;
            int upgrades = M.State.upgrades.Count;
            yield return Gesture(new[] { p0 - sep, p0 + sep }, new[] { p0 - sep + right, p0 + sep + right }, 20);
            yield return new WaitForSecondsRealtime(1.0f);
            float panned = screenOf(row).x - rowBefore.x;
            Debug.Log(panned > Screen.height * 0.1f && M.TotalAgents == hired && M.State.upgrades.Count == upgrades
                ? $"[Tour] PASS touch: moving two fingers right panned the zoomed screen with them (the store row moved {panned:0} px)"
                : $"[Tour] FAIL touch: pan moved the store row {panned:0} px, agents {M.TotalAgents - hired}, upgrades {M.State.upgrades.Count - upgrades}");

            for (int i = 0; i < 3 && _gm.Cam.MonitorZoom > 1f; i++)
                yield return Gesture(new[] { p0 - v * 4, p0 + v * 4 }, new[] { p0 - v, p0 + v }, 15);
            yield return new WaitForSecondsRealtime(1.0f);
            float h2 = ShipHeight();
            Debug.Log(_gm.Cam.MonitorZoom == 1f && Mathf.Abs(h2 / h0 - 1f) < 0.05f
                ? "[Tour] PASS touch: pinching in went back to the whole monitor"
                : $"[Tour] FAIL touch: after pinching in zoom x{_gm.Cam.MonitorZoom:0.00}, SHIP CODE {h2:0} px (was {h0:0})");

            // zoomed into the SHIP CODE column, a model drop appears outside the view: a chip at the screen's edge points at
            // it, a tap on the chip moves the view onto the card (still zoomed), and a tap on the card catches it
            System.Func<RectTransform, bool> inView = r =>
            {
                var w = new Vector3[4];
                r.GetWorldCorners(w);
                foreach (var c in w)
                {
                    Vector2 sp = cam.WorldToScreenPoint(c);
                    if (sp.x < 0 || sp.y < 0 || sp.x > Screen.width || sp.y > Screen.height) return false;
                }
                return true;
            };
            var shipAt = screenOf(_gm.Computer.ShipButton);
            yield return Gesture(new[] { shipAt - v * 0.5f, shipAt + v * 0.5f }, new[] { shipAt - v * 3, shipAt + v * 3 }, 20);
            yield return new WaitForSecondsRealtime(1.0f);
            M.SpawnDrop();
            yield return new WaitForSecondsRealtime(0.3f);
            var dropCard = _gm.Computer.DropCard;
            bool chipShown = _gm.ZoomChip.Visible && _gm.ZoomChip.Text.Contains("MODEL DROP"), dropHidden = !inView(dropCard);
            string chipText = _gm.ZoomChip.Text;
            yield return Shot("32c_touch_zoom_chip");
            Debug.Log(chipShown && dropHidden && _gm.Cam.MonitorZoom > 1.8f
                ? $"[Tour] PASS touch: zoomed in x{_gm.Cam.MonitorZoom:0.00}, a model drop out of view shows the chip \"{chipText}\""
                : $"[Tour] FAIL touch: zoom x{_gm.Cam.MonitorZoom:0.00}, drop out of view {dropHidden}, chip {_gm.ZoomChip.Visible} \"{chipText}\"");
            if (_gm.ZoomChip.Visible) yield return Tap(_gm.ZoomChip.ScreenCenter);
            yield return new WaitForSecondsRealtime(1.0f); // the camera eases over
            bool moved = inView(dropCard) && _gm.Cam.MonitorZoom > 1.8f && !_gm.ZoomChip.Visible;
            yield return Shot("32d_touch_zoom_chip_moved");
            Debug.Log(moved
                ? $"[Tour] PASS touch: a tap on the chip moved the view onto the drop card, still zoomed x{_gm.Cam.MonitorZoom:0.00}"
                : $"[Tour] FAIL touch: after tapping the chip the drop card in view {inView(dropCard)}, zoom x{_gm.Cam.MonitorZoom:0.00}, chip {_gm.ZoomChip.Visible}");
            yield return Tap(screenOf(dropCard));
            yield return null;
            Debug.Log(M.ActiveDrop == null
                ? "[Tour] PASS touch: a tap on the drop card while zoomed caught it"
                : "[Tour] FAIL touch: the drop card wasn't caught by a tap while zoomed");

            // a dialog opening while zoomed in goes back to the whole monitor
            yield return Gesture(new[] { p0 - v * 0.5f, p0 + v * 0.5f }, new[] { p0 - v * 3, p0 + v * 3 }, 20);
            yield return new WaitForSecondsRealtime(1.0f);
            float zoomedTo = _gm.Cam.MonitorZoom;
            _gm.Computer.ShowInbox(null);
            yield return new WaitForSecondsRealtime(1.0f);
            Debug.Log(zoomedTo > 1.8f && _gm.Cam.MonitorZoom == 1f && _gm.Computer.ModalOpen && inView(_gm.Computer.ShipButton)
                ? $"[Tour] PASS touch: a dialog opening while zoomed x{zoomedTo:0.00} went back to the whole monitor"
                : $"[Tour] FAIL touch: dialog while zoomed x{zoomedTo:0.00}: zoom now x{_gm.Cam.MonitorZoom:0.00}, modal {_gm.Computer.ModalOpen}");
            _gm.Computer.CloseModal();

            // not zoomed: no chip
            M.SpawnDrop();
            yield return new WaitForSecondsRealtime(0.3f);
            Debug.Log(!_gm.ZoomChip.Visible && M.ActiveDrop != null
                ? "[Tour] PASS touch: with the whole monitor in view a model drop shows no chip"
                : $"[Tour] FAIL touch: unzoomed, chip {_gm.ZoomChip.Visible}, drop {M.ActiveDrop != null}");
            M.ClaimDrop();

            // the day autopilot leaves a tapping player alone, then takes over once they stop
            var ap = _gm.Autopilot;
            _gm.Settings.autopilotDay = true;
            _gm.AutopilotAllowed = true;
            ap.ClockOutAfter = 1.5f;
            M.State.dayMinutes = GameDatabase.WorkdayMinutes + 1f;
            _gm.Computer.CloseModal();
            for (float t = 0; t < 3f; t += 0.25f)
            {
                yield return Tap(new Vector2(Screen.width * 0.5f, Screen.height * 0.03f));
                yield return new WaitForSecondsRealtime(0.2f);
            }
            bool held = M.Phase == GamePhase.Working;
            yield return WaitFor(() => M.Phase == GamePhase.Review, 6f);
            Debug.Log(held && M.Phase == GamePhase.Review
                ? "[Tour] PASS touch: the autopilot waited while the player tapped, then clocked out"
                : $"[Tour] FAIL touch: held {held}, then {M.Phase}");
            ap.ClockOutAfter = new DayAutopilot().ClockOutAfter;
            _gm.AutopilotAllowed = false;

            // the mouse takes over again
            yield return new WaitForSecondsRealtime(0.8f);
            var mouse = Mouse.current ?? InputSystem.AddDevice<Mouse>();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(300, 300), delta = new Vector2(40, 30) });
            yield return null; yield return null; yield return null;
            Debug.Log(!_gm.Touch.Active ? "[Tour] PASS touch: moving the mouse brought the mouse prompts back"
                                        : "[Tour] FAIL touch: still in touch mode after the mouse moved");
            InputSystem.RemoveDevice(ts);
            M.RandomEventsEnabled = true;
        }

        static IEnumerator WaitFor(System.Func<bool> done, float timeout)
        {
            for (float t = 0; t < timeout && !done(); t += Time.unscaledDeltaTime) yield return null;
        }

        static IEnumerator RealMove(Vector2 pos)
        {
            var mouse = Mouse.current ?? InputSystem.AddDevice<Mouse>();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pos });
            yield return null;
            yield return null;
        }

        static IEnumerator RealClick(Vector2 pos)
        {
            var mouse = Mouse.current ?? InputSystem.AddDevice<Mouse>();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pos });
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pos }.WithButton(MouseButton.Left, true));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pos }.WithButton(MouseButton.Left, false));
            yield return null;
            yield return null;
        }

        const float MinScreenText = 15f, MinMonitorText = 14f;
        static readonly System.Collections.Generic.SortedDictionary<string, string> _monitorSmall =
            new System.Collections.Generic.SortedDictionary<string, string>();
        static float _screenMinAll = float.MaxValue, _monitorMinAll = float.MaxValue;
        static string _screenMinWhat = "", _monitorMinWhat = "";

        /// <summary>Settings hints are one line each; only a long save-folder path may end in "…".</summary>
        static void CheckHintsFit(string tab)
        {
            int n = 0;
            var cut = new System.Collections.Generic.List<string>();
            foreach (var t in FindObjectsByType<TMPro.TMP_Text>())
            {
                if (t.name != "Hint" || !t.isActiveAndEnabled || t.canvas == null || t.canvas.rootCanvas.name != "Menu Canvas") continue;
                t.ForceMeshUpdate();
                n++;
                if (t.isTextTruncated && !t.text.Contains("agentclicker_save.json")) cut.Add(t.text);
            }
            Debug.Log(cut.Count == 0 ? $"[Tour] PASS {tab} settings hints fit on one line ({n})"
                                     : $"[Tour] FAIL {tab} settings hints cut off: {string.Join(" | ", cut)}");
        }

        /// <summary>Smallest text drawn on screen (menus and overlays) and on the CorpOS monitor, in points at the 1600×900 reference.</summary>
        static void LogSmallestText(string shot)
        {
            float screenMin = float.MaxValue, monitorMin = float.MaxValue;
            string screenWhat = "", monitorWhat = "";
            foreach (var t in FindObjectsByType<TMPro.TMP_Text>())
            {
                if (!t.isActiveAndEnabled || t.color.a < 0.05f || t.textInfo == null || t.textInfo.characterCount == 0) continue;
                var root = t.canvas ? t.canvas.rootCanvas : null;
                if (root == null) continue;
                bool monitor = root.name == "CorpOS Canvas";
                if (!monitor && root.renderMode == RenderMode.WorldSpace) continue; // the decorative side monitors
                if (monitor && t.name == "Mono") continue; // two-letter agent badges in the fleet list are icons, not text
                bool hidden = false;
                foreach (var g in t.GetComponentsInParent<CanvasGroup>())
                    if (g.alpha < 0.05f) { hidden = true; break; }
                if (hidden) continue;
                var info = t.textInfo;
                for (int i = 0; i < info.characterCount; i++)
                {
                    var c = info.characterInfo[i];
                    if (!c.isVisible) continue;
                    if (monitor && c.pointSize < MinMonitorText - 0.05f)
                    {
                        string key = t.transform.parent ? t.transform.parent.name + "/" + t.name : t.name;
                        if (!_monitorSmall.ContainsKey(key))
                            _monitorSmall[key] = $"{key} {c.pointSize:0.#} pt \"{t.GetParsedText().Replace("\n", " ").Substring(0, Mathf.Min(30, t.GetParsedText().Length))}\" ({shot})";
                    }
                    if (monitor ? c.pointSize < monitorMin : c.pointSize < screenMin)
                    {
                        string what = $"{t.name} \"{t.GetParsedText().Replace("\n", " ").Substring(0, Mathf.Min(40, t.GetParsedText().Length))}\"";
                        if (monitor) { monitorMin = c.pointSize; monitorWhat = what; }
                        else { screenMin = c.pointSize; screenWhat = what; }
                    }
                }
            }
            if (screenMin < _screenMinAll) { _screenMinAll = screenMin; _screenMinWhat = $"{screenWhat} in {shot}"; }
            if (monitorMin < _monitorMinAll) { _monitorMinAll = monitorMin; _monitorMinWhat = $"{monitorWhat} in {shot}"; }
            string Fmt(float v, string w) => v == float.MaxValue ? "none" : $"{v:0.#} pt ({w})";
            Debug.Log($"[Text] {shot}: screen {Fmt(screenMin, screenWhat)}, monitor {Fmt(monitorMin, monitorWhat)}");
        }

        /// <summary>Every toast's text lies inside its card, and the cards hide the activity feed under them.</summary>
        void CheckToasts()
        {
            int n = 0, lines = 0;
            var bad = new System.Collections.Generic.List<string>();
            foreach (var (card, text) in _gm.Computer.ToastCards())
            {
                text.ForceMeshUpdate();
                n++;
                lines += text.textInfo.lineCount;
                // the text's laid-out lines, in the card's space
                var b = text.textBounds;
                Vector3 lo = card.InverseTransformPoint(text.transform.TransformPoint(b.min));
                Vector3 hi = card.InverseTransformPoint(text.transform.TransformPoint(b.max));
                var r = card.rect;
                if (lo.y < r.yMin + 2 || hi.y > r.yMax - 2)
                    bad.Add($"\"{text.GetParsedText()}\" spans {lo.y:0}..{hi.y:0} in a card {r.yMin:0}..{r.yMax:0}");
                if (card.GetComponent<UnityEngine.UI.Image>().color.a < 1f) bad.Add($"\"{text.GetParsedText()}\" has a see-through card");
            }
            Debug.Log(n >= 3 && bad.Count == 0
                ? $"[Tour] PASS toasts: {n} opaque cards over the activity feed, {lines} lines of text, all inside their cards"
                : $"[Tour] FAIL toasts ({n} cards): {(bad.Count == 0 ? "fewer than 3 on screen" : string.Join(" | ", bad))}");
        }

        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            string path = Path.Combine(OutputDir, name + ".png");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("[Tour] " + path);
            LogSmallestText(name);
            yield return null;
            yield return null;
        }
    }
}
