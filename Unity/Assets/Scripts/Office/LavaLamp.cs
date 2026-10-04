using UnityEngine;

namespace AgentClicker.Office
{
    /// <summary>Wax blobs drifting up and down inside the lava lamp.</summary>
    public class LavaLamp : MonoBehaviour
    {
        public Transform[] Blobs;
        public float Bottom = 0.11f, Top = 0.29f;
        float[] _phase;

        void Start()
        {
            _phase = new float[Blobs.Length];
            for (int i = 0; i < _phase.Length; i++) _phase[i] = Random.value * 10f;
        }

        void Update()
        {
            for (int i = 0; i < Blobs.Length; i++)
            {
                if (!Blobs[i]) continue;
                float t = Time.time * (0.12f + i * 0.035f) + _phase[i];
                float k = 0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 2f);
                var p = Blobs[i].localPosition;
                p.y = Mathf.Lerp(Bottom, Top, k);
                Blobs[i].localPosition = p;
                float squash = 1f + 0.25f * Mathf.Sin(t * 7f);
                // blob meshes keep Blender axes: local Z is "up"
                Blobs[i].localScale = new Vector3(1f / squash, 1f / squash, squash);
            }
        }
    }
}
