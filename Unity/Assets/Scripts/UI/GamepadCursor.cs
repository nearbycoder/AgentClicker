using System.Collections.Generic;
using AgentClicker.Office;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace AgentClicker.UI
{
    /// <summary>
    /// Gamepad play without rebuilding every screen for focus navigation: the left stick moves an on-screen cursor
    /// that drives a virtual mouse, and A is its left button, so every button, slider, the store and the 3D monitor
    /// work exactly as they do with a mouse. The right stick scrolls the list under the cursor. Shortcut buttons
    /// (ship code, phone, pause, view) are read where their keyboard keys are. The cursor appears when the gamepad
    /// is used and goes away as soon as the real mouse moves.
    /// </summary>
    public class GamepadCursor : MonoBehaviour
    {
        /// <summary>The gamepad was used this frame (counts as being at the keyboard for the day autopilot).</summary>
        public bool InputThisFrame { get; private set; }
        public bool Active { get; private set; }
        /// <summary>The D-pad is moving the menus' focus ring: the cursor is hidden and A presses the focused item instead.</summary>
        public bool Parked { get; private set; }
        public Vector2 Position => _pos;

        GameManager _gm;
        Mouse _mouse;
        readonly List<Mouse> _realMice = new List<Mouse>();
        Canvas _canvas;
        RectTransform _cursor;
        Image _ring;
        Vector2 _pos;
        bool _wasDown;
        readonly List<RaycastResult> _hits = new List<RaycastResult>();

        public void Init(GameManager gm)
        {
            _gm = gm;
            var go = new GameObject("Gamepad Cursor Canvas", typeof(RectTransform));
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 32000; // above every menu
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = 1f;
            _cursor = UIKit.Rect("Cursor", go.transform);
            _cursor.anchorMin = _cursor.anchorMax = Vector2.zero;
            _cursor.sizeDelta = new Vector2(34, 34);
            var outline = UIKit.Image(_cursor, "Outline", new Color(0.04f, 0.05f, 0.08f, 0.9f));
            outline.sprite = UIKit.Circle;
            outline.rectTransform.Fill();
            _ring = UIKit.Image(_cursor, "Ring", Theme.Accent);
            _ring.sprite = UIKit.Circle;
            _ring.rectTransform.Fill(3);
            var hole = UIKit.Image(_cursor, "Hole", new Color(0.04f, 0.05f, 0.08f, 0.55f));
            hole.sprite = UIKit.Circle;
            hole.rectTransform.Fill(7);
            var dot = UIKit.Image(_cursor, "Dot", Color.white);
            dot.sprite = UIKit.Circle;
            dot.rectTransform.Fill(11);
            foreach (var g in _cursor.GetComponentsInChildren<Graphic>()) g.raycastTarget = false;
            _cursor.gameObject.SetActive(false);
        }

        void Update()
        {
            InputThisFrame = false;
            var pad = Gamepad.current;
            if (pad == null || !pad.added)
            {
                if (Active) Deactivate();
                return;
            }
            Vector2 stick = pad.leftStick.ReadValue(), right = pad.rightStick.ReadValue();
            InputThisFrame = stick.sqrMagnitude > 0.04f || right.sqrMagnitude > 0.04f || AnyButton(pad);
            if (InputThisFrame && !Active) Activate();
            if (!Active) return;
            if (RealMouseUsed() || _gm.Touch.InputThisFrame)
            {
                Deactivate();
                return;
            }
            if (Parked)
            {
                // the stick takes the cursor back from the D-pad
                if (stick.sqrMagnitude > 0.04f) Unpark();
                else return;
            }

            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            Vector2 last = _pos;
            float m = Mathf.Clamp01(stick.magnitude);
            if (m > 0.15f)
            {
                // gentle near the centre for small buttons, fast at full tilt to cross the screen in about a second
                float k = (m - 0.15f) / 0.85f;
                float speed = Screen.height * Mathf.Lerp(0.12f, 1.3f, k * k);
                _pos += stick / m * speed * dt;
            }
            _pos = new Vector2(Mathf.Clamp(_pos.x, 0, Screen.width - 1), Mathf.Clamp(_pos.y, 0, Screen.height - 1));

            bool down = pad.buttonSouth.isPressed;
            if (_pos != last || down != _wasDown)
                InputSystem.QueueStateEvent(_mouse, new MouseState { position = _pos, delta = _pos - last }.WithButton(MouseButton.Left, down));
            _wasDown = down;
            _cursor.anchoredPosition = _pos / _canvas.scaleFactor;
            _cursor.localScale = Vector3.one * (down ? 0.82f : 1f);

            // Buttons never keep the selection (see UIKit.Button); sliders would, and the UI module's gamepad
            // navigation would then nudge them with the stick that is moving the cursor.
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
                EventSystem.current.SetSelectedGameObject(null);

            if (Mathf.Abs(right.y) > 0.2f && _gm.Cam.Mode != CamMode.Office) ScrollUnderCursor(right.y, dt);
        }

        void Activate()
        {
            _realMice.Clear();
            foreach (var d in InputSystem.devices)
                if (d is Mouse m && m != _mouse) _realMice.Add(m);
            var real = Mouse.current;
            _pos = real != null && real != _mouse ? real.position.ReadValue() : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            if (_mouse == null || !_mouse.added) _mouse = InputSystem.AddDevice<Mouse>("GamepadCursor");
            _mouse.MakeCurrent();
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = _pos });
            _wasDown = false;
            Active = true;
            _cursor.gameObject.SetActive(!Parked);
            _cursor.anchoredPosition = _pos / _canvas.scaleFactor;
            Cursor.visible = false;
            Debug.Log("[Gamepad] cursor on: " + Gamepad.current?.displayName);
        }

        /// <summary>The D-pad took over the menus: hide the cursor (letting go of its button) until the stick moves.</summary>
        public void Park()
        {
            if (Parked) return;
            Parked = true;
            if (_mouse != null && _mouse.added && _wasDown)
                InputSystem.QueueStateEvent(_mouse, new MouseState { position = _pos });
            _wasDown = false;
            _cursor.gameObject.SetActive(false);
        }

        public void Unpark()
        {
            if (!Parked) return;
            Parked = false;
            _cursor.gameObject.SetActive(Active);
            _cursor.localScale = Vector3.one;
        }

        void Deactivate()
        {
            Active = false;
            Parked = false;
            _cursor.gameObject.SetActive(false);
            Cursor.visible = true;
            if (_mouse != null && _mouse.added) InputSystem.RemoveDevice(_mouse);
            _mouse = null;
            foreach (var m in _realMice)
                if (m.added) { m.MakeCurrent(); break; }
            Debug.Log("[Gamepad] cursor off");
        }

        bool RealMouseUsed()
        {
            foreach (var m in _realMice)
                if (m.added && (m.delta.ReadValue().sqrMagnitude > 4f || m.leftButton.wasPressedThisFrame || m.rightButton.wasPressedThisFrame))
                    return true;
            return false;
        }

        static bool AnyButton(Gamepad p) =>
            p.buttonSouth.isPressed || p.buttonEast.isPressed || p.buttonWest.isPressed || p.buttonNorth.isPressed ||
            p.startButton.isPressed || p.selectButton.isPressed || p.leftShoulder.isPressed || p.rightShoulder.isPressed ||
            p.leftTrigger.ReadValue() > 0.3f || p.rightTrigger.ReadValue() > 0.3f || p.dpad.ReadValue().sqrMagnitude > 0.25f;

        /// <summary>The right stick scrolls whichever list is under the cursor, about a screenful per second.</summary>
        void ScrollUnderCursor(float y, float dt)
        {
            var es = EventSystem.current;
            if (es == null) return;
            _hits.Clear();
            es.RaycastAll(new PointerEventData(es) { position = _pos }, _hits);
            foreach (var h in _hits)
            {
                var sr = h.gameObject.GetComponentInParent<ScrollRect>();
                if (sr == null || !sr.vertical || sr.content == null) continue;
                float view = sr.viewport ? sr.viewport.rect.height : ((RectTransform)sr.transform).rect.height;
                float extra = sr.content.rect.height - view;
                if (extra <= 1f) return;
                sr.verticalNormalizedPosition = Mathf.Clamp01(sr.verticalNormalizedPosition + y * dt * view * 1.2f / extra);
                return;
            }
        }

        void OnDestroy()
        {
            if (_mouse != null && _mouse.added) InputSystem.RemoveDevice(_mouse);
        }
    }
}
