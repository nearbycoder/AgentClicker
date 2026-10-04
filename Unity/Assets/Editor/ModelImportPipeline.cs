using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace AgentClicker.EditorTools
{
    /// <summary>
    /// Import rules for the procedurally generated Blender models in Assets/Art/Models.
    ///  * Blender axis conversion is baked so every model has an identity root rotation.
    ///  * Materials become URP/Lit, configured from their Blender name prefix
    ///    (EMIT_, GLASS_, METAL_, GLOSS_, MATTE_, Screen).
    ///  * If a sidecar "<model>.anim.json" exists, the single baked take is split into
    ///    named clips (Idle, Typing, ...).
    /// </summary>
    public class ModelImportPipeline : AssetPostprocessor
    {
        const string ModelsRoot = "Assets/Art/Models/";

        public override int GetPostprocessOrder() => 100; // run after URP's own material preprocessors
        public override uint GetVersion() => 7;

        bool IsOurs => assetPath.StartsWith(ModelsRoot, StringComparison.Ordinal) && assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase);

        [Serializable]
        class AnimClipDef { public string name; public int start; public int end; public bool loop; }

        [Serializable]
        class AnimMeta { public bool animated; public int fps; public AnimClipDef[] clips; }

        AnimMeta LoadMeta()
        {
            string json = Path.ChangeExtension(assetPath, null) + ".anim.json";
            if (!File.Exists(json)) return null;
            return JsonUtility.FromJson<AnimMeta>(File.ReadAllText(json));
        }

        void OnPreprocessModel()
        {
            if (!IsOurs) return;
            var importer = (ModelImporter)assetImporter;
            var meta = LoadMeta();
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;
            importer.importBlendShapes = false;
            importer.addCollider = false;
            importer.isReadable = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.importNormals = ModelImporterNormals.Import;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            bool animated = meta != null && meta.animated;
            importer.importAnimation = animated;
            importer.animationType = animated ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.None;
            importer.avatarSetup = animated ? ModelImporterAvatarSetup.CreateFromThisModel : ModelImporterAvatarSetup.NoAvatar;
        }

        void OnPreprocessAnimation()
        {
            if (!IsOurs) return;
            var meta = LoadMeta();
            if (meta == null || meta.clips == null || meta.clips.Length == 0) return;
            var importer = (ModelImporter)assetImporter;
            var defaults = importer.defaultClipAnimations;
            if (defaults == null || defaults.Length == 0) return;
            string take = defaults[0].takeName;
            var clips = new List<ModelImporterClipAnimation>();
            foreach (var c in meta.clips)
            {
                clips.Add(new ModelImporterClipAnimation
                {
                    name = c.name,
                    takeName = take,
                    firstFrame = c.start,
                    lastFrame = c.end,
                    loopTime = c.loop,
                    loopPose = false,
                    wrapMode = c.loop ? WrapMode.Loop : WrapMode.ClampForever,
                    keepOriginalOrientation = true,
                    keepOriginalPositionY = true,
                    keepOriginalPositionXZ = true,
                });
            }
            importer.clipAnimations = clips.ToArray();
        }

        void OnPreprocessMaterialDescription(MaterialDescription description, Material material, AnimationClip[] clips)
        {
            if (!IsOurs) return;
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) return;
            material.shader = lit;

            Color color = Color.gray;
            if (description.TryGetProperty("DiffuseColor", out Vector4 diffuse))
                color = new Color(diffuse.x, diffuse.y, diffuse.z, 1f);
            if (PlayerSettings.colorSpace == ColorSpace.Linear)
                color = color.gamma;

            string name = description.materialName ?? material.name;
            MaterialStyle.Apply(material, name, color);
        }
    }

    /// <summary>Configures a URP/Lit material from a semantic name prefix. Shared with runtime-created materials in the editor.</summary>
    public static class MaterialStyle
    {
        public static void Apply(Material m, string name, Color color)
        {
            float smooth = 0.3f, metal = 0f;
            bool transparent = false;
            float alpha = 1f;
            Color emission = Color.black;

            if (name.StartsWith("EMIT_", StringComparison.OrdinalIgnoreCase))
            {
                emission = color * 2.2f;
                smooth = 0.5f;
            }
            else if (name.StartsWith("GLASS_", StringComparison.OrdinalIgnoreCase))
            {
                transparent = true; alpha = 0.22f; smooth = 0.95f;
            }
            else if (name.StartsWith("METAL_", StringComparison.OrdinalIgnoreCase))
            {
                metal = 1f; smooth = 0.72f;
            }
            else if (name.StartsWith("GLOSS_", StringComparison.OrdinalIgnoreCase))
            {
                smooth = 0.78f;
            }
            else if (name.StartsWith("MATTE_", StringComparison.OrdinalIgnoreCase))
            {
                smooth = 0.06f;
            }
            else if (name.StartsWith("Screen", StringComparison.OrdinalIgnoreCase))
            {
                color = new Color(0.03f, 0.04f, 0.05f); smooth = 0.92f;
            }

            color.a = alpha;
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);

            if (emission.maxColorComponent > 0f)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                m.DisableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Color.black);
            }

            if (transparent)
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
                m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                m.SetShaderPassEnabled("DepthOnly", false);
                m.SetShaderPassEnabled("ShadowCaster", false);
            }
        }
    }
}
