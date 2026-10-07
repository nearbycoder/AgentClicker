using System;
using AgentClicker.Core;
using AgentClicker.Util;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AgentClicker.UI
{
    /// <summary>
    /// Phone-call interruptions: an incoming-call card (answer / decline) and a dialogue box with the caller's
    /// portrait, a typewriter line and choices. Screen-space, above the HUD.
    /// </summary>
    public class CallUI : MonoBehaviour
    {
        GameManager _gm;
        RectTransform _root, _incoming, _dialogue;
        Image _incomingAvatar, _ringFill, _portrait;
        TextMeshProUGUI _incomingInitials, _incomingName, _incomingRole, _portraitInitials, _speaker, _role, _line, _effect, _hint;
        readonly Button[] _choices = new Button[3];
        readonly TextMeshProUGUI[] _choiceLabels = new TextMeshProUGUI[3];
        readonly TextMeshProUGUI[] _choiceHints = new TextMeshProUGUI[3];
        ActiveCall _call;
        float _reveal, _closeAt = -1;
        Action _onClosed;
        bool _choosing;

        public bool Busy => _incoming.gameObject.activeSelf || _dialogue.gameObject.activeSelf;
        public bool Ringing => _incoming.gameObject.activeSelf;
        TextMeshProUGUI _answerLabel, _declineLabel;

        public void Init(GameManager gm)
        {
            _gm = gm;
            var go = new GameObject("Call Canvas", typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            _root = (RectTransform)go.transform;
            BuildIncoming();
            BuildDialogue();
            _incoming.gameObject.SetActive(false);
            _dialogue.gameObject.SetActive(false);
        }

        void BuildIncoming()
        {
            _incoming = UIKit.Rect("Incoming", _root);
            _incoming.anchorMin = _incoming.anchorMax = _incoming.pivot = new Vector2(0.5f, 1f);
            _incoming.anchoredPosition = new Vector2(0, -22);
            _incoming.sizeDelta = new Vector2(640, 128);
            var glow = UIKit.Panel(_incoming, "Glow", Theme.Good.WithAlpha(0.5f), 22);
            glow.rectTransform.Fill(-4);
            var card = UIKit.Panel(_incoming, "Card", Theme.Hex("#0D1420").WithAlpha(0.97f), 20, true);
            card.rectTransform.Fill();
            var pulse = _incoming.gameObject.AddComponent<Pulse>();
            pulse.Glow = glow;
            pulse.Speed = 9f;
            pulse.ScaleAmount = 0.015f;

            _incomingAvatar = UIKit.Image(card.transform, "Avatar", Theme.Accent);
            _incomingAvatar.sprite = UIKit.Circle;
            _incomingAvatar.rectTransform.TopLeft(22, 22, 84, 84);
            _incomingInitials = UIKit.Text(_incomingAvatar.transform, "I", "", 28, Color.white, TextAlignmentOptions.Center, UIFonts.Bold);
            _incomingInitials.rectTransform.Fill();
            UIKit.Text(card.transform, "Kicker", "☎ INCOMING CALL", 14, Theme.Good, TextAlignmentOptions.TopLeft, UIFonts.Bold)
                 .rectTransform.TopLeft(124, 20, 260, 18);
            _incomingName = UIKit.Text(card.transform, "Name", "", 24, Color.white, TextAlignmentOptions.TopLeft, UIFonts.Bold);
            _incomingName.rectTransform.TopLeft(124, 40, 290, 30);
            _incomingRole = UIKit.Text(card.transform, "Role", "", 14, Theme.TextDim, TextAlignmentOptions.TopLeft);
            _incomingRole.rectTransform.TopLeft(124, 72, 290, 20);
            UIKit.Bar(card.transform, "Ring", Theme.PanelLight, Theme.Good, out _ringFill, 3).rectTransform.TopLeft(124, 100, 290, 6);

            var answer = UIKit.Button(card.transform, "Answer", Theme.Good, () => _gm.Model.AnswerCall(), 12);
            answer.GetComponent<RectTransform>().TopLeft(430, 20, 190, 46);
            _answerLabel = answer.Label("ANSWER  [E]", 18, Theme.Bg);
            var decline = UIKit.Button(card.transform, "Decline", Theme.Bad, () => _gm.Model.DeclineCall(), 12);
            decline.GetComponent<RectTransform>().TopLeft(430, 72, 190, 36);
            _declineLabel = decline.Label("DECLINE  [Q]", 14, Color.white);
        }

        void BuildDialogue()
        {
            _dialogue = UIKit.Rect("Dialogue", _root);
            _dialogue.anchorMin = _dialogue.anchorMax = _dialogue.pivot = new Vector2(0.5f, 0f);
            _dialogue.anchoredPosition = new Vector2(0, 26);
            _dialogue.sizeDelta = new Vector2(1320, 270);
            var card = UIKit.Panel(_dialogue, "Card", Theme.Hex("#0B111B").WithAlpha(0.95f), 22, true);
            card.rectTransform.Fill();
            PointerRelay.On(card).Click = _ => SkipOrClose();

            _portrait = UIKit.Image(card.transform, "Portrait", Theme.Accent);
            _portrait.sprite = UIKit.Circle;
            _portrait.rectTransform.TopLeft(30, 30, 120, 120);
            _portraitInitials = UIKit.Text(_portrait.transform, "I", "", 42, Color.white, TextAlignmentOptions.Center, UIFonts.Bold);
            _portraitInitials.rectTransform.Fill();
            UIKit.Text(card.transform, "OnCall", "☎ ON CALL", 13, Theme.Good, TextAlignmentOptions.Top, UIFonts.Bold).rectTransform.TopLeft(30, 160, 120, 18);

            _speaker = UIKit.Text(card.transform, "Speaker", "", 24, Color.white, TextAlignmentOptions.TopLeft, UIFonts.Bold);
            _speaker.rectTransform.TopLeft(176, 26, 640, 30);
            _role = UIKit.Text(card.transform, "Role", "", 14, Theme.TextDim, TextAlignmentOptions.TopLeft);
            _role.rectTransform.TopLeft(176, 56, 640, 20);
            _line = UIKit.Text(card.transform, "Line", "", 22, Theme.Text, TextAlignmentOptions.TopLeft, UIFonts.Medium);
            _line.rectTransform.TopLeft(176, 86, 600, 130);
            _line.lineSpacing = 6;
            _effect = UIKit.Text(card.transform, "Effect", "", 16, Theme.Gold, TextAlignmentOptions.TopLeft, UIFonts.Bold);
            _effect.rectTransform.TopLeft(176, 222, 620, 24);
            _hint = UIKit.Text(card.transform, "Hint", "", 13, Theme.TextFaint, TextAlignmentOptions.BottomRight, UIFonts.Medium);
            _hint.rectTransform.TopLeft(820, 238, 470, 20);

            for (int i = 0; i < _choices.Length; i++)
            {
                int index = i;
                var b = UIKit.Button(card.transform, "Choice" + i, Theme.PanelLight, () => Choose(index), 12);
                b.GetComponent<RectTransform>().TopLeft(820, 24 + i * 72, 470, 64);
                _choiceLabels[i] = UIKit.Text(b.transform, "Label", "", 19, Color.white, TextAlignmentOptions.TopLeft, UIFonts.Bold);
                _choiceLabels[i].rectTransform.Fill().Insets(18, 26, 14, 8);
                _choiceHints[i] = UIKit.Text(b.transform, "Hint", "", 13, Theme.TextDim, TextAlignmentOptions.BottomLeft, UIFonts.Medium);
                _choiceHints[i].rectTransform.Fill().Insets(18, 8, 14, 30);
                PointerRelay.On(b).Enter = _ => _gm.Sfx.Play(Sound.UiClick, 0.2f, 1.4f);
                _choices[i] = b;
            }
        }

        // ------------------------------------------------------------------ flow
        public void ShowIncoming(ActiveCall call)
        {
            _call = call;
            var d = call.Def;
            _incomingAvatar.color = Theme.Hex(d.Color);
            _incomingInitials.text = d.Monogram;
            _incomingName.text = d.Name;
            _incomingRole.text = d.RoleText + (d.Story ? "  <color=#FFD166>· important</color>" : "");
            // the button prompts follow whatever the player is holding
            bool pad = _gm.Pad.Active;
            _answerLabel.text = pad ? "ANSWER  [Y]" : "ANSWER  [E]";
            _declineLabel.text = pad ? "DECLINE  [B]" : "DECLINE  [Q]";
            _incoming.gameObject.SetActive(true);
        }

        public void ShowDialogue(ActiveCall call)
        {
            _call = call;
            _incoming.gameObject.SetActive(false);
            var d = call.Def;
            _portrait.color = Theme.Hex(d.Color);
            _portraitInitials.text = d.Monogram;
            _speaker.text = d.Name;
            _role.text = d.RoleText;
            _line.text = d.Opening;
            _line.maxVisibleCharacters = 0;
            _reveal = 0;
            _effect.text = "";
            _hint.text = _gm.Pad.Active ? "Pick a reply  ·  D-pad ← ↑ →" : "Pick a reply  ·  1 / 2 / 3";
            _choosing = true;
            _closeAt = -1;
            for (int i = 0; i < _choices.Length; i++)
            {
                bool on = i < d.Choices.Length;
                _choices[i].gameObject.SetActive(on);
                if (!on) continue;
                _choiceLabels[i].text = $"<color=#56627A>{i + 1}.</color> {d.Choices[i].Label}";
                _choiceHints[i].text = CallDatabase.Describe(d.Choices[i]);
            }
            _dialogue.gameObject.SetActive(true);
        }

        void Choose(int index)
        {
            if (!_choosing || _call == null) return;
            _gm.Sfx.Play(Sound.UiClick, 0.7f);
            _gm.Model.ChooseCallOption(index); // CallEnded → ShowResponse
        }

        public void ShowResponse(CallChoice choice, string summary, Action onClosed)
        {
            _choosing = false;
            _onClosed = onClosed;
            foreach (var b in _choices) b.gameObject.SetActive(false);
            _line.text = $"<color=#8A97AD>You: \"{choice.Label}\"</color>\n{choice.Response}";
            _line.maxVisibleCharacters = 0;
            _reveal = 0;
            _effect.text = string.IsNullOrEmpty(summary) ? "" : summary;
            _hint.text = "click to hang up";
            _closeAt = Time.unscaledTime + 4.2f;
        }

        public void HideAll()
        {
            _incoming.gameObject.SetActive(false);
            _dialogue.gameObject.SetActive(false);
            _choosing = false;
            _closeAt = -1;
            _call = null;
        }

        void SkipOrClose()
        {
            if (_line.maxVisibleCharacters < _line.textInfo.characterCount) { _reveal = 9999; return; }
            if (!_choosing && _closeAt > 0) Close();
        }

        void Close()
        {
            _dialogue.gameObject.SetActive(false);
            _closeAt = -1;
            var done = _onClosed;
            _onClosed = null;
            done?.Invoke();
        }

        void Update()
        {
            if (_gm == null) return;
            if (_incoming.gameObject.activeSelf && _call != null)
                UIKit.SetFill(_ringFill, _call.Ring / CallDatabase.RingSeconds);

            if (_dialogue.gameObject.activeSelf)
            {
                _reveal += Time.unscaledDeltaTime * 60f;
                int shown = (int)_reveal;
                if (shown != _line.maxVisibleCharacters) _line.maxVisibleCharacters = shown;
                if (_closeAt > 0 && Time.unscaledTime > _closeAt) Close();
            }

            if (_gm.Menu.Blocking) return;
            var pad = Gamepad.current;
            if (pad != null)
            {
                // Y answers, B declines; the d-pad picks replies left to right
                if (_incoming.gameObject.activeSelf)
                {
                    if (pad.buttonNorth.wasPressedThisFrame) _gm.Model.AnswerCall();
                    else if (pad.buttonEast.wasPressedThisFrame) _gm.Model.DeclineCall();
                }
                else if (_dialogue.gameObject.activeSelf && _choosing)
                {
                    if (pad.dpad.left.wasPressedThisFrame) Choose(0);
                    else if (pad.dpad.up.wasPressedThisFrame) Choose(1);
                    else if (pad.dpad.right.wasPressedThisFrame && _call != null && _call.Def.Choices.Length > 2) Choose(2);
                }
            }
            var kb = Keyboard.current;
            if (kb == null) return;
            if (_incoming.gameObject.activeSelf)
            {
                if (kb.eKey.wasPressedThisFrame) _gm.Model.AnswerCall();
                else if (kb.qKey.wasPressedThisFrame) _gm.Model.DeclineCall();
            }
            else if (_dialogue.gameObject.activeSelf && _choosing)
            {
                if (kb.digit1Key.wasPressedThisFrame) Choose(0);
                if (kb.digit2Key.wasPressedThisFrame) Choose(1);
                if (kb.digit3Key.wasPressedThisFrame && _call != null && _call.Def.Choices.Length > 2) Choose(2);
            }
        }
    }
}
