using AgentClicker.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AgentClicker.UI
{
    /// <summary>Screen-space HUD: fades, the morning title card, the night screen, the ending and control hints.</summary>
    public class Overlay : MonoBehaviour
    {
        GameManager _gm;
        RectTransform _root;
        Image _fade;
        float _fadeTarget, _fadeSpeed = 1.5f;

        CanvasGroup _dayCard, _night, _ending, _hint;
        TextMeshProUGUI _dayTitle, _daySub, _dayChapter, _nightTitle, _nightBody, _endingStats, _hintText, _fps;
        float _dayCardUntil, _fpsTimer, _fpsAccum;
        int _fpsFrames;

        public void Init(GameManager gm)
        {
            _gm = gm;
            var go = new GameObject("Overlay Canvas", typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            _root = (RectTransform)go.transform;

            // control hint (bottom-left, office view only)
            _hint = Group("Hint");
            var hintBg = UIKit.Panel(_hint.transform, "Bg", new Color(0, 0, 0, 0.55f), 10);
            hintBg.rectTransform.Anchor(0, 0, 0, 0);
            hintBg.rectTransform.pivot = Vector2.zero;
            hintBg.rectTransform.anchoredPosition = new Vector2(20, 20);
            hintBg.rectTransform.sizeDelta = new Vector2(560, 40);
            _hintText = UIKit.Text(hintBg.transform, "Text", "", 16, Theme.Text, TextAlignmentOptions.Center, UIFonts.Medium);
            _hintText.rectTransform.Fill();

            // day title card
            _dayCard = Group("DayCard");
            var band = UIKit.Image(_dayCard.transform, "Band", new Color(0.02f, 0.03f, 0.06f, 0.55f));
            band.rectTransform.Anchor(0, 0.5f, 1, 0.5f).Insets(0, -75, 0, -160);
            _dayTitle = UIKit.Text(_dayCard.transform, "Title", "", 72, Color.white, TextAlignmentOptions.Center, UIFonts.Bold);
            _dayTitle.rectTransform.Anchor(0, 0.5f, 1, 0.5f).Insets(0, 10, 0, -110);
            _dayTitle.outlineWidth = 0.12f;
            _dayTitle.outlineColor = new Color32(0, 0, 0, 140);
            _daySub = UIKit.Text(_dayCard.transform, "Sub", "", 24, Theme.Text, TextAlignmentOptions.Center, UIFonts.Medium);
            _daySub.rectTransform.Anchor(0, 0.5f, 1, 0.5f).Insets(0, -40, 0, 0);
            _daySub.outlineWidth = 0.15f;
            _daySub.outlineColor = new Color32(0, 0, 0, 160);
            _dayChapter = UIKit.Text(_dayCard.transform, "Chapter", "", 22, Theme.Gold, TextAlignmentOptions.Center, UIFonts.Bold);
            _dayChapter.rectTransform.Anchor(0, 0.5f, 1, 0.5f).Insets(0, 116, 0, -150);
            _dayChapter.outlineWidth = 0.18f;
            _dayChapter.outlineColor = new Color32(0, 0, 0, 170);

            _fps = UIKit.Text(_root, "Fps", "", 15, Theme.Good, TextAlignmentOptions.TopRight, UIFonts.Mono);
            _fps.rectTransform.Anchor(1, 1, 1, 1);
            _fps.rectTransform.pivot = new Vector2(1, 1);
            _fps.rectTransform.anchoredPosition = new Vector2(-12, -8);
            _fps.rectTransform.sizeDelta = new Vector2(240, 24);

            // fade
            _fade = UIKit.Image(_root, "Fade", Color.black, false);
            _fade.rectTransform.Fill();

            // night screen
            _night = Group("Night", raycast: true);
            UIKit.Image(_night.transform, "Bg", Theme.Hex("#05070D")).rectTransform.Fill();
            var stars = UIKit.Image(_night.transform, "Moon", Theme.Hex("#F4F1DE"));
            stars.sprite = UIKit.Circle;
            stars.rectTransform.Center(90, 90, 520, 260);
            _nightTitle = UIKit.Text(_night.transform, "Title", "", 64, Color.white, TextAlignmentOptions.Center, UIFonts.Bold);
            _nightTitle.rectTransform.Center(1200, 90, 0, 140);
            _nightBody = UIKit.Text(_night.transform, "Body", "", 24, Theme.TextDim, TextAlignmentOptions.Center, UIFonts.Medium);
            _nightBody.rectTransform.Center(1100, 160, 0, 10);
            var clockIn = UIKit.Button(_night.transform, "ClockIn", Theme.Accent, () => _gm.ClockIn(), 16);
            clockIn.GetComponent<RectTransform>().Center(380, 84, 0, -150);
            clockIn.Label("CLOCK IN  →", 30, Theme.Bg);
            clockIn.gameObject.AddComponent<Pulse>().ScaleAmount = 0.02f;

            // ending
            _ending = Group("Ending", raycast: true);
            var endBg = UIKit.Image(_ending.transform, "Shade", new Color(0.02f, 0.02f, 0.05f, 0.72f));
            endBg.rectTransform.Anchor(0, 0, 1, 0.42f).Insets(0, 0, 0, 0);
            UIKit.Text(_ending.transform, "Title", "100% AUTOMATED", 76, Theme.Gold, TextAlignmentOptions.Center, UIFonts.Bold)
                 .rectTransform.Anchor(0, 0.27f, 1, 0.4f).Insets(0, 0, 0, 0);
            _endingStats = UIKit.Text(_ending.transform, "Stats", "", 22, Color.white, TextAlignmentOptions.Center, UIFonts.Medium);
            _endingStats.rectTransform.Anchor(0, 0.13f, 1, 0.27f).Insets(40, 0, 40, 0);
            var epi = UIKit.Button(_ending.transform, "Epilogue", Theme.Accent, () => _gm.PlayEpilogue(), 14);
            var epiRt = epi.GetComponent<RectTransform>();
            epiRt.anchorMin = epiRt.anchorMax = new Vector2(0.5f, 0.07f);
            epiRt.anchoredPosition = new Vector2(-170, 0);
            epiRt.sizeDelta = new Vector2(320, 64);
            epi.Label("EPILOGUE  →", 22, Theme.Bg);
            var keep = UIKit.Button(_ending.transform, "Keep", Theme.PanelLight, () => _gm.KeepPlaying(), 14);
            var keepRt = keep.GetComponent<RectTransform>();
            keepRt.anchorMin = keepRt.anchorMax = new Vector2(0.5f, 0.07f);
            keepRt.anchoredPosition = new Vector2(170, 0);
            keepRt.sizeDelta = new Vector2(320, 64);
            keep.Label("KEEP PLAYING", 22, Theme.Text);

            Show(_night, false, true);
            Show(_ending, false, true);
            Show(_dayCard, false, true);
            _fade.color = Color.black;
            _fadeTarget = 0;
        }

        CanvasGroup Group(string name, bool raycast = false)
        {
            var rt = UIKit.Rect(name, _root);
            rt.Fill();
            var cg = rt.gameObject.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = raycast;
            cg.interactable = raycast;
            return cg;
        }

        static void Show(CanvasGroup g, bool on, bool instant = false)
        {
            if (instant)
            {
                g.alpha = on ? 1 : 0;
                g.gameObject.SetActive(on);
            }
            Fader.Set(g, on ? 1 : 0);
        }

        public void ShowDayCard(int day)
        {
            Show(_night, false);
            _dayChapter.text = StoryDatabase.ChapterLine(_gm.Model);
            _dayTitle.text = $"DAY {day}";
            _daySub.text = $"{FlavorText.Weekday(day)} · 9:00 AM · Click the monitor to log in";
            Show(_dayCard, true);
            _dayCardUntil = Time.unscaledTime + 3.2f;
            _fade.enabled = true;
            _fade.color = Color.black;
            _fadeTarget = 0;
            _fadeSpeed = 0.9f;
        }

        CanvasGroup _chapter;
        TextMeshProUGUI _chapterKicker, _chapterTitle, _chapterBlurb;
        float _chapterUntil;

        // the banner sits in the upper part of the screen, clear of SHIP CODE and the store
        const float BannerY = 0.74f;

        /// <summary>Mid-day story beat: "CHAPTER 3 · Scale Out".</summary>
        public void ShowChapterBanner(int chapter)
        {
            if (_chapter == null)
            {
                _chapter = Group("Chapter");
                var band = UIKit.Image(_chapter.transform, "Band", new Color(0.02f, 0.03f, 0.06f, 0.8f));
                band.rectTransform.Anchor(0, BannerY, 1, BannerY).Insets(0, -90, 0, -110);
                _chapterKicker = UIKit.Text(_chapter.transform, "Kicker", "", 22, Theme.Gold, TextAlignmentOptions.Center, UIFonts.Bold);
                _chapterKicker.rectTransform.Anchor(0, BannerY, 1, BannerY).Insets(0, 60, 0, -96);
                _chapterTitle = UIKit.Text(_chapter.transform, "Title", "", 64, Color.white, TextAlignmentOptions.Center, UIFonts.Bold);
                _chapterTitle.rectTransform.Anchor(0, BannerY, 1, BannerY).Insets(0, -20, 0, -60);
                _chapterBlurb = UIKit.Text(_chapter.transform, "Blurb", "", 22, Theme.TextDim, TextAlignmentOptions.Center, UIFonts.Medium);
                _chapterBlurb.rectTransform.Anchor(0, BannerY, 1, BannerY).Insets(0, -80, 0, 20);
            }
            _chapterKicker.text = $"CHAPTER {chapter}";
            _chapterTitle.text = StoryDatabase.Chapters[chapter];
            _chapterBlurb.text = StoryDatabase.ChapterBlurbs[chapter];
            _chapter.alpha = 0;
            Fader.Set(_chapter, 1);
            _chapterUntil = Time.unscaledTime + 4f;
        }

        public void ShowNight(double nightEarnings, bool instant = false)
        {
            var m = _gm.Model;
            _nightTitle.text = $"NIGHT {m.State.day}";
            _nightBody.text = nightEarnings > 0
                ? $"You went home. Your agents did not.\nNight shift earnings: <color=#4DD0E1>+{NumberFormat.Credits(nightEarnings)}</color>" +
                  (m.HasBoardPerk("night_owl") ? "  <color=#A4AFC2>(Night Owl x4)</color>" : m.NightBonus > 0 ? "  <color=#A4AFC2>(Mini Fridge bonus)</color>" : "")
                : "You went home. Your agents did not.";
            Show(_night, true, instant);
        }

        public void ShowEnding()
        {
            var s = _gm.Model.State;
            _endingStats.text =
                $"Software Factory online on day {s.day}, after {NumberFormat.Duration(s.playSeconds)} at your desk.\n" +
                $"{_gm.Model.TotalAgents} agents · {NumberFormat.Short(s.lifetimeEarned)} credits shipped · ★ {s.stars}\n" +
                "<color=#C9D3E3><i>Your day job now runs itself. Sam never has to work again.</i></color>";
            Show(_ending, true);
        }

        public void HideEnding() => Show(_ending, false);
        public void HideNight() => Show(_night, false, true);
        public void HideDayCard() => Show(_dayCard, false, true);

        public void FadeIn(float speed = 0.9f)
        {
            _fade.enabled = true;
            _fade.color = Color.black;
            _fadeTarget = 0;
            _fadeSpeed = speed;
        }

        void Update()
        {
            if (_gm == null) return;
            var c = _fade.color;
            if (c.a != _fadeTarget)
            {
                c.a = Mathf.MoveTowards(c.a, _fadeTarget, Time.unscaledDeltaTime * _fadeSpeed);
                _fade.color = c;
                _fade.raycastTarget = c.a > 0.5f;
                _fade.enabled = c.a > 0.001f;
            }

            if (_dayCard.gameObject.activeSelf && Time.unscaledTime > _dayCardUntil) Show(_dayCard, false);
            if (_chapter != null && _chapter.gameObject.activeSelf && Time.unscaledTime > _chapterUntil) Show(_chapter, false);

            if (_gm.Settings.showFps)
            {
                _fpsAccum += Time.unscaledDeltaTime;
                _fpsFrames++;
                _fpsTimer -= Time.unscaledDeltaTime;
                if (_fpsTimer <= 0)
                {
                    float ms = _fpsAccum / Mathf.Max(1, _fpsFrames) * 1000f;
                    _fps.text = $"{1000f / ms:0} FPS · {ms:0.0} ms";
                    _fpsTimer = 0.25f;
                    _fpsAccum = 0;
                    _fpsFrames = 0;
                }
            }
            else if (_fps.text.Length > 0) _fps.text = "";

            bool office = _gm.Cam.Mode == Office.CamMode.Office && !_gm.InEnding && !_night.gameObject.activeSelf && !_gm.Menu.Blocking && !_gm.Calls.Busy;
            Fader.Set(_hint, office ? 1 : 0);
            if (office)
                UIKit.Set(_hintText, _gm.Pad.Active
                    ? (_gm.Model.Phase == GamePhase.Login
                        ? "A on the monitor to log in  ·  Right stick to look around"
                        : "View or RB to sit down at the computer  ·  Right stick to look around")
                    : _gm.Model.Phase == GamePhase.Login
                        ? "Click the monitor to log in  ·  Right-drag to look around"
                        : "[Tab] or scroll up to sit down at the computer  ·  Right-drag to look around");
        }
    }

    /// <summary>Animates CanvasGroup alpha toward a target.</summary>
    public class Fader : MonoBehaviour
    {
        float _target;
        CanvasGroup _g;

        public static void Set(CanvasGroup g, float target)
        {
            var f = g.GetComponent<Fader>();
            if (!f) f = g.gameObject.AddComponent<Fader>();
            f._g = g;
            f._target = target;
            if (target > 0) g.gameObject.SetActive(true);
        }

        void Update()
        {
            if (_g == null) return;
            if (_g.alpha != _target) _g.alpha = Mathf.MoveTowards(_g.alpha, _target, Time.unscaledDeltaTime * 3f);
            bool block = _target > 0 && _g.interactable;
            if (_g.blocksRaycasts != block) _g.blocksRaycasts = block;
            if (_g.alpha <= 0 && _target <= 0) gameObject.SetActive(false);
        }
    }
}
