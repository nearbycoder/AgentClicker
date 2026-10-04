using UnityEngine;

namespace AgentClicker.Office
{
    /// <summary>Gentle bob, used by the Software Factory desk toy.</summary>
    public class Bob : MonoBehaviour
    {
        public float Amplitude = 0.004f, Speed = 2f;
        Vector3 _base;
        void Start() => _base = transform.localPosition;
        void Update() => transform.localPosition = _base + Vector3.up * Mathf.Sin(Time.time * Speed) * Amplitude;
    }
}
