using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AgentClicker.Core;
using AgentClicker.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace AgentClicker
{
    /// <summary>
    /// Base for scripted playthroughs (Demo, Trailer): a visible cursor that glides to real UI elements and
    /// clicks them with genuine Input System events, plus the canned mid/late-game states they jump to.
    /// </summary>
    public abstract class Director : MonoBehaviour
    {
        protected GameManager _gm;
        protected GameModel M => _gm.Model;
        RectTransform _cursor;
        Canvas _cursorCanvas;
        protected Vector2 _pos;

        protected void InitDirector()
        {
            _gm = GetComponent<GameManager>();
            Cursor.visible = false;
            BuildCursor();
            _pos = new Vector2(Screen.width * 0.6f, Screen.height * 0.5f);
            Place(_pos);
        }

        protected bool CursorVisible
        {
            get => _cursorCanvas.enabled;
            set => _cursorCanvas.enabled = value;
        }

        // ================================================================== canned states
        protected void MidGame()
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

        protected void LateGame()
        {
            var s = M.State;
            s.day = 31;
            s.playSeconds = 2 * 3600 + 44 * 60;
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

        protected void Place(Vector2 screen)
        {
            _cursor.anchoredPosition = screen / _cursorCanvas.scaleFactor;
            var mouse = Mouse.current ?? InputSystem.AddDevice<Mouse>();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = screen });
        }

        protected IEnumerator MoveTo(Vector2 target, float seconds = 0.5f)
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

        protected IEnumerator Press()
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

        protected IEnumerator ClickAt(Vector2 screen, bool move = true)
        {
            if (move) yield return MoveTo(screen);
            yield return Wait(0.12f);
            yield return Press();
            yield return Wait(0.2f);
        }

        protected IEnumerator Tap(float gap, string name = null)
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

        protected IEnumerator ClickWorld(Vector3 world) => ClickAt(_gm.Refs.MainCamera.WorldToScreenPoint(world));

        protected IEnumerator MoveToName(string name, float seconds = 0.5f)
        {
            var p = ScreenPos(name, null);
            if (p.HasValue) yield return MoveTo(p.Value, seconds);
        }

        protected IEnumerator ClickName(string name) => ClickNameUnder(name, null);

        protected IEnumerator ClickNameUnder(string name, string under)
        {
            Vector2? p = null;
            for (int i = 0; i < 60 && !p.HasValue; i++)
            {
                p = ScreenPos(name, under);
                if (!p.HasValue) yield return null;
            }
            if (!p.HasValue) { Debug.LogWarning("[Demo] could not find " + name); yield break; }
            yield return MoveTo(p.Value);
            if (LogClicks) Debug.Log($"[Demo] click {name} at {p.Value} → {HitUnderCursor()}");
            yield return ClickAt(p.Value, move: false);
        }

        protected bool LogClicks;

        string HitUnderCursor()
        {
            if (EventSystem.current == null) return "no EventSystem";
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = _pos }, hits);
            return hits.Count == 0 ? "nothing" : hits[0].gameObject.name + " / " + hits[0].gameObject.transform.parent?.name;
        }

        /// <summary>Screen position of the centre of the first active UI element with this name.</summary>
        protected Vector2? ScreenPos(string name, string under)
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

        protected static WaitForSeconds Wait(float s) => new WaitForSeconds(s);
    }
}
