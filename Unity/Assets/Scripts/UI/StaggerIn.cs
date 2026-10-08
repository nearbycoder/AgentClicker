using UnityEngine;

namespace AgentClicker.UI
{
    /// <summary>
    /// The title screen's column arriving: the logo, the tagline, each button and the version line slide in from the
    /// left and fade up one after another, about half a second in all. With Reduce motion on everything is in place at once.
    /// </summary>
    public class StaggerIn : MonoBehaviour
    {
        const float Step = 0.055f, Duration = 0.38f, Slide = 28f;
        RectTransform[] _items;
        CanvasGroup[] _groups;
        Vector2[] _home;
        float _t = 99f;

        public bool Done => _t > Step * (_items?.Length ?? 0) + Duration;

        public void Play()
        {
            int n = transform.childCount;
            if (_items == null || _items.Length != n)
            {
                _items = new RectTransform[n];
                _groups = new CanvasGroup[n];
                _home = new Vector2[n];
                for (int i = 0; i < n; i++)
                {
                    _items[i] = (RectTransform)transform.GetChild(i);
                    _home[i] = _items[i].anchoredPosition;
                    _groups[i] = _items[i].GetComponent<CanvasGroup>();
                    if (!_groups[i]) _groups[i] = _items[i].gameObject.AddComponent<CanvasGroup>();
                }
            }
            _t = UIKit.ReduceMotion ? 99f : 0f;
            Apply();
        }

        void Update()
        {
            if (_items == null || Done) return;
            _t += Time.unscaledDeltaTime;
            Apply();
        }

        void Apply()
        {
            // inactive children (CONTINUE without a save, QUIT in the browser) keep their slot in the order
            for (int i = 0; i < _items.Length; i++)
            {
                float k = Mathf.Clamp01((_t - i * Step) / Duration);
                float e = 1f - (1f - k) * (1f - k) * (1f - k);
                _items[i].anchoredPosition = _home[i] + new Vector2(-Slide * (1f - e), 0);
                _groups[i].alpha = e;
            }
        }

        void OnDisable()
        {
            if (_items == null) return;
            _t = 99f;
            Apply();
        }
    }
}
