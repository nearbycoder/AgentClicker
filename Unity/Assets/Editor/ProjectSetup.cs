using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AgentClicker.EditorTools
{
    /// <summary>
    /// One-time (idempotent) project configuration. Safe to re-run.
    ///   Unity -batchmode -quit -projectPath Unity -executeMethod AgentClicker.EditorTools.ProjectSetup.Run
    /// </summary>
    public static class ProjectSetup
    {
        public const string SettingsDir = "Assets/Settings";
        public const string UrpAssetPath = SettingsDir + "/URP.asset";
        public const string UrpRendererPath = SettingsDir + "/URP_Renderer.asset";
        public const string PostFxPath = SettingsDir + "/PostFX.asset";

        [MenuItem("Agent Clicker/Setup/Run Project Setup")]
        public static void Run()
        {
            Directory.CreateDirectory(SettingsDir);
            ImportTmpEssentials();
            var urp = EnsureUrp();
            EnsurePostFx();
            EnsureSsao();
            ConfigurePlayer();
            AlwaysIncludeShaders("TextMeshPro/Mobile/Distance Field", "TextMeshPro/Distance Field",
                "Universal Render Pipeline/Lit", "Universal Render Pipeline/Unlit", "UI/Default");
            AssetDatabase.SaveAssets();
            ReimportModels();
            Debug.Log($"[ProjectSetup] done. Pipeline = {GraphicsSettings.defaultRenderPipeline?.name}, urp={urp != null}");
        }

        static void ImportTmpEssentials()
        {
            if (File.Exists("Assets/TextMesh Pro/Resources/TMP Settings.asset")) return;
            // Package import is asynchronous and does not finish before -quit in batch mode, so
            // Tools/extract_unitypackage.py unpacks it directly. In an interactive editor this is fine.
            Debug.LogWarning("[ProjectSetup] TMP Essential Resources missing. Run: python3 Tools/extract_unitypackage.py " +
                             "\"<Unity>/Editor/Data/Resources/PackageManager/BuiltInPackages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage\" Unity");
            AssetDatabase.Refresh();
        }

        static UniversalRenderPipelineAsset EnsureUrp()
        {
            var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(UrpAssetPath);
            if (urp == null)
            {
                var data = ScriptableObject.CreateInstance<UniversalRendererData>();
                var getPpd = typeof(PostProcessData).GetMethod("GetDefaultPostProcessData", BindingFlags.NonPublic | BindingFlags.Static);
                if (getPpd != null) data.postProcessData = (PostProcessData)getPpd.Invoke(null, null);
                AssetDatabase.CreateAsset(data, UrpRendererPath);
                urp = UniversalRenderPipelineAsset.Create(data);
                AssetDatabase.CreateAsset(urp, UrpAssetPath);
            }

            urp.supportsHDR = true;
            urp.msaaSampleCount = 4;
            urp.renderScale = 1f;
            urp.shadowDistance = 14f;
            urp.shadowCascadeCount = 2;
            urp.mainLightShadowmapResolution = 2048;
            urp.additionalLightsShadowmapResolution = 1024;
            urp.maxAdditionalLightsCount = 8;
            var so = new SerializedObject(urp);
            SetProp(so, "m_MainLightRenderingMode", (int)LightRenderingMode.PerPixel);
            SetProp(so, "m_MainLightShadowsSupported", 1);
            SetProp(so, "m_AdditionalLightsRenderingMode", (int)LightRenderingMode.PerPixel);
            SetProp(so, "m_AdditionalLightShadowsSupported", 1);
            SetProp(so, "m_SoftShadowsSupported", 1);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(urp);

            GraphicsSettings.defaultRenderPipeline = urp;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = urp;
            }
            QualitySettings.SetQualityLevel(current, false);
            return urp;
        }

        static void SetProp(SerializedObject so, string name, int value)
        {
            var p = so.FindProperty(name);
            if (p == null) { Debug.LogWarning($"[ProjectSetup] URP property {name} not found"); return; }
            if (p.propertyType == SerializedPropertyType.Boolean) p.boolValue = value != 0;
            else p.intValue = value;
        }

        /// <summary>Screen-space ambient occlusion as a renderer feature (toggled at runtime by the quality preset).</summary>
        static void EnsureSsao()
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(UrpRendererPath);
            if (data == null || data.rendererFeatures.Any(f => f is ScreenSpaceAmbientOcclusion)) return;
            var ssao = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
            ssao.name = "SSAO";
            AssetDatabase.AddObjectToAsset(ssao, data);
            data.rendererFeatures.Add(ssao);
            var so = new SerializedObject(ssao);
            void F(string path, float v) { var p = so.FindProperty(path); if (p != null) p.floatValue = v; else Debug.LogWarning("[ProjectSetup] SSAO prop missing: " + path); }
            F("m_Settings.Intensity", 0.9f);
            F("m_Settings.Radius", 0.22f);
            F("m_Settings.DirectLightingStrength", 0.2f);
            so.ApplyModifiedPropertiesWithoutUndo();
            var dso = new SerializedObject(data);
            var map = dso.FindProperty("m_RendererFeatureMap");
            if (map != null)
            {
                map.InsertArrayElementAtIndex(map.arraySize);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(ssao, out _, out long localId);
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                dso.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            Debug.Log("[ProjectSetup] added SSAO renderer feature");
        }

        static void EnsurePostFx()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostFxPath);
            if (profile != null) return;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, PostFxPath);

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(0.65f);
            bloom.scatter.Override(0.6f);

            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.Neutral);

            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.15f);
            color.contrast.Override(8f);
            color.saturation.Override(8f);

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.22f);
            vignette.smoothness.Override(0.5f);

            foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
            EditorUtility.SetDirty(profile);
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "Nearby Games";
            PlayerSettings.productName = "Agent Clicker";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.runInBackground = true;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.visibleInBackground = true;
        }

        static void AlwaysIncludeShaders(params string[] names)
        {
            var gs = AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/GraphicsSettings.asset");
            var so = new SerializedObject(gs);
            var arr = so.FindProperty("m_AlwaysIncludedShaders");
            var existing = Enumerable.Range(0, arr.arraySize).Select(i => arr.GetArrayElementAtIndex(i).objectReferenceValue).ToList();
            foreach (var n in names)
            {
                var sh = Shader.Find(n);
                if (sh == null) { Debug.LogWarning("[ProjectSetup] shader not found: " + n); continue; }
                if (existing.Contains(sh)) continue;
                arr.InsertArrayElementAtIndex(arr.arraySize);
                arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = sh;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [MenuItem("Agent Clicker/Setup/Reimport Models")]
        public static void ReimportModels()
        {
            AssetDatabase.Refresh();
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/Art/Models" }))
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
        }
    }
}
