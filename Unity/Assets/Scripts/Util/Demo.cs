using System.Collections;
using System.Linq;
using AgentClicker.Core;
using AgentClicker.Office;
using AgentClicker.UI;
using AgentClicker.Util;
using UnityEngine;
using UnityEngine.UI;

namespace AgentClicker
{
    /// <summary>
    /// Scripted showcase playthrough, used to record the gameplay video:
    ///   AgentClicker.x86_64 -demo [-record out.mp4]
    /// A visible cursor glides to real UI elements and clicks them with genuine Input System events.
    /// </summary>
    public class Demo : Director
    {
        public string RecordPath;
        VideoCapture _video;

        IEnumerator Start()
        {
            InitDirector();
            yield return null;
            M.RandomEventsEnabled = false;
            Debug.Log($"[Demo] start; capture fps {Time.captureFramerate}");
            _gm.SuppressShowcase = false;
            if (!string.IsNullOrEmpty(RecordPath))
            {
                _video = gameObject.AddComponent<VideoCapture>();
                _video.OutputPath = RecordPath;
                _video.Begin();
            }
            yield return Wait(0.5f);
            yield return Script();
            yield return Wait(1.5f);
            _video?.End();
            Debug.Log("[Demo] done");
            Application.Quit();
        }

        // ================================================================== the show
        IEnumerator Script()
        {
            // --- title screen ---------------------------------------------------
            yield return Wait(4.0f);
            yield return ClickName("NEW GAME");

            // --- intro ---------------------------------------------------------------
            for (int i = 0; i < 3; i++)
            {
                yield return Wait(i == 0 ? 3.8f : 3.4f);
                yield return ClickAt(new Vector2(Screen.width * 0.72f, Screen.height * 0.3f));
                if (_gm.Menu.CardsOpen) yield return ClickAt(new Vector2(Screen.width * 0.72f, Screen.height * 0.3f), move: false);
            }

            // --- day 1 -----------------------------------------------------------------
            yield return Wait(2.8f);
            yield return ClickWorld(_gm.Refs.MainScreen.bounds.center);
            yield return Wait(2.6f);                               // dolly into the monitor; CEO memo pops open
            yield return Wait(3.5f);
            yield return ClickName("Close");
            yield return Wait(0.6f);

            yield return MoveToName("ShipButton");
            for (int i = 0; i < 34; i++) yield return Tap(0.13f);   // build focus
            yield return ClickName("Agent0");
            for (int i = 0; i < 14; i++) yield return Tap(0.13f, "ShipButton");
            yield return ClickName("Agent0");
            yield return ClickName("Agent0");
            for (int i = 0; i < 20; i++) yield return Tap(0.12f, "ShipButton");
            M.State.credits += 80;
            yield return ClickName("Agent1");
            yield return Wait(0.8f);

            // gadget: the company mug appears on the desk
            M.State.credits += 60;
            yield return ClickName("TabOFFICE");
            yield return Wait(0.8f);
            yield return ClickName("mug");
            yield return Wait(3.4f);                               // camera showcase
            yield return ClickName("TabAGENTS");

            // --- the phone rings ---------------------------------------------------------
            yield return Wait(1.0f);
            M.RingPhone(CallDatabase.ById("dana_demo"));
            yield return Wait(2.2f);
            yield return ClickName("Answer");
            yield return Wait(4.2f);
            yield return ClickName("Choice2");
            yield return Wait(4.6f);

            // --- a model drop ----------------------------------------------------------------
            M.SpawnDrop();
            yield return Wait(1.2f);
            yield return ClickName("ModelDrop");
            yield return Wait(2.5f);

            // --- time skip: two weeks later -------------------------------------------------
            yield return Skip("TWO WEEKS LATER", "Sam has a fleet now.", "Twelve kinds of problems, solved by forty kinds of agents.", MidGame);

            yield return ClickWorld(_gm.Refs.MainScreen.bounds.center);
            yield return Wait(3.6f);                               // chapter banner
            for (int i = 0; i < 16; i++) yield return Tap(0.12f, "ShipButton");
            M.StartOutage();
            yield return Wait(1.6f);
            for (int i = 0; i < GameDatabase.OutageClicks; i++) yield return Tap(0.2f, "Outage");
            yield return Wait(2.2f);
            yield return ClickName("Agent5");
            yield return ClickName("Agent5");
            yield return Wait(0.6f);

            // look around the office
            yield return ClickName("View");
            yield return Wait(4.5f);
            M.RingPhone(CallDatabase.ById("priya_borrow"));
            yield return Wait(2.0f);
            yield return ClickName("Answer");
            yield return Wait(4.4f);
            yield return ClickName("Choice0");
            yield return Wait(4.6f);
            yield return ClickName("View");                       // sit back down
            yield return Wait(1.8f);

            // --- the end of the day ------------------------------------------------------------
            M.State.dayMinutes = GameDatabase.WorkdayMinutes - 4;
            yield return Wait(3.4f);
            yield return ClickNameUnder("ClockOut", "Modal");
            yield return Wait(4.0f);
            yield return ClickName("GoHome");
            yield return Wait(3.4f);
            yield return ClickName("ClockIn");
            yield return Wait(3.0f);

            // --- time skip: months later --------------------------------------------------
            yield return Skip("MONTHS LATER", "The Factory is within reach.", "Orchestrators manage the agents that manage the agents.", LateGame);
            yield return ClickWorld(_gm.Refs.MainScreen.bounds.center);
            yield return Wait(2.6f);
            yield return ClickName("TabOFFICE");
            yield return Wait(0.6f);
            foreach (var sr in FindObjectsByType<ScrollRect>())
                if (sr.isActiveAndEnabled && sr.GetComponentsInParent<Transform>().Any(t => t.name == "PageOffice")) sr.verticalNormalizedPosition = 0f;
            yield return Wait(0.4f);
            yield return ClickName("recliner");
            yield return Wait(3.6f);
            yield return ClickName("TabFACTORY");
            yield return Wait(2.4f);
            yield return ClickName("Build");
            yield return Wait(12.5f);                              // boot, camera pull-back, feet up
            yield return ClickName("Epilogue");
            for (int i = 0; i < 3; i++)
            {
                yield return Wait(3.4f);
                yield return ClickAt(new Vector2(Screen.width * 0.72f, Screen.height * 0.3f));
                if (_gm.Menu.CardsOpen) yield return ClickAt(new Vector2(Screen.width * 0.72f, Screen.height * 0.3f), move: false);
            }
            yield return Wait(3.0f);
        }

        IEnumerator Skip(string kicker, string title, string body, System.Action setup)
        {
            _gm.Menu.ShowStoryCards(new System.Collections.Generic.List<StoryCard> { new StoryCard(kicker, title, body) }, null);
            yield return Wait(0.4f);
            setup();
            yield return Wait(3.2f);
            _gm.Menu.HideAll();
            yield return Wait(2.6f);
        }
    }
}
