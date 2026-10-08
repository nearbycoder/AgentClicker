using UnityEngine;
using UnityEngine.UI;

namespace AgentClicker.UI
{
    /// <summary>
    /// Scales a screen-space canvas laid out for 1600×900 by the geometric mean of the window's width and height (Unity's
    /// match 0.5), but never lets it get shorter than <see cref="MinHeight"/> units: a 32:9 window would otherwise get a
    /// 636-unit canvas, which pushed the Settings card's title and DONE button off the screen. 16:9, 4:3 and 21:9 keep match 0.5.
    /// </summary>
    [RequireComponent(typeof(CanvasScaler))]
    public class OverlayScaler : MonoBehaviour
    {
        public static readonly Vector2 Reference = new Vector2(1600, 900);

        /// <summary>The shortest canvas the overlays get: about what a 21:9 window gets at match 0.5 (785 units at 1680×720).</summary>
        public const float MinHeight = 780f;

        CanvasScaler _scaler;
        int _w, _h;

        /// <summary>Adds a scaler for 1600×900 that keeps the canvas at least <see cref="MinHeight"/> tall.</summary>
        public static CanvasScaler Add(GameObject canvas)
        {
            var scaler = canvas.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = Reference;
            scaler.matchWidthOrHeight = 0.5f;
            canvas.AddComponent<OverlayScaler>().Apply();
            return scaler;
        }

        /// <summary>
        /// The CanvasScaler match for a window: 0.5 unless that makes the canvas shorter than <see cref="MinHeight"/>, then
        /// whatever leans far enough toward the height for the canvas to be exactly that tall.
        /// </summary>
        public static float Match(float width, float height)
        {
            if (width <= 0 || height <= 0) return 0.5f;
            float a = Mathf.Log(width / Reference.x, 2), b = Mathf.Log(height / Reference.y, 2);
            float m = 0.5f;
            float scale = Mathf.Pow(2, Mathf.Lerp(a, b, m));
            if (height / scale >= MinHeight || a - b < 1e-4f) return m;
            // scale = height / MinHeight  ⇔  lerp(a, b, m) = log2(height / MinHeight)
            m = (a - Mathf.Log(height / MinHeight, 2)) / (a - b);
            return Mathf.Clamp(m, 0.5f, 1f);
        }

        /// <summary>The canvas height in units that <see cref="Match"/> gives a window.</summary>
        public static float CanvasHeight(float width, float height)
        {
            float a = Mathf.Log(width / Reference.x, 2), b = Mathf.Log(height / Reference.y, 2);
            return height / Mathf.Pow(2, Mathf.Lerp(a, b, Match(width, height)));
        }

        void Apply()
        {
            if (!_scaler) _scaler = GetComponent<CanvasScaler>();
            _w = Screen.width;
            _h = Screen.height;
            float m = Match(_w, _h);
            if (Mathf.Abs(_scaler.matchWidthOrHeight - m) > 1e-4f) _scaler.matchWidthOrHeight = m;
        }

        void Update()
        {
            if (Screen.width != _w || Screen.height != _h) Apply();
        }
    }
}
