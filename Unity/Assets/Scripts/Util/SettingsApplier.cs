using System.Collections.Generic;
using System.Linq;
using AgentClicker.Core;
using AgentClicker.Office;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AgentClicker.Util
{
    /// <summary>Pushes <see cref="GameSettings"/> into Unity: URP quality, display, frame pacing, audio, camera.</summary>
    public static class SettingsApplier
    {
        static List<Resolution> _resolutions;

        /// <summary>Distinct window sizes the display supports, largest last.</summary>
        public static List<Resolution> Resolutions =>
            _resolutions ??= Screen.resolutions
                .GroupBy(r => (r.width, r.height)).Select(g => g.First())
                .Where(r => r.width >= 1024)
                .OrderBy(r => r.width * r.height).ToList();

        public static string ResolutionName(int index) =>
            index < 0 || index >= Resolutions.Count ? "Current window" : $"{Resolutions[index].width} × {Resolutions[index].height}";

        public static void ApplyAll(GameSettings s, SceneRefs refs, Sfx sfx, GameModel model)
        {
            ApplyGraphics(s, refs);
            ApplyDisplay(s);
            ApplyFramePacing(s, Application.isFocused);
            ApplyAudio(s, sfx, Application.isFocused);
            if (model != null) model.DayLengthSeconds = s.DayLengthSeconds;
            NumberFormat.Style = (NumberStyle)s.numberStyle;
            UI.UIKit.ReduceMotion = s.reduceMotion;
        }

        static ScriptableRendererFeature _ssaoUltra;
        static bool _ssaoUltraLooked;

        /// <summary>Ultra's ambient occlusion: a second SSAO feature on the renderer with more samples and finer normals.</summary>
        static ScriptableRendererFeature SsaoUltra
        {
            get
            {
                if (_ssaoUltraLooked) return _ssaoUltra;
                _ssaoUltraLooked = true;
                foreach (var f in Resources.FindObjectsOfTypeAll<ScreenSpaceAmbientOcclusion>())
                    if (f.name == "SSAO Ultra") _ssaoUltra = f;
                if (_ssaoUltra == null) Debug.LogWarning("[Settings] no SSAO Ultra feature on the renderer; Ultra uses the standard SSAO");
                return _ssaoUltra;
            }
        }

        /// <summary>Settings → Graphics → Graphics fidelity (and the toggles that go with it).</summary>
        public static void ApplyGraphics(GameSettings s, SceneRefs refs)
        {
            var p = Fidelity.Step(s.quality);
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.msaaSampleCount = p.Msaa;
                urp.renderScale = Mathf.Clamp(s.renderScale * p.RenderScale, 0.4f, 2f);
                urp.shadowDistance = p.ShadowDistance;
                urp.shadowCascadeCount = p.Cascades;
                urp.mainLightShadowmapResolution = p.ShadowRes;
                urp.additionalLightsShadowmapResolution = p.LampShadows ? 2048 : 1024;
                urp.maxAdditionalLightsCount = p.AdditionalLights;
                urp.hdrColorBufferPrecision = p.HighPrecision ? HDRColorBufferPrecision._64Bits : HDRColorBufferPrecision._32Bits;
                urp.colorGradingLutSize = p.HighPrecision ? 64 : 32;
            }

            if (refs == null) return;
            if (refs.Sun)
            {
                refs.Sun.shadows = p.SoftShadows ? LightShadows.Soft : LightShadows.Hard;
                SoftQuality(refs.Sun, p);
            }
            if (refs.CeilingLights != null)
                foreach (var l in refs.CeilingLights)
                {
                    if (!l) continue;
                    l.shadows = p.LampShadows ? LightShadows.Soft : LightShadows.None;
                    l.shadowStrength = 0.7f;
                    l.shadowNearPlane = 0.2f;
                    SoftQuality(l, p);
                }
            var ultra = SsaoUltra;
            if (refs.Ssao) refs.Ssao.SetActive(s.postProcessing && (p.Ssao == 1 || (p.Ssao == 2 && ultra == null)));
            if (ultra) ultra.SetActive(s.postProcessing && p.Ssao == 2);
            if (FidelityEffects.Instance) FidelityEffects.Instance.Apply(p);
            if (Atmosphere.Instance) Atmosphere.Instance.SetFidelity(p, s.reduceMotion);

            var cam = refs.MainCamera;
            if (cam)
            {
                var data = cam.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = s.postProcessing;
                // MSAA already smooths edges; only fall back to FXAA when it's off.
                data.antialiasing = p.Msaa > 1 ? AntialiasingMode.None : AntialiasingMode.FastApproximateAntialiasing;
            }
        }

        static void SoftQuality(Light light, FidelityStep p)
        {
            var data = light.GetComponent<UniversalAdditionalLightData>();
            if (!data) data = light.gameObject.AddComponent<UniversalAdditionalLightData>();
            data.softShadowQuality = p.HighSoftShadows ? SoftShadowQuality.High : SoftShadowQuality.UsePipelineSettings;
        }

        public static void ApplyDisplay(GameSettings s)
        {
            if (Platform.IsWeb) return; // the page decides the canvas size
            var mode = s.displayMode switch
            {
                1 => FullScreenMode.FullScreenWindow,
                2 => FullScreenMode.ExclusiveFullScreen,
                _ => FullScreenMode.Windowed,
            };
            if (s.resolutionIndex >= 0 && s.resolutionIndex < Resolutions.Count)
            {
                var r = Resolutions[s.resolutionIndex];
                if (Screen.width != r.width || Screen.height != r.height || Screen.fullScreenMode != mode)
                    Screen.SetResolution(r.width, r.height, mode);
            }
            else if (Screen.fullScreenMode != mode)
            {
                if (mode == FullScreenMode.Windowed) Screen.fullScreenMode = mode;
                else Screen.SetResolution(Screen.currentResolution.width, Screen.currentResolution.height, mode);
            }
        }

        /// <summary>Idle games run all day in the background, so drop to a trickle when unfocused.</summary>
        public static void ApplyFramePacing(GameSettings s, bool focused)
        {
            if (Platform.IsWeb)
            {
                // the browser paces frames to the display and stops hidden tabs on its own, but a visible tab behind
                // another window keeps rendering at full speed. A frame cap only takes effect there with vsync off.
                bool throttle = !focused && s.throttleInBackground;
                QualitySettings.vSyncCount = throttle ? 0 : 1;
                Application.targetFrameRate = throttle ? 15 : -1;
                return;
            }
            if (!focused && s.throttleInBackground)
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = 15;
                return;
            }
            QualitySettings.vSyncCount = s.vsync ? 1 : 0;
            int cap = s.TargetFps;
            Application.targetFrameRate = cap <= 0 ? -1 : cap;
        }

        public static void ApplyAudio(GameSettings s, Sfx sfx, bool focused)
        {
            AudioListener.volume = s.ListenerVolume(focused);
            if (sfx) sfx.SetVolumes(s.sfxVolume, s.musicVolume, s.ambienceVolume);
        }
    }
}
