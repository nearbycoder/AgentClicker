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
            yield return new WaitForSeconds(1.0f);

            // ---- menus & intro ------------------------------------------------
            _gm.ShowTitle();
            yield return new WaitForSeconds(2.5f);
            yield return Shot("00a_title");
            _gm.Menu.OpenSettings(() => { });
            yield return new WaitForSeconds(0.6f);
            yield return Shot("00b_settings");
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

            // ---- the ending ---------------------------------------------------
            _gm.Login();
            s.credits = 1e13;
            s.agentCounts[GameDatabase.OrchestratorIndex] = 12;
            M.MarkDirty();
            M.BuyOffice("recliner");
            _gm.Office.Refresh(false);
            _gm.Computer.SelectStoreTab(3);
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
            _gm.Computer.SelectStoreTab(StorePanel.TrophiesTabIndex);
            yield return new WaitForSeconds(0.6f);
            yield return Shot("24_endgame_trophies");

            Debug.Log("[Tour] done");
            Application.Quit();
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

        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            string path = Path.Combine(OutputDir, name + ".png");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("[Tour] " + path);
            yield return null;
            yield return null;
        }
    }
}
