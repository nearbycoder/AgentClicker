using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace AgentClicker.Office
{
    public enum CamMode { Office, Monitor, Showcase, Ending, Menu, Fixed, Call }

    /// <summary>Moves the single game camera between the over-the-shoulder office view and the monitor.</summary>
    public class CameraRig : MonoBehaviour
    {
        // Negative yaw places the camera behind the employee's right shoulder.
        public float OfficeYaw = -32f, OfficePitch = 24f, OfficeDistance = 2.45f;
        public Vector2 YawLimits = new Vector2(-80f, 55f), PitchLimits = new Vector2(2f, 40f), DistanceLimits = new Vector2(1.4f, 3.6f);
        public float MonitorMargin = 1.015f;

        public CamMode Mode { get; private set; } = CamMode.Office;
        public float MouseSensitivity { get; set; } = 1f;
        /// <summary>Settings → Field of view (degrees, vertical), for every view but the monitor's, which fits the screen.</summary>
        public float FieldOfView { get; set; } = 50f;
        /// <summary>Settings → Reduce motion: every camera move is a cut, and the title camera holds still.</summary>
        public bool ReduceMotion { get; set; }
        public bool InTransition => _transition < 1f;

        SceneRefs _refs;
        Camera _cam;
        float _yaw, _pitch, _dist;
        Vector3 _fromPos;
        Quaternion _fromRot;
        float _fromFov;
        float _transition = 1f, _transitionTime = 1f;
        Transform _showcaseTarget;
        float _showcaseUntil;
        CamMode _afterShowcase;

        public void Init(SceneRefs refs)
        {
            _refs = refs;
            _cam = refs.MainCamera;
            _yaw = OfficeYaw;
            _pitch = OfficePitch;
            _dist = OfficeDistance;
            GetTarget(out var p, out var r);
            _cam.transform.SetPositionAndRotation(p, r);
        }

        public void SetMode(CamMode mode, float seconds = 1.1f)
        {
            // standing up, the title screen and the ending leave the monitor at its fitted view
            if (mode == CamMode.Office || mode == CamMode.Menu || mode == CamMode.Ending) ResetMonitorZoom();
            if (mode == Mode && !InTransition) return;
            _fromPos = _cam.transform.position;
            _fromRot = _cam.transform.rotation;
            _fromFov = _cam.fieldOfView;
            Mode = mode;
            _transition = 0f;
            _transitionTime = ReduceMotion ? 0.0001f : Mathf.Max(0.01f, seconds);
        }

        Vector3 _fixedPos, _fixedLook;
        CamMode _beforeCall = CamMode.Monitor;

        /// <summary>Cut to a close-up of the employee on the phone; EndCall returns to where you were.</summary>
        public void BeginCall()
        {
            if (Mode == CamMode.Call || Mode == CamMode.Ending) return;
            _beforeCall = Mode == CamMode.Showcase ? _afterShowcase : Mode;
            SetMode(CamMode.Call, 0.9f);
        }

        public void EndCall()
        {
            if (Mode == CamMode.Call) SetMode(_beforeCall, 0.9f);
        }

        /// <summary>Holds the camera at an explicit viewpoint (used by the screenshot tour).</summary>
        public void SetFixed(Vector3 position, Vector3 lookAt)
        {
            _fixedPos = position;
            _fixedLook = lookAt;
            SetMode(CamMode.Fixed, 0.01f);
        }

        public void Toggle() => SetMode(Mode == CamMode.Monitor ? CamMode.Office : CamMode.Monitor);

        /// <summary>Briefly look at a newly installed item, then return.</summary>
        public void Showcase(Transform target, float seconds = 2.6f)
        {
            if (target == null || Mode == CamMode.Ending) return;
            _afterShowcase = Mode == CamMode.Showcase ? _afterShowcase : Mode;
            _showcaseTarget = target;
            _showcaseUntil = Time.time + seconds;
            SetMode(CamMode.Showcase, 0.8f);
        }

        void LateUpdate()
        {
            if (_cam == null) return;
            HandleInput();

            if (Mode == CamMode.Showcase && Time.time > _showcaseUntil)
                SetMode(_afterShowcase, 0.9f);

            GetTarget(out var pos, out var rot);
            float fov = TargetFov();
            if (_transition < 1f)
            {
                _transition = Mathf.Min(1f, _transition + Time.deltaTime / _transitionTime);
                float k = Mathf.SmoothStep(0f, 1f, Mathf.SmoothStep(0f, 1f, _transition));
                // arc slightly upward while travelling for a nicer dolly
                Vector3 p = Vector3.Lerp(_fromPos, pos, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.06f;
                _cam.transform.SetPositionAndRotation(p, Quaternion.Slerp(_fromRot, rot, k));
                _cam.fieldOfView = Mathf.Lerp(_fromFov, fov, k);
            }
            else
            {
                float s = 1f - Mathf.Exp(-Time.deltaTime * 10f);
                _cam.transform.SetPositionAndRotation(Vector3.Lerp(_cam.transform.position, pos, s),
                                                      Quaternion.Slerp(_cam.transform.rotation, rot, s));
                _cam.fieldOfView = Mathf.Abs(_cam.fieldOfView - fov) < 0.01f ? fov : Mathf.Lerp(_cam.fieldOfView, fov, s);
            }
        }

        void HandleInput()
        {
            HandleGamepad();
            var mouse = Mouse.current;
            if (mouse == null) return;
            float scroll = mouse.scroll.ReadValue().y;
            // only ask the EventSystem when it matters (it's a raycast)
            bool overUi = Mathf.Abs(scroll) > 0.01f && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

            if (Mode == CamMode.Office)
            {
                if (mouse.rightButton.isPressed)
                {
                    Vector2 d = mouse.delta.ReadValue();
                    Look(d.x * 0.15f, d.y * 0.12f);
                }
                if (!overUi && Mathf.Abs(scroll) > 0.01f)
                {
                    if (scroll > 0 && AtClosest) SetMode(CamMode.Monitor);
                    Zoom(Mathf.Sign(scroll) * 0.18f);
                }
            }
        }

        /// <summary>Office view: turn the camera (degrees, scaled by the look sensitivity setting).</summary>
        public void Look(float yaw, float pitch)
        {
            _yaw = Mathf.Clamp(_yaw + yaw * MouseSensitivity, YawLimits.x, YawLimits.y);
            _pitch = Mathf.Clamp(_pitch - pitch * MouseSensitivity, PitchLimits.x, PitchLimits.y);
        }

        /// <summary>Office view: move the camera in (positive metres) or out.</summary>
        public void Zoom(float metres) => _dist = Mathf.Clamp(_dist - metres, DistanceLimits.x, DistanceLimits.y);

        /// <summary>The office camera is as close as it goes; zooming in further sits back down at the computer.</summary>
        public bool AtClosest => _dist <= DistanceLimits.x + 0.01f;

        public float Yaw => _yaw;
        public float Pitch => _pitch;
        public float Distance => _dist;

        /// <summary>Office view on a gamepad: the right stick looks around, the shoulder buttons zoom (RB sits back down).</summary>
        void HandleGamepad()
        {
            var pad = Gamepad.current;
            if (pad == null || Mode != CamMode.Office) return;
            Vector2 r = pad.rightStick.ReadValue();
            if (r.sqrMagnitude > 0.04f)
            {
                float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
                _yaw = Mathf.Clamp(_yaw + r.x * 80f * dt * MouseSensitivity, YawLimits.x, YawLimits.y);
                _pitch = Mathf.Clamp(_pitch - r.y * 50f * dt * MouseSensitivity, PitchLimits.x, PitchLimits.y);
            }
            if (pad.rightShoulder.wasPressedThisFrame)
            {
                if (AtClosest) SetMode(CamMode.Monitor);
                Zoom(0.3f);
            }
            if (pad.leftShoulder.wasPressedThisFrame) Zoom(-0.3f);
        }

        // Monitor view zoom (touch screens, where the whole monitor makes small text): 1 shows the whole screen; above
        // that the camera moves closer, panned in the screen's plane (metres along the camera's right and up axes).
        public const float MaxMonitorZoom = 3f;
        float _monZoom = 1f;
        Vector2 _monPan;

        /// <summary>How far the monitor view is zoomed in (1 = the whole screen).</summary>
        public float MonitorZoom => _monZoom;

        /// <summary>
        /// Monitor view: zoom by <paramref name="factor"/> keeping the point under <paramref name="screenPoint"/> (pixels)
        /// where it is, then move the view by <paramref name="screenDelta"/> pixels, like a page under two fingers.
        /// </summary>
        public void ZoomMonitor(float factor, Vector2 screenPoint, Vector2 screenDelta)
        {
            MonitorExtents(_monZoom, out var before);
            float zoom = Mathf.Clamp(_monZoom * factor, 1f, MaxMonitorZoom);
            MonitorExtents(zoom, out var after);
            // the fingers' point, from -1 to 1 across the view
            Vector2 n = new Vector2(screenPoint.x / Mathf.Max(1, Screen.width) * 2f - 1f, screenPoint.y / Mathf.Max(1, Screen.height) * 2f - 1f);
            _monPan += Vector2.Scale(n, before - after);
            _monPan -= new Vector2(screenDelta.x / Mathf.Max(1, Screen.width), screenDelta.y / Mathf.Max(1, Screen.height)) * 2f * after;
            _monZoom = zoom;
            ClampPan();
        }

        /// <summary>Monitor view, zoomed in: centres the view on a point on the screen (as far as the screen's edges allow).</summary>
        public void PanMonitorTo(Vector3 worldPoint)
        {
            if (_refs == null || _refs.MainScreen == null) return;
            SceneRefs.ScreenFrame(_refs.MainScreen, out var center, out var frame, out _);
            Vector3 normal = frame * Vector3.back;
            var rot = Quaternion.LookRotation(-normal, frame * Vector3.up);
            Vector3 local = Quaternion.Inverse(rot) * (worldPoint - center);
            _monPan = new Vector2(local.x, local.y);
            ClampPan();
        }

        /// <summary>Back to the whole monitor.</summary>
        public void ResetMonitorZoom()
        {
            _monZoom = 1f;
            _monPan = Vector2.zero;
        }

        /// <summary>Half the width and height (metres) the monitor view shows at a zoom, in the screen's plane.</summary>
        void MonitorExtents(float zoom, out Vector2 half)
        {
            half = Vector2.zero;
            if (_refs == null || _refs.MainScreen == null) return;
            SceneRefs.ScreenFrame(_refs.MainScreen, out _, out _, out var size);
            MonitorFrame(size, out float distance, out float fov);
            float d = distance / zoom;
            float tanV = Mathf.Tan(fov * Mathf.Deg2Rad / 2f);
            half = new Vector2(d * tanV * _cam.aspect, d * tanV);
        }

        void ClampPan()
        {
            if (_refs == null || _refs.MainScreen == null) return;
            SceneRefs.ScreenFrame(_refs.MainScreen, out _, out _, out var size);
            MonitorExtents(_monZoom, out var half);
            float mx = Mathf.Max(0f, size.x * 0.5f * MonitorMargin - half.x), my = Mathf.Max(0f, size.y * 0.5f * MonitorMargin - half.y);
            _monPan = new Vector2(Mathf.Clamp(_monPan.x, -mx, mx), Mathf.Clamp(_monPan.y, -my, my));
        }

        /// <summary>How far the camera is from where its mode wants it (metres), for checks.</summary>
        public float DistanceToTarget()
        {
            GetTarget(out var pos, out _);
            return Vector3.Distance(_cam.transform.position, pos);
        }

        void GetTarget(out Vector3 pos, out Quaternion rot)
        {
            switch (Mode)
            {
                case CamMode.Monitor:
                    MonitorPose(out pos, out rot);
                    return;
                case CamMode.Showcase when _showcaseTarget != null:
                    ShowcasePose(out pos, out rot);
                    return;
                case CamMode.Menu:
                    MenuPose(out pos, out rot);
                    return;
                case CamMode.Call when _refs.CallView != null:
                    pos = _refs.CallView.position;
                    rot = _refs.CallView.rotation;
                    return;
                case CamMode.Fixed:
                    pos = _fixedPos;
                    rot = Quaternion.LookRotation(_fixedLook - _fixedPos);
                    return;
                case CamMode.Ending when _refs.EndingView != null:
                    pos = _refs.EndingView.position;
                    rot = _refs.EndingView.rotation;
                    return;
                default:
                    OfficePose(out pos, out rot);
                    return;
            }
        }

        void OfficePose(out Vector3 pos, out Quaternion rot)
        {
            Vector3 pivot = _refs.OfficeViewPivot.position;
            rot = Quaternion.Euler(_pitch, _yaw, 0f);
            pos = pivot - rot * Vector3.forward * _dist;
        }

        /// <summary>Slow cinematic drift around the office behind the title screen.</summary>
        void MenuPose(out Vector3 pos, out Quaternion rot)
        {
            float t = ReduceMotion ? 0f : Time.unscaledTime;
            float yaw = 8f + Mathf.Sin(t * 0.07f) * 34f;
            float pitch = 14f + Mathf.Sin(t * 0.05f + 1f) * 4f;
            float dist = 2.9f + Mathf.Sin(t * 0.045f) * 0.25f;
            Vector3 pivot = _refs.OfficeViewPivot.position + new Vector3(-0.35f, 0.05f, 0f);
            rot = Quaternion.Euler(pitch, yaw, 0f);
            pos = pivot - rot * Vector3.forward * dist;
            // look a little to the right of the pivot so the title text on the left doesn't cover the desk
            rot = Quaternion.LookRotation(pivot + new Vector3(0.0f, 0f, 0f) - pos) * Quaternion.Euler(0, -14f, 0);
        }

        void MonitorPose(out Vector3 pos, out Quaternion rot)
        {
            SceneRefs.ScreenFrame(_refs.MainScreen, out var center, out var frame, out var size);
            Vector3 normal = frame * Vector3.back;
            rot = Quaternion.LookRotation(-normal, frame * Vector3.up);
            MonitorFrame(size, out float distance, out _);
            pos = center + normal * (distance / _monZoom) + rot * new Vector3(_monPan.x, _monPan.y, 0f);
        }

        // The monitor view's camera stays this close to the screen (metres): in front of Sam's head and hands when he leans
        // in (a facepalm at an outage, the morning stretch). Fitting the screen at 50° put it 0.37 m away on a 16:9 screen,
        // where a facepalm's hand reached a corner of the screen, and farther on a 4:3 one or at 40°, behind his head. At
        // 0.30 m the right hand still clipped the bottom edge for a moment as a facepalm from typing came down.
        const float MonitorMaxDistance = 0.28f;

        /// <summary>
        /// The monitor view's distance from the screen (unzoomed) and its field of view: no farther back than
        /// <see cref="MonitorMaxDistance"/>, and wide enough that the whole monitor (plus a small margin) fills the view.
        /// A flat screen seen head-on looks the same at any distance, so this only changes what's in front of it.
        /// </summary>
        void MonitorFrame(Vector2 size, out float distance, out float fov)
        {
            distance = Mathf.Min(FitDistance(size, FieldOfView, _cam.aspect), MonitorMaxDistance);
            float tanV = Mathf.Max(size.y * 0.5f * MonitorMargin / distance, size.x * 0.5f * MonitorMargin / (distance * _cam.aspect));
            fov = 2f * Mathf.Atan(tanV) * Mathf.Rad2Deg;
        }

        /// <summary>The distance at which the whole monitor (plus a small margin) fills a view of this field of view and aspect.</summary>
        float FitDistance(Vector2 size, float fovDegrees, float aspect)
        {
            float tanV = Mathf.Tan(fovDegrees * Mathf.Deg2Rad / 2f);
            return Mathf.Max(size.y * 0.5f * MonitorMargin / tanV, size.x * 0.5f * MonitorMargin / (tanV * aspect));
        }

        float TargetFov()
        {
            if (Mode != CamMode.Monitor || _refs == null || _refs.MainScreen == null) return FieldOfView;
            SceneRefs.ScreenFrame(_refs.MainScreen, out _, out _, out var size);
            MonitorFrame(size, out _, out float fov);
            return fov;
        }

        /// <summary>The monitor view's distance and field of view right now, for checks.</summary>
        public void MonitorFrame(out float distance, out float fov)
        {
            SceneRefs.ScreenFrame(_refs.MainScreen, out _, out _, out var size);
            MonitorFrame(size, out distance, out fov);
        }

        void ShowcasePose(out Vector3 pos, out Quaternion rot)
        {
            var r = _showcaseTarget.GetComponentInChildren<Renderer>();
            Vector3 c = r ? r.bounds.center : _showcaseTarget.position;
            float size = r ? Mathf.Max(0.25f, r.bounds.extents.magnitude) : 0.4f;
            Vector3 dir;
            Vector3 toItem = c - _refs.Employee.position;
            toItem.y = 0;
            if (toItem.magnitude < 1.2f)
            {
                // Desk items: stand beside the item, perpendicular to the employee→item line and on the
                // room side (-Z), so Sam's head is never between the camera and the gadget.
                Vector3 perp = new Vector3(toItem.z, 0, -toItem.x).normalized;
                if (perp.z > 0) perp = -perp;
                dir = (perp * 0.8f + toItem.normalized * 0.2f).normalized;
                dir.y = 0.95f;
            }
            else
            {
                Vector3 officePos;
                OfficePose(out officePos, out _);
                dir = officePos - c;
                float horizontal = new Vector2(dir.x, dir.z).magnitude;
                dir.y = Mathf.Max(dir.y, 0.6f * horizontal);
            }
            dir.Normalize();
            pos = c + dir * (size * 3.2f + 0.35f);
            rot = Quaternion.LookRotation(c - pos, Vector3.up);
        }
    }
}
