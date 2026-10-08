using UnityEngine;
using UnityEngine.Rendering;

namespace AgentClicker.Office
{
    /// <summary>Drives sunlight, sky colour, ambient light, city lights and the wall clock from the in-game hour.</summary>
    public class TimeOfDay : MonoBehaviour
    {
        SceneRefs _refs;
        MaterialPropertyBlock _mpb;
        Color _cityEmission;
        Quaternion _hourBase, _minuteBase;
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        // hour, sky colour
        static readonly (float h, Color c)[] Sky =
        {
            (6f, C("#3A4A78")), (8.5f, C("#A9CFF0")), (12f, C("#8EC5FF")), (15.5f, C("#A8C8E8")),
            (16.8f, C("#F2B880")), (17.6f, C("#E07A5F")), (18.6f, C("#5B3F78")), (19.6f, C("#1E2350")), (21f, C("#0B1026")),
        };

        // the top of the sky, above the window's horizon colour (the sky colour above)
        static readonly (float h, Color c)[] Zenith =
        {
            (6f, C("#1B2550")), (8.5f, C("#5E9BD8")), (12f, C("#4A88DB")), (15.5f, C("#5F90CC")),
            (16.8f, C("#6C86B8")), (17.6f, C("#5A5C99")), (18.6f, C("#2E2A5C")), (19.6f, C("#12163A")), (21f, C("#060A1C")),
        };

        /// <summary>The sky behind the city, the sunbeam and its dust (set by GameManager).</summary>
        public Atmosphere Atmosphere { get; set; }

        static Color C(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }

        public void Init(SceneRefs refs)
        {
            _refs = refs;
            _mpb = new MaterialPropertyBlock();
            if (refs.CityWindows) _cityEmission = refs.CityWindows.sharedMaterial.GetColor(EmissionId);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            if (refs.ClockHour) _hourBase = refs.ClockHour.localRotation;
            if (refs.ClockMinute) _minuteBase = refs.ClockMinute.localRotation;
        }

        static Color Gradient((float h, Color c)[] keys, float hour)
        {
            if (hour <= keys[0].h) return keys[0].c;
            for (int i = 1; i < keys.Length; i++)
                if (hour <= keys[i].h)
                    return Color.Lerp(keys[i - 1].c, keys[i].c, Mathf.InverseLerp(keys[i - 1].h, keys[i].h, hour));
            return keys[keys.Length - 1].c;
        }

        float _appliedHour = -1;

        public void Apply(float hour)
        {
            // Lighting barely changes from frame to frame: update when the clock moves ~1.5 game minutes.
            if (_refs == null || Mathf.Abs(hour - _appliedHour) < 0.025f) return;
            _appliedHour = hour;
            Color sky = Gradient(Sky, hour);
            _refs.MainCamera.backgroundColor = sky;

            // Sun: rises in the window (left wall), sets around 18:30.
            float day = Mathf.InverseLerp(7f, 18.7f, hour);
            float elevation = Mathf.Sin(day * Mathf.PI) * 48f;
            float yaw = Mathf.Lerp(55f, 128f, day);
            var sun = _refs.Sun;
            sun.transform.rotation = Quaternion.Euler(Mathf.Max(elevation, 3f), yaw, 0f);
            float strength = Mathf.Clamp01(Mathf.Sin(day * Mathf.PI) * 1.6f);
            sun.intensity = 2.4f * strength;
            sun.color = Color.Lerp(C("#FF9A5A"), C("#FFF4E0"), Mathf.Clamp01(elevation / 25f));
            sun.enabled = strength > 0.01f;

            // Ambient follows the sky but never gets too dark inside.
            float daylight = Mathf.Clamp01(strength);
            RenderSettings.ambientSkyColor = Color.Lerp(C("#3A4060"), C("#B8C8DC"), daylight) * 0.85f;
            RenderSettings.ambientEquatorColor = Color.Lerp(C("#2E3044"), C("#8E8A84"), daylight) * 0.8f;
            RenderSettings.ambientGroundColor = Color.Lerp(C("#1A1A22"), C("#4A4640"), daylight) * 0.7f;

            if (Atmosphere)
            {
                Color horizon = Color.Lerp(sky, Color.white, 0.12f * daylight);
                Color haze = Color.Lerp(horizon, C("#8A93A0") * Mathf.Lerp(0.25f, 1f, daylight), 0.35f);
                Atmosphere.Apply(Gradient(Zenith, hour), horizon, haze, sun, strength, 1f - Mathf.Clamp01(strength * 6f));
            }

            // Indoor lights carry the evening.
            foreach (var l in _refs.CeilingLights)
                if (l) l.intensity = Mathf.Lerp(2.6f, 1.5f, daylight);
            if (_refs.MonitorGlow) _refs.MonitorGlow.intensity = Mathf.Lerp(0.9f, 0.45f, daylight);

            // City windows light up as the sun goes down.
            if (_refs.CityWindows)
            {
                float lit = Mathf.Lerp(0.15f, 1.3f, 1f - daylight);
                _refs.CityWindows.GetPropertyBlock(_mpb);
                _mpb.SetColor(EmissionId, _cityEmission * lit);
                _refs.CityWindows.SetPropertyBlock(_mpb);
            }

            // Wall clock (hands point to 12 at rest; clockwise is negative about +Z as seen from the room).
            float h12 = hour % 12f;
            // (rotate about the clock root's Z axis, keeping the imported axis-conversion rotation)
            if (_refs.ClockHour) _refs.ClockHour.localRotation = Quaternion.AngleAxis(-h12 / 12f * 360f, Vector3.forward) * _hourBase;
            if (_refs.ClockMinute) _refs.ClockMinute.localRotation = Quaternion.AngleAxis(-(hour % 1f) * 360f, Vector3.forward) * _minuteBase;
        }
    }
}
