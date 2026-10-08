using System;
using System.Collections.Generic;
using AgentClicker.Core;
using AgentClicker.Util;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AgentClicker.UI
{
    /// <summary>One thing the keyboard or D-pad can focus: a button, or a whole settings row that Left and Right change.</summary>
    public class FocusItem
    {
        public RectTransform Rect;
        /// <summary>Enter, Space or A.</summary>
        public Action Press;
        /// <summary>Left (-1) or Right (+1). Without it, Left and Right move between the items on the row.</summary>
        public Action<int> Adjust;
        /// <summary>Items with the same Row sit side by side.</summary>
        public object Row;
        public string Label;
        /// <summary>Where the focus starts when the screen opens (else the first item).</summary>
        public bool Default;
    }

    /// <summary>
    /// The menus without a mouse. Whatever screen is in front (a menu, a dialog on the monitor, the login, the night
    /// screen) gets a focus ring: arrows, WASD or the D-pad move it, Enter, Space or A press, Left and Right change a
    /// setting. Settings rows are listed by MenuUI; everywhere else the screen's buttons are found and laid out in rows
    /// by position. The ring shows up with the first key or D-pad press and goes away when the mouse, a finger or the
    /// gamepad's stick moves, so pointer players never see it. While the D-pad is in use the gamepad cursor steps aside.
    /// </summary>
    public class MenuFocus : MonoBehaviour
    {
        GameManager _gm;
        Canvas _canvas;
        RectTransform _ring;
        Image _ringImage;
        Transform _scope;
        int _version = -1;
        readonly List<List<FocusItem>> _rows = new List<List<FocusItem>>();
        readonly List<FocusItem> _found = new List<FocusItem>();
        readonly List<Button> _buttons = new List<Button>();
        readonly Vector3[] _corners = new Vector3[4];
        int _row, _col;
        bool _keyMode, _inGame;
        float _scopeOpenedAt, _repeatAt, _lastKeySubmit = -10f, _keyQuiet = 10f;
        Vector2Int _held;

        /// <summary>A press right after a screen opens doesn't count.</summary>
        const float SubmitGrace = 0.35f;
        /// <summary>
        /// On the monitor's dialogs, the login and the night screen, Space and Enter (which also ship code) only press after
        /// a pause: someone hammering Space for code when the 5 PM card opens mustn't pick WORK LATE for themselves.
        /// </summary>
        const float DeliberatePress = 0.6f;

        public bool RingVisible => _ring && _ring.gameObject.activeSelf;
        public FocusItem Focused => _row < _rows.Count && _col < _rows[_row].Count ? _rows[_row][_col] : null;
        public string FocusedLabel => Focused?.Label ?? "";

        public void Init(GameManager gm)
        {
            _gm = gm;
            var go = new GameObject("Focus Canvas", typeof(RectTransform));
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 31000; // over every menu, under the gamepad cursor
            OverlayScaler.Add(go);
            _ringImage = UIKit.Image(go.transform, "Ring", Theme.Accent);
            _ringImage.sprite = UIKit.RoundedRing;
            _ringImage.type = Image.Type.Sliced;
            _ringImage.pixelsPerUnitMultiplier = 32f / 14f;
            _ring = _ringImage.rectTransform;
            _ring.anchorMin = _ring.anchorMax = _ring.pivot = Vector2.zero;
            _ring.gameObject.SetActive(false);
        }

        void Update()
        {
            if (_gm == null) return;
            var kb0 = Keyboard.current;
            if (kb0 != null && (kb0.spaceKey.wasPressedThisFrame || kb0.enterKey.wasPressedThisFrame || kb0.numpadEnterKey.wasPressedThisFrame))
            {
                _keyQuiet = Time.unscaledTime - _lastKeySubmit;
                _lastKeySubmit = Time.unscaledTime;
            }
            var scope = FindScope(out var listed, out int version);
            if (scope != _scope || (listed != null && version != _version))
            {
                bool fresh = scope != _scope;
                _scope = scope;
                _version = version;
                if (fresh) _scopeOpenedAt = Time.unscaledTime;
                Rebuild(listed, fresh);
            }
            if (PointerMoved()) _keyMode = false;
            if (_scope == null)
            {
                if (_gm.Pad.Parked) _gm.Pad.Unpark();
                return;
            }
            Navigate(listed);
        }

        void LateUpdate() => PlaceRing();

        Transform FindScope(out List<FocusItem> listed, out int version)
        {
            var menu = _gm.Menu.FocusLayer(out listed, out version);
            _inGame = menu == null;
            if (menu) return menu;
            version = 0;
            var ending = _gm.Overlay.EndingRoot;
            if (ending) return ending;
            if (_gm.InEnding || _gm.Calls.Busy) return null;
            var modal = _gm.Computer.ModalRoot;
            if (modal) return modal;
            var night = _gm.Overlay.NightRoot;
            if (night) return night;
            if (_gm.Model.Phase == GamePhase.Login) return _gm.Computer.LoginRoot;
            return null;
        }

        /// <summary>Lays the screen's items out in rows: MenuUI's list for Settings, else its buttons by position.</summary>
        void Rebuild(List<FocusItem> listed, bool reset)
        {
            _rows.Clear();
            if (_scope == null) return;
            var items = listed;
            if (items == null)
            {
                Collect(_scope);
                items = _found;
            }
            foreach (var it in items)
            {
                if (it.Rect == null || !it.Rect.gameObject.activeInHierarchy) continue;
                if (_rows.Count == 0 || _rows[_rows.Count - 1][0].Row != it.Row || it.Row == null) _rows.Add(new List<FocusItem>());
                _rows[_rows.Count - 1].Add(it);
            }
            if (reset)
            {
                _row = _col = 0;
                for (int r = 0; r < _rows.Count; r++)
                    for (int c = 0; c < _rows[r].Count; c++)
                        if (_rows[r][c].Default) { _row = r; _col = c; r = _rows.Count; break; }
            }
            _row = Mathf.Clamp(_row, 0, Mathf.Max(0, _rows.Count - 1));
            _col = _rows.Count == 0 ? 0 : Mathf.Clamp(_col, 0, _rows[_row].Count - 1);
        }

        /// <summary>The screen's buttons (not the ones in scrolling lists), in reading order, a row per line.</summary>
        void Collect(Transform root)
        {
            _found.Clear();
            _buttons.Clear();
            root.GetComponentsInChildren(false, _buttons);
            var placed = new List<(Vector2 p, Button b)>();
            foreach (var b in _buttons)
            {
                if (!b.IsInteractable() || !b.isActiveAndEnabled) continue;
                var sr = b.GetComponentInParent<ScrollRect>();
                if (sr && sr.transform.IsChildOf(root)) continue;
                var rt = (RectTransform)b.transform;
                if (rt.rect.width < 1 || rt.rect.height < 1) continue;
                Vector2 p = root.InverseTransformPoint(rt.TransformPoint(rt.rect.center));
                placed.Add((p, b));
            }
            // top to bottom, then a row per line (centres within 12 units), left to right within it
            placed.Sort((a, c) => c.p.y.CompareTo(a.p.y));
            for (int start = 0; start < placed.Count;)
            {
                int end = start + 1;
                while (end < placed.Count && placed[start].p.y - placed[end].p.y <= 12f) end++;
                placed.Sort(start, end - start, Comparer<(Vector2 p, Button b)>.Create((a, c) => a.p.x.CompareTo(c.p.x)));
                start = end;
            }
            object row = null;
            float rowY = float.NaN;
            foreach (var (p, b) in placed)
            {
                if (float.IsNaN(rowY) || rowY - p.y > 12f) { row = new object(); rowY = p.y; }
                var button = b;
                var label = b.GetComponentInChildren<TMP_Text>();
                _found.Add(new FocusItem
                {
                    Rect = (RectTransform)b.transform, Row = row, Label = label ? label.text : b.name,
                    Press = () => ExecuteEvents.Execute(button.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler),
                });
            }
        }

        void Navigate(List<FocusItem> listed)
        {
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            Vector2Int dir = Vector2Int.zero;
            bool submit = false, fromPad = false, keySubmit = false;
            if (kb != null)
            {
                if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) dir.y = 1;
                else if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) dir.y = -1;
                else if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) dir.x = -1;
                else if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) dir.x = 1;
                submit = keySubmit = kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame;
            }
            if (pad != null && dir == Vector2Int.zero)
            {
                var d = pad.dpad;
                if (d.up.wasPressedThisFrame) dir.y = 1;
                else if (d.down.wasPressedThisFrame) dir.y = -1;
                else if (d.left.wasPressedThisFrame) dir.x = -1;
                else if (d.right.wasPressedThisFrame) dir.x = 1;
                fromPad = dir != Vector2Int.zero;
                // A is the cursor's click until the D-pad has taken over
                if (pad.buttonSouth.wasPressedThisFrame && _gm.Pad.Parked) { submit = true; fromPad = true; }
            }
            if (dir == Vector2Int.zero) dir = Repeat(kb, pad);
            else { _held = dir; _repeatAt = Time.unscaledTime + 0.42f; }
            if (dir == Vector2Int.zero && !submit) return;

            if (fromPad) _gm.Pad.Park();
            if (listed == null) Rebuild(null, false); // buttons come and go (a dialog's state, CONTINUE on the title)
            if (_rows.Count == 0) return;
            if (!_keyMode)
            {
                // the first press shows where the focus is; it doesn't act
                _keyMode = true;
                return;
            }
            if (submit)
            {
                if (Time.unscaledTime - _scopeOpenedAt < SubmitGrace) return;
                if (keySubmit && _inGame && _keyQuiet < DeliberatePress) return;
                Focused?.Press?.Invoke();
                return;
            }
            if (dir.y != 0) MoveRow(-dir.y);
            else if (Focused?.Adjust != null) Focused.Adjust(dir.x);
            else MoveCol(dir.x);
        }

        /// <summary>A held arrow or D-pad direction repeats after a moment, a few steps a second.</summary>
        Vector2Int Repeat(Keyboard kb, Gamepad pad)
        {
            if (_held == Vector2Int.zero) return Vector2Int.zero;
            bool down = _held.y > 0 ? (kb != null && (kb.upArrowKey.isPressed || kb.wKey.isPressed)) || (pad != null && pad.dpad.up.isPressed)
                      : _held.y < 0 ? (kb != null && (kb.downArrowKey.isPressed || kb.sKey.isPressed)) || (pad != null && pad.dpad.down.isPressed)
                      : _held.x < 0 ? (kb != null && (kb.leftArrowKey.isPressed || kb.aKey.isPressed)) || (pad != null && pad.dpad.left.isPressed)
                      : (kb != null && (kb.rightArrowKey.isPressed || kb.dKey.isPressed)) || (pad != null && pad.dpad.right.isPressed);
            if (!down) { _held = Vector2Int.zero; return Vector2Int.zero; }
            if (Time.unscaledTime < _repeatAt) return Vector2Int.zero;
            _repeatAt = Time.unscaledTime + 0.11f;
            return _held;
        }

        void MoveRow(int d)
        {
            float x = X(Focused);
            _row = (_row + d + _rows.Count) % _rows.Count;
            // keep to the same column as near as the new row allows
            var row = _rows[_row];
            int best = 0;
            for (int c = 1; c < row.Count; c++)
                if (Mathf.Abs(X(row[c]) - x) < Mathf.Abs(X(row[best]) - x)) best = c;
            _col = best;
            _gm.Sfx.Play(Sound.UiClick, 0.25f, 1.4f);
        }

        void MoveCol(int d)
        {
            int col = Mathf.Clamp(_col + d, 0, _rows[_row].Count - 1);
            if (col == _col) return;
            _col = col;
            _gm.Sfx.Play(Sound.UiClick, 0.25f, 1.4f);
        }

        float X(FocusItem it)
        {
            if (it == null || it.Rect == null || _scope == null) return 0;
            return _scope.InverseTransformPoint(it.Rect.TransformPoint(it.Rect.rect.center)).x;
        }

        /// <summary>Mouse, finger or stick movement hands the screen back to the pointer.</summary>
        bool PointerMoved()
        {
            if (_gm.Touch.InputThisFrame) return true;
            if (_gm.Pad.Active && !_gm.Pad.Parked) return Gamepad.current != null && Gamepad.current.leftStick.ReadValue().sqrMagnitude > 0.04f;
            var m = Mouse.current;
            return m != null && (m.delta.ReadValue().sqrMagnitude > 16f || m.leftButton.wasPressedThisFrame);
        }

        void PlaceRing()
        {
            if (_ring == null) return;
            var it = Focused;
            bool show = _keyMode && _scope != null && it != null && it.Rect != null && it.Rect.gameObject.activeInHierarchy;
            if (_ring.gameObject.activeSelf != show) _ring.gameObject.SetActive(show);
            if (!show) return;
            var canvas = it.Rect.GetComponentInParent<Canvas>();
            if (canvas == null) return;
            canvas = canvas.rootCanvas;
            it.Rect.GetWorldCorners(_corners);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera ? canvas.worldCamera : _gm.Refs.MainCamera;
            foreach (var c in _corners)
            {
                Vector2 s = cam ? (Vector2)cam.WorldToScreenPoint(c) : (Vector2)c;
                min = Vector2.Min(min, s);
                max = Vector2.Max(max, s);
            }
            float k = _canvas.scaleFactor, pad = 6f;
            _ring.anchoredPosition = min / k - new Vector2(pad, pad);
            _ring.sizeDelta = (max - min) / k + new Vector2(pad, pad) * 2f;
            float a = UIKit.ReduceMotion ? 1f : 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 5f);
            _ringImage.color = Theme.Accent.WithAlpha(a);
        }
    }
}
