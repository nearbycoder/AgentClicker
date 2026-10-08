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
            CheckTitleFits();
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
            CheckCreditsLabel("day 1", null);
            CheckScrollbar("day 1 agent list", _gm.Computer.Store.AgentRowRect(0).GetComponentInParent<UnityEngine.UI.ScrollRect>(), false);
            yield return MuteSegment();
            yield return HoldToShipSegment();
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
            _gm.Computer.Toast("☎ Missed call from Gary Okonkwo.", Theme.TextDim, 6f); // at least one toast on the feed
            yield return new WaitForSeconds(1.0f);
            yield return Shot("07_desktop_lategame_outage");
            LogGoal("late game");
            CheckFeedUnderToasts("late game");
            yield return MonitorClearSegment();
            yield return TimedEffectsSegment();
            yield return ChapterBannerSegment();

            // ---- review & night ----------------------------------------------
            _gm.Overlay.ShowChapterBanner(5); // clocking out under a banner (round 9's tour drew it across the review)
            yield return new WaitForSeconds(0.3f);
            SeedDayHistory();
            M.ClockOut();
            yield return new WaitForSeconds(0.8f);
            yield return Shot("08_review");
            CheckReviewTrend();
            CheckNoBanner("on the review");
            _gm.Computer.CloseModal();
            _gm.GoHome();
            yield return new WaitForSeconds(1.5f);
            yield return Shot("09_night");
            CheckNoBanner("on the night screen");
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
            CheckCreditsLabel("after the Factory", "22.0 quadrillion");
            CheckScrollbar("late-game agent list", _gm.Computer.Store.AgentRowRect(0).GetComponentInParent<UnityEngine.UI.ScrollRect>(), true);
            // a feed line too long for the panel, then three toasts, two of them two lines long, stacked on the full feed
            _gm.Computer.ClearToasts();
            _gm.Computer.LogActivity("<color=#E040FB>●</color> Fib-Mini #400: opened PR #4382: 'small refactor' (+4,812 lines, -3 lines, 212 files)");
            yield return null;
            CheckFeedFits();
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
            CheckDayChart();
            CheckScrollbar("Stats tab", ActiveList("Stats"), true);
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
            yield return ScrollbarDragSegment(ActiveList("BoardView"));

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
            CheckCreditsLabel("end game", "190 tredecillion");
            yield return TopBarNamesSegment();
            _gm.Computer.SelectStoreTab(StorePanel.TrophiesTabIndex);
            yield return new WaitForSeconds(0.6f);
            yield return Shot("24_endgame_trophies");
            double bank = M.State.credits;
            var installed = new System.Collections.Generic.List<string>(M.State.office);
            foreach (var o in GameDatabase.OfficeItems) { M.State.credits = 1e300; M.BuyOffice(o.Id); }
            M.State.credits = bank;
            _gm.Computer.SelectStoreTab(StorePanel.OfficeTabIndex); // every gadget installed: all eighteen rows are laid out
            yield return new WaitForSeconds(0.6f);
            yield return Shot("24b_endgame_office");
            // back as it was: the Macro Pad's auto-clicks would add to the touch checks' counts of shipped code
            M.State.office.Clear();
            M.State.office.AddRange(installed);
            M.Load(M.State);
            M.State.Phase = GamePhase.Working;
            _gm.Office.Refresh(false);

            yield return GamepadSegment();
            yield return TouchSegment();

            Debug.Log(_screenMinAll >= MinScreenText
                ? $"[Tour] PASS menus and overlays never draw text below {MinScreenText} pt (smallest {_screenMinAll:0.#}: {_screenMinWhat})"
                : $"[Tour] FAIL menus and overlays draw {_screenMinAll:0.#} pt text: {_screenMinWhat}");
            Debug.Log(_monitorSmall.Count == 0
                ? $"[Tour] PASS the CorpOS monitor never draws text below {MinMonitorText} pt (smallest {_monitorMinAll:0.#}: {_monitorMinWhat})"
                : $"[Tour] FAIL the CorpOS monitor draws text below {MinMonitorText} pt: {string.Join(" | ", _monitorSmall.Values)}");
            Debug.Log(_cut.Count == 0
                ? $"[Tour] PASS nothing cut off: no text on the monitor, the overlays or the menus ends in \"…\" or is truncated in {_cutShots} shots (the activity feed's long lines excepted)"
                : $"[Tour] FAIL text cut off: {string.Join(" | ", _cut.Values)}");
            Debug.Log(_offscreen.Count == 0
                ? $"[Tour] PASS nothing off the screen: every button and line of text on the menus, overlays and calls is inside the {Screen.width}×{Screen.height} window in {_cutShots} shots (overlay canvas {OverlayCanvasSize()})"
                : $"[Tour] FAIL off the screen ({Screen.width}×{Screen.height}): {string.Join(" | ", _offscreen.Values)}");
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

        /// <summary>
        /// Hold to keep shipping: with the setting on, holding Space, the mouse button on SHIP CODE or RT keeps shipping about
        /// six lines a second and builds Focus, and nothing more is shipped once it's let go; with it off a held Space ships once.
        /// </summary>
        IEnumerator HoldToShipSegment()
        {
            var st = _gm.Settings;
            bool wasHold = st.holdToShip, wasMail = st.autoOpenStoryMail, wasEvents = M.RandomEventsEnabled;
            st.autoOpenStoryMail = false; // the CEO's email would open in a pause between holds
            M.RandomEventsEnabled = false;
            var cam = _gm.Refs.MainCamera;
            var kb = Keyboard.current ?? InputSystem.AddDevice<Keyboard>();
            var mouse = Mouse.current ?? InputSystem.AddDevice<Mouse>();
            var rt = _gm.Computer.ShipButton;
            Vector2 ship = cam.WorldToScreenPoint(rt.TransformPoint(rt.rect.center));
            var results = new System.Collections.Generic.List<string>();
            bool ok = true;

            IEnumerator Hold(string what, System.Action down, System.Action up, bool on)
            {
                st.holdToShip = on;
                _gm.Computer.CloseModal();
                yield return new WaitForSecondsRealtime(1.5f); // Focus drains between holds
                long n0 = _gm.HandShips;
                float f0 = M.Focus;
                down();
                yield return new WaitForSecondsRealtime(2f);
                long held = _gm.HandShips - n0;
                float f1 = M.Focus;
                up();
                yield return new WaitForSecondsRealtime(0.6f);
                long after = _gm.HandShips - n0 - held;
                bool good = on ? held >= 11 && held <= 15 && after == 0 && f1 - f0 > 0.2f : held == 1 && after == 0;
                ok &= good;
                results.Add($"{what} {(on ? "on" : "off")}: {held} in 2 s, {after} after letting go, Focus {f0:0.00} → {f1:0.00}{(good ? "" : " ✗")}");
            }

            yield return Hold("Space", () => InputSystem.QueueStateEvent(kb, new KeyboardState(Key.Space)),
                              () => InputSystem.QueueStateEvent(kb, new KeyboardState()), true);
            yield return RealMove(ship);
            yield return Hold("mouse on SHIP CODE", () => InputSystem.QueueStateEvent(mouse, new MouseState { position = ship }.WithButton(MouseButton.Left, true)),
                              () => InputSystem.QueueStateEvent(mouse, new MouseState { position = ship }), true);
            var pad = InputSystem.AddDevice<Gamepad>();
            yield return Hold("RT", () => InputSystem.QueueStateEvent(pad, new GamepadState { rightTrigger = 1f }),
                              () => InputSystem.QueueStateEvent(pad, new GamepadState()), true);
            InputSystem.RemoveDevice(pad);
            yield return Hold("Space", () => InputSystem.QueueStateEvent(kb, new KeyboardState(Key.Space)),
                              () => InputSystem.QueueStateEvent(kb, new KeyboardState()), false);

            st.holdToShip = wasHold;
            st.autoOpenStoryMail = wasMail;
            M.RandomEventsEnabled = wasEvents;
            Debug.Log(ok ? $"[Tour] PASS hold to keep shipping: {string.Join("; ", results)}"
                         : $"[Tour] FAIL hold to keep shipping: {string.Join("; ", results)}");
        }

        /// <summary>
        /// Two weeks of earlier days for the review and the Stats chart (the tour jumps between saved states, so it has no
        /// real history): each day about a third up on the one before, against a quota of 90% of it, with day 15 missed.
        /// Yesterday is today's earnings over 1.62, so the review can say exactly what it should.
        /// </summary>
        void SeedDayHistory()
        {
            var s = M.State;
            s.history.Clear();
            double earned = s.earnedToday / 1.62;
            var days = new System.Collections.Generic.List<DayRecord>();
            for (int d = s.day - 1; d >= System.Math.Max(1, s.day - 14); d--)
            {
                bool missed = d == 15;
                days.Insert(0, new DayRecord { day = d, earned = earned, quota = missed ? earned * 1.25 : earned * 0.9 });
                earned /= 1.33;
            }
            s.history.AddRange(days);
        }

        void CheckReviewTrend()
        {
            var t = _gm.Computer.ReviewNumbers;
            string text = t ? t.GetParsedText() : "";
            bool ok = text.Contains("Best day yet · +62% on yesterday");
            if (t) t.ForceMeshUpdate();
            Debug.Log(ok && t && !t.isTextOverflowing
                ? $"[Tour] PASS the review says how the day compares (\"{text.Replace("\n", " / ")}\")"
                : $"[Tour] FAIL the review's comparison: \"{text.Replace("\n", " / ")}\" (overflowing {t?.isTextOverflowing})");
        }

        /// <summary>The Stats chart: one bar per day shown, heights in the order of the values, colours by met and missed.</summary>
        void CheckDayChart()
        {
            var store = _gm.Computer.Store;
            var bars = store.DayBars;
            var bad = new System.Collections.Generic.List<string>();
            int met = 0, missed = 0, today = 0;
            for (int i = 0; i < bars.Count; i++)
            {
                var img = store.DayBarImage(i);
                if (!img.gameObject.activeInHierarchy) bad.Add($"day {bars[i].Day} has no bar");
                var want = bars[i].Today ? Theme.Accent : bars[i].Met ? Theme.Good : Theme.Bad;
                if (img.color != want) bad.Add($"day {bars[i].Day} is the wrong colour");
                if (bars[i].Today) today++; else if (bars[i].Met) met++; else missed++;
                for (int j = 0; j < i; j++)
                {
                    float hi = img.rectTransform.anchorMax.y, hj = store.DayBarImage(j).rectTransform.anchorMax.y;
                    if (bars[i].Earned > bars[j].Earned * 1.01 && hi <= hj) bad.Add($"day {bars[i].Day} isn't taller than day {bars[j].Day}");
                    if (bars[i].Earned < bars[j].Earned * 0.99 && hi >= hj) bad.Add($"day {bars[i].Day} isn't shorter than day {bars[j].Day}");
                }
            }
            if (bars.Count != DayHistory.Shown) bad.Add($"{bars.Count} bars, not {DayHistory.Shown}");
            if (today != 1 || missed < 1) bad.Add($"today {today}, missed {missed}");
            Debug.Log(bad.Count == 0
                ? $"[Tour] PASS the Stats chart shows the last {bars.Count} days (day {bars[0].Day} to today, {met} met, {missed} missed), heights in order of the credits shipped " +
                  $"({string.Join(", ", System.Linq.Enumerable.Select(bars, b => $"{b.Day}: {NumberFormat.Short(b.Earned)}"))})"
                : $"[Tour] FAIL the Stats chart: {string.Join(" | ", bad)}");
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
            var dropCard = _gm.Computer.DropCard;
            // at the far end of the drop's area, so its centre is out of the zoomed view at any window shape (a random spot
            // could be half in view at 21:9, where the chip rightly stays away)
            dropCard.anchoredPosition = new Vector2(ComputerUI.DropCardArea.xMax, -ComputerUI.DropCardArea.yMax);
            yield return new WaitForSecondsRealtime(0.3f);
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

        /// <summary>
        /// Benchmark Hype, Caffeine Rush and an outage at once: every effect is on screen with its multiplier and seconds
        /// left, none cut off or overlapping the rate. Then the 5 PM card's quota line follows overtime past the quota.
        /// </summary>
        /// <summary>
        /// In the monitor view no part of Sam is between the camera and the screen while he leans in (the outage's
        /// facepalm, the morning stretch, typing), at this window's shape and at both ends of the Field of view setting,
        /// and the whole monitor fills the view.
        /// </summary>
        IEnumerator MonitorClearSegment()
        {
            var cam = _gm.Refs.MainCamera;
            float setting = _gm.Settings.fieldOfView;
            var bad = new System.Collections.Generic.List<string>();
            var frames = new System.Collections.Generic.List<string>();
            int samples = 0;
            foreach (float f in new[] { setting, 40f, 70f })
            {
                _gm.Settings.fieldOfView = f;
                _gm.ApplySettings(false);
                _gm.Cam.SetMode(CamMode.Monitor, 0.01f);
                yield return WaitFor(() => _gm.Cam.DistanceToTarget() < 0.0005f, 3f);
                yield return new WaitForSeconds(0.3f);
                _gm.Cam.MonitorFrame(out float distance, out float fov);
                string fit = MonitorFit(cam, out bool fits);
                frames.Add($"{f:0}°: {distance:0.000} m at {cam.fieldOfView:0.0}°, {fit}");
                if (!fits) bad.Add($"at {f:0}° the monitor doesn't fill the view ({fit})");
                if (Mathf.Abs(cam.fieldOfView - fov) > 0.05f) bad.Add($"at {f:0}° the camera is at {cam.fieldOfView:0.0}°, not {fov:0.0}°");
                // at the default field of view the facepalm also starts from each pose Sam can be in when an outage begins
                // (typing, idle, and leaning back once the agents do the work), since the blend between them differs
                var runs = new System.Collections.Generic.List<(string pose, string from)>();
                if (f == setting)
                    foreach (var b in new[] { EmployeeController.Relax, EmployeeController.Typing, EmployeeController.Idle })
                        runs.Add((EmployeeController.Facepalm, b));
                else runs.Add((EmployeeController.Facepalm, null));
                runs.Add((EmployeeController.Stretch, null));
                runs.Add((EmployeeController.Typing, null));
                foreach (var (pose, start) in runs)
                {
                    if (start != null)
                    {
                        Time.timeScale = 1f;
                        _gm.Employee.PlayOneShot(start, 3f);
                        yield return new WaitForSeconds(0.6f);
                    }
                    // the facepalm's hand sweeps past the camera quickly: sample it four times as densely (in slow motion)
                    bool dense = pose == EmployeeController.Facepalm;
                    float step = dense ? 0.0125f : 0.05f;
                    Time.timeScale = dense ? 0.25f : 1f;
                    string from = _gm.Employee.Current;
                    _gm.Employee.PlayOneShot(pose, 3f);
                    for (float t = 0; t < 2f; t += step)
                    {
                        yield return new WaitForSeconds(step);
                        samples++;
                        int covered = 0, total = 0;
                        yield return SamPixelsOnScreen(cam, (c, n) => { covered = c; total = n; });
                        if (total < 1000) { bad.Add($"the screen is {total} px in the probe"); break; }
                        if (covered > 0)
                        {
                            // which parts: time stands still while each renderer is drawn alone
                            float scale = Time.timeScale;
                            Time.timeScale = 0f;
                            var which = new System.Collections.Generic.List<string>();
                            foreach (var r in _gm.Refs.Employee.GetComponentsInChildren<Renderer>())
                            {
                                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                                int part = 0;
                                yield return SamPixelsOnScreen(cam, (c, n) => part = c, save: false, only: r);
                                if (part > 0) which.Add($"{r.name} {part}");
                            }
                            Time.timeScale = scale;
                            Debug.Log($"[Tour] probe parts ({pose} from {from}, {t + step:0.00} s in): {string.Join(", ", which)}");
                            bad.Add($"{pose} at {f:0}°: Sam covers {covered} of {total} px of the screen ({t + step:0.00} s in, from {from})");
                            yield return Shot($"07x_covered_{pose}_{f:0}");
                            break;
                        }
                        if (f == setting && pose == EmployeeController.Facepalm && Mathf.Abs(t - 0.6f) < step * 0.5f) yield return Shot("07a_monitor_facepalm");
                    }
                    Time.timeScale = 1f;
                }
            }
            _gm.Settings.fieldOfView = setting;
            _gm.ApplySettings(false);
            // control: from 0.55 m in front of the screen (behind Sam's head) the probe must see him
            SceneRefs.ScreenFrame(_gm.Refs.MainScreen, out var screen, out var frame, out _);
            _gm.Cam.SetFixed(screen + frame * Vector3.back * 0.55f, screen);
            _gm.Employee.PlayOneShot(EmployeeController.Facepalm, 3f);
            yield return new WaitForSeconds(0.6f);
            int seen = 0, of = 0;
            yield return SamPixelsOnScreen(cam, (c, n) => { seen = c; of = n; }, save: false);
            if (seen == 0) bad.Add("the probe didn't see Sam from behind his head (control)");
            _gm.Cam.SetMode(CamMode.Monitor, 0.01f);
            _gm.Employee.PlayOneShot(EmployeeController.Idle, 0.01f);
            yield return new WaitForSeconds(0.6f);
            Debug.Log(bad.Count == 0
                ? $"[Tour] PASS Sam never covers the monitor ({Screen.width}×{Screen.height}, {samples} samples of Facepalm (from Relax, Typing and Idle at {setting:0}°), Stretch and Typing; {string.Join("; ", frames)}; control from 0.55 m: Sam covers {seen} of {of} px)"
                : $"[Tour] FAIL Sam covers the monitor ({Screen.width}×{Screen.height}): {string.Join(" | ", bad)}; {string.Join("; ", frames)}");
        }

        /// <summary>
        /// Renders Sam alone (moved to a spare layer for one frame) through a copy of the game camera and counts his
        /// pixels inside the main screen's rectangle. Exact for any pose, unlike the parts' bounds, which reach past the
        /// camera: in the monitor view it sits at Sam's eye height, just in front of his face.
        /// </summary>
        int _probeShots;

        IEnumerator SamPixelsOnScreen(Camera cam, System.Action<int, int> result, bool save = true, Renderer only = null)
        {
            const int Layer = 31;
            var parts = only ? new[] { only } : _gm.Refs.Employee.GetComponentsInChildren<Renderer>();
            var layers = new int[parts.Length];
            for (int i = 0; i < parts.Length; i++) { layers[i] = parts[i].gameObject.layer; parts[i].gameObject.layer = Layer; }
            int w = 320, h = Mathf.Max(1, Mathf.RoundToInt(320 / cam.aspect));
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            var probe = new GameObject("Sam probe").AddComponent<Camera>();
            probe.CopyFrom(cam);
            probe.cullingMask = 1 << Layer;
            probe.clearFlags = CameraClearFlags.SolidColor;
            probe.backgroundColor = Color.magenta;
            probe.allowMSAA = false;
            probe.allowHDR = false;
            probe.targetTexture = rt;
            var data = UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(probe);
            data.renderPostProcessing = false;
            data.renderShadows = false;
            data.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.None;
            yield return new WaitForEndOfFrame();
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var was = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            RenderTexture.active = was;
            Destroy(probe.gameObject);
            RenderTexture.ReleaseTemporary(rt);
            for (int i = 0; i < parts.Length; i++) if (parts[i]) parts[i].gameObject.layer = layers[i];

            ScreenRect(cam, out var lo, out var hi);
            int x0 = Mathf.Clamp(Mathf.CeilToInt(lo.x * w), 0, w), x1 = Mathf.Clamp(Mathf.FloorToInt(hi.x * w), 0, w);
            int y0 = Mathf.Clamp(Mathf.CeilToInt(lo.y * h), 0, h), y1 = Mathf.Clamp(Mathf.FloorToInt(hi.y * h), 0, h);
            var px = tex.GetPixels32();
            int covered = 0, cx0 = w, cx1 = -1, cy0 = h, cy1 = -1;
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                {
                    var c = px[y * w + x];
                    if (c.r < 235 || c.g > 20 || c.b < 235)
                    {
                        covered++;
                        cx0 = Mathf.Min(cx0, x); cx1 = Mathf.Max(cx1, x); cy0 = Mathf.Min(cy0, y); cy1 = Mathf.Max(cy1, y);
                    }
                }
            if (covered > 0 && save)
            {
                // what the probe saw, for a look afterwards: Sam on magenta, the screen's rectangle in the log
                string file = Path.Combine(OutputDir, $"07y_probe_{++_probeShots}.png");
                File.WriteAllBytes(file, tex.EncodeToPNG());
                Debug.Log($"[Tour] probe {file}: {w}×{h}, screen x {x0}–{x1} y {y0}–{y1}, Sam's pixels on it x {cx0}–{cx1} y {cy0}–{cy1}");
            }
            Destroy(tex);
            result(covered, (x1 - x0) * (y1 - y0));
        }

        /// <summary>The main screen's rectangle in the view (viewport coordinates).</summary>
        void ScreenRect(Camera cam, out Vector2 lo, out Vector2 hi)
        {
            SceneRefs.ScreenFrame(_gm.Refs.MainScreen, out var center, out var frame, out var size);
            lo = Vector2.one * float.MaxValue;
            hi = Vector2.one * float.MinValue;
            foreach (var c in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) })
            {
                Vector3 v = cam.WorldToViewportPoint(center + frame * new Vector3(c.x * size.x / 2, c.y * size.y / 2, 0));
                lo = Vector2.Min(lo, v);
                hi = Vector2.Max(hi, v);
            }
        }

        /// <summary>Where the main screen lands in the view: all inside, and filling it in one direction.</summary>
        string MonitorFit(Camera cam, out bool fits)
        {
            ScreenRect(cam, out var lo, out var hi);
            Vector2 span = hi - lo;
            fits = lo.x >= -0.001f && lo.y >= -0.001f && hi.x <= 1.001f && hi.y <= 1.001f && Mathf.Max(span.x, span.y) >= 0.97f;
            return $"screen spans {span.x:P0} × {span.y:P0} of the view";
        }

        void CheckNoBanner(string where) =>
            Debug.Log(_gm.Overlay.ChapterBannerAlpha < 0.01f ? $"[Tour] PASS no chapter banner {where}"
                                                            : $"[Tour] FAIL a chapter banner {where} (alpha {_gm.Overlay.ChapterBannerAlpha:0.00})");

        /// <summary>
        /// The chapter banner stays its four seconds with nothing else up, fades within half a second when a model drop,
        /// the 5 PM card or a ringing phone arrives, and one cut short comes back once that's dealt with.
        /// </summary>
        IEnumerator ChapterBannerSegment()
        {
            var o = _gm.Overlay;
            var bad = new System.Collections.Generic.List<string>();
            var log = new System.Collections.Generic.List<string>();
            for (int i = 0; i < 20 && M.ActiveOutage != null; i++) M.ClickOutage();
            if (M.ActiveDrop != null) M.ClaimDrop();
            _gm.Computer.CloseModal();
            yield return new WaitForSeconds(0.5f);

            o.ShowChapterBanner(5);
            yield return new WaitForSeconds(3.0f);
            if (o.ChapterBannerUp && o.ChapterBannerAlpha > 0.99f) log.Add("stays with nothing else up (3 s)");
            else bad.Add($"with nothing else up it went after {3.0f} s (alpha {o.ChapterBannerAlpha:0.00})");
            yield return WaitFor(() => o.ChapterBannerAlpha < 0.01f, 3f);

            IEnumerator Interrupt(string what, System.Action arrive, System.Action leave, System.Func<bool> gone)
            {
                o.ShowChapterBanner(5);
                yield return new WaitForSeconds(0.6f);
                arrive();
                float t0 = Time.unscaledTime;
                yield return WaitFor(() => o.ChapterBannerAlpha < 0.01f, 2f);
                float took = Time.unscaledTime - t0;
                if (o.ChapterBannerAlpha < 0.01f && took <= 0.5f) log.Add($"{what}: gone in {took:0.00} s");
                else bad.Add($"{what}: still up after {took:0.00} s (alpha {o.ChapterBannerAlpha:0.00})");
                if (what == "the 5 PM card") yield return Shot("07d_five_pm_no_banner");
                leave();
                yield return WaitFor(gone, 8f);
                yield return WaitFor(() => o.ChapterBannerUp && o.ChapterBannerAlpha > 0.99f, 3f);
                if (o.ChapterBannerUp) log.Add("back after");
                else bad.Add($"{what}: the banner didn't come back afterwards");
                yield return WaitFor(() => o.ChapterBannerAlpha < 0.01f, 6f);
            }
            yield return Interrupt("a model drop", () => M.SpawnDrop(), () => M.ClaimDrop(), () => M.ActiveDrop == null);
            yield return Interrupt("the 5 PM card", () => _gm.Computer.ShowDayEndPrompt(), () => _gm.Computer.CloseModal(), () => !_gm.Computer.ModalOpen);
            yield return Interrupt("a ringing phone", () => M.RingPhone(CallDatabase.ById("dana_demo")), () => M.DeclineCall(), () => !_gm.Calls.Busy);
            Debug.Log(bad.Count == 0 ? $"[Tour] PASS chapter banner: {string.Join("; ", log)}"
                                     : $"[Tour] FAIL chapter banner: {string.Join(" | ", bad)}; {string.Join("; ", log)}");
        }

        IEnumerator TimedEffectsSegment()
        {
            _gm.Computer.CloseModal();
            for (int i = 0; i < 300 && !(M.Buffs.Exists(b => b.Kind == BuffKind.Hype) && M.Buffs.Exists(b => b.Kind == BuffKind.Caffeine)); i++)
            {
                if (M.ActiveDrop == null) M.SpawnDrop();
                M.ClaimDrop();
            }
            if (M.ActiveOutage == null) M.StartOutage();
            yield return new WaitForSeconds(0.4f);
            var bad = new System.Collections.Generic.List<string>();
            var fx = _gm.Computer.EffectsText;
            var click = _gm.Computer.ClickInfoText;
            var rate = _gm.Computer.RateText;
            foreach (var (t, wants) in new[] { (fx, new[] { "Hype x", "Outage x0.5" }), (click, new[] { "Caffeine x77" }) })
            {
                t.ForceMeshUpdate();
                string text = t.GetParsedText();
                foreach (var w in wants)
                    if (!System.Text.RegularExpressions.Regex.IsMatch(text, System.Text.RegularExpressions.Regex.Escape(w) + @"\d* \d+s")) bad.Add($"\"{text}\" lacks \"{w} … Ns\"");
                if (t.isTextTruncated || t.textInfo.lineCount != 1) bad.Add($"\"{text}\" is cut off or wraps ({t.textInfo.lineCount} lines)");
                var r = t.rectTransform.rect;
                if (t.textBounds.min.x < r.xMin - 0.5f || t.textBounds.max.x > r.xMax + 0.5f) bad.Add($"\"{text}\" runs outside its box");
            }
            rate.ForceMeshUpdate();
            float rateRight = fx.rectTransform.InverseTransformPoint(rate.transform.TransformPoint(rate.textBounds.max)).x;
            if (fx.textBounds.min.x < rateRight + 4) bad.Add($"the effects start {fx.textBounds.min.x - rateRight:0} px from the rate");
            Debug.Log(bad.Count == 0
                ? $"[Tour] PASS timed effects: \"{fx.GetParsedText()}\" beside \"{rate.GetParsedText()}\", \"{click.GetParsedText()}\" on SHIP CODE, all on one line inside their boxes"
                : $"[Tour] FAIL timed effects: {string.Join(" | ", bad)}");
            yield return Shot("07b_timed_effects");

            var s = M.State;
            double quota = s.quotaToday, earned = s.earnedToday;
            s.quotaToday = 1e15;
            s.earnedToday = 4e14;
            _gm.Computer.ShowDayEndPrompt();
            yield return new WaitForSeconds(0.3f);
            string before = _gm.Computer.DayEndQuota.GetParsedText();
            s.earnedToday = 1.2e15; // overtime passes the quota while the card is up
            yield return new WaitForSeconds(0.4f);
            string after = _gm.Computer.DayEndQuota.GetParsedText();
            Debug.Log(before.StartsWith("Quota not met yet: 400T of 1.00Qa") && after.StartsWith("Quota met: 1.20Qa of 1.00Qa")
                ? $"[Tour] PASS the 5 PM card follows the day: \"{before}\" became \"{after}\""
                : $"[Tour] FAIL the 5 PM card's quota line: \"{before}\", then \"{after}\"");
            yield return Shot("07c_five_pm_quota_met");
            _gm.Computer.CloseModal();
            s.quotaToday = quota;
            s.earnedToday = earned;
        }

        /// <summary>The visible scroll list whose parent (or first row) has this name.</summary>
        static UnityEngine.UI.ScrollRect ActiveList(string name)
        {
            foreach (var sr in FindObjectsByType<UnityEngine.UI.ScrollRect>())
                if (sr.isActiveAndEnabled && (sr.transform.parent.name == name || sr.content.Find(name) != null)) return sr;
            return null;
        }

        /// <summary>
        /// A list shows a scrollbar exactly when its rows don't fit, and the thumb's share of the track is the share of the
        /// list in view; the bar sits in its own lane, so no row runs under it.
        /// </summary>
        static bool CheckScrollbar(string what, UnityEngine.UI.ScrollRect sr, bool expectBar)
        {
            if (sr == null) { Debug.Log($"[Tour] FAIL scrollbar, {what}: no list found"); return false; }
            Canvas.ForceUpdateCanvases();
            var bar = sr.verticalScrollbar;
            float view = sr.viewport.rect.height, content = sr.content.rect.height;
            bool shown = bar != null && bar.gameObject.activeInHierarchy;
            if (!expectBar)
            {
                Debug.Log(!shown && content <= view + 0.5f
                    ? $"[Tour] PASS scrollbar, {what}: none while the rows fit ({content:0} of {view:0} px)"
                    : $"[Tour] FAIL scrollbar, {what}: shown {shown}, rows {content:0} px in {view:0} px");
                return !shown;
            }
            var track = (RectTransform)bar.handleRect.parent;
            float thumb = bar.handleRect.rect.height / track.rect.height, visible = view / content;
            var barRt = (RectTransform)bar.transform;
            float lane = sr.viewport.InverseTransformPoint(barRt.TransformPoint(barRt.rect.min)).x, rowsEnd = float.NegativeInfinity;
            foreach (RectTransform row in sr.content)
                if (row.gameObject.activeSelf) rowsEnd = Mathf.Max(rowsEnd, sr.viewport.InverseTransformPoint(row.TransformPoint(row.rect.max)).x);
            bool ok = shown && content > view && Mathf.Abs(thumb - visible) < 0.02f && rowsEnd <= lane + 0.5f;
            Debug.Log(ok
                ? $"[Tour] PASS scrollbar, {what}: shown, thumb {thumb:P0} of the track for {visible:P0} of the list in view, rows end {lane - rowsEnd:0} px left of it"
                : $"[Tour] FAIL scrollbar, {what}: shown {shown}, thumb {thumb:P0} for {visible:P0} in view ({content:0} px in {view:0}), rows end at {rowsEnd:0}, bar at {lane:0}");
            return ok;
        }

        /// <summary>Real mouse events grab the scrollbar's thumb and drag it to the bottom: the list follows to its end.</summary>
        IEnumerator ScrollbarDragSegment(UnityEngine.UI.ScrollRect sr)
        {
            if (!CheckScrollbar("Board Room", sr, true)) yield break;
            sr.verticalNormalizedPosition = 1;
            Canvas.ForceUpdateCanvases();
            var cam = _gm.Refs.MainCamera;
            var handle = sr.verticalScrollbar.handleRect;
            var track = (RectTransform)handle.parent;
            Vector2 from = cam.WorldToScreenPoint(handle.TransformPoint(handle.rect.center));
            Vector2 to = cam.WorldToScreenPoint(track.TransformPoint(new Vector2(track.rect.center.x, track.rect.yMin - 40)));
            var mouse = Mouse.current ?? InputSystem.AddDevice<Mouse>();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = from });
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = from }.WithButton(MouseButton.Left, true));
            yield return null;
            for (int i = 1; i <= 12; i++)
            {
                InputSystem.QueueStateEvent(mouse, new MouseState { position = Vector2.Lerp(from, to, i / 12f) }.WithButton(MouseButton.Left, true));
                yield return null;
            }
            InputSystem.QueueStateEvent(mouse, new MouseState { position = to });
            yield return null;
            yield return null;
            float pos = sr.verticalNormalizedPosition;
            Debug.Log(pos < 0.01f
                ? $"[Tour] PASS scrollbar, Board Room: a real mouse drag of the thumb scrolled the list to its end ({pos:0.00})"
                : $"[Tour] FAIL scrollbar, Board Room: after dragging the thumb down the list is at {pos:0.00} (1 = top, 0 = end)");
            yield return Shot("22b_board_room_scrolled");
            sr.verticalNormalizedPosition = 1;
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

        /// <summary>The title screen's buttons and version line are inside the window, none overlapping another.</summary>
        void CheckTitleFits()
        {
            Transform column = null;
            foreach (var rt in FindObjectsByType<RectTransform>())
                if (rt.name == "Column" && rt.parent && rt.parent.name == "Title") column = rt;
            var boxes = new System.Collections.Generic.List<(string, Rect)>();
            foreach (Transform c in column)
                if (c.gameObject.activeInHierarchy && (c.GetComponent<UnityEngine.UI.Button>() || c.name == "Version"))
                {
                    var w = new Vector3[4];
                    ((RectTransform)c).GetWorldCorners(w);
                    boxes.Add((c.name, Rect.MinMaxRect(w[0].x, w[0].y, w[2].x, w[2].y)));
                }
            var bad = new System.Collections.Generic.List<string>();
            var screen = new Rect(0, 0, Screen.width, Screen.height);
            for (int i = 0; i < boxes.Count; i++)
            {
                var (n, r) = boxes[i];
                if (r.xMin < -0.5f || r.yMin < -0.5f || r.xMax > screen.width + 0.5f || r.yMax > screen.height + 0.5f) bad.Add($"{n} runs off the window ({r.yMin:0}..{r.yMax:0} of {Screen.height})");
                for (int j = i + 1; j < boxes.Count; j++)
                    if (r.Overlaps(boxes[j].Item2)) bad.Add($"{n} overlaps {boxes[j].Item1}");
            }
            Debug.Log(bad.Count == 0 && boxes.Count >= 5
                ? $"[Tour] PASS the title screen fits: {boxes.Count} items inside the window, none overlapping (column x{column.localScale.x:0.00})"
                : $"[Tour] FAIL the title screen ({boxes.Count} items): {string.Join(" | ", bad)}");
        }

        /// <summary>CorpOS's top bar names every division in full: the fifteen authored ones and the multiverse's timelines.</summary>
        IEnumerator TopBarNamesSegment()
        {
            TMPro.TMP_Text corp = null;
            foreach (var t in FindObjectsByType<TMPro.TMP_Text>())
                if (t.name == "Corp" && t.transform.parent && t.transform.parent.name == "TopBar") corp = t;
            int was = M.State.reorgs, n = 0;
            var cut = new System.Collections.Generic.List<string>();
            string widest = "";
            float widestW = 0;
            foreach (int r in new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 23, 100, 10000 })
            {
                M.State.reorgs = r;
                yield return null;
                yield return null;
                corp.ForceMeshUpdate();
                n++;
                if (corp.isTextTruncated) cut.Add(corp.text);
                if (corp.textBounds.size.x > widestW) { widestW = corp.textBounds.size.x; widest = corp.text; }
            }
            M.State.reorgs = was;
            yield return null;
            Debug.Log(corp != null && cut.Count == 0
                ? $"[Tour] PASS the top bar names all {n} divisions tried in full (widest \"{widest}\", {widestW:0} of {corp.rectTransform.rect.width:0} px)"
                : $"[Tour] FAIL the top bar cuts division names: {string.Join(" | ", cut)}");
        }

        static readonly System.Collections.Generic.SortedDictionary<string, string> _cut =
            new System.Collections.Generic.SortedDictionary<string, string>();
        static int _cutShots;

        /// <summary>
        /// Text that TextMesh Pro had to cut (an ellipsis, or lines dropped) on the CorpOS monitor, the overlays and the
        /// menus. The activity feed's long lines and the settings' save-folder path end in "…" on purpose.
        /// </summary>
        void CollectCutText(string shot)
        {
            _cutShots++;
            var feed = new System.Collections.Generic.HashSet<TMPro.TMP_Text>(_gm.Computer.FeedLines);
            foreach (var t in FindObjectsByType<TMPro.TMP_Text>())
            {
                if (!t.isActiveAndEnabled || t.color.a < 0.05f || string.IsNullOrEmpty(t.text) || feed.Contains(t)) continue;
                if (t.text.Contains("agentclicker_save.json")) continue;
                var root = t.canvas ? t.canvas.rootCanvas : null;
                if (root == null || (root.name != "CorpOS Canvas" && root.renderMode == RenderMode.WorldSpace)) continue;
                bool hidden = false;
                foreach (var g in t.GetComponentsInParent<CanvasGroup>())
                    if (g.alpha < 0.05f) { hidden = true; break; }
                if (hidden) continue;
                t.ForceMeshUpdate();
                if (!t.isTextTruncated) continue;
                string key = (t.transform.parent ? t.transform.parent.name + "/" : "") + t.name;
                if (!_cut.ContainsKey(key))
                    _cut[key] = $"{key} \"{t.GetParsedText().Replace("\n", " ")}\" ({shot})";
            }
        }

        static readonly System.Collections.Generic.SortedDictionary<string, string> _offscreen =
            new System.Collections.Generic.SortedDictionary<string, string>();

        static readonly string[] ScreenCanvases = { "Menu Canvas", "Overlay Canvas", "Call Canvas", "Zoom Chip Canvas" };

        static string Trim(string s, int n) => s.Length <= n ? s : s.Substring(0, n) + "…";

        string OverlayCanvasSize()
        {
            var c = GameObject.Find("Overlay Canvas");
            var r = c ? ((RectTransform)c.transform).rect : default;
            return $"{r.width:0}×{r.height:0} units";
        }

        /// <summary>
        /// Buttons and text on the screen-space canvases (menus, overlays, calls, the zoom chip) that reach past the window's
        /// edges: a 32:9 window once put the Settings card's DONE button below the bottom of the screen.
        /// </summary>
        void CollectOffscreen(string shot)
        {
            var corners = new Vector3[4];
            void Test(Component c, Rect r, string what)
            {
                if (r.xMin >= -1 && r.yMin >= -1 && r.xMax <= Screen.width + 1 && r.yMax <= Screen.height + 1) return;
                string key = (c.transform.parent ? c.transform.parent.name + "/" : "") + c.name;
                if (!_offscreen.ContainsKey(key))
                    _offscreen[key] = $"{key} {what} at x {r.xMin:0}–{r.xMax:0}, y {r.yMin:0}–{r.yMax:0} ({shot})";
            }
            bool Visible(Component c)
            {
                if (!c.gameObject.activeInHierarchy) return false;
                var canvas = c.GetComponentInParent<Canvas>();
                var root = canvas ? canvas.rootCanvas : null;
                if (root == null || root.renderMode != RenderMode.ScreenSpaceOverlay || System.Array.IndexOf(ScreenCanvases, root.name) < 0) return false;
                foreach (var g in c.GetComponentsInParent<CanvasGroup>())
                    if (g.alpha < 0.05f) return false;
                // rows of a scrolling list may sit outside their (masked) viewport
                var mask = c.GetComponentInParent<UnityEngine.UI.RectMask2D>();
                return mask == null;
            }
            foreach (var b in FindObjectsByType<UnityEngine.UI.Selectable>())
            {
                if (!b.isActiveAndEnabled || !Visible(b)) continue;
                ((RectTransform)b.transform).GetWorldCorners(corners);
                Test(b, Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y), "button");
            }
            foreach (var t in FindObjectsByType<TMPro.TMP_Text>())
            {
                if (!t.isActiveAndEnabled || t.color.a < 0.05f || string.IsNullOrEmpty(t.text) || !Visible(t)) continue;
                t.ForceMeshUpdate();
                if (t.textInfo.characterCount == 0) continue;
                var b = t.textBounds;
                var lo = t.transform.TransformPoint(b.min);
                var hi = t.transform.TransformPoint(b.max);
                Test(t, Rect.MinMaxRect(Mathf.Min(lo.x, hi.x), Mathf.Min(lo.y, hi.y), Mathf.Max(lo.x, hi.x), Mathf.Max(lo.y, hi.y)),
                     $"\"{Trim(t.GetParsedText().Replace("\n", " "), 50)}\"");
            }
        }

        /// <summary>The credits card names a big number in words (none for small ones), on one line inside the card.</summary>
        void CheckCreditsLabel(string when, string words)
        {
            var t = _gm.Computer.CreditsLabel;
            t.ForceMeshUpdate();
            string text = t.GetParsedText();
            float right = t.textBounds.max.x, width = t.rectTransform.rect.xMax;
            bool named = words == null ? text == "COMPUTE CREDITS" : text.EndsWith(words);
            Debug.Log(named && t.textInfo.lineCount == 1 && right <= width + 0.5f
                ? $"[Tour] PASS credits label {when}: \"{text}\" ({right:0} of {width:0} px)"
                : $"[Tour] FAIL credits label {when}: \"{text}\", {t.textInfo.lineCount} lines, {right:0} of {width:0} px, expected {words ?? "no name"}");
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
            CheckFeedUnderToasts("three toasts");
        }

        /// <summary>No activity feed line shows below the top of the toast stack: lines under it are hidden whole.</summary>
        void CheckFeedUnderToasts(string when)
        {
            var feed = _gm.Computer.FeedLines;
            var space = (RectTransform)feed[0].transform.parent;
            float Y(RectTransform rt, float y) => space.InverseTransformPoint(rt.TransformPoint(new Vector3(0, y, 0))).y;
            float stackTop = float.NegativeInfinity;
            foreach (var (card, _) in _gm.Computer.ToastCards()) stackTop = Mathf.Max(stackTop, Y(card, card.rect.yMax));
            int shown = 0, hidden = 0;
            var under = new System.Collections.Generic.List<string>();
            foreach (var t in feed)
            {
                if (!t.gameObject.activeInHierarchy) { hidden++; continue; }
                if (string.IsNullOrEmpty(t.text)) continue;
                shown++;
                var r = t.rectTransform;
                if (Y(r, r.rect.yMin) < stackTop - 0.5f) under.Add(t.GetParsedText());
            }
            Debug.Log(hidden > 0 && under.Count == 0
                ? $"[Tour] PASS activity feed under {when}: {hidden} lines hidden whole, {shown} shown above the stack"
                : $"[Tour] FAIL activity feed under {when}: {hidden} hidden, {shown} shown, showing under a toast: {string.Join(" | ", under)}");
        }

        /// <summary>Every activity feed line ends inside the panel; one too long for it ends in "…".</summary>
        void CheckFeedFits()
        {
            int shown = 0, shortened = 0;
            var bad = new System.Collections.Generic.List<string>();
            foreach (var t in _gm.Computer.FeedLines)
            {
                if (!t.gameObject.activeInHierarchy || string.IsNullOrEmpty(t.text)) continue;
                t.ForceMeshUpdate();
                shown++;
                if (t.isTextTruncated) shortened++;
                var r = t.rectTransform;
                if (t.textBounds.max.x > r.rect.xMax + 0.5f) bad.Add($"\"{t.GetParsedText()}\" runs {t.textBounds.max.x - r.rect.xMax:0} px past the panel");
                if (t.textInfo.lineCount != 1) bad.Add($"\"{t.GetParsedText()}\" takes {t.textInfo.lineCount} lines");
            }
            Debug.Log(shown >= 5 && shortened > 0 && bad.Count == 0
                ? $"[Tour] PASS activity feed: {shown} lines, all inside the panel, {shortened} too long ending in \"…\""
                : $"[Tour] FAIL activity feed: {shown} lines, {shortened} shortened; {string.Join(" | ", bad)}");
        }

        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            string path = Path.Combine(OutputDir, name + ".png");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("[Tour] " + path);
            LogSmallestText(name);
            CollectCutText(name);
            CollectOffscreen(name);
            yield return null;
            yield return null;
        }
    }
}
