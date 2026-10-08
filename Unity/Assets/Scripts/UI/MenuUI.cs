using System;
using System.Collections.Generic;
using AgentClicker.Core;
using AgentClicker.Util;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AgentClicker.UI
{
    /// <summary>
    /// Screen-space menus: the title screen, pause menu, settings, how-to-play, credits, confirm dialogs and the
    /// full-screen story cards used for the intro and epilogue.
    /// </summary>
    public class MenuUI : MonoBehaviour
    {
        GameManager _gm;
        RectTransform _root;
        CanvasGroup _title, _pause, _settings, _info, _confirm, _cards, _credits;
        TextMeshProUGUI _continueSub, _infoTitle, _infoBody, _confirmText, _cardKicker, _cardTitle, _cardBody, _cardHint, _creditsText;
        Button _continue, _confirmYes;
        Action _confirmAction, _cardsDone, _creditsDone;
        List<StoryCard> _cardList;
        int _cardIndex;
        float _reveal;
        float _creditsY;

        // settings
        internal enum SettingsTab { Graphics, Audio, Gameplay, Controls }
        SettingsTab _tab;
        RectTransform _settingsBody;
        readonly Dictionary<SettingsTab, Button> _settingsTabs = new Dictionary<SettingsTab, Button>();
        Action _settingsBack;

        public bool TitleOpen => _title.gameObject.activeSelf;
        public bool PauseOpen => _pause.gameObject.activeSelf;
        public bool CardsOpen => _cards.gameObject.activeSelf;
        public bool SettingsOpen => _settings.gameObject.activeSelf;
        public bool Blocking => TitleOpen || PauseOpen || _settings.gameObject.activeSelf || _info.gameObject.activeSelf ||
                                _confirm.gameObject.activeSelf || CardsOpen || _credits.gameObject.activeSelf;

        public void Init(GameManager gm)
        {
            _gm = gm;
            var go = new GameObject("Menu Canvas", typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 80;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            _root = (RectTransform)go.transform;

            BuildTitle();
            BuildPause();
            BuildSettings();
            BuildInfo();
            BuildCards();
            BuildCredits();
            BuildConfirm();
            foreach (var g in new[] { _title, _pause, _settings, _info, _confirm, _cards, _credits }) g.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ helpers
        CanvasGroup Layer(string name, Color dim)
        {
            var rt = UIKit.Rect(name, _root);
            rt.Fill();
            var cg = rt.gameObject.AddComponent<CanvasGroup>();
            if (dim.a > 0) UIKit.Image(rt, "Dim", dim, true).rectTransform.Fill();
            return cg;
        }

        Button MenuButton(Transform parent, string label, Action onClick, float x, float y, float w = 380, float h = 58, bool primary = false)
        {
            var b = UIKit.Button(parent, label, primary ? Theme.Accent : Theme.Panel.WithAlpha(0.92f), () =>
            {
                _gm.Sfx.Play(Sound.UiClick, 0.8f);
                onClick();
            }, 12);
            b.GetComponent<RectTransform>().TopLeft(x, y, w, h);
            var t = UIKit.Text(b.transform, "Label", label, 22, primary ? Theme.Bg : Theme.Text, TextAlignmentOptions.MidlineLeft, UIFonts.Bold);
            t.rectTransform.Fill().Insets(24, 0, 16, 0);
            PointerRelay.On(b).Enter = _ => _gm.Sfx.Play(Sound.UiClick, 0.25f, 1.4f);
            return b;
        }

        void Show(CanvasGroup g, bool on)
        {
            g.gameObject.SetActive(on);
            g.alpha = on ? 0 : 1;
            if (on) Fader.Set(g, 1);
        }

        // ------------------------------------------------------------------ title
        void BuildTitle()
        {
            _title = Layer("Title", Color.clear);
            var shade = UIKit.Image(_title.transform, "Shade", new Color(0.03f, 0.04f, 0.07f, 0.86f));
            shade.rectTransform.Anchor(0, 0, 0, 1);
            shade.rectTransform.pivot = new Vector2(0, 0.5f);
            shade.rectTransform.sizeDelta = new Vector2(640, 0);
            var fade = UIKit.Image(_title.transform, "ShadeFade", new Color(0.03f, 0.04f, 0.07f, 0.86f));
            fade.sprite = UIKit.FadeRight;
            fade.rectTransform.Anchor(0, 0, 0, 1);
            fade.rectTransform.pivot = new Vector2(0, 0.5f);
            fade.rectTransform.anchoredPosition = new Vector2(640, 0);
            fade.rectTransform.sizeDelta = new Vector2(320, 0);

            // the logo, tagline, buttons and version line, laid out for a 900-unit-tall screen and scaled down to fit a
            // shorter one (a 21:9 window is about 785 units tall, which put QUIT below its bottom edge)
            _titleColumn = UIKit.Rect("Column", _title.transform).TopLeft(0, 0, 700, 900);
            var col = _titleColumn.transform;
            var t = UIKit.Text(col, "Logo", $"AGENT\n<color={UIKit.Hex(Theme.Accent)}>CLICKER</color>", 104, Color.white,
                               TextAlignmentOptions.TopLeft, UIFonts.Bold);
            t.rectTransform.TopLeft(70, 70, 600, 240);
            t.lineSpacing = -18;
            UIKit.Text(col, "Tagline", "Automate yourself out of a job.\nKeep the paycheck.", 24, Theme.TextDim,
                       TextAlignmentOptions.TopLeft, UIFonts.Medium).rectTransform.TopLeft(74, 300, 560, 70);

            float y = 410;
            _continue = MenuButton(col, "CONTINUE", () => _gm.ContinueGame(), 70, y, primary: true);
            _continueSub = UIKit.Text(_continue.transform, "Sub", "", 15, Theme.Bg.WithAlpha(0.75f), TextAlignmentOptions.MidlineRight, UIFonts.Medium);
            _continueSub.rectTransform.Fill().Insets(0, 0, 20, 0);
            y += 70;
            MenuButton(col, "NEW GAME", () =>
            {
                if (_gm.SavingEnabled && SaveSystem.Load() != null) Confirm("Start a new game? Your current career will be lost.", "START OVER", _gm.NewGame);
                else _gm.NewGame();
            }, 70, y);
            y += 70;
            MenuButton(col, "SETTINGS", () => OpenSettings(() => Show(_title, true)), 70, y);
            y += 70;
            MenuButton(col, "HOW TO PLAY", () => ShowInfo("How to play", HowToPlay), 70, y);
            y += 70;
            MenuButton(col, "CREDITS", () => ShowCredits(() => { }), 70, y);
            if (!Platform.IsWeb)
            {
                y += 70;
                MenuButton(col, "QUIT", () => Confirm("Quit to desktop?", "QUIT", _gm.QuitGame), 70, y);
            }
            y += 58 + 26;
            UIKit.Text(col, "Version", $"v{Application.version} · {Application.companyName} · all labs, models and companies are fictional",
                       15, Theme.TextFaint, TextAlignmentOptions.TopLeft).rectTransform.TopLeft(74, y, 620, 22);
            _titleColumnHeight = y + 22 + 22;
            _titleColumn.sizeDelta = new Vector2(700, _titleColumnHeight);
        }

        RectTransform _titleColumn;
        float _titleColumnHeight;

        /// <summary>The title column fits the screen's height (scaled down when the window is short and wide).</summary>
        void FitTitleColumn()
        {
            float scale = Mathf.Min(1f, _root.rect.height / _titleColumnHeight);
            if (Mathf.Abs(_titleColumn.localScale.x - scale) > 0.001f) _titleColumn.localScale = new Vector3(scale, scale, 1f);
        }

        public void ShowTitle()
        {
            HideAll();
            var save = _gm.SavingEnabled ? SaveSystem.Load() : null;
            bool canContinue = save != null && save.introSeen;
            _continue.gameObject.SetActive(canContinue);
            if (canContinue)
            {
                var title = GameDatabase.Titles[Mathf.Clamp(save.titleIndex, 0, GameDatabase.Titles.Length - 1)].Name;
                _continueSub.text = save.reorgs > 0 ? $"{GameDatabase.DivisionName(save.reorgs)} · Day {save.day}" : $"Day {save.day} · {title}";
            }
            Show(_title, true);
        }

        public void HideAll()
        {
            foreach (var g in new[] { _title, _pause, _settings, _info, _confirm, _cards, _credits }) g.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ pause
        void BuildPause()
        {
            _pause = Layer("Pause", new Color(0.02f, 0.03f, 0.06f, 0.72f));
            var card = UIKit.Panel(_pause.transform, "Card", Theme.Panel, 22);
            card.rectTransform.Center(460, 520);
            UIKit.Text(card.transform, "Title", "PAUSED", 40, Color.white, TextAlignmentOptions.Top, UIFonts.Bold).rectTransform.TopLeft(0, 34, 460, 50);
            float y = 110;
            MenuButton(card.transform, "RESUME", ClosePause, 40, y, primary: true);
            y += 70;
            MenuButton(card.transform, "SETTINGS", () => { _pause.gameObject.SetActive(false); OpenSettings(() => Show(_pause, true)); }, 40, y);
            y += 70;
            MenuButton(card.transform, "HOW TO PLAY", () => ShowInfo("How to play", HowToPlay), 40, y);
            y += 70;
            MenuButton(card.transform, "SAVE & EXIT TO TITLE", () => { ClosePause(); _gm.QuitToTitle(); }, 40, y);
            y += 70;
            if (!Platform.IsWeb) MenuButton(card.transform, "QUIT TO DESKTOP", () => Confirm("Save and quit to desktop?", "QUIT", _gm.QuitGame), 40, y);
            else if (Platform.CanFullscreen)
            {
                // browsers only go fullscreen shortly after a tap or click: ask right away, while the press that pressed the button
                // still counts; if that didn't work (a gamepad press isn't one the browser sees), Unity asks at the next input
                _fullscreen = MenuButton(card.transform, "FULLSCREEN", () =>
                {
                    bool want = !Screen.fullScreen;
                    Platform.RequestFullscreen(want);
                    SetFullscreenLabel(want);
                    Debug.Log("[Menu] fullscreen " + (want ? "on" : "off") + " requested");
                    StartCoroutine(FullscreenFallback(want));
                }, 40, y);
            }
        }

        Button _fullscreen;

        System.Collections.IEnumerator FullscreenFallback(bool want)
        {
            yield return new WaitForSecondsRealtime(0.5f);
            if (Screen.fullScreen != want)
            {
                Debug.Log("[Menu] fullscreen: the browser didn't switch yet, asking again at the next tap or click");
                Screen.fullScreen = want;
            }
        }

        void SetFullscreenLabel(bool full)
        {
            if (_fullscreen) _fullscreen.GetComponentInChildren<TMP_Text>().text = full ? "EXIT FULLSCREEN" : "FULLSCREEN";
        }

        public void OpenPause()
        {
            if (Blocking || _gm.InEnding) return;
            Show(_pause, true);
            SetFullscreenLabel(Screen.fullScreen);
            Time.timeScale = 0f;
            _gm.Sfx.DuckMusic(0.5f);
        }

        public void ClosePause()
        {
            _pause.gameObject.SetActive(false);
            Time.timeScale = 1f;
            _gm.Sfx.DuckMusic(1f);
        }

        // ------------------------------------------------------------------ settings
        void BuildSettings()
        {
            _settings = Layer("Settings", new Color(0.02f, 0.03f, 0.06f, 0.8f));
            var card = UIKit.Panel(_settings.transform, "Card", Theme.Panel, 22);
            card.rectTransform.Center(1000, 760);
            UIKit.Text(card.transform, "Title", "SETTINGS", 34, Color.white, TextAlignmentOptions.TopLeft, UIFonts.Bold).rectTransform.TopLeft(40, 30, 400, 44);
            string[] tabs = { "GRAPHICS", "AUDIO", "GAMEPLAY", "CONTROLS" };
            for (int i = 0; i < tabs.Length; i++)
            {
                var tab = (SettingsTab)i;
                var b = UIKit.Button(card.transform, tabs[i], Theme.PanelLight, () => { _gm.Sfx.Play(Sound.UiClick, 0.6f); SetSettingsTab(tab); }, 8);
                b.GetComponent<RectTransform>().TopLeft(40 + i * 160, 92, 150, 40);
                b.Label(tabs[i], 16, Theme.Text, UIFonts.Bold);
                _settingsTabs[tab] = b;
            }
            _settingsBody = UIKit.Rect("Body", card.transform).TopLeft(40, 150, 920, 520);
            var reset = UIKit.Button(card.transform, "Defaults", Theme.PanelLight, () =>
            {
                _gm.Settings = new GameSettings();
                _gm.ApplySettings();
                SetSettingsTab(_tab);
            }, 10);
            reset.GetComponent<RectTransform>().TopLeft(40, 690, 220, 48);
            reset.Label("RESET TO DEFAULTS", 15, Theme.TextDim, UIFonts.Bold);
            var done = UIKit.Button(card.transform, "Done", Theme.Accent, CloseSettings, 10);
            done.GetComponent<RectTransform>().TopLeft(760, 690, 200, 48);
            done.Label("DONE", 20, Theme.Bg);
        }

        public void OpenSettings(Action back)
        {
            _settingsBack = back;
            _title.gameObject.SetActive(false);
            Show(_settings, true);
            SetSettingsTab(_tab);
        }

        void CloseSettings()
        {
            _gm.Sfx.Play(Sound.UiClick, 0.8f);
            _gm.Settings.Save();
            _settings.gameObject.SetActive(false);
            _settingsBack?.Invoke();
        }

        internal void SetSettingsTab(SettingsTab tab)
        {
            _tab = tab;
            foreach (var kv in _settingsTabs) kv.Value.GetComponent<Image>().color = kv.Key == tab ? Theme.Accent2 : Theme.PanelLight;
            foreach (Transform c in _settingsBody) Destroy(c.gameObject);
            var s = _gm.Settings;
            float y = 0;
            void Next() => y += 52;
            switch (tab)
            {
                case SettingsTab.Graphics:
                    Stepper("Quality preset", GameSettings.QualityNames, () => s.quality, v => s.quality = v, y,
                            "Low: no SSAO, hard shadows, 85% scale · Ultra: 4K shadows"); Next();
                    if (!Platform.IsWeb) // the browser owns the window and the frame rate
                    {
                        Stepper("Display mode", GameSettings.DisplayModeNames, () => s.displayMode, v => s.displayMode = v, y); Next();
                        var resNames = new List<string> { "Current window" };
                        for (int i = 0; i < SettingsApplier.Resolutions.Count; i++) resNames.Add(SettingsApplier.ResolutionName(i));
                        Stepper("Resolution", resNames.ToArray(), () => s.resolutionIndex + 1, v => s.resolutionIndex = v - 1, y); Next();
                        Toggle("V-Sync", () => s.vsync, v => s.vsync = v, y); Next();
                        Stepper("Frame rate limit", GameSettings.FpsCapNames, () => s.fpsCap, v => s.fpsCap = v, y); Next();
                    }
                    Slider("Render scale", 0.5f, 1f, () => s.renderScale, v => s.renderScale = v, y, v => $"{v * 100:0}%"); Next();
                    Slider("Field of view", 40f, 70f, () => s.fieldOfView, v => s.fieldOfView = v, y, v => $"{v:0}°"); Next();
                    Toggle("Post-processing (bloom, AO, grading)", () => s.postProcessing, v => s.postProcessing = v, y); Next();
                    Toggle("Show FPS counter", () => s.showFps, v => s.showFps = v, y); Next();
                    Toggle("Save power when in background", () => s.throttleInBackground, v => s.throttleInBackground = v, y,
                           "15 fps while another window is in front");
                    break;
                case SettingsTab.Audio:
                    Slider("Master volume", 0, 1, () => s.masterVolume, v => s.masterVolume = v, y, Pct); Next();
                    Slider("Sound effects", 0, 1, () => s.sfxVolume, v => s.sfxVolume = v, y, Pct); Next();
                    Slider("Music", 0, 1, () => s.musicVolume, v => s.musicVolume = v, y, Pct); Next();
                    Slider("Office ambience", 0, 1, () => s.ambienceVolume, v => s.ambienceVolume = v, y, Pct); Next();
                    Toggle("Mute when in background", () => s.muteInBackground, v => s.muteInBackground = v, y); Next();
                    Toggle("Mute all sound", () => s.muted, v => s.muted = v, y, "M, or the ♪ button on the CorpOS top bar");
                    break;
                case SettingsTab.Gameplay:
                    Stepper("Work day length", GameSettings.DayLengthNames, () => s.dayLength, v => s.dayLength = v, y,
                            "Real time from 9:00 AM to 5:00 PM"); Next();
                    Toggle("Show off new gadgets (camera)", () => s.purchaseShowcase, v => s.purchaseShowcase = v, y); Next();
                    Toggle("Run the work day when I'm away", () => s.autopilotDay, v => s.autopilotDay = v, y,
                           "Idle after 5 PM: your agents clock out, go home and log back in"); Next();
                    Toggle("Reduce motion", () => s.reduceMotion, v => s.reduceMotion = v, y,
                           "Camera cuts instead of flying; buttons and numbers hold still"); Next();
                    Toggle("Tutorial tips", () => s.tutorialTips, v => s.tutorialTips = v, y); Next();
                    Toggle("Open important emails automatically", () => s.autoOpenStoryMail, v => s.autoOpenStoryMail = v, y); Next();
                    Slider("Mouse look sensitivity", 0.25f, 3f, () => s.mouseSensitivity, v => s.mouseSensitivity = v, y, v => $"{v:0.00}x"); Next();
                    Stepper("Number format", GameSettings.NumberStyleNames, () => s.numberStyle, v => s.numberStyle = v, y,
                            "How very large numbers are written"); Next();
                    SaveFileRow(y);
                    break;
                default:
                    var t = UIKit.Text(_settingsBody, "Controls",
                        Row("", "<color=#A4AFC2><size=15>KEYBOARD & MOUSE</size></color>", "<color=#A4AFC2><size=15>GAMEPAD</size></color>",
                            "<color=#A4AFC2><size=15>TOUCH</size></color>") +
                        Row("Point and click", "Mouse", "Left stick · A", "Tap") +
                        Row("Ship code", "SHIP CODE · Space · Enter", "RT · X", "Tap SHIP CODE") +
                        Row("Look around / sit down", "Tab · on-screen button", "View", "On-screen button") +
                        Row("Orbit the office", "Right mouse drag", "Right stick", "Drag the room") +
                        Row("Zoom", "Mouse wheel (office view)", "LB · RB", "Pinch") +
                        Row("Scroll a list", "Mouse wheel", "Right stick", "Drag the list") +
                        Row("Answer / decline the phone", "E · Q", "Y · B", "Tap a button") +
                        Row("Pick a reply on a call", "1 · 2 · 3", "D-pad ← ↑ →", "Tap a reply") +
                        Row("Pause menu", "Esc · ⚙ button", "Start", "⚙ button") +
                        Row("Back / close", "Esc", "B", "On-screen buttons") +
                        Row("Mute all sound", "M · ♪ button", "♪ button", "♪ button") +
                        (Platform.IsWeb ? "" : Row("Screenshot", "F12", "", "")), 19, Theme.Text, TextAlignmentOptions.TopLeft, UIFonts.Medium);
                    t.rectTransform.TopLeft(0, 0, 920, 520);
                    t.lineSpacing = 18;
                    break;
            }
        }

        static string Pct(float v) => $"{v * 100:0}%";
        static string Row(string a, string b, string c, string d) => $"<color=#A4AFC2>{a}</color><pos=30%>{b}<pos=59%>{c}<pos=80%>{d}\n";

        RectTransform SettingRow(string label, float y, string hint = null)
        {
            var row = UIKit.Rect(label, _settingsBody).TopLeft(0, y, 920, 50);
            UIKit.Image(row, "Line", Theme.Border.WithAlpha(0.5f)).rectTransform.Anchor(0, 0, 1, 0).Insets(0, 0, 0, -1);
            var t = UIKit.Text(row, "Label", label, 19, Theme.Text, TextAlignmentOptions.MidlineLeft, UIFonts.Medium);
            t.rectTransform.TopLeft(0, 0, 440, hint == null ? 50 : 32);
            if (hint != null)
            {
                // one line, never spilling into the next row (a long save-folder path ends in "…")
                var h = UIKit.Text(row, "Hint", hint, 15, Theme.TextFaint, TextAlignmentOptions.TopLeft);
                h.rectTransform.TopLeft(0, 30, 540, 20);
                h.textWrappingMode = TextWrappingModes.NoWrap;
                h.overflowMode = TextOverflowModes.Ellipsis;
            }
            return row;
        }

        /// <summary>Keep a copy of the career or bring one in: download / load in the browser, the save folder on the desktop.</summary>
        void SaveFileRow(float y)
        {
            Button RowButton(RectTransform row, string label, float x, float w, Action onClick)
            {
                var b = UIKit.Button(row, label, Theme.PanelLight, () => { _gm.Sfx.Play(Sound.UiClick, 0.6f); onClick(); }, 8);
                b.GetComponent<RectTransform>().TopLeft(x, 8, w, 34);
                b.Label(label, 15, Theme.Text, UIFonts.Bold);
                return b;
            }
            if (Platform.IsWeb)
            {
                var row = SettingRow("Save file", y, "Keep a copy, or move your career to another browser or computer");
                RowButton(row, "DOWNLOAD", 600, 140, _gm.DownloadSave);
                RowButton(row, "LOAD FILE…", 750, 150, _gm.LoadSaveFile);
            }
            else
            {
                string folder = SaveSystem.Folder, home = Environment.GetEnvironmentVariable("HOME");
                if (!string.IsNullOrEmpty(home) && folder.StartsWith(home)) folder = "~" + folder.Substring(home.Length);
                var row = SettingRow("Save file (the browser game reads it too)", y, $"{folder}/agentclicker_save.json");
                RowButton(row, "OPEN FOLDER", 740, 160, _gm.OpenSaveFolder);
            }
        }

        void Stepper(string label, string[] options, Func<int> get, Action<int> set, float y, string hint = null)
        {
            var row = SettingRow(label, y, hint);
            var value = UIKit.Text(row, "Value", "", 19, Theme.Accent, TextAlignmentOptions.Center, UIFonts.Bold);
            value.rectTransform.TopLeft(600, 0, 260, 50);
            void Refresh() => value.text = options[Mathf.Clamp(get(), 0, options.Length - 1)];
            void Step(int d)
            {
                _gm.Sfx.Play(Sound.UiClick, 0.6f);
                set((get() + d + options.Length) % options.Length);
                _gm.ApplySettings();
                Refresh();
            }
            var left = UIKit.Button(row, "Prev", Theme.PanelLight, () => Step(-1), 8);
            left.GetComponent<RectTransform>().TopLeft(560, 8, 40, 34);
            left.Label("◀", 15, Theme.Text, UIFonts.Mono);
            var right = UIKit.Button(row, "Next", Theme.PanelLight, () => Step(1), 8);
            right.GetComponent<RectTransform>().TopLeft(860, 8, 40, 34);
            right.Label("▶", 15, Theme.Text, UIFonts.Mono);
            Refresh();
        }

        void Toggle(string label, Func<bool> get, Action<bool> set, float y, string hint = null)
        {
            var row = SettingRow(label, y, hint);
            Button b = null;
            TextMeshProUGUI t = null;
            void Refresh()
            {
                b.GetComponent<Image>().color = get() ? Theme.Good : Theme.PanelLight;
                t.text = get() ? "ON" : "OFF";
                t.color = get() ? Theme.Bg : Theme.TextDim;
            }
            b = UIKit.Button(row, "Toggle", Theme.PanelLight, () =>
            {
                _gm.Sfx.Play(Sound.UiClick, 0.6f);
                set(!get());
                _gm.ApplySettings();
                Refresh();
            }, 17);
            b.GetComponent<RectTransform>().TopLeft(790, 8, 110, 34);
            t = b.Label("", 16, Theme.Text, UIFonts.Bold);
            Refresh();
        }

        void Slider(string label, float min, float max, Func<float> get, Action<float> set, float y, Func<float, string> fmt)
        {
            var row = SettingRow(label, y);
            var area = UIKit.Rect("Slider", row).TopLeft(560, 13, 250, 24);
            var bg = UIKit.Panel(area, "Bg", Theme.PanelLight, 6, true);
            bg.rectTransform.Anchor(0, 0.3f, 1, 0.7f).Insets(0, 0, 0, 0);
            var fillArea = UIKit.Rect("Fill Area", area);
            fillArea.Anchor(0, 0.3f, 1, 0.7f).Insets(0, 0, 0, 0);
            var fill = UIKit.Panel(fillArea, "Fill", Theme.Accent, 6);
            fill.rectTransform.sizeDelta = Vector2.zero;
            var handleArea = UIKit.Rect("Handle Area", area);
            handleArea.Fill().Insets(10, 0, 10, 0);
            var handle = UIKit.Image(handleArea, "Handle", Color.white, true);
            handle.sprite = UIKit.Circle;
            handle.rectTransform.sizeDelta = new Vector2(22, 0);
            var slider = area.gameObject.AddComponent<UnityEngine.UI.Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = get();
            var value = UIKit.Text(row, "Value", fmt(get()), 18, Theme.Accent, TextAlignmentOptions.MidlineRight, UIFonts.Bold);
            value.rectTransform.TopLeft(820, 0, 80, 50);
            slider.onValueChanged.AddListener(v =>
            {
                set(v);
                value.text = fmt(v);
                _gm.ApplySettings();
            });
        }

        // ------------------------------------------------------------------ info / how to play
        const string HowToPlay =
            "<b>Ship code.</b> Click SHIP CODE (or press Space) to earn compute credits. Steady clicking builds Focus, up to x3 " +
            "per click; it drains as soon as you stop.\n\n" +
            "<b>Hire agents.</b> Spend credits in the <color=#4DD0E1>ModelMart</color> on AI agents. They earn every second, and each " +
            "costs 15% more than the last. Upgrades multiply them; office gadgets appear on your desk. The <b>NEXT GOAL</b> card says " +
            "what to save for next.\n\n" +
            "<b>Work the day.</b> 9 to 5. Beat the quota for a ★ and finish your manager's two asks for a bonus. Click gold model " +
            "drops before they vanish, click an outage banner to fail over, and answer the phone with E (Q declines), replying with 1, 2 or 3.\n\n" +
            "<b>Walk away.</b> Leave the game running and your agents clock you out after 5 PM, go home and log back in the next " +
            "morning (Settings → Gameplay). A <i>While you were away</i> card sums it up. With the game closed they earn at a reduced rate. " +
            "M (or the ♪ button) mutes everything.\n\n" +
            "<b>Build the <color=#FFD166>Software Factory</color>.</b> Every agent type, five Orchestrator Clusters and the recliner. " +
            "It doubles everything and unlocks frontier agents. There is no last agent.\n\n" +
            "<b>Reorg, forever.</b> Roll the Factory out to the next division and start over with <color=#FFD166>Stock Options</color>: " +
            "+1% production each, permanently. Spend them on Board Room perks. <b>Trophies</b> add Clout, which influence upgrades turn into production.\n\n" +
            "<b>Gamepad or touch.</b> The left stick moves a cursor and A clicks; RT ships code. On a touch screen, tap; drag the office to " +
            "look around, and use two fingers to zoom the office or the monitor. Settings → Controls lists the rest.";

        void BuildInfo()
        {
            _info = Layer("Info", new Color(0.02f, 0.03f, 0.06f, 0.8f));
            var card = UIKit.Panel(_info.transform, "Card", Theme.Panel, 22);
            card.rectTransform.Center(900, 700);
            _infoTitle = UIKit.Text(card.transform, "Title", "", 32, Color.white, TextAlignmentOptions.TopLeft, UIFonts.Bold);
            _infoTitle.rectTransform.TopLeft(44, 34, 800, 44);
            _infoBody = UIKit.Text(card.transform, "Body", "", 19, Theme.Text, TextAlignmentOptions.TopLeft);
            _infoBody.enableAutoSizing = true;
            _infoBody.fontSizeMin = 14;
            _infoBody.fontSizeMax = 19;
            _infoBody.rectTransform.TopLeft(44, 100, 812, 500);
            var back = UIKit.Button(card.transform, "Back", Theme.Accent, () => { _gm.Sfx.Play(Sound.UiClick); _info.gameObject.SetActive(false); }, 10);
            back.GetComponent<RectTransform>().TopLeft(660, 624, 200, 50);
            back.Label("BACK", 20, Theme.Bg);
        }

        public void ShowHowToPlay() => ShowInfo("How to play", HowToPlay);

        /// <summary>The info card's text after layout: its auto-sized font size and whether it still overflows.</summary>
        public (float size, bool overflowing) InfoTextFit()
        {
            _infoBody.ForceMeshUpdate();
            return (_infoBody.fontSize, _infoBody.isTextOverflowing);
        }

        public void ShowInfo(string title, string body)
        {
            _infoTitle.text = title;
            _infoBody.text = body;
            Show(_info, true);
        }

        // ------------------------------------------------------------------ confirm
        void BuildConfirm()
        {
            _confirm = Layer("Confirm", new Color(0.02f, 0.03f, 0.06f, 0.75f));
            var card = UIKit.Panel(_confirm.transform, "Card", Theme.Panel, 22);
            card.rectTransform.Center(560, 260);
            _confirmText = UIKit.Text(card.transform, "Text", "", 24, Color.white, TextAlignmentOptions.Center, UIFonts.Medium);
            _confirmText.rectTransform.TopLeft(36, 26, 488, 128);
            _confirmText.enableAutoSizing = true;
            _confirmText.fontSizeMin = 14;
            _confirmText.fontSizeMax = 24;
            var no = UIKit.Button(card.transform, "No", Theme.PanelLight, () => { _gm.Sfx.Play(Sound.UiClick); _confirm.gameObject.SetActive(false); }, 10);
            no.GetComponent<RectTransform>().TopLeft(40, 170, 230, 56);
            no.Label("CANCEL", 20, Theme.Text);
            _confirmYes = UIKit.Button(card.transform, "Yes", Theme.Bad, () =>
            {
                _gm.Sfx.Play(Sound.UiClick);
                _confirm.gameObject.SetActive(false);
                _confirmAction?.Invoke();
            }, 10);
            _confirmYes.GetComponent<RectTransform>().TopLeft(290, 170, 230, 56);
            _confirmYes.Label("YES", 20, Color.white);
        }

        public void Confirm(string message, string yes, Action onYes, Color? yesColor = null)
        {
            _confirmText.text = message;
            _confirmYes.GetComponent<Image>().color = yesColor ?? Theme.Bad;
            var yesLabel = _confirmYes.GetComponentInChildren<TextMeshProUGUI>();
            yesLabel.text = yes;
            yesLabel.color = yesColor.HasValue ? Theme.Bg : Color.white;
            _confirmAction = onYes;
            Show(_confirm, true);
        }

        // ------------------------------------------------------------------ story cards
        void BuildCards()
        {
            _cards = Layer("StoryCards", Theme.Hex("#05070D"));
            var glow = UIKit.Image(_cards.transform, "Glow", Theme.Accent2.WithAlpha(0.035f));
            glow.sprite = UIKit.Circle;
            glow.rectTransform.Center(1300, 900, 520, -360);
            _cardKicker = UIKit.Text(_cards.transform, "Kicker", "", 18, Theme.Accent, TextAlignmentOptions.TopLeft, UIFonts.Bold);
            _cardKicker.rectTransform.Center(1100, 30, 0, 170);
            _cardTitle = UIKit.Text(_cards.transform, "Title", "", 54, Color.white, TextAlignmentOptions.TopLeft, UIFonts.Bold);
            _cardTitle.rectTransform.Center(1100, 140, 0, 80);
            _cardBody = UIKit.Text(_cards.transform, "Body", "", 26, Theme.Text, TextAlignmentOptions.TopLeft, UIFonts.Medium);
            _cardBody.rectTransform.Center(1100, 260, 0, -130);
            _cardBody.lineSpacing = 10;
            _cardHint = UIKit.Text(_cards.transform, "Hint", "", 16, Theme.TextFaint, TextAlignmentOptions.BottomRight, UIFonts.Medium);
            _cardHint.rectTransform.Anchor(0, 0, 1, 0).Insets(40, 36, 60, -60);
            var hit = UIKit.Image(_cards.transform, "Hit", Color.clear, true);
            hit.rectTransform.Fill();
            PointerRelay.On(hit).Click = _ => AdvanceCard();
            var skip = UIKit.Button(_cards.transform, "Skip", Theme.PanelLight.WithAlpha(0.8f), () => FinishCards(), 8);
            var skipRt = skip.GetComponent<RectTransform>();
            skipRt.anchorMin = skipRt.anchorMax = skipRt.pivot = new Vector2(1, 1);
            skipRt.anchoredPosition = new Vector2(-30, -30);
            skipRt.sizeDelta = new Vector2(120, 40);
            skip.Label("SKIP ▶", 15, Theme.TextDim, UIFonts.Bold);
        }

        public void ShowStoryCards(List<StoryCard> cards, Action done)
        {
            _cardList = cards;
            _cardsDone = done;
            _cardIndex = -1;
            Show(_cards, true);
            AdvanceCard();
        }

        void AdvanceCard()
        {
            if (_cardIndex >= 0 && _cardBody.maxVisibleCharacters < _cardBody.textInfo.characterCount)
            {
                _reveal = 9999; // first click finishes the typewriter
                return;
            }
            _cardIndex++;
            if (_cardIndex >= _cardList.Count) { FinishCards(); return; }
            _gm.Sfx.Play(Sound.Page, 0.8f);
            var c = _cardList[_cardIndex];
            _cardKicker.text = c.Kicker;
            _cardTitle.text = c.Title;
            _cardBody.text = c.Body;
            _cardBody.maxVisibleCharacters = 0;
            _reveal = 0;
            _cardHint.text = $"{_cardIndex + 1} / {_cardList.Count}   ·   " + (_gm.Touch.Active ? "tap to continue" : "click or press Space to continue");
        }

        /// <summary>Skips to the end of the story cards (runs their completion callback).</summary>
        public void SkipCards() => FinishCards();

        void FinishCards()
        {
            if (!CardsOpen) return;
            _cards.gameObject.SetActive(false);
            var done = _cardsDone;
            _cardsDone = null;
            done?.Invoke();
        }

        // ------------------------------------------------------------------ credits
        void BuildCredits()
        {
            _credits = Layer("Credits", Theme.Hex("#05070D"));
            var mask = UIKit.Rect("Mask", _credits.transform);
            mask.Fill();
            mask.gameObject.AddComponent<RectMask2D>();
            _creditsText = UIKit.Text(mask, "Roll", "", 26, Theme.Text, TextAlignmentOptions.Top, UIFonts.Medium);
            _creditsText.rectTransform.anchorMin = new Vector2(0.5f, 0);
            _creditsText.rectTransform.anchorMax = new Vector2(0.5f, 0);
            _creditsText.rectTransform.pivot = new Vector2(0.5f, 1);
            _creditsText.rectTransform.sizeDelta = new Vector2(1000, 2600);
            _creditsText.lineSpacing = 12;
            var hit = UIKit.Image(_credits.transform, "Hit", Color.clear, true);
            hit.rectTransform.Fill();
            PointerRelay.On(hit).Click = _ => FinishCredits();
        }

        public void ShowCredits(Action done)
        {
            _creditsDone = done;
            _creditsText.text =
                "<size=200%><b>AGENT <color=#4DD0E1>CLICKER</color></b></size>\n\n" +
                $"<color=#A4AFC2>a {Application.companyName} game</color>\n\n\n" +
                "<color=#FFD166>STARRING</color>\n" + FlavorText.EmployeeName + " as themself\n" +
                string.Join("\n", Array.ConvertAll(StoryDatabase.Cast, c => $"{c.Name} <color=#A4AFC2>as {c.Role}</color>")) + "\n\n\n" +
                "<color=#FFD166>FRONTIER LABS</color>\n" +
                string.Join("\n", Array.ConvertAll(GameDatabase.Labs, l => $"{l.Name} <color=#A4AFC2>· \"{l.Tagline}\"</color>")) + "\n\n\n" +
                "<color=#FFD166>BUILT WITH</color>\nUnity 6 · Universal Render Pipeline\nBlender (every model generated from Python)\n" +
                "Procedurally synthesised sound and lo-fi music\nFira Sans & DejaVu Sans Mono\n\n\n" +
                "<color=#A4AFC2>Every lab, model, company and person in this game is fictional.\nAny resemblance to real frontier labs is " +
                "statistically inevitable.</color>\n\n\n" +
                "<size=140%><b>Thanks for playing.</b></size>\n<color=#A4AFC2>(Your agents say thanks too.)</color>";
            _creditsY = -40;
            Show(_credits, true);
        }

        void FinishCredits()
        {
            if (!_credits.gameObject.activeSelf) return;
            _credits.gameObject.SetActive(false);
            var done = _creditsDone;
            _creditsDone = null;
            done?.Invoke();
        }

        // ------------------------------------------------------------------ frame
        void Update()
        {
            if (_gm == null) return;
            float dt = Time.unscaledDeltaTime;
            if (TitleOpen) FitTitleColumn();

            if (CardsOpen)
            {
                _reveal += dt * 55f;
                _cardBody.maxVisibleCharacters = (int)_reveal;
            }
            if (_credits.gameObject.activeSelf)
            {
                _creditsY += dt * 70f;
                _creditsText.rectTransform.anchoredPosition = new Vector2(0, _creditsY);
                if (_creditsY > _creditsText.preferredHeight + 950) FinishCredits();
            }

            var pad = Gamepad.current;
            if (pad != null)
            {
                // Start is Esc; B backs out of whatever is open but never opens the pause menu (and declines a ringing
                // phone instead, in CallUI)
                if (pad.startButton.wasPressedThisFrame) OnEscape();
                else if (pad.buttonEast.wasPressedThisFrame && !_gm.Calls.Ringing) Back();
            }
            var kb = Keyboard.current;
            if (kb == null) return;
            if (CardsOpen && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)) AdvanceCard();
            if (kb.escapeKey.wasPressedThisFrame) OnEscape();
        }

        void OnEscape()
        {
            if (!Back() && !TitleOpen) OpenPause();
        }

        /// <summary>Closes the topmost menu, card or dismissable window; false if there was nothing to close.</summary>
        bool Back()
        {
            if (_confirm.gameObject.activeSelf) { _confirm.gameObject.SetActive(false); return true; }
            if (_info.gameObject.activeSelf) { _info.gameObject.SetActive(false); return true; }
            if (_settings.gameObject.activeSelf) { CloseSettings(); return true; }
            if (_credits.gameObject.activeSelf) { FinishCredits(); return true; }
            if (CardsOpen) { FinishCards(); return true; }
            if (PauseOpen) { ClosePause(); return true; }
            if (TitleOpen) return false;
            return _gm.Computer.CloseDismissableModal();
        }
    }
}
