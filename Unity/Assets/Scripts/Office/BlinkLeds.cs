using UnityEngine;

namespace AgentClicker.Office
{
    /// <summary>Alternates two LED groups on and off at random intervals.</summary>
    public class BlinkLeds : MonoBehaviour
    {
        public Renderer A, B;
        float _next;

        void Update()
        {
            if (Time.time < _next) return;
            _next = Time.time + Random.Range(0.08f, 0.45f);
            if (A) A.enabled = Random.value > 0.3f;
            if (B) B.enabled = Random.value > 0.3f;
        }
    }
}
