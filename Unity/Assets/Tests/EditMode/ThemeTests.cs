using AgentClicker.UI;
using NUnit.Framework;
using UnityEngine;

namespace AgentClicker.Tests
{
    public class ThemeTests
    {
        // WCAG 2 relative luminance and contrast ratio of sRGB colours
        static double Channel(float c) => c <= 0.04045 ? c / 12.92 : System.Math.Pow((c + 0.055) / 1.055, 2.4);
        static double Luminance(Color c) => 0.2126 * Channel(c.r) + 0.7152 * Channel(c.g) + 0.0722 * Channel(c.b);

        static double Contrast(Color a, Color b)
        {
            double la = Luminance(a), lb = Luminance(b);
            return (System.Math.Max(la, lb) + 0.05) / (System.Math.Min(la, lb) + 0.05);
        }

        [Test]
        public void BodyDimAndFaintTextAreReadableOnEveryPanel()
        {
            var surfaces = new[] { ("Bg", Theme.Bg), ("Panel", Theme.Panel), ("PanelLight", Theme.PanelLight) };
            var texts = new[] { ("Text", Theme.Text), ("TextDim", Theme.TextDim), ("TextFaint", Theme.TextFaint) };
            foreach (var (tn, t) in texts)
            foreach (var (sn, s) in surfaces)
                Assert.GreaterOrEqual(Contrast(t, s), 4.5, $"{tn} on {sn}");
        }

        [Test]
        public void FaintTextStaysDimmerThanSecondaryText()
        {
            Assert.GreaterOrEqual(Contrast(Theme.TextDim, Theme.TextFaint), 1.3, "dim vs faint");
            Assert.GreaterOrEqual(Contrast(Theme.Text, Theme.TextDim), 1.6, "body vs dim");
        }
    }
}
