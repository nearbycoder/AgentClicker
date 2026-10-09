using System;
using AgentClicker.Core;
using NUnit.Framework;
using UnityEngine;

namespace AgentClicker.Tests
{
    public class FidelityTests
    {
        static readonly FidelityStep[] S = Fidelity.Steps;

        [Test]
        public void FourStepsFromLowToUltraWithHighTheDefault()
        {
            CollectionAssert.AreEqual(new[] { "Low", "Medium", "High", "Ultra" }, Array.ConvertAll(S, f => f.Name));
            Assert.AreEqual(2, Fidelity.Default);
            Assert.AreEqual(Fidelity.Default, Fidelity.PlayerDefault); // the editor and the desktop players
            Assert.AreEqual(Fidelity.Default, new GameSettings().quality);
            CollectionAssert.AreEqual(Array.ConvertAll(S, f => f.Name), GameSettings.QualityNames);
        }

        [Test]
        public void EveryStepCostsAtLeastAsMuchAsTheOneBelow()
        {
            for (int i = 1; i < S.Length; i++)
            {
                FidelityStep a = S[i - 1], b = S[i];
                string at = $"{a.Name} → {b.Name}";
                Assert.GreaterOrEqual(b.Msaa, a.Msaa, at + " MSAA");
                Assert.GreaterOrEqual(b.RenderScale, a.RenderScale, at + " render scale");
                Assert.GreaterOrEqual(b.ShadowRes, a.ShadowRes, at + " shadow map");
                Assert.GreaterOrEqual(b.Cascades, a.Cascades, at + " cascades");
                Assert.GreaterOrEqual(b.ShadowDistance, a.ShadowDistance, at + " shadow distance");
                Assert.GreaterOrEqual(b.AdditionalLights, a.AdditionalLights, at + " lights");
                Assert.GreaterOrEqual(b.Ssao, a.Ssao, at + " AO");
                Assert.GreaterOrEqual(b.BloomIterations, a.BloomIterations, at + " bloom iterations");
                Assert.GreaterOrEqual(b.ReflectionRes, a.ReflectionRes, at + " reflections");
                Assert.GreaterOrEqual(b.Dust, a.Dust, at + " dust");
                Assert.IsTrue(!a.Sunbeams || b.Sunbeams, at + " sunbeams");
                // once a feature is on it stays on (and quarter-resolution bloom, a saving, only goes away)
                Assert.IsTrue(!a.SoftShadows || b.SoftShadows, at + " soft shadows");
                Assert.IsTrue(!a.HighSoftShadows || b.HighSoftShadows, at + " soft shadow filter");
                Assert.IsTrue(!a.LampShadows || b.LampShadows, at + " lamp shadows");
                Assert.IsTrue(!a.BloomHighQuality || b.BloomHighQuality, at + " bloom filter");
                Assert.IsTrue(!a.DepthOfField || b.DepthOfField, at + " depth of field");
                Assert.IsTrue(!a.HighPrecision || b.HighPrecision, at + " HDR precision");
                Assert.IsTrue(a.BloomQuarterRes || !b.BloomQuarterRes, at + " bloom resolution");
            }
        }

        [Test]
        public void HighIsTheLookTheGameHadBefore()
        {
            // the old "High" quality preset: 4x MSAA, 2K soft shadows over two cascades to 14 m, 8 lights, SSAO
            var h = Fidelity.Step(2);
            Assert.AreEqual(4, h.Msaa);
            Assert.AreEqual(1f, h.RenderScale);
            Assert.AreEqual(2048, h.ShadowRes);
            Assert.AreEqual(2, h.Cascades);
            Assert.AreEqual(14f, h.ShadowDistance);
            Assert.AreEqual(8, h.AdditionalLights);
            Assert.AreEqual(1, h.Ssao);
            Assert.IsTrue(h.SoftShadows);
            Assert.IsFalse(h.HighSoftShadows || h.LampShadows || h.DepthOfField || h.BloomHighQuality || h.HighPrecision);
        }

        [Test]
        public void UltraGoesPastHighAndLowIsTheCheapest()
        {
            FidelityStep low = Fidelity.Step(0), high = Fidelity.Step(2), ultra = Fidelity.Step(3);
            Assert.Greater(ultra.Msaa, high.Msaa);
            Assert.Greater(ultra.RenderScale, 1f);
            Assert.Greater(ultra.ShadowRes, high.ShadowRes);
            Assert.IsTrue(ultra.LampShadows && ultra.DepthOfField && ultra.HighSoftShadows);
            Assert.AreEqual(1, low.Msaa, "Low smooths edges with FXAA instead of MSAA");
            Assert.Less(low.RenderScale, 1f);
            Assert.AreEqual(0, low.Ssao);
            Assert.IsFalse(low.Sunbeams);
            Assert.AreEqual(0, low.Dust);
            Assert.Greater(ultra.Dust, high.Dust);
            Assert.IsFalse(low.SoftShadows);
        }

        [Test]
        public void StepOutOfRangeClampsAndEverySummaryFitsUnderTheSlider()
        {
            Assert.AreEqual("Low", Fidelity.Step(-4).Name);
            Assert.AreEqual("Ultra", Fidelity.Step(17).Name);
            // the hint line is one 15-point line about 540 units wide
            foreach (var f in S) Assert.LessOrEqual(f.Summary.Length, 64, f.Name);
            for (int i = 0; i < S.Length; i++)
            {
                Assert.LessOrEqual(Fidelity.Hint(i, Fidelity.Default).Length, 64, S[i].Name);
                Assert.LessOrEqual(Fidelity.Hint(i, Fidelity.WebDefault).Length, 64, S[i].Name + " (web)");
                Assert.LessOrEqual(Fidelity.Hint(i, Fidelity.MobileWebDefault).Length, 64, S[i].Name + " (phone)");
            }
        }

        [Test]
        public void PhonesAndTabletsInTheBrowserStartOnLow()
        {
            Assert.AreEqual("Low", S[Fidelity.MobileWebDefault].Name);
            Assert.AreEqual(1, S[Fidelity.MobileWebDefault].Msaa); // no multisampled render targets
            Assert.Less(S[Fidelity.MobileWebDefault].RenderScale, 1f);
            for (int i = 0; i < S.Length; i++)
                Assert.AreEqual(i == Fidelity.MobileWebDefault, Fidelity.Hint(i, Fidelity.MobileWebDefault).EndsWith(" (default)"), S[i].Name);
        }

        [Test]
        public void TheBrowserStartsOnMediumAndOnlyTheDefaultStepSaysSo()
        {
            Assert.AreEqual("Medium", S[Fidelity.WebDefault].Name);
            Assert.AreEqual(1, Fidelity.WebDefault);
            for (int i = 0; i < S.Length; i++)
            {
                Assert.AreEqual(i == Fidelity.Default, Fidelity.Hint(i, Fidelity.Default).EndsWith(" (default)"), S[i].Name);
                Assert.AreEqual(i == Fidelity.WebDefault, Fidelity.Hint(i, Fidelity.WebDefault).EndsWith(" (default)"), S[i].Name + " (web)");
            }
            Assert.AreEqual("4x MSAA, soft shadows, ambient occlusion, sunbeams (default)", Fidelity.Hint(2, Fidelity.Default));
        }

        [Test]
        public void SettingsFromBeforeTheSliderKeepTheirStep()
        {
            // round 11's settings JSON stored the quality preset as "quality"; the slider reads the same field
            foreach (int q in new[] { 0, 1, 2, 3 })
            {
                var old = JsonUtility.FromJson<GameSettings>($"{{\"quality\":{q},\"vsync\":true,\"fpsCap\":1}}").Clamp();
                Assert.AreEqual(q, old.quality);
            }
            var copy = JsonUtility.FromJson<GameSettings>(JsonUtility.ToJson(new GameSettings { quality = 3 })).Clamp();
            Assert.AreEqual(3, copy.quality);
        }
    }
}
