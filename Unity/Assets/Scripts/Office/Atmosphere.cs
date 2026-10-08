using AgentClicker.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace AgentClicker.Office
{
    /// <summary>
    /// Light through the office window: the sky behind the city (a gradient with the sun's glow and a few stars at night),
    /// a soft shaft of sunlight from the window into the room, and dust drifting in it. TimeOfDay feeds it the sun and the
    /// sky colours; Graphics fidelity decides whether the shaft is drawn and how much dust there is. The shaft and the dust
    /// step aside in the monitor view, where nothing should lie between the camera and the screen.
    /// </summary>
    public class Atmosphere : MonoBehaviour
    {
        public static Atmosphere Instance { get; private set; }

        // the window opening in the left wall (x = -3), from the room model (Blender/scripts/assets/room.py)
        const float WallX = -3.0f, WinZ0 = -2.1f, WinZ1 = 0.7f, WinY0 = 0.85f, WinY1 = 2.45f, Frame = 0.06f;
        const float MaxBeam = 4.2f;

        SceneRefs _refs;
        CameraRig _rig;
        Material _sky, _beam, _dust;
        Mesh _beamMesh, _dustMesh;
        Transform _dustBox;
        MeshRenderer _beamRenderer, _dustRenderer;
        readonly Vector3[] _beamVerts = new Vector3[16];
        bool _beamsAllowed;
        int _dustCount = -1;
        float _beamStrength, _beamBoost = 1f, _shown;

        static readonly int ZenithId = Shader.PropertyToID("_Zenith"), HorizonId = Shader.PropertyToID("_Horizon"),
            HazeId = Shader.PropertyToID("_Haze"), SunDirId = Shader.PropertyToID("_SunDir"), SunColorId = Shader.PropertyToID("_SunColor"),
            SunStrengthId = Shader.PropertyToID("_SunStrength"), StarsId = Shader.PropertyToID("_Stars"),
            ColorId = Shader.PropertyToID("_Color"), IntensityId = Shader.PropertyToID("_Intensity"), DriftId = Shader.PropertyToID("_Drift");

        public bool BeamVisible => _beamRenderer && _beamRenderer.enabled;
        /// <summary>The shaft's brightness right now (added light, before tone mapping).</summary>
        public float BeamIntensity => 0.16f * _beamBoost * _beamStrength * _shown;
        public int DustCount => _dustRenderer && _dustRenderer.enabled ? _dustCount : 0;

        public void Init(SceneRefs refs, CameraRig rig)
        {
            Instance = this;
            _refs = refs;
            _rig = rig;
            var root = new GameObject("Atmosphere").transform;
            root.SetParent(refs.OfficeRoot, false);

            var skyShader = Shader.Find("AgentClicker/Sky");
            if (skyShader)
            {
                _sky = new Material(skyShader) { name = "Sky (runtime)" };
                // beyond the farthest row of towers (x = -50), tall and wide enough to fill the window from anywhere in the room
                // a mesh of its own rather than CreatePrimitive, which adds a MeshCollider (the browser build strips physics)
                var sky = new GameObject("SkyBackdrop", typeof(MeshFilter), typeof(MeshRenderer));
                var quad = new Mesh { name = "SkyBackdrop" };
                quad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(0.5f, 0.5f, 0), new Vector3(-0.5f, 0.5f, 0) };
                quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
                quad.RecalculateBounds();
                sky.GetComponent<MeshFilter>().sharedMesh = quad;
                sky.transform.SetParent(root, false);
                sky.transform.position = new Vector3(-75f, 10f, -10f);
                sky.transform.rotation = Quaternion.Euler(0, -90f, 0);
                sky.transform.localScale = new Vector3(260f, 140f, 1f);
                var r = sky.GetComponent<MeshRenderer>();
                r.sharedMaterial = _sky;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.lightProbeUsage = LightProbeUsage.Off;
                r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
            else Debug.LogWarning("[Atmosphere] sky shader missing; the window shows the flat sky colour");

            var beamShader = Shader.Find("AgentClicker/Sunbeam");
            if (beamShader)
            {
                _beam = new Material(beamShader) { name = "Sunbeam (runtime)" };
                var go = new GameObject("Sunbeam", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(root, false);
                _beamMesh = new Mesh { name = "Sunbeam" };
                _beamMesh.MarkDynamic();
                BuildBeamTopology();
                go.GetComponent<MeshFilter>().sharedMesh = _beamMesh;
                _beamRenderer = Quiet(go.GetComponent<MeshRenderer>(), _beam);
            }

            var dustShader = Shader.Find("AgentClicker/Mote");
            if (dustShader)
            {
                _dust = new Material(dustShader) { name = "Dust (runtime)" };
                var go = new GameObject("Dust", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(root, false);
                _dustBox = go.transform;
                _dustMesh = new Mesh { name = "Dust" };
                go.GetComponent<MeshFilter>().sharedMesh = _dustMesh;
                _dustRenderer = Quiet(go.GetComponent<MeshRenderer>(), _dust);
            }
            SetFidelity(Fidelity.Step(Fidelity.Default), false);
        }

        static MeshRenderer Quiet(MeshRenderer r, Material m)
        {
            r.sharedMaterial = m;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            r.enabled = false;
            return r;
        }

        /// <summary>Graphics fidelity: the shaft on Medium and up, and the step's amount of dust.</summary>
        public void SetFidelity(FidelityStep p, bool reduceMotion)
        {
            _beamsAllowed = p.Sunbeams;
            if (_dust) _dust.SetFloat(DriftId, reduceMotion ? 0f : 1f);
            if (_dustMesh != null && p.Dust != _dustCount) BuildDust(p.Dust);
        }

        /// <summary>Called by TimeOfDay when the light changes: the sky's colours and where the sun is.</summary>
        public void Apply(Color zenith, Color horizon, Color haze, Light sun, float sunStrength, float night)
        {
            Vector3 toSun = sun ? -sun.transform.forward : Vector3.up;
            Color sunColor = sun ? sun.color : Color.white;
            if (_sky)
            {
                _sky.SetColor(ZenithId, zenith);
                _sky.SetColor(HorizonId, horizon);
                _sky.SetColor(HazeId, haze);
                _sky.SetVector(SunDirId, toSun);
                _sky.SetColor(SunColorId, sunColor);
                _sky.SetFloat(SunStrengthId, sunStrength);
                _sky.SetFloat(StarsId, night);
            }
            _beamStrength = sun && sun.enabled ? sunStrength : 0f;
            // a low sun's shaft is longer and warmer, and it's what people notice: brighter towards the evening and morning
            if (sun) _beamBoost = Mathf.Lerp(2.4f, 1f, Mathf.Clamp01(-sun.transform.forward.y / 0.6f));
            if (_beamStrength > 0.01f) PlaceBeam(sun.transform.forward, sunColor);
        }

        /// <summary>The shaft: the window opening pushed along the light until it meets the floor (or fades out first).</summary>
        void PlaceBeam(Vector3 light, Color color)
        {
            if (_beamMesh == null || light.x <= 0.05f) { _beamStrength = 0; return; }
            float y0 = WinY0 + Frame, y1 = WinY1 - Frame, z0 = WinZ0 + Frame, z1 = WinZ1 - Frame;
            Vector3[] near = { new Vector3(WallX, y1, z0), new Vector3(WallX, y1, z1), new Vector3(WallX, y0, z1), new Vector3(WallX, y0, z0) };
            var far = new Vector3[4];
            for (int i = 0; i < 4; i++)
            {
                float len = light.y < -0.01f ? Mathf.Min(MaxBeam, (near[i].y - 0.03f) / -light.y) : MaxBeam;
                far[i] = near[i] + light * len;
            }
            // four sides: top (0-1), window side (1-2), bottom (2-3), back side (3-0)
            for (int f = 0; f < 4; f++)
            {
                int a = f, b = (f + 1) % 4;
                _beamVerts[f * 4 + 0] = near[a];
                _beamVerts[f * 4 + 1] = near[b];
                _beamVerts[f * 4 + 2] = far[b];
                _beamVerts[f * 4 + 3] = far[a];
            }
            _beamMesh.vertices = _beamVerts;
            _beamMesh.RecalculateNormals();
            _beamMesh.RecalculateBounds();
            _beam.SetColor(ColorId, Color.Lerp(color, Color.white, 0.25f));

            if (_dustBox)
            {
                // the dust's box runs along the shaft, a little narrower than it so specks stay in the light: its local y
                // follows the window's width (world z), local x its height seen across the shaft
                Vector3 nearC = (near[0] + near[2]) * 0.5f, farC = (far[0] + far[2]) * 0.5f;
                Vector3 axis = farC - nearC, dir = axis.normalized;
                _dustBox.position = (nearC + farC) * 0.5f;
                _dustBox.rotation = Quaternion.LookRotation(dir, Vector3.forward);
                float tall = (y1 - y0) * Mathf.Sqrt(Mathf.Max(0.05f, 1f - dir.y * dir.y));
                float wide = (z1 - z0) * Mathf.Sqrt(Mathf.Max(0.05f, 1f - dir.z * dir.z));
                _dustBox.localScale = new Vector3(tall * 0.8f, wide * 0.8f, axis.magnitude * 0.9f);
                _dust.SetColor(ColorId, Color.Lerp(color, Color.white, 0.4f));
            }
        }

        void BuildBeamTopology()
        {
            var uv = new Vector2[16];
            var tris = new int[24];
            for (int f = 0; f < 4; f++)
            {
                uv[f * 4 + 0] = new Vector2(0, 0);
                uv[f * 4 + 1] = new Vector2(1, 0);
                uv[f * 4 + 2] = new Vector2(1, 1);
                uv[f * 4 + 3] = new Vector2(0, 1);
                int v = f * 4, t = f * 6;
                tris[t] = v; tris[t + 1] = v + 1; tris[t + 2] = v + 2;
                tris[t + 3] = v; tris[t + 4] = v + 2; tris[t + 5] = v + 3;
            }
            _beamMesh.vertices = _beamVerts;
            _beamMesh.uv = uv;
            _beamMesh.triangles = tris;
        }

        void BuildDust(int count)
        {
            _dustCount = count;
            _dustMesh.Clear();
            if (count <= 0) return;
            var rng = new System.Random(1234);
            float R() => (float)rng.NextDouble();
            var verts = new Vector3[count * 4];
            var uv = new Vector2[count * 4];
            var seed = new Vector4[count * 4];
            var tris = new int[count * 6];
            Vector2[] corners = { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            for (int i = 0; i < count; i++)
            {
                var start = new Vector3(R(), R(), R());
                // slow: a few centimetres a second, mostly drifting down and along the shaft
                var s = new Vector4((R() - 0.5f) * 0.02f, (R() - 0.5f) * 0.02f - 0.004f, (R() - 0.5f) * 0.012f, R());
                for (int c = 0; c < 4; c++)
                {
                    verts[i * 4 + c] = start;
                    uv[i * 4 + c] = corners[c];
                    seed[i * 4 + c] = s;
                }
                int v = i * 4, t = i * 6;
                tris[t] = v; tris[t + 1] = v + 1; tris[t + 2] = v + 2;
                tris[t + 3] = v; tris[t + 4] = v + 2; tris[t + 5] = v + 3;
            }
            _dustMesh.vertices = verts;
            _dustMesh.uv = uv;
            _dustMesh.SetUVs(1, seed);
            _dustMesh.triangles = tris;
            // the vertex shader keeps every speck inside the unit box
            _dustMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1.2f);
        }

        void LateUpdate()
        {
            if (_rig == null) return;
            // fade out on the way to the monitor, back in on the way out
            float target = _beamsAllowed && _beamStrength > 0.01f && _rig.Mode != CamMode.Monitor ? 1f : 0f;
            _shown = Mathf.MoveTowards(_shown, target, Time.unscaledDeltaTime * (target > _shown ? 1.2f : 4f));
            bool on = _shown > 0.001f;
            if (_beamRenderer)
            {
                if (_beamRenderer.enabled != on) _beamRenderer.enabled = on;
                if (on) _beam.SetFloat(IntensityId, BeamIntensity);
            }
            if (_dustRenderer)
            {
                bool dust = on && _dustCount > 0;
                if (_dustRenderer.enabled != dust) _dustRenderer.enabled = dust;
                if (dust) _dust.SetFloat(IntensityId, 1.1f * _beamStrength * _shown);
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_sky) Destroy(_sky);
            if (_beam) Destroy(_beam);
            if (_dust) Destroy(_dust);
            if (_beamMesh) Destroy(_beamMesh);
            if (_dustMesh) Destroy(_dustMesh);
        }
    }
}
