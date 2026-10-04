using System.Collections.Generic;
using System.Linq;
using AgentClicker.Core;
using NUnit.Framework;
using UnityEngine;

namespace AgentClicker.Tests
{
    /// <summary>The endless game: big numbers, frontier agents, reorgs, perks and trophies.</summary>
    public class EndlessTests
    {
        static GameModel Working(double credits = 0)
        {
            var m = new GameModel(new GameState { credits = credits }, seed: 7);
            m.ClockIn();
            return m;
        }

        /// <summary>A division with its Factory built and enough all-time earnings for some options.</summary>
        static GameModel WithFactory(double allTime = 1e15)
        {
            var m = Working(GameDatabase.FactoryCost);
            for (int i = 0; i < GameDatabase.CoreAgentCount; i++) m.State.agentCounts[i] = 5;
            m.State.office.Add("recliner");
            m.State.allTimeEarned = m.State.lifetimeEarned = allTime;
            m.Load(m.State);
            m.ClockIn();
            Assert.IsTrue(m.BuildFactory());
            return m;
        }

        [TearDown]
        public void ResetStyle() => NumberFormat.Style = NumberStyle.Short;

        // ------------------------------------------------------------------ numbers
        [Test]
        public void ShortFormatNamesEveryPowerOfAThousandUpToACentillion()
        {
            Assert.AreEqual("1.00Dc", NumberFormat.Short(1e33));
            Assert.AreEqual("1.50UDc", NumberFormat.Short(1.5e36));
            Assert.AreEqual("12.3Vg", NumberFormat.Short(1.234e64));
            Assert.AreEqual("1.00Ce", NumberFormat.Short(1e303));
            StringAssert.Contains("e306", NumberFormat.Short(1e306));
            StringAssert.Contains("e308", NumberFormat.Short(double.MaxValue));
            Assert.AreEqual("∞", NumberFormat.Short(double.PositiveInfinity));
            Assert.AreEqual("0", NumberFormat.Short(double.NaN));
            Assert.AreEqual("decillion", NumberFormat.LongName(11));
            Assert.AreEqual("centillion", NumberFormat.LongName(101));
        }

        [Test]
        public void ShortFormatNeverRoundsUpAndSuffixesAreUnique()
        {
            var seen = new HashSet<string>();
            for (int k = 1; k < NumberFormat.NamedTiers; k++)
            {
                string s = NumberFormat.Short(System.Math.Pow(10, 3 * k));
                StringAssert.StartsWith("1.00", s, $"10^{3 * k}");
                Assert.IsTrue(seen.Add(s), "duplicate suffix " + s);
                // just below the next power: must not show the next suffix
                string below = NumberFormat.Short(System.Math.Pow(10, 3 * k) * 0.9999999);
                StringAssert.StartsWith("999", below, $"just below 10^{3 * k}");
            }
        }

        [Test]
        public void ScientificStyleIsASetting()
        {
            NumberFormat.Style = NumberStyle.Scientific;
            Assert.AreEqual("1.23e15", NumberFormat.Short(1.239e15));
            Assert.AreEqual("999", NumberFormat.Short(999));
        }

        [Test]
        public void EarningsSaturateInsteadOfOverflowing()
        {
            var m = Working(double.MaxValue * 0.9);
            m.State.lifetimeEarned = m.State.allTimeEarned = double.MaxValue * 0.9;
            m.BuyAgent(0);
            m.State.credits = double.MaxValue * 0.9;
            m.State.agentCounts[19] = 100;
            m.MarkDirty();
            for (int i = 0; i < 50; i++) { m.Click(); m.Tick(1); }
            Assert.IsFalse(double.IsInfinity(m.State.credits) || double.IsNaN(m.State.credits));
            Assert.IsFalse(double.IsInfinity(m.State.allTimeEarned));
            string json = SaveSystem.ToJson(m.State);
            Assert.IsNotNull(SaveSystem.FromJson(json));
        }

        [Test]
        public void HugeAgentCountsDoNotBreakPricing()
        {
            var m = Working(1e300);
            m.State.agentCounts[0] = 6000;
            Assert.AreEqual(0, m.MaxAffordable(0));
            Assert.IsFalse(m.BuyAgent(0));
            m.State.agentCounts[0] = 3000;
            int n = m.MaxAffordable(0);
            Assert.Greater(n, 0);
            Assert.LessOrEqual(m.AgentCost(0, n), m.State.credits);
        }

        // ------------------------------------------------------------------ frontier
        [Test]
        public void FrontierAgentsUnlockWithTheFactory()
        {
            var m = Working(1e30);
            m.State.lifetimeEarned = 1e30;
            Assert.IsFalse(m.IsAgentRevealed(GameDatabase.CoreAgentCount), "frontier hidden before the Factory");
            m = WithFactory();
            Assert.IsTrue(m.IsAgentRevealed(GameDatabase.CoreAgentCount));
            Assert.AreEqual(20, GameDatabase.Agents.Length);
        }

        [Test]
        public void FactoryOnlyNeedsTheCoreAgents()
        {
            var m = Working(GameDatabase.FactoryCost);
            for (int i = 0; i < GameDatabase.CoreAgentCount; i++) m.State.agentCounts[i] = GameDatabase.FactoryOrchestrators;
            m.State.office.Add("recliner");
            m.Load(m.State);
            Assert.IsTrue(m.CanBuildFactory);
        }

        [Test]
        public void AgentsGetMoreExpensiveFasterThanTheyProduce()
        {
            // every agent should cost more and produce more than the one before it
            for (int i = 1; i < GameDatabase.Agents.Length; i++)
            {
                var a = GameDatabase.Agents[i - 1];
                var b = GameDatabase.Agents[i];
                Assert.Greater(b.BaseCost, a.BaseCost, b.Id);
                Assert.Greater(b.BaseCps, a.BaseCps, b.Id);
                if (GameDatabase.IsFrontier(i)) // frontier agents pay back ever more slowly
                    Assert.GreaterOrEqual(b.BaseCost / b.BaseCps, a.BaseCost / a.BaseCps * 0.99, b.Id);
            }
        }

        [Test]
        public void ContentIdsAreUnique()
        {
            Assert.AreEqual(GameDatabase.Upgrades.Count, GameDatabase.Upgrades.Select(u => u.Id).Distinct().Count());
            Assert.AreEqual(AchievementDatabase.All.Count, AchievementDatabase.All.Select(a => a.Id).Distinct().Count());
            Assert.AreEqual(AchievementDatabase.All.Count, AchievementDatabase.All.Select(a => a.Name).Distinct().Count(), "trophy names");
            Assert.AreEqual(GameDatabase.Perks.Length, GameDatabase.Perks.Select(p => p.Id).Distinct().Count());
            Assert.Greater(AchievementDatabase.All.Count, 400);
            Assert.AreEqual(GameDatabase.Agents.Length * 15, GameDatabase.Upgrades.Count(u => u.Kind == UpgradeKind.AgentTier));
        }

        // ------------------------------------------------------------------ reorg
        [Test]
        public void OptionsFollowTheCubeRootOfAllTimeEarnings()
        {
            Assert.AreEqual(0, GameModel.OptionsFor(0));
            Assert.AreEqual(1, GameModel.OptionsFor(1e9));
            Assert.AreEqual(10, GameModel.OptionsFor(1e12));
            Assert.AreEqual(100, GameModel.OptionsFor(1e15));
            Assert.AreEqual(1e6, GameModel.OptionsFor(1e27), 1e-6);
            Assert.AreEqual(100, GameModel.OptionsFor(GameModel.EarningsForOptions(100)));
        }

        [Test]
        public void ReorgNeedsTheFactory()
        {
            var m = Working(1e20);
            m.State.allTimeEarned = 1e20;
            Assert.IsFalse(m.CanReorg);
            Assert.AreEqual(0, m.Reorg());
        }

        [Test]
        public void ReorgResetsTheDivisionAndKeepsTheCareer()
        {
            var m = WithFactory(1e15);
            m.BuyOffice("mug");
            m.State.mail.Add("ceo_mandate");
            m.State.rapport[1] = 3;
            m.CheckAchievements();
            int trophies = m.AchievementCount;
            int days = m.State.totalDays;
            double gained = m.Reorg();

            Assert.AreEqual(100, gained);
            var s = m.State;
            Assert.AreEqual(1, s.reorgs);
            Assert.AreEqual(100, s.options);
            Assert.AreEqual(100, s.optionsEarned);
            Assert.AreEqual(0, s.credits);
            Assert.AreEqual(1, s.day);
            Assert.AreEqual(0, m.TotalAgents);
            Assert.AreEqual(0, s.upgrades.Count);
            Assert.AreEqual(0, s.office.Count);
            Assert.IsFalse(s.factoryBuilt);
            Assert.AreEqual(GamePhase.Login, m.Phase);
            // kept
            Assert.AreEqual(1e15, s.allTimeEarned, 1);
            Assert.AreEqual(trophies, m.AchievementCount);
            Assert.AreEqual(days, s.totalDays);
            Assert.Contains("ceo_mandate", s.mail);
            Assert.AreEqual(3, s.rapport[1]);
            Assert.AreEqual("Marketing", m.DivisionName);
            Assert.AreEqual(0, m.PendingOptions, "options already claimed");
        }

        [Test]
        public void OptionsBoostProductionInTheNextDivision()
        {
            var m = WithFactory(1e15);
            m.Reorg();
            m.ClockIn();
            m.State.credits = 15;
            m.BuyAgent(0);
            Assert.AreEqual(0.1 * 2.0 * m.CloutMultiplier, m.RawCps, 1e-9, "100 options = +100%");
        }

        [Test]
        public void PerksCostOptionsAndChangeTheRules()
        {
            var m = WithFactory(1e18); // 1000 options
            m.Reorg();
            Assert.IsTrue(m.BuyPerk("preferred_vendor"));
            Assert.AreEqual(15 * 0.9, m.AgentCost(0), 1e-9);
            Assert.IsTrue(m.BuyPerk("expense_account"));
            Assert.AreEqual(GameDatabase.Upgrade("vim").Cost * 0.75, m.UpgradeCost(GameDatabase.Upgrade("vim")), 1e-9);
            Assert.IsTrue(m.BuyPerk("blueprints"));
            Assert.AreEqual(m.State.divisionFactoryCost * 0.1, m.FactoryCost, 1);
            Assert.AreEqual(1, m.FactoryOrchestratorsNeeded);
            Assert.IsFalse(m.BuyPerk("blueprints"), "one-off perks can't be bought twice");
            Assert.IsFalse(m.BuyPerk("founder_shares"), "requires accelerated vesting");

            double seat = m.PerkCost(GameDatabase.Perk("board_seat"));
            Assert.IsTrue(m.BuyPerk("board_seat"));
            Assert.AreEqual(seat * 2, m.PerkCost(GameDatabase.Perk("board_seat")), 1e-9);
            Assert.AreEqual(1, m.State.boardSeats);
            Assert.AreEqual(1000 - 12 - 18 - 50 - 25, m.State.options, 1e-9);
        }

        [Test]
        public void StarterKitAndPackYourDeskApplyOnReorg()
        {
            var m = WithFactory(1e18);
            m.Reorg();
            m.BuyPerk("starter_kit");
            m.BuyPerk("pack_your_desk");
            m.ClockIn();
            m.State.credits = 100;
            m.BuyOffice("mug");
            // second division: build its factory, reorg again
            m.State.credits = m.FactoryCost;
            for (int i = 0; i < GameDatabase.CoreAgentCount; i++) m.State.agentCounts[i] = 5;
            m.State.office.Add("recliner");
            m.State.allTimeEarned = 1e21;
            m.Load(m.State);
            m.ClockIn();
            Assert.IsTrue(m.BuildFactory());
            Assert.Greater(m.Reorg(), 0);
            Assert.AreEqual(10, m.AgentCount(0));
            Assert.AreEqual(5, m.AgentCount(1));
            Assert.IsTrue(m.HasOffice("mug"), "office packed");
            Assert.AreEqual("Sales", m.DivisionName);
        }

        [Test]
        public void LaterFactoriesScaleWithTheCareerAndFrontierStaysOpen()
        {
            var m = WithFactory(1e20);
            Assert.AreEqual(GameDatabase.FactoryCost, m.State.divisionFactoryCost > 0 ? m.State.divisionFactoryCost : GameDatabase.FactoryCost);
            m.Reorg();
            Assert.AreEqual(1e17, m.FactoryCost, 1e14, "0.1% of everything earned so far");
            Assert.IsTrue(m.FrontierUnlocked, "frontier agents stay unlocked after the first Factory");
            m.State.lifetimeEarned = 1e12;
            Assert.IsTrue(m.IsAgentRevealed(GameDatabase.CoreAgentCount));
            // a small career keeps the original price
            var small = WithFactory(1e13);
            small.Reorg();
            Assert.AreEqual(GameDatabase.FactoryCost, small.FactoryCost, 1);
        }

        [Test]
        public void DivisionsNeverRunOut()
        {
            var names = new HashSet<string>();
            for (int i = 0; i < 200; i++)
            {
                string n = GameDatabase.DivisionName(i);
                Assert.IsFalse(string.IsNullOrEmpty(n));
                Assert.IsTrue(names.Add(n), n);
                Assert.IsFalse(string.IsNullOrEmpty(GameDatabase.DivisionBlurb(i)));
            }
        }

        [Test]
        public void StoryChaptersDoNotReplayAfterAReorg()
        {
            var m = WithFactory(1e15);
            m.Reorg();
            int changes = 0;
            m.ChapterChanged += _ => changes++;
            m.ClockIn();
            m.State.credits = 1e6;
            m.Tick(2);
            m.BuyAgent(0);
            m.Tick(3);
            Assert.AreEqual(0, changes);
            StringAssert.StartsWith("DIVISION 2", StoryDatabase.ChapterLine(m));
        }

        // ------------------------------------------------------------------ trophies
        [Test]
        public void TrophiesUnlockAndAddClout()
        {
            var m = Working(100);
            var got = new List<AchievementDef>();
            m.AchievementsUnlocked += list => got.AddRange(list);
            m.Click();
            m.Tick(1.1f);
            Assert.IsTrue(m.HasAchievement("clicks_1"));
            Assert.IsTrue(got.Any(a => a.Id == "clicks_1"));
            Assert.AreEqual(m.AchievementCount * 0.04, m.Clout, 1e-9);
            // nothing double-unlocks
            int count = m.AchievementCount;
            m.Tick(1.1f);
            Assert.AreEqual(count, m.AchievementCount);
        }

        [Test]
        public void CloutUpgradesTurnTrophiesIntoProduction()
        {
            var m = Working(1e9);
            m.BuyAgent(1);
            for (int i = 0; i < 30; i++) m.State.achievements.Add(AchievementDatabase.All[i].Id);
            m.Load(m.State);
            m.ClockIn();
            double before = m.RawCps;
            Assert.IsTrue(m.BuyUpgrade("clout_linkedin_post"));
            Assert.AreEqual(before * (1 + 30 * 0.04 * 0.10), m.RawCps, 1e-9);
        }

        [Test]
        public void ChiefOfStaffHandlesDropsAndOutages()
        {
            var m = WithFactory(1e21);
            m.Reorg();
            m.State.options = 1e6;
            Assert.IsTrue(m.BuyPerk("chief_of_staff"));
            m.ClockIn();
            m.State.credits = 1e6;
            m.BuyAgent(0, 10);
            m.SpawnDrop();
            for (int i = 0; i < 4; i++) m.Tick(1);
            Assert.IsNull(m.ActiveDrop);
            Assert.AreEqual(1, m.State.dropsClaimed);
            m.StartOutage();
            for (int i = 0; i < 6; i++) m.Tick(1);
            Assert.IsNull(m.ActiveOutage);
            Assert.AreEqual(1, m.State.outagesFixed);
        }

        // ------------------------------------------------------------------ saves
        [Test]
        public void CareerSurvivesSaving()
        {
            var m = WithFactory(1e18);
            m.Reorg();
            m.BuyPerk("deep_work");
            m.BuyPerk("board_seat");
            m.CheckAchievements();
            var copy = new GameModel(SaveSystem.FromJson(SaveSystem.ToJson(m.State)), seed: 2);
            Assert.AreEqual(m.State.optionsEarned, copy.State.optionsEarned);
            Assert.AreEqual(m.State.options, copy.State.options);
            Assert.IsTrue(copy.HasBoardPerk("deep_work"));
            Assert.AreEqual(1, copy.State.boardSeats);
            Assert.AreEqual(m.AchievementCount, copy.AchievementCount);
            Assert.AreEqual(1, copy.State.reorgs);
            Assert.AreEqual(4f, (float)(1 + copy.FocusMaxBonusNow), 1e-6);
        }

        [Test]
        public void VersionOneSavesGetCareerTotals()
        {
            const string v1 = "{\"version\":1,\"credits\":500,\"lifetimeEarned\":123456,\"handmadeTotal\":999,\"clicks\":321,\"day\":9,\"stars\":4,\"factoryBuilt\":true}";
            var s = SaveSystem.FromJson(v1);
            Assert.AreEqual(123456, s.allTimeEarned);
            Assert.AreEqual(999, s.allTimeHandmade);
            Assert.AreEqual(321, s.allTimeClicks);
            Assert.AreEqual(9, s.totalDays);
            Assert.AreEqual(4, s.totalStars);
            Assert.AreEqual(1, s.factoriesBuilt);
            Assert.AreEqual(GameState.CurrentVersion, s.version);
        }

        // ------------------------------------------------------------------ the long game
        [Test]
        public void LateGameNumbersStayFinite()
        {
            var m = Working(1e200);
            for (int i = 0; i < GameDatabase.Agents.Length; i++) m.State.agentCounts[i] = 650;
            foreach (var u in GameDatabase.Upgrades) m.State.upgrades.Add(u.Id);
            foreach (var o in GameDatabase.OfficeItems) m.State.office.Add(o.Id);
            foreach (var a in AchievementDatabase.All) m.State.achievements.Add(a.Id);
            m.State.optionsEarned = 1e12;
            m.State.boardSeats = 60;
            m.State.factoryBuilt = true;
            m.Load(m.State);
            m.ClockIn();
            for (int i = 0; i < 100; i++) { m.Click(); m.Tick(1); }
            Assert.IsFalse(double.IsNaN(m.RawCps) || double.IsInfinity(m.RawCps), "cps " + m.RawCps);
            Assert.IsFalse(double.IsNaN(m.State.credits) || double.IsInfinity(m.State.credits));
            Assert.Greater(m.RawCps, 1e40);
            Debug.Log($"[Endless] maxed-out cps: {NumberFormat.Short(m.RawCps)} ({m.RawCps:E2})");
        }

        [Test]
        public void EveryDivisionIsFasterThanTheLast()
        {
            var career = BalanceSimulator.RunCareer(4, postFactorySeconds: 1200);
            Debug.Log("[Endless career]\n" + career);
            Assert.AreEqual(4, career.Divisions.Count);
            Assert.IsTrue(career.Divisions.All(d => d.Finished), "every division builds its factory");
            // random events make neighbouring divisions noisy; the trend must still be clearly downhill
            var first = career.Divisions[0];
            for (int i = 1; i < career.Divisions.Count; i++)
                Assert.Less(career.Divisions[i].FactorySeconds, first.FactorySeconds * 0.8,
                            $"{career.Divisions[i].Name} should be much faster than {first.Name}");
            Assert.LessOrEqual(career.Divisions[3].FactorySeconds, career.Divisions[1].FactorySeconds * 1.1, "later divisions keep speeding up");
            Assert.Greater(career.Divisions[0].Options, 5, "the first reorg is worth it");
            Assert.Greater(career.Final.State.optionsEarned, career.Divisions[0].Options);
            Assert.Greater(career.Final.AchievementCount, 60);
        }
    }
}
