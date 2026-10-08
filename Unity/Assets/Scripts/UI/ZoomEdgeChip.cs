using AgentClicker.Office;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AgentClicker.UI
{
    /// <summary>
    /// Touch screens, monitor zoomed in: a model drop or outage banner outside the view would be missed (the drop expires
    /// in seconds). A chip at that edge of the screen says what it is and which way; tapping it moves the view onto it.
    /// </summary>
    public class ZoomEdgeChip : MonoBehaviour
    {
        GameManager _gm;
        RectTransform _root, _chip;
        TextMeshProUGUI _text;
        RectTransform _target;

        /// <summary>The chip is on screen (the tour checks it).</summary>
        public bool Visible => _chip && _chip.gameObject.activeSelf;
        /// <summary>The chip's centre in screen pixels.</summary>
        public Vector2 ScreenCenter => RectTransformUtility.WorldToScreenPoint(null, _chip.TransformPoint(_chip.rect.center));
        public string Text => _text ? _text.text : "";

        public void Init(GameManager gm)
        {
            _gm = gm;
            var go = new GameObject("Zoom Chip Canvas", typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 55; // above the overlay hints, below calls and menus
            OverlayScaler.Add(go);
            go.AddComponent<GraphicRaycaster>();
            _root = (RectTransform)go.transform;

            var bg = UIKit.Panel(_root, "Chip", Theme.Hex("#1B1430"), 16, true);
            _chip = bg.rectTransform;
            _chip.anchorMin = _chip.anchorMax = Vector2.zero;
            _chip.sizeDelta = new Vector2(320, 72);
            var glow = UIKit.Panel(_chip, "Glow", Theme.Gold, 16);
            glow.rectTransform.Fill(-3);
            glow.transform.SetAsFirstSibling();
            var face = UIKit.Panel(_chip, "Face", Theme.Hex("#1B1430"), 14);
            face.rectTransform.Fill();
            _text = UIKit.Text(_chip, "Text", "", 26, Theme.Gold, TextAlignmentOptions.Center, UIFonts.Bold);
            _text.rectTransform.Fill(6);
            PointerRelay.On(bg).Click = _ => GoToTarget();
            _chip.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            var cam = _gm.Cam;
            RectTransform target = null;
            string what = null;
            if (cam.Mode == CamMode.Monitor && cam.MonitorZoom > 1.05f && _gm.Computer.DesktopShown && !_gm.Computer.ModalOpen)
            {
                if (Active(_gm.Computer.DropCard) && OffView(_gm.Computer.DropCard)) { target = _gm.Computer.DropCard; what = "MODEL DROP"; }
                else if (Active(_gm.Computer.OutageBanner) && OffView(_gm.Computer.OutageBanner)) { target = _gm.Computer.OutageBanner; what = "API OUTAGE"; }
            }
            _target = target;
            if (target == null) { UIKit.SetActive(_chip, false); return; }

            // pin the chip to the screen's edge on the line from the centre towards the target, and point the arrow that way
            Vector2 c = new Vector2(Screen.width, Screen.height) * 0.5f, d = WorldToScreen(target.TransformPoint(target.rect.center)) - c;
            bool sideways = Mathf.Abs(d.x) * Screen.height > Mathf.Abs(d.y) * Screen.width;
            string arrow = sideways ? (d.x > 0 ? "→" : "←") : (d.y > 0 ? "↑" : "↓");
            UIKit.Set(_text, arrow == "→" ? $"{what}  {arrow}" : $"{arrow}  {what}");
            UIKit.SetActive(_chip, true);
            float scale = _root.localScale.x <= 0 ? 1 : _root.localScale.x;
            Vector2 room = c - (_chip.sizeDelta * 0.5f + new Vector2(16, 16)) * scale;
            float t = Mathf.Min(Mathf.Abs(d.x) > 0.01f ? room.x / Mathf.Abs(d.x) : float.MaxValue,
                                Mathf.Abs(d.y) > 0.01f ? room.y / Mathf.Abs(d.y) : float.MaxValue);
            _chip.anchoredPosition = (c + d * Mathf.Min(t, 1e4f)) / scale;
        }

        /// <summary>Taps the chip: the view moves onto what it points at (the zoom stays).</summary>
        public void GoToTarget()
        {
            if (_target == null) return;
            _gm.Cam.PanMonitorTo(_target.TransformPoint(_target.rect.center));
            Debug.Log("[Touch] zoom chip: moved the view onto " + _target.name);
        }

        static bool Active(RectTransform rt) => rt && rt.gameObject.activeInHierarchy;

        /// <summary>The element's centre is outside the screen (so at most half of it shows, and it may not be noticed).</summary>
        bool OffView(RectTransform rt)
        {
            var s = WorldToScreen(rt.TransformPoint(rt.rect.center));
            return s.x < 0 || s.y < 0 || s.x > Screen.width || s.y > Screen.height;
        }

        Vector2 WorldToScreen(Vector3 w) => _gm.Refs.MainCamera.WorldToScreenPoint(w);
    }
}
