using AgentClicker.Core;
using NUnit.Framework;
using UnityEngine;

namespace AgentClicker.Tests
{
    public class HoldToShipTests
    {
        static int HoldFor(HoldToShip h, float seconds, float frame)
        {
            int shipped = 1; // the press itself
            h.Press();
            int frames = Mathf.CeilToInt(seconds / frame);
            for (int i = 0; i < frames; i++) shipped += h.Tick(frame);
            return shipped;
        }

        [Test]
        public void HoldingShipsOnceThenSixTimesASecond()
        {
            // (a hair past the second, so float sums of frame times can't land just short of it)
            Assert.AreEqual(13, HoldFor(new HoldToShip(), 2.02f, 1f / 60f), "60 fps");
            Assert.AreEqual(13, HoldFor(new HoldToShip(), 2.02f, 1f / 15f), "15 fps behind another window");
            Assert.AreEqual(7, HoldFor(new HoldToShip(), 1.01f, 1f / 144f), "144 fps");
            Assert.AreEqual(1, HoldFor(new HoldToShip(), 0.1f, 1f / 60f), "a quick tap is one line");
        }

        [Test]
        public void LettingGoStopsAndAHitchDoesNotBurst()
        {
            var h = new HoldToShip();
            h.Press();
            Assert.IsTrue(h.Holding);
            h.Release();
            Assert.IsFalse(h.Holding);
            Assert.AreEqual(0, h.Tick(5f), "nothing after letting go");

            h.Press();
            Assert.AreEqual(HoldToShip.MaxPerTick, h.Tick(2f), "a two-second frame ships at most a few lines");
            Assert.AreEqual(0, h.Tick(0.1f), "and doesn't catch up afterwards");
            Assert.AreEqual(1, h.Tick(0.1f));
        }

        [Test]
        public void TheSettingIsOffByDefaultAndSurvivesJson()
        {
            Assert.IsFalse(new GameSettings().holdToShip);
            var s = new GameSettings { holdToShip = true };
            Assert.IsTrue(JsonUtility.FromJson<GameSettings>(JsonUtility.ToJson(s)).holdToShip);
            Assert.IsTrue(s.Clone().holdToShip);
        }
    }
}
