using System.Linq;
using AgentClicker.Core;
using NUnit.Framework;

namespace AgentClicker.Tests
{
    public class InterruptionTests
    {
        static GameModel Working(double credits = 0)
        {
            var m = new GameModel(new GameState { credits = credits }, seed: 21);
            m.ClockIn();
            return m;
        }

        [Test]
        public void FocusBuildsWithClicksAndBoostsThem()
        {
            var m = Working();
            Assert.AreEqual(1, m.Click().Amount, 1e-9);
            for (int i = 0; i < 40; i++) m.Click();
            Assert.AreEqual(1f, m.Focus, 1e-6);
            Assert.AreEqual(3, m.Click().Amount, 1e-9);
        }

        [Test]
        public void FocusDecaysWhenYouStopClicking()
        {
            var m = Working();
            for (int i = 0; i < 40; i++) m.Click();
            m.Tick(0.5f);
            Assert.AreEqual(1f, m.Focus, 1e-6, "short pauses are fine");
            for (int i = 0; i < 10; i++) m.Tick(1f);
            Assert.AreEqual(0f, m.Focus, 1e-6);
        }

        [Test]
        public void AnsweringACallBreaksFocus()
        {
            var m = Working();
            for (int i = 0; i < 40; i++) m.Click();
            m.RingPhone(CallDatabase.ById("priya_coffee"));
            m.AnswerCall();
            Assert.AreEqual(0.5f, m.Focus, 1e-6);
            m.ChooseCallOption(0); // coffee run doesn't keep focus
            Assert.AreEqual(0f, m.Focus, 1e-6);
        }

        [Test]
        public void StoryCallChoiceChangesRapportOnce()
        {
            var m = Working();
            m.RingPhone(CallDatabase.ById("gary_password"));
            m.AnswerCall();
            m.ChooseCallOption(0);
            Assert.AreEqual(2, m.Rapport(Person.Gary));
            Assert.Contains("gary_password", m.State.callsDone);
            m.State.day = 5;
            m.State.agentCounts[1] = 10;
            Assert.AreNotEqual("gary_password", m.PickCall()?.Id, "story calls ring once");
        }

        [Test]
        public void MissingAStoryCallCostsRapport()
        {
            var m = Working();
            m.State.agentCounts[0] = 1;
            m.RingPhone(CallDatabase.ById("dana_demo"));
            for (int i = 0; i < 20; i++) m.Tick(1f);
            Assert.IsNull(m.ActiveCall);
            Assert.AreEqual(-1, m.Rapport(Person.Dana));
            Assert.AreEqual(1, m.State.callsMissed);
        }

        [Test]
        public void MeetingsAdvanceTheClockButAgentsKeepEarning()
        {
            var m = Working(1e6);
            m.BuyAgent(2, 10);
            float before = m.State.dayMinutes;
            double credits = m.State.credits;
            m.RingPhone(CallDatabase.ById("dana_standup"));
            m.AnswerCall();
            m.ChooseCallOption(0);
            Assert.AreEqual(before + 15, m.State.dayMinutes, 1e-3);
            Assert.Greater(m.State.credits, credits);
        }

        [Test]
        public void SalesDiscountAppliesToTheNextOrderOnly()
        {
            var m = Working(1e6);
            double full = m.AgentCost(1, 10);
            m.RingPhone(CallDatabase.ById("lab_sales"));
            m.AnswerCall();
            m.ChooseCallOption(0);
            Assert.AreEqual(full * 0.75, m.AgentCost(1, 10), 1e-6);
            m.BuyAgent(1, 10);
            Assert.AreEqual(0f, m.State.agentDiscount);
        }

        [Test]
        public void PerksUnlockAtRapportThree()
        {
            var m = Working();
            bool unlocked = false;
            m.PerkUnlocked += p => unlocked = p == Person.Dana;
            m.AddRapport(Person.Dana, 2);
            Assert.IsFalse(m.HasPerk(Person.Dana));
            m.AddRapport(Person.Dana, 1);
            Assert.IsTrue(m.HasPerk(Person.Dana) && unlocked);
            Assert.AreEqual(0.85, m.QuotaRapportFactor, 1e-9);
            m.AddRapport(Person.Dana, 99);
            Assert.AreEqual(GameModel.RapportMax, m.Rapport(Person.Dana));
        }

        [Test]
        public void CallsOnlyStartOnceYouHaveAgents()
        {
            var m = Working();
            for (int i = 0; i < 300; i++) m.Tick(1f);
            Assert.IsNull(m.ActiveCall);
            Assert.AreEqual(0, m.State.callsAnswered + m.State.callsMissed);
        }

        [Test]
        public void DayOneAsksAreShipAndHire()
        {
            var m = Working();
            CollectionAssert.AreEqual(new[] { "ship", "hire" }, m.State.asks.Select(a => a.id).ToArray());
        }

        [Test]
        public void CompletingAnAskPaysAReward()
        {
            var m = Working(1e6);
            double credits = m.State.credits;
            m.BuyAgent(0, 5);
            double afterBuy = m.State.credits;
            m.Tick(1f);
            var hire = m.State.asks.First(a => a.id == "hire");
            Assert.IsTrue(hire.done);
            Assert.Greater(m.State.credits, afterBuy + 49);
        }

        [Test]
        public void NewDayResetsCountersAndRollsNewAsks()
        {
            var m = Working(1e6);
            m.BuyAgent(0, 5);
            m.ClockOut();
            m.GoHome();
            m.StartNextDay();
            Assert.AreEqual(0, m.State.hiresToday);
            m.ClockIn();
            Assert.AreEqual(2, m.State.asksDay);
            Assert.IsTrue(m.State.asks.All(a => !a.done));
        }

        [Test]
        public void EpilogueReflectsRelationships()
        {
            var m = Working();
            m.AddRapport(Person.Priya, 4);
            Assert.IsTrue(StoryDatabase.Epilogue(m).Any(c => c.Title.Contains("co-founder")));
            m.AddRapport(Person.Priya, -6);
            Assert.IsTrue(StoryDatabase.Epilogue(m).Any(c => c.Title.Contains("automated her job")));
        }

        [Test]
        public void EveryCallHasChoicesAndKnownCallers()
        {
            foreach (var c in CallDatabase.Calls)
            {
                Assert.GreaterOrEqual(c.Choices.Length, 2, c.Id);
                Assert.IsFalse(string.IsNullOrEmpty(c.Name), c.Id);
                Assert.IsFalse(string.IsNullOrEmpty(c.Opening), c.Id);
            }
            Assert.AreEqual(CallDatabase.Calls.Length, CallDatabase.Calls.Select(c => c.Id).Distinct().Count());
        }
    }
}
