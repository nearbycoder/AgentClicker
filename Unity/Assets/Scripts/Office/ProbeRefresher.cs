using UnityEngine;

namespace AgentClicker.Office
{
    /// <summary>
    /// Renders the realtime reflection probe only when something changed (new furniture, a new hour of daylight).
    /// Rendering it on a timer cost six extra scene renders every few seconds.
    /// </summary>
    public class ProbeRefresher : MonoBehaviour
    {
        ReflectionProbe _probe;
        float _renderAt = 0.2f;

        void Start() => _probe = GetComponent<ReflectionProbe>();

        public void RequestRender(float delay = 0.1f)
        {
            float at = Time.unscaledTime + delay;
            if (_renderAt < 0 || at < _renderAt) _renderAt = at;
        }

        void Update()
        {
            if (_probe == null || _renderAt < 0 || Time.unscaledTime < _renderAt) return;
            _renderAt = -1;
            _probe.RenderProbe();
        }
    }
}
