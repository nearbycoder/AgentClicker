namespace AgentClicker.Core
{
    /// <summary>One step of Settings → Graphics → Graphics fidelity. Applied by Util.SettingsApplier.</summary>
    public struct FidelityStep
    {
        public string Name;
        /// <summary>One line under the slider: what this step does.</summary>
        public string Summary;
        /// <summary>MSAA samples; 1 means no MSAA (FXAA smooths the edges instead).</summary>
        public int Msaa;
        /// <summary>Multiplies the player's Render scale (above 1 renders more pixels than the window has).</summary>
        public float RenderScale;
        public int ShadowRes, Cascades;
        public float ShadowDistance;
        public bool SoftShadows, HighSoftShadows;
        /// <summary>The ceiling lights cast shadows too (point-light shadows: six extra shadow views each).</summary>
        public bool LampShadows;
        public int AdditionalLights;
        /// <summary>Ambient occlusion: 0 off, 1 the standard pass, 2 more samples and finer normals.</summary>
        public int Ssao;
        public bool BloomHighQuality, BloomQuarterRes;
        public int BloomIterations;
        public bool DepthOfField;
        public int ReflectionRes;
        /// <summary>A 64-bit HDR buffer and a finer colour-grading LUT (smoother gradients in the sky and the bloom).</summary>
        public bool HighPrecision;
        /// <summary>Sunbeams through the window, and how many dust motes drift in them.</summary>
        public bool Sunbeams;
        public int Dust;
    }

    /// <summary>
    /// The Graphics fidelity ladder, Low to Ultra. High is the default and the game's long-standing look; every step costs
    /// at least as much as the one below it (EditMode tests hold it to that).
    /// </summary>
    public static class Fidelity
    {
        public const int Default = 2;

        public static readonly FidelityStep[] Steps =
        {
            new FidelityStep
            {
                Name = "Low", Summary = "For weak GPUs: 85% scale, FXAA, hard shadows, no AO or sunbeams",
                Msaa = 1, RenderScale = 0.85f, ShadowRes = 1024, Cascades = 1, ShadowDistance = 9, AdditionalLights = 3,
                BloomQuarterRes = true, BloomIterations = 4, ReflectionRes = 64,
            },
            new FidelityStep
            {
                Name = "Medium", Summary = "2x MSAA, soft 2K shadows, sunbeams with a little dust",
                Msaa = 2, RenderScale = 1f, ShadowRes = 2048, Cascades = 2, ShadowDistance = 12, AdditionalLights = 5,
                SoftShadows = true, BloomIterations = 6, ReflectionRes = 128, Sunbeams = true, Dust = 24,
            },
            new FidelityStep
            {
                Name = "High", Summary = "4x MSAA, soft shadows, ambient occlusion, sunbeams (default)",
                Msaa = 4, RenderScale = 1f, ShadowRes = 2048, Cascades = 2, ShadowDistance = 14, AdditionalLights = 8,
                SoftShadows = true, Ssao = 1, BloomIterations = 6, ReflectionRes = 128, Sunbeams = true, Dust = 48,
            },
            new FidelityStep
            {
                Name = "Ultra", Summary = "8x MSAA, 125% scale, 4K and lamp shadows, depth of field, dust",
                Msaa = 8, RenderScale = 1.25f, ShadowRes = 4096, Cascades = 4, ShadowDistance = 16, AdditionalLights = 8,
                SoftShadows = true, HighSoftShadows = true, LampShadows = true, Ssao = 2, BloomHighQuality = true, BloomIterations = 8,
                DepthOfField = true, ReflectionRes = 256, HighPrecision = true, Sunbeams = true, Dust = 120,
            },
        };

        public static FidelityStep Step(int index) => Steps[index < 0 ? 0 : index >= Steps.Length ? Steps.Length - 1 : index];
    }
}
