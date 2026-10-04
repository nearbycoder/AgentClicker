using UnityEngine;

namespace AgentClicker.Office
{
    /// <summary>Neon sign hum: steady glow with the occasional flicker.</summary>
    public class NeonFlicker : MonoBehaviour
    {
        public Renderer Tube;
        public Light Glow;
        float _base, _until, _next;

        void Start()
        {
            if (Glow) _base = Glow.intensity;
            _next = Time.time + Random.Range(4f, 12f);
        }

        void Update()
        {
            if (Time.time > _next)
            {
                _until = Time.time + Random.Range(0.1f, 0.35f);
                _next = Time.time + Random.Range(5f, 15f);
            }
            bool off = Time.time < _until && Mathf.Repeat(Time.time * 23f, 1f) > 0.5f;
            if (Tube) Tube.enabled = !off;
            if (Glow) Glow.intensity = off ? _base * 0.1f : _base;
        }
    }
}
