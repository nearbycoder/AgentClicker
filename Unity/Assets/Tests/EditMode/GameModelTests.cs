using System.Linq;
using AgentClicker.Core;
using NUnit.Framework;
using UnityEngine;

namespace AgentClicker.Tests
{
    public class GameModelTests
    {
        static GameModel Working(double credits = 0)
        {
            var m = new GameModel(new GameState { credits = credits }, seed: 42);
            m.ClockIn();
            return m;
        }

        [Test]
        public void CostGrowsBy15PercentPerAgent()
        {
            Assert.AreEqual(15, GameModel.CostFor(15, 0, 1), 1e-9);
            Assert.AreEqual(15 * 1.15, GameModel.CostFor(15, 1, 1), 1e-9);
            Assert.AreEqual(15 + 15 * 1.15, GameModel.CostFor(15, 0, 2), 1e-9);
        }

        [Test]
        public void BuyingAnAgentSpendsCreditsAndAddsProduction()
        {
            var m = Working(100);
            Assert.IsTrue(m.BuyAgent(0));
            Assert.AreEqual(85, m.State.credits, 1e-9);
            Assert.AreEqual(1, m.AgentCount(0));
            Assert.AreEqual(0.1, m.Cps, 1e-9);
        }

        [Test]
        public void CannotBuyWithoutCredits()
        {
            var m = Working(10);
            Assert.IsFalse(m.BuyAgent(0));
            Assert.AreEqual(10, m.State.credits);
            Assert.IsFalse(m.BuyOffice("mug"));
        }

        [Test]
        public void MaxAffordableMatchesBulkCost()
        {
            var m = Working(1000);
            int n = m.MaxAffordable(0);
            Assert.LessOrEqual(m.AgentCost(0, n), 1000);
            Assert.Greater(m.AgentCost(0, n + 1), 1000);
        }

        [Test]
        public void TierUpgradeDoublesAgentOutput()
        {
            var m = Working(1e6);
            m.BuyAgent(1);
            double before = m.Cps;
            Assert.IsTrue(m.BuyUpgrade("chat_t1"));
            Assert.AreEqual(before * 2, m.Cps, 1e-9);
        }

        [Test]
        public void TierUpgradeRequiresOwningAgents()
        {
            var m = Working(1e6);
            Assert.IsFalse(m.BuyUpgrade("chat_t1"));
        }

        [Test]
        public void ClickPowerStacksMugKeyboardAndVim()
        {
            var m = Working(1e6);
            Assert.AreEqual(1, m.ClickPower, 1e-9);
            m.BuyOffice("mug");
            Assert.AreEqual(2, m.ClickPower, 1e-9);
            m.BuyOffice("mech_keyboard");
            Assert.AreEqual(4, m.ClickPower, 1e-9);
            m.State.clicks = 100;
            m.BuyUpgrade("vim");
            Assert.AreEqual(8, m.ClickPower, 1e-9);
        }

        [Test]
        public void ClickEarnsCreditsAndCountsAsHandmade()
        {
            var m = Working();
            var r = m.Click();
            Assert.AreEqual(1, r.Amount, 1e-9);
            Assert.AreEqual(1, m.State.credits, 1e-9);
            Assert.AreEqual(1, m.State.handmadeTotal, 1e-9);
            Assert.AreEqual(1, m.State.clicks);
        }

        [Test]
        public void OfficeItemsWithPrerequisitesAreGated()
        {
            var m = Working(1e12);
            Assert.IsFalse(m.BuyOffice("monitor_wall"));
            Assert.IsTrue(m.BuyOffice("exec_desk"));
            Assert.IsTrue(m.BuyOffice("monitor_wall"));
        }

        [Test]
        public void OfficeCpsPercentAppliesGlobally()
        {
            var m = Working(1e6);
            m.BuyAgent(1);
            m.BuyOffice("monitor2");
            Assert.AreEqual(1.1, m.Cps, 1e-9);
        }

        [Test]
        public void ProductionOnlyRunsWhileWorking()
        {
            var m = new GameModel(new GameState { credits = 1000 }, seed: 1);
            m.BuyAgent(1);
            m.Tick(10);
            Assert.AreEqual(1000 - 100, m.State.credits, 1e-6, "no production on the login screen");
            m.ClockIn();
            m.Tick(10);
            Assert.AreEqual(1000 - 100 + 10, m.State.credits, 1e-6);
        }

        [Test]
        public void WorkdayEndsAtFivePm()
        {
            var m = Working();
            bool ended = false;
            m.DayEndReached += () => ended = true;
            m.Tick(GameDatabase.DefaultDayLengthSeconds * 0.5f);
            Assert.AreEqual(13f, m.ClockHours, 0.01f);
            Assert.IsFalse(ended);
            m.Tick(GameDatabase.DefaultDayLengthSeconds * 0.5f + 0.1f);
            Assert.IsTrue(ended);
            Assert.IsTrue(m.PastFiveOClock);
        }

        [Test]
        public void OvertimeForcesClockOut()
        {
            var m = Working();
            DayReview review = null;
            m.ClockedOut += r => review = r;
            for (int i = 0; i < 1000 && m.IsWorking; i++) m.Tick(1f);
            Assert.IsNotNull(review);
            Assert.AreEqual(GamePhase.Review, m.Phase);
        }

        [Test]
        public void MeetingQuotaAwardsStarAndBonus()
        {
            var m = Working();
            for (int i = 0; i < 200; i++) m.Click();
            double earned = m.State.earnedToday;
            var review = m.ClockOut();
            Assert.IsTrue(review.Met);
            Assert.AreEqual(1, m.State.stars);
            Assert.AreEqual(System.Math.Floor(earned * 0.1), review.Bonus, 1e-9);
            Assert.AreEqual(earned + review.Bonus, m.State.credits, 1e-6);
        }

        [Test]
        public void MissingQuotaGivesNoStar()
        {
            var m = Working();
            m.Click();
            var review = m.ClockOut();
            Assert.IsFalse(review.Met);
            Assert.AreEqual(0, m.State.stars);
        }

        [Test]
        public void NightShiftPaysAndNextDayResetsClock()
        {
            var m = Working(1e6);
            m.BuyAgent(1);
            m.Tick(30);
            m.ClockOut();
            double night = m.GoHome();
            Assert.AreEqual(Mathf.Floor((float)(m.RawCps * 60)), night, 1e-6);
            m.StartNextDay();
            Assert.AreEqual(2, m.State.day);
            Assert.AreEqual(0, m.State.dayMinutes);
            Assert.AreEqual(0, m.State.earnedToday);
            Assert.AreEqual(GamePhase.Login, m.Phase);
        }

        [Test]
        public void MiniFridgeBoostsNightShift()
        {
            var a = Working(1e12);
            a.BuyAgent(2);
            a.ClockOut();
            double plain = a.GoHome();

            var b = Working(1e12);
            b.BuyAgent(2);
            b.BuyOffice("minifridge");
            b.ClockOut();
            double withFridge = b.GoHome();
            Assert.AreEqual(plain * 1.5, withFridge, 1.0);
        }

        [Test]
        public void QuotaGrowsWithDayAndYesterday()
        {
            Assert.AreEqual(150, GameModel.QuotaFor(1, 0));
            Assert.AreEqual(300, GameModel.QuotaFor(2, 10));
            Assert.AreEqual(900, GameModel.QuotaFor(2, 1000));
        }

        [Test]
        public void PromotionFiresWhenLifetimeCrossesThreshold()
        {
            var m = Working();
            int promotedTo = -1;
            m.Promoted += t => promotedTo = t;
            for (int i = 0; i < 1000; i++) m.Click();
            Assert.AreEqual(1, promotedTo);
            Assert.AreEqual("Developer", m.Title.Name);
        }

        [Test]
        public void HypeBuffMultipliesCps()
        {
            var m = Working(1e6);
            m.BuyAgent(1);
            m.SpawnDrop();
            // Find a seed outcome that is Hype by retrying until we get one.
            for (int i = 0; i < 50; i++)
            {
                if (m.ActiveDrop == null) m.SpawnDrop();
                var r = m.ClaimDrop();
                if (r.Outcome == DropOutcome.Hype) break;
            }
            Assert.IsTrue(m.Buffs.Any(b => b.Kind == BuffKind.Hype));
            Assert.AreEqual(m.RawCps * GameDatabase.HypeMult, m.Cps, 1e-6);
        }

        [Test]
        public void OutageHalvesCpsUntilFixed()
        {
            var m = Working(1e6);
            m.BuyAgent(1);
            double normal = m.Cps;
            m.StartOutage();
            Assert.AreEqual(normal * 0.5, m.Cps, 1e-9);
            for (int i = 0; i < GameDatabase.OutageClicks - 1; i++) Assert.IsFalse(m.ClickOutage());
            Assert.IsTrue(m.ClickOutage());
            Assert.AreEqual(normal, m.Cps, 1e-9);
        }

        [Test]
        public void FactoryNeedsEveryRequirement()
        {
            var m = Working(GameDatabase.FactoryCost * 10);
            Assert.IsFalse(m.CanBuildFactory);
            for (int i = 0; i < GameDatabase.Agents.Length; i++) m.State.agentCounts[i] = 1;
            m.State.agentCounts[GameDatabase.OrchestratorIndex] = GameDatabase.FactoryOrchestrators;
            m.MarkDirty();
            Assert.IsFalse(m.CanBuildFactory, "recliner missing");
            Assert.IsTrue(m.BuyOffice("recliner"));
            Assert.IsTrue(m.CanBuildFactory);
            double cps = m.RawCps;
            Assert.IsTrue(m.BuildFactory());
            Assert.IsTrue(m.State.factoryBuilt);
            Assert.AreEqual(cps * 2, m.RawCps, 1e-3);
            Assert.AreEqual(1.0, m.Automation);
        }

        [Test]
        public void SaveRoundTripPreservesState()
        {
            var m = Working(1e9);
            m.BuyAgent(3, 5);
            m.BuyOffice("mug");
            m.State.agentCounts[3] = 5;
            m.BuyUpgrade("tester_t1");
            m.Tick(12);
            string json = SaveSystem.ToJson(m.State);
            var copy = new GameModel(SaveSystem.FromJson(json), seed: 3);
            Assert.AreEqual(m.State.credits, copy.State.credits, 1e-6);
            Assert.AreEqual(5, copy.AgentCount(3));
            Assert.IsTrue(copy.HasOffice("mug"));
            Assert.IsTrue(copy.HasUpgrade("tester_t1"));
            Assert.AreEqual(m.RawCps, copy.RawCps, 1e-9);
            Assert.AreEqual(m.State.dayMinutes, copy.State.dayMinutes, 1e-4);
        }

        [Test]
        public void OldSavesWithFewerAgentsAreRepaired()
        {
            var s = new GameState { agentCounts = new int[3] };
            var m = new GameModel(s);
            Assert.AreEqual(GameDatabase.Agents.Length, m.State.agentCounts.Length);
        }

        [Test]
        public void OfflineProgressIsCappedAndReduced()
        {
            var m = Working(1e6);
            m.BuyAgent(1);
            double before = m.State.credits;
            double gain = m.ApplyOffline(10 * 3600);
            Assert.AreEqual(Mathf.Floor((float)(m.RawCps * 3600 * 0.1)), gain, 1e-6);
            Assert.AreEqual(before + gain, m.State.credits, 1e-6);
            Assert.AreEqual(0, m.ApplyOffline(30));
        }

        [Test]
        public void MacroPadAutoClicks()
        {
            var m = Working(1e9);
            m.BuyOffice("macropad");
            long clicks = m.State.clicks;
            m.Tick(5);
            Assert.AreEqual(clicks + 10, m.State.clicks);
            Assert.AreEqual(0, m.State.handmadeTotal, 1e-9, "macro clicks are not hand-made");
        }

        [Test]
        public void AutomationRisesWhenAgentsDoTheWork()
        {
            var m = Working(1e9);
            for (int i = 0; i < 60; i++) { m.Click(); m.Tick(1); }
            double manual = m.Automation;
            m.BuyAgent(4, 10);
            for (int i = 0; i < 60; i++) m.Tick(1);
            Assert.Less(manual, 0.5);
            Assert.Greater(m.Automation, 0.9);
        }

        [Test]
        public void EveryUpgradeAndOfficeItemHasUniqueId()
        {
            Assert.AreEqual(GameDatabase.Upgrades.Count, GameDatabase.Upgrades.Select(u => u.Id).Distinct().Count());
            Assert.AreEqual(GameDatabase.OfficeItems.Length, GameDatabase.OfficeItems.Select(o => o.Id).Distinct().Count());
            foreach (var o in GameDatabase.OfficeItems.Where(o => o.Requires != null))
                Assert.IsNotNull(GameDatabase.Office(o.Requires), o.Id);
        }
    }

    public class NumberFormatTests
    {
        [TestCase(0, "0")]
        [TestCase(7, "7")]
        [TestCase(0.5, "0.5")]
        [TestCase(999, "999")]
        [TestCase(1000, "1.00K")]
        [TestCase(1234, "1.23K")]
        [TestCase(12345, "12.3K")]
        [TestCase(999999, "999K")]
        [TestCase(1.5e6, "1.50M")]
        [TestCase(2.5e9, "2.50B")]
        [TestCase(1e13, "10.0T")]
        public void ShortFormat(double v, string expected) => Assert.AreEqual(expected, NumberFormat.Short(v));

        [TestCase(9f, "9:00 AM")]
        [TestCase(12.5f, "12:30 PM")]
        [TestCase(17f, "5:00 PM")]
        [TestCase(23.25f, "11:15 PM")]
        public void ClockFormat(float h, string expected) => Assert.AreEqual(expected, NumberFormat.Clock(h));
    }

    public class BalanceTests
    {
        [Test]
        public void GreedyPlayerFinishesTheFactoryInAReasonableTime()
        {
            var r = BalanceSimulator.Run();
            Debug.Log("[Balance]\n" + r);
            Assert.IsTrue(r.Finished, r.ToString());
            Assert.Greater(r.Seconds, 1.5 * 3600, "too fast:\n" + r);
            Assert.Less(r.Seconds, 5 * 3600, "too slow:\n" + r);
        }
    }
}
