using System.Collections.Generic;
using System.Linq;
using AgentClicker.Core;
using NUnit.Framework;
using UnityEngine;

namespace AgentClicker.Tests
{
    /// <summary>The "next goal" card: which goal is picked at each stage, and that it holds up along a whole bot run.</summary>
    public class NextGoalTests
    {
        static GameModel Model(double credits, params int[] counts)
        {
            var s = new GameState { credits = credits };
            for (int i = 0; i < counts.Length; i++) s.agentCounts[i] = counts[i];
            var m = new GameModel(s, seed: 3);
            m.ClockIn();
            return m;
        }

        [Test]
        public void FreshGameAimsAtTheFirstAgent()
        {
            var g = NextGoal.Pick(Model(6));
            Assert.AreEqual(GoalKind.Agent, g.Kind);
            Assert.AreEqual(0, g.AgentIndex);
            Assert.AreEqual(15, g.Need, 1e-9);
            Assert.AreEqual(0.4, g.Progress, 1e-9);
            Assert.AreEqual(double.PositiveInfinity, g.SecondsAt(0)); // nothing earning yet: no estimate
        }

        [Test]
        public void AimsAtTheCheapestAgentTypeNotOwnedYet()
        {
            var m = Model(1e9, 40, 30, 22, 12, 6, 3, 2, 1);
            var g = NextGoal.Pick(m);
            Assert.AreEqual(GoalKind.Agent, g.Kind);
            Assert.AreEqual(8, g.AgentIndex, "Product Manager Agent comes after the Architect");
            Assert.AreEqual(m.AgentCost(8, 1), g.Need, 1e-6);
            StringAssert.Contains(GameDatabase.Agents[8].Name, g.Title);
            Assert.That(g.Progress, Is.InRange(0.0, 1.0));
            Assert.AreEqual((g.Need - g.Have) / 100.0, g.SecondsAt(100), 1e-6);
        }

        [Test]
        public void ThenTheFactoryRequirementsInOrder()
        {
            // every type, one Orchestrator: the remaining Orchestrators
            var m = Model(1e10, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1);
            var g = NextGoal.Pick(m);
            Assert.AreEqual(GoalKind.Orchestrators, g.Kind);
            StringAssert.Contains("1/5", g.Title);
            Assert.AreEqual(m.AgentCost(GameDatabase.OrchestratorIndex, 1), g.Need, 1e-3);

            // five Orchestrators: the recliner
            m.State.agentCounts[GameDatabase.OrchestratorIndex] = 5;
            m.MarkDirty();
            g = NextGoal.Pick(m);
            Assert.AreEqual(GoalKind.Office, g.Kind);
            Assert.AreEqual(GameDatabase.FactoryRequiredOffice, g.OfficeId);

            // recliner too: the Factory's price
            m.State.office.Add("recliner");
            m.Load(m.State);
            m.ClockIn();
            g = NextGoal.Pick(m);
            Assert.AreEqual(GoalKind.Factory, g.Kind);
            Assert.AreEqual(m.FactoryCost, g.Need, 1e-3);
            Assert.IsFalse(g.Reached);
        }

        [Test]
        public void BlueprintsNeedOnlyOneOrchestrator()
        {
            var m = Model(1e10, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1);
            m.State.perks.Add("blueprints");
            m.Load(m.State);
            m.ClockIn();
            Assert.AreNotEqual(GoalKind.Orchestrators, NextGoal.Pick(m).Kind);
        }

        [Test]
        public void AfterTheFactoryFrontierAgentsThenOptions()
        {
            var m = Model(GameDatabase.FactoryCost, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5);
            m.State.office.Add("recliner");
            m.State.allTimeEarned = m.State.lifetimeEarned = 2e12;
            m.Load(m.State);
            m.ClockIn();
            Assert.IsTrue(m.BuildFactory());

            var g = NextGoal.Pick(m);
            Assert.AreEqual(GoalKind.Agent, g.Kind);
            Assert.AreEqual(GameDatabase.CoreAgentCount, g.AgentIndex, "the first frontier agent");

            for (int i = 0; i < GameDatabase.Agents.Length; i++) m.State.agentCounts[i] = 1;
            m.MarkDirty();
            g = NextGoal.Pick(m);
            Assert.AreEqual(GoalKind.Option, g.Kind);
            StringAssert.Contains("first Stock Options", g.Title);
            Assert.IsTrue(g.Reached, "the first Factory vests options: the first reorg is worth it right away");

            // with options from earlier reorgs, the goal is a reorg that doubles them
            m.State.optionsEarned = 10;
            g = NextGoal.Pick(m);
            Assert.AreEqual(GameModel.EarningsForOptions(20), g.Need, 1e-3);
            Assert.AreEqual(GameModel.EarningsForOptions(10), g.From, 1e-3);
            Assert.AreEqual(m.PendingOptions >= 10, g.Reached);
            Assert.That(g.Progress, Is.InRange(0.0, 1.0));
        }

        [Test]
        public void HugeNumbersStayInRange()
        {
            var m = Model(1e300);
            m.State.factoryBuilt = true;
            m.State.allTimeEarned = m.State.lifetimeEarned = double.MaxValue;
            for (int i = 0; i < GameDatabase.Agents.Length; i++) m.State.agentCounts[i] = 600;
            m.Load(m.State);
            var g = NextGoal.Pick(m);
            Assert.That(g.Progress, Is.InRange(0.0, 1.0));
            Assert.IsFalse(double.IsNaN(g.SecondsAt(m.Cps)));
        }

        /// <summary>Along a whole greedy run to the first Factory there is always a goal, and every goal shown gets done.</summary>
        [Test]
        public void EveryGoalAlongTheBotRunGetsDone()
        {
            var seen = new List<(Goal goal, double at)>();
            string lastKey = null;
            var r = BalanceSimulator.Run(observe: (m, t) =>
            {
                var g = NextGoal.Pick(m);
                Assert.IsNotNull(g);
                Assert.That(g.Progress, Is.InRange(0.0, 1.0));
                if (g.Key != lastKey) { seen.Add((g, t)); lastKey = g.Key; }
            });
            Assert.IsTrue(r.Finished, r.ToString());

            var kinds = seen.Select(x => (int)x.goal.Kind).ToList();
            for (int i = 1; i < kinds.Count; i++)
                Assert.GreaterOrEqual(kinds[i], kinds[i - 1], "goals only move forward: " + string.Join(", ", seen.Select(x => x.goal.Title)));
            // the bot sometimes hires two new types in the same second, so a goal can be skipped, never repeated
            var agents = seen.Where(x => x.goal.Kind == GoalKind.Agent).Select(x => x.goal.AgentIndex).ToList();
            for (int i = 1; i < agents.Count; i++) Assert.Greater(agents[i], agents[i - 1], "story agent goals in order");
            Assert.AreEqual(GameDatabase.OrchestratorIndex, agents.Last());
            // the last goal is the Factory, which needs everything before it: every goal shown was met
            Assert.AreEqual(GoalKind.Factory, seen.Last().goal.Kind);

            // how long each goal stays on screen, for the pacing notes
            var lines = new List<string>();
            for (int i = 0; i < seen.Count; i++)
            {
                double end = i + 1 < seen.Count ? seen[i + 1].at : r.Seconds;
                lines.Add($"{NumberFormat.Duration(seen[i].at),8}  {NumberFormat.Duration(end - seen[i].at),7}  {seen[i].goal.Title}");
            }
            Debug.Log("[Goals] next goal along the bot run (start, time on screen, goal)\n" + string.Join("\n", lines));
        }
    }
}
