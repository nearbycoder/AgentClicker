using System.Collections.Generic;
using AgentClicker.Office;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace AgentClicker.UI
{
    /// <summary>
    /// Touch screens. Taps already reach every button through the UI input module; this adds what a mouse does with
    /// its right button and wheel: in the office view one finger dragged across the room looks around, and two fingers
    /// pinch to zoom (pinching in at the closest distance sits back down, like the wheel). A drag that starts on a
    /// button, a list or the monitor's login screen leaves the camera alone. In the monitor view two fingers zoom into
    /// the screen and move around it, like a page, because the whole monitor makes small text on a phone. Touch also
    /// counts as being at the keyboard, and while it's the last thing used the prompts say "tap" instead of naming keys.
    /// </summary>
    public class TouchControls : MonoBehaviour
    {
        /// <summary>A finger is on the screen this frame (counts as being at the keyboard for the day autopilot).</summary>
        public bool InputThisFrame { get; private set; }
        /// <summary>Touch is the input in use: prompts drop the key names.</summary>
        public bool Active { get; private set; }

        // degrees of camera turn for a drag across the full height of the screen
        const float YawPerScreen = 140f, PitchPerScreen = 90f;
        // metres of zoom for fingers spreading by the full height of the screen
        const float ZoomPerScreen = 3f;

        class Finger
        {
            public Vector2 Start, Last;
            public bool OnControl, Dragging;
        }

        GameManager _gm;
        readonly Dictionary<int, Finger> _fingers = new Dictionary<int, Finger>();
        readonly List<int> _lifted = new List<int>();
        readonly List<RaycastResult> _hits = new List<RaycastResult>();
        float _lastTouch = -10f, _spread = -1f, _sitDown, _yawStart, _distStart;
        bool _looked, _pinched;
        // monitor view: where two fingers started (spread < 0: no two-finger touch), whether they've moved enough to be a
        // zoom, their spread and midpoint last frame, and the zoom at the start
        float _monStartSpread = -1f, _monSpread, _monZoomStart;
        Vector2 _monStartMid, _monMid;
        bool _monZooming;
        // the monitor's raycasters, switched off while two fingers zoom so lifting them can't click (or buy) anything
        readonly List<GraphicRaycaster> _muted = new List<GraphicRaycaster>();
        int _idleFrames;

        public void Init(GameManager gm) => _gm = gm;

        /// <summary>Whether a UI pointer event came from a finger (touch has no hover).</summary>
        public static bool IsTouch(PointerEventData e) => e is ExtendedPointerEventData x && x.pointerType == UIPointerType.Touch;

        void Update()
        {
            InputThisFrame = false;
            var ts = Touchscreen.current;
            if (_gm == null) return;
            if (ts == null || !ts.added)
            {
                _fingers.Clear();
                if (_muted.Count > 0) Unmute();
                return;
            }

            int pressed = 0;
            foreach (var t in ts.touches)
            {
                if (!t.press.isPressed) continue;
                pressed++;
                int id = t.touchId.ReadValue();
                Vector2 pos = t.position.ReadValue();
                if (!_fingers.ContainsKey(id))
                    _fingers[id] = new Finger { Start = pos, Last = pos, OnControl = OverControl(pos) };
            }
            _lifted.Clear();
            foreach (var kv in _fingers)
                if (!StillPressed(ts, kv.Key)) _lifted.Add(kv.Key);
            foreach (int id in _lifted) _fingers.Remove(id);

            InputThisFrame = pressed > 0;
            // give the monitor its taps back once the fingers have been off the screen for a frame (the UI module has seen
            // them lift by then)
            _idleFrames = pressed > 0 ? 0 : _idleFrames + 1;
            if (_muted.Count > 0 && _idleFrames >= 2) Unmute();
            if (InputThisFrame)
            {
                _lastTouch = Time.unscaledTime;
                if (!Active)
                {
                    Active = true;
                    Debug.Log("[Touch] on: " + ts.displayName);
                }
            }
            else if (Active && (_gm.Pad.InputThisFrame || MouseUsed()))
            {
                Active = false;
                Debug.Log("[Touch] off");
            }

            bool free = !_gm.OnTitle && !_gm.InEnding && !_gm.Menu.Blocking && !_gm.Calls.Busy;
            bool office = _gm.Cam.Mode == CamMode.Office && free;
            bool monitor = _gm.Cam.Mode == CamMode.Monitor && !_gm.Cam.InTransition && free;
            if (monitor && _fingers.Count == 2) ZoomMonitor(ts);
            else EndMonitorZoom();
            if (office && _fingers.Count == 2) Pinch(ts);
            else EndPinch();
            if (office && _fingers.Count == 1 && !_pinched) Drag(ts);
            else EndDrag();
            if (_fingers.Count == 0) _pinched = false;

            foreach (var t in ts.touches)
                if (t.press.isPressed && _fingers.TryGetValue(t.touchId.ReadValue(), out var f)) f.Last = t.position.ReadValue();
        }

        void Drag(Touchscreen ts)
        {
            foreach (var t in ts.touches)
            {
                if (!t.press.isPressed || !_fingers.TryGetValue(t.touchId.ReadValue(), out var f) || f.OnControl) continue;
                Vector2 pos = t.position.ReadValue();
                if (!f.Dragging && (pos - f.Start).magnitude > Screen.height * 0.012f)
                {
                    f.Dragging = true;
                    if (!_looked) _yawStart = _gm.Cam.Yaw;
                    _looked = true;
                }
                if (!f.Dragging) return;
                Vector2 d = (pos - f.Last) / Mathf.Max(1, Screen.height);
                _gm.Cam.Look(d.x * YawPerScreen, d.y * PitchPerScreen);
                return;
            }
        }

        void EndDrag()
        {
            if (!_looked) return;
            _looked = false;
            Debug.Log($"[Touch] looked around: yaw {_yawStart:0}° → {_gm.Cam.Yaw:0}°, pitch {_gm.Cam.Pitch:0}°");
        }

        void Pinch(Touchscreen ts)
        {
            Vector2 a = default, b = default;
            int n = 0;
            foreach (var t in ts.touches)
            {
                if (!t.press.isPressed || !_fingers.ContainsKey(t.touchId.ReadValue())) continue;
                if (n++ == 0) a = t.position.ReadValue();
                else b = t.position.ReadValue();
            }
            float spread = Vector2.Distance(a, b);
            if (!_pinched)
            {
                // a second finger turns a look-around drag into a pinch; lifting one finger doesn't turn it back
                _pinched = true;
                _spread = spread;
                _sitDown = 0;
                _distStart = _gm.Cam.Distance;
                EndDrag();
                return;
            }
            if (_spread < 0) return;
            float metres = (spread - _spread) / Mathf.Max(1, Screen.height) * ZoomPerScreen;
            _spread = spread;
            if (metres > 0 && _gm.Cam.AtClosest)
            {
                // keep pinching in at the closest distance to sit back down at the computer
                _sitDown += metres;
                if (_sitDown > 0.25f)
                {
                    Debug.Log("[Touch] pinched in all the way: sitting down at the computer");
                    _gm.Cam.SetMode(CamMode.Monitor);
                    _spread = -1;
                    return;
                }
            }
            _gm.Cam.Zoom(metres);
        }

        void EndPinch()
        {
            if (_spread < 0) return;
            if (_gm.Cam.Mode == CamMode.Office) Debug.Log($"[Touch] pinch zoom: distance {_distStart:0.00} → {_gm.Cam.Distance:0.00} m");
            _spread = -1;
        }

        /// <summary>
        /// Monitor view: spreading two fingers zooms into the screen around them, moving them pans. Two fingers that stay
        /// put are two taps (drumming on SHIP CODE), so nothing happens until they move.
        /// </summary>
        void ZoomMonitor(Touchscreen ts)
        {
            if (!TwoFingers(ts, out var a, out var b)) return;
            float spread = Mathf.Max(1f, Vector2.Distance(a, b));
            Vector2 mid = (a + b) * 0.5f;
            if (_monStartSpread < 0)
            {
                _monStartSpread = spread;
                _monStartMid = mid;
                _monZooming = false;
                return;
            }
            if (!_monZooming)
            {
                float moved = Mathf.Max(Mathf.Abs(spread - _monStartSpread), (mid - _monStartMid).magnitude);
                if (moved < Screen.height * 0.03f) return;
                _monZooming = true;
                _pinched = true;
                _monSpread = spread;
                _monMid = mid;
                _monZoomStart = _gm.Cam.MonitorZoom;
                MuteMonitor();
                return;
            }
            _gm.Cam.ZoomMonitor(spread / _monSpread, mid, mid - _monMid);
            _monSpread = spread;
            _monMid = mid;
        }

        void EndMonitorZoom()
        {
            if (_monStartSpread < 0) return;
            _monStartSpread = -1f;
            if (!_monZooming) return;
            _monZooming = false;
            // nearly all the way out is all the way out
            if (_gm.Cam.MonitorZoom < 1.08f) _gm.Cam.ResetMonitorZoom();
            Debug.Log($"[Touch] monitor zoom: x{_monZoomStart:0.00} → x{_gm.Cam.MonitorZoom:0.00}");
        }

        void MuteMonitor()
        {
            var root = _gm.Computer.ScreenRoot;
            if (root == null) return;
            foreach (var r in root.GetComponentsInChildren<GraphicRaycaster>())
                if (r.enabled)
                {
                    r.enabled = false;
                    _muted.Add(r);
                }
        }

        void Unmute()
        {
            foreach (var r in _muted)
                if (r) r.enabled = true;
            _muted.Clear();
        }

        void OnDisable() => Unmute();

        bool TwoFingers(Touchscreen ts, out Vector2 a, out Vector2 b)
        {
            a = b = default;
            int n = 0;
            foreach (var t in ts.touches)
            {
                if (!t.press.isPressed || !_fingers.ContainsKey(t.touchId.ReadValue())) continue;
                if (n++ == 0) a = t.position.ReadValue();
                else b = t.position.ReadValue();
            }
            return n >= 2;
        }

        static bool StillPressed(Touchscreen ts, int id)
        {
            foreach (var t in ts.touches)
                if (t.press.isPressed && t.touchId.ReadValue() == id) return true;
            return false;
        }

        /// <summary>
        /// The finger landed on something that takes taps or drags (a button, a list, the monitor's login screen),
        /// so dragging it shouldn't turn the camera.
        /// </summary>
        bool OverControl(Vector2 pos)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            _hits.Clear();
            es.RaycastAll(new PointerEventData(es) { position = pos }, _hits);
            foreach (var h in _hits)
                if (h.gameObject.GetComponentInParent<Selectable>() || h.gameObject.GetComponentInParent<ScrollRect>() ||
                    h.gameObject.GetComponentInParent<PointerRelay>())
                    return true;
            return false;
        }

        /// <summary>
        /// A real mouse moved or clicked. Some browsers follow a tap with emulated mouse events, so the mouse only
        /// counts once the fingers have been off the screen for a moment.
        /// </summary>
        bool MouseUsed()
        {
            if (_gm.Pad.Active || Time.unscaledTime - _lastTouch < 0.6f) return false;
            var m = Mouse.current;
            return m != null && m.added &&
                   (m.delta.ReadValue().sqrMagnitude > 4f || m.leftButton.wasPressedThisFrame || m.rightButton.wasPressedThisFrame);
        }
    }
}
