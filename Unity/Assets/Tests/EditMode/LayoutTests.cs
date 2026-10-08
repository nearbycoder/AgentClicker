using AgentClicker.UI;
using NUnit.Framework;

namespace AgentClicker.Tests
{
    public class LayoutTests
    {
        // CorpOS desktop pixels (1600 x 900, y down). The chapter banner's band ends near y 327 in the monitor view;
        // the toast stack (three wrapped toasts at most) starts near y 658; SHIP CODE's column ends at x 436.
        const float BannerBottom = 330, ToastTop = 655, ShipColumnRight = 436;

        [Test]
        public void ModelDropCardsKeepClearOfBannerToastsAndShipCode()
        {
            var rng = new System.Random(3);
            for (int i = 0; i < 2000; i++)
            {
                var p = ComputerUI.DropCardPosition(rng);
                Assert.GreaterOrEqual(p.y, BannerBottom, "below the chapter banner");
                Assert.LessOrEqual(p.y + ComputerUI.DropCardHeight, ToastTop, "above the toasts");
                Assert.Greater(p.x, ShipColumnRight, "never over SHIP CODE");
                Assert.LessOrEqual(p.x + ComputerUI.DropCardWidth, ComputerUI.Width, "on the screen");
            }
        }

        [Test]
        public void OverlaysKeepTheirScaleUpTo21by9AndStayTallOnWiderScreens()
        {
            // 16:9, 4:3, 16:10 and 21:9 keep Unity's match 0.5, as before
            foreach (var (w, h) in new[] { (1600, 900), (1920, 1080), (1024, 768), (1280, 800), (1680, 720) })
                Assert.AreEqual(0.5f, OverlayScaler.Match(w, h), 1e-4f, $"{w}x{h}");
            Assert.AreEqual(785f, OverlayScaler.CanvasHeight(1680, 720), 1f, "21:9 as before");
            Assert.AreEqual(780f, OverlayScaler.CanvasHeight(2560, 1080), 1f, "a 64:27 monitor: 779 units before, now 780");
            // 32:9 would get a 636-unit canvas at match 0.5; it leans toward the height until the canvas is 780 units tall
            foreach (var (w, h) in new[] { (2560, 720), (3840, 1080), (5120, 1440) })
            {
                Assert.Greater(OverlayScaler.Match(w, h), 0.5f, $"{w}x{h}");
                Assert.AreEqual(OverlayScaler.MinHeight, OverlayScaler.CanvasHeight(w, h), 0.5f, $"{w}x{h}");
            }
            Assert.AreEqual(0.5f, OverlayScaler.Match(0, 0), "no window yet");
        }
    }
}
