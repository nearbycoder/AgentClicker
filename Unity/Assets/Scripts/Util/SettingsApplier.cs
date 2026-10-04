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
        struct Preset
        {
            public int Msaa, ShadowRes, Cascades, AdditionalLights;
            public float ShadowDistance, ScaleMult;
            public bool SoftShadows, Ssao, Shadows, PointShadows;
        }

        static readonly Preset[] Presets =
        {
            new Preset { Msaa = 1, ShadowRes = 1024, Cascades = 1, ShadowDistance = 9, AdditionalLights = 3, ScaleMult = 0.85f, Shadows = true },
            new Preset { Msaa = 2, ShadowRes = 2048, Cascades = 2, ShadowDistance = 12, AdditionalLights = 5, ScaleMult = 1f, Shadows = true, SoftShadows = true },
            new Preset { Msaa = 4, ShadowRes = 2048, Cascades = 2, ShadowDistance = 14, AdditionalLights = 8, ScaleMult = 1f, Shadows = true, SoftShadows = true, Ssao = true },
            new Preset { Msaa = 4, ShadowRes = 4096, Cascades = 2, ShadowDistance = 16, AdditionalLights = 8, ScaleMult = 1f, Shadows = true, SoftShadows = true, Ssao = true },
        };

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
        }

        public static void ApplyGraphics(GameSettings s, SceneRefs refs)
        {
            var p = Presets[Mathf.Clamp(s.quality, 0, Presets.Length - 1)];
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.msaaSampleCount = p.Msaa;
                urp.renderScale = Mathf.Clamp(s.renderScale * p.ScaleMult, 0.4f, 1f);
                urp.shadowDistance = p.ShadowDistance;
                urp.shadowCascadeCount = p.Cascades;
                urp.mainLightShadowmapResolution = p.ShadowRes;
                urp.maxAdditionalLightsCount = p.AdditionalLights;
            }

            if (refs == null) return;
            if (refs.Sun)
                refs.Sun.shadows = !p.Shadows ? LightShadows.None : p.SoftShadows ? LightShadows.Soft : LightShadows.Hard;
            if (refs.Ssao) refs.Ssao.SetActive(p.Ssao && s.postProcessing);

            var cam = refs.MainCamera;
            if (cam)
            {
                cam.fieldOfView = s.fieldOfView;
                var data = cam.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = s.postProcessing;
                // MSAA already smooths edges; only fall back to FXAA when it's off.
                data.antialiasing = p.Msaa > 1 ? AntialiasingMode.None : AntialiasingMode.FastApproximateAntialiasing;
            }
        }

        public static void ApplyDisplay(GameSettings s)
        {
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
            AudioListener.volume = !focused && s.muteInBackground ? 0f : s.masterVolume;
            if (sfx) sfx.SetVolumes(s.sfxVolume, s.musicVolume, s.ambienceVolume);
        }
    }
}
