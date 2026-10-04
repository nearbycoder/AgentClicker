using System.Collections;
using System.Linq;
using AgentClicker.Core;
using AgentClicker.Office;
using AgentClicker.UI;
using AgentClicker.Util;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace AgentClicker
{
    /// <summary>
    /// Scripted showcase playthrough, used to record the gameplay video:
    ///   AgentClicker.x86_64 -demo [-record out.mp4]
    /// A visible cursor glides to real UI elements and clicks them with genuine Input System events.
    /// </summary>
    public class Demo : MonoBehaviour
    {
        public string RecordPath;
        GameManager _gm;
        GameModel M => _gm.Model;
        RectTransform _cursor;
        Canvas _cursorCanvas;
        Vector2 _pos;
        VideoCapture _video;

        IEnumerator Start()
        {
            _gm = GetComponent<GameManager>();
            Cursor.visible = false;
            BuildCursor();
            _pos = new Vector2(Screen.width * 0.6f, Screen.height * 0.5f);
            Place(_pos);
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

        void MidGame()
        {
            var s = M.State;
            s.day = 9;
            s.credits = 4e6;
            s.lifetimeEarned = 2.4e7;
            s.handmadeTotal = 3e5;
            s.clicks = 5200;
            s.stars = 6;
            s.titleIndex = 2;
            int[] counts = { 45, 32, 24, 14, 7, 2, 0, 0, 0, 0 };
            for (int i = 0; i < counts.Length; i++) s.agentCounts[i] = counts[i];
            M.MarkDirty();
            foreach (var id in new[] { "duck", "plant", "mech_keyboard", "monitor2", "headphones", "lava_lamp" }) { s.credits += 2e6; M.BuyOffice(id); }
            foreach (var u in GameDatabase.Upgrades) if (u.Cost < 2e5) { s.credits += u.Cost; M.BuyUpgrade(u.Id); }
            s.credits = 4.1e6;
            s.rapport[(int)Person.Dana] = 2;
            s.rapport[(int)Person.Priya] = 1;
            foreach (var id in new[] { "dana_welcome", "gary_keys", "hallucin8_news", "priya_hello", "dana_quota", "drop_first", "dana_promo_dev", "facilities_open" })
                if (!s.mail.Contains(id)) { s.mail.Add(id); s.mailRead.Add(id); }
            s.callsDone.Add("dana_demo");
            s.Phase = GamePhase.Night;
            M.StartNextDay();
            s.dayMinutes = 75;
            _gm.Office.Refresh(false);
            _gm.BeginMorning(true);
        }

        void LateGame()
        {
            var s = M.State;
            s.day = 31;
            s.titleIndex = 5;
            s.lifetimeEarned = 2.2e12;
            s.stars = 24;
            int[] late = { 160, 130, 110, 100, 90, 70, 50, 32, 12, 6 };
            for (int i = 0; i < late.Length; i++) s.agentCounts[i] = late[i];
            M.MarkDirty();
            foreach (var o in GameDatabase.OfficeItems) if (o.Id != "recliner") { s.credits = 1e12; M.BuyOffice(o.Id); }
            foreach (var u in GameDatabase.Upgrades) if (u.Cost < 1e11) { s.credits = 1e13; M.BuyUpgrade(u.Id); }
            s.credits = 3.6e12;
            s.rapport = new[] { 3, 4, 2, 3 };
            foreach (var mail in StoryDatabase.Mail)
                if (!mail.Id.StartsWith("ceo_quarter") && !mail.Id.StartsWith("priya_lunch") && !mail.Id.StartsWith("rex_twist") && !s.mail.Contains(mail.Id))
                { s.mail.Add(mail.Id); s.mailRead.Add(mail.Id); }
            foreach (var c in CallDatabase.Calls) if (c.Story && !s.callsDone.Contains(c.Id)) s.callsDone.Add(c.Id);
            s.Phase = GamePhase.Night;
            M.StartNextDay();
            s.dayMinutes = 7.2f * 60;
            _gm.Office.Refresh(false);
            _gm.BeginMorning(true);
        }

        // ================================================================== cursor & input
        void BuildCursor()
        {
            var go = new GameObject("Demo Cursor", typeof(RectTransform));
            _cursorCanvas = go.AddComponent<Canvas>();
            _cursorCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _cursorCanvas.sortingOrder = 500;
            var img = UIKit.Image(go.transform, "Arrow", Color.white);
            img.sprite = ArrowSprite();
            _cursor = img.rectTransform;
            _cursor.anchorMin = _cursor.anchorMax = Vector2.zero;
            _cursor.pivot = new Vector2(0.08f, 0.94f);
            float scale = Screen.height / 900f;
            _cursor.sizeDelta = new Vector2(30, 40) * scale;
        }

        static Sprite ArrowSprite()
        {
            const int w = 48, h = 64;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Vector2[] poly = { new Vector2(4, 60), new Vector2(4, 10), new Vector2(16, 22), new Vector2(25, 4), new Vector2(32, 8), new Vector2(23, 25), new Vector2(40, 26) };
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float d = DistanceToPolygon(p, poly, out bool inside);
                Color c = inside ? (d > 2.6f ? Color.white : Color.black) : new Color(0, 0, 0, Mathf.Clamp01(1.2f - d) * 0.9f);
                if (!inside && d < 1.2f) c = new Color(0, 0, 0, 1);
                tex.SetPixel(x, y, c);
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.08f, 0.94f), 100);
        }

        static float DistanceToPolygon(Vector2 p, Vector2[] poly, out bool inside)
        {
            inside = false;
            float best = float.MaxValue;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                Vector2 a = poly[i], b = poly[j];
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
                Vector2 ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
            }
            return best;
        }

        void Place(Vector2 screen)
        {
            _cursor.anchoredPosition = screen / _cursorCanvas.scaleFactor;
            var mouse = Mouse.current ?? InputSystem.AddDevice<Mouse>();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = screen });
        }

        IEnumerator MoveTo(Vector2 target, float seconds = 0.5f)
        {
            Vector2 from = _pos;
            float t = 0;
            while (t < seconds)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / seconds));
                // a slight arc reads as a human hand
                Vector2 p = Vector2.Lerp(from, target, k) + Vector2.up * Mathf.Sin(k * Mathf.PI) * 30f * (Screen.height / 900f);
                Place(p);
                yield return null;
            }
            _pos = target;
            Place(_pos);
        }

        IEnumerator Press()
        {
            var mouse = Mouse.current;
            _cursor.localScale = Vector3.one * 0.85f;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = _pos }.WithButton(MouseButton.Left, true));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = _pos }.WithButton(MouseButton.Left, false));
            _cursor.localScale = Vector3.one;
            yield return null;
        }

        IEnumerator ClickAt(Vector2 screen, bool move = true)
        {
            if (move) yield return MoveTo(screen);
            yield return Wait(0.12f);
            yield return Press();
            yield return Wait(0.2f);
        }

        IEnumerator Tap(float gap, string name = null)
        {
            if (name != null)
            {
                var p = ScreenPos(name, null);
                if (p.HasValue && (p.Value - _pos).sqrMagnitude > 400) yield return MoveTo(p.Value, 0.3f);
            }
            var jitter = new Vector2(Random.Range(-14f, 14f), Random.Range(-8f, 8f));
            Place(_pos + jitter);
            var mouse = Mouse.current;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = _pos + jitter }.WithButton(MouseButton.Left, true));
            _cursor.localScale = Vector3.one * 0.85f;
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = _pos + jitter }.WithButton(MouseButton.Left, false));
            _cursor.localScale = Vector3.one;
            yield return Wait(gap);
        }

        IEnumerator ClickWorld(Vector3 world) => ClickAt(_gm.Refs.MainCamera.WorldToScreenPoint(world));

        IEnumerator MoveToName(string name)
        {
            var p = ScreenPos(name, null);
            if (p.HasValue) yield return MoveTo(p.Value);
        }

        IEnumerator ClickName(string name) => ClickNameUnder(name, null);

        IEnumerator ClickNameUnder(string name, string under)
        {
            Vector2? p = null;
            for (int i = 0; i < 60 && !p.HasValue; i++)
            {
                p = ScreenPos(name, under);
                if (!p.HasValue) yield return null;
            }
            if (!p.HasValue) { Debug.LogWarning("[Demo] could not find " + name); yield break; }
            yield return ClickAt(p.Value);
        }

        /// <summary>Screen position of the centre of the first active UI element with this name.</summary>
        Vector2? ScreenPos(string name, string under)
        {
            foreach (var rt in FindObjectsByType<RectTransform>())
            {
                if (rt.name != name || !rt.gameObject.activeInHierarchy) continue;
                if (under != null && !rt.GetComponentsInParent<Transform>().Any(t => t.name == under)) continue;
                var canvas = rt.GetComponentInParent<Canvas>().rootCanvas;
                Vector3 world = rt.TransformPoint(rt.rect.center);
                if (canvas.renderMode == RenderMode.WorldSpace)
                    return (Vector2)_gm.Refs.MainCamera.WorldToScreenPoint(world);
                return RectTransformUtility.WorldToScreenPoint(null, world);
            }
            return null;
        }

        static WaitForSeconds Wait(float s) => new WaitForSeconds(s);
    }
}
