using UnityEngine;

namespace AgentClicker.Office
{
    /// <summary>The desk phone: the handset rattles and the LED blinks while it rings; the handset leaves the cradle on a call.</summary>
    public class PhoneProp : MonoBehaviour
    {
        public Transform Handset;
        public Renderer Led;
        bool _ringing, _offHook;
        Quaternion _base;
        Renderer[] _handsetRenderers;

        void Awake()
        {
            if (Handset)
            {
                _base = Handset.localRotation;
                _handsetRenderers = Handset.GetComponentsInChildren<Renderer>(true);
            }
            if (Led) Led.enabled = false;
        }

        public void SetRinging(bool ringing)
        {
            _ringing = ringing;
            if (!ringing)
            {
                if (Handset) Handset.localRotation = _base;
                if (Led) Led.enabled = false;
            }
        }

        public void SetOffHook(bool offHook)
        {
            _offHook = offHook;
            if (_handsetRenderers != null) foreach (var r in _handsetRenderers) r.enabled = !offHook;
            if (Led) Led.enabled = offHook;
        }

        void Update()
        {
            if (!_ringing || _offHook) return;
            // classic ring cadence: two bursts, then a pause (matches the ring sound)
            float t = Time.time % 3f;
            bool burst = t < 0.4f || (t > 0.6f && t < 1.0f);
            if (Handset)
                Handset.localRotation = _base * Quaternion.Euler(0, burst ? Mathf.Sin(Time.time * 70f) * 3.5f : 0f, 0);
            if (Led) Led.enabled = burst || Mathf.Repeat(Time.time, 0.5f) < 0.25f;
        }
    }
}
