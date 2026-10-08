using UnityEngine;

namespace AgentClicker.UI
{
    /// <summary>
    /// A menu or dialog card arriving: it grows from 96% to full size with an ease-out over a fifth of a second while
    /// its layer fades in. With Reduce motion on it's simply there (the fade still runs).
    /// </summary>
    public class CardIn : MonoBehaviour
    {
        const float Duration = 0.2f, From = 0.96f;
        float _t = 1f;

        public static void Play(Transform card)
        {
            if (card == null) return;
            var c = card.GetComponent<CardIn>();
            if (!c) c = card.gameObject.AddComponent<CardIn>();
            c.Begin();
        }

        /// <summary>The card's scale right now (the tour checks it's 1 from the first frame with Reduce motion on).</summary>
        public float Scale => transform.localScale.x;

        void Begin()
        {
            if (UIKit.ReduceMotion)
            {
                _t = 1f;
                transform.localScale = Vector3.one;
                return;
            }
            _t = 0f;
            transform.localScale = Vector3.one * From;
        }

        void Update()
        {
            if (_t >= 1f) return;
            _t = Mathf.Min(1f, _t + Time.unscaledDeltaTime / Duration);
            float k = 1f - (1f - _t) * (1f - _t) * (1f - _t);
            transform.localScale = Vector3.one * Mathf.Lerp(From, 1f, k);
        }

        void OnDisable()
        {
            _t = 1f;
            transform.localScale = Vector3.one;
        }
    }
}
