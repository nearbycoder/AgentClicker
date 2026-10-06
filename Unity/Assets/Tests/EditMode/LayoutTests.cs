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
    }
}
