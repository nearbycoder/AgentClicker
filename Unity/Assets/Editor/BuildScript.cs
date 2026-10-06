using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AgentClicker.EditorTools
{
    public static class BuildScript
    {
        [MenuItem("Agent Clicker/Build Linux Player")]
        public static void BuildLinux() => Build(BuildTarget.StandaloneLinux64, "Linux/AgentClicker.x86_64");

        [MenuItem("Agent Clicker/Build Linux Development Player")]
        public static void BuildLinuxDev() => Build(BuildTarget.StandaloneLinux64, "LinuxDev/AgentClicker.x86_64", BuildOptions.Development);

        [MenuItem("Agent Clicker/Build Windows Player")]
        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, "Windows/AgentClicker.exe");

        /// <summary>Universal (Intel + Apple Silicon) macOS app, Mono backend so it can be built from Linux.</summary>
        [MenuItem("Agent Clicker/Build macOS Player")]
        public static void BuildMac()
        {
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone, "com.nearbygames.agentclicker");
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            UnityEditor.OSXStandalone.UserBuildSettings.architecture = UnityEditor.Build.OSArchitecture.x64ARM64;
            Build(BuildTarget.StandaloneOSX, "Mac/Agent Clicker.app");
        }

        /// <summary>
        /// Browser build → Builds/WebGL. Brotli with the JavaScript decompression fallback, so it runs from any static
        /// host (no Content-Encoding headers needed). The page comes from Assets/WebGLTemplates/AgentClicker.
        /// </summary>
        [MenuItem("Agent Clicker/Build WebGL Player")]
        public static void BuildWebGL()
        {
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.template = "PROJECT:AgentClicker"; // Assets/WebGLTemplates/AgentClicker: full-window page
            Build(BuildTarget.WebGL, "WebGL");
        }

        static void Build(BuildTarget target, string relative, BuildOptions options = BuildOptions.None)
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Builds", relative));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Main.unity" },
                locationPathName = path,
                target = target,
                options = options,
            });
            var s = report.summary;
            Debug.Log($"[BuildScript] {s.result}: {path} ({s.totalSize / (1024 * 1024)} MB, {s.totalErrors} errors)");
            if (Application.isBatchMode) EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
