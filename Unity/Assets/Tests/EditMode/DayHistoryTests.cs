using System.Collections.Generic;
using AgentClicker.Core;
using NUnit.Framework;

namespace AgentClicker.Tests
{
    public class DayHistoryTests
    {
        static GameModel Working(double credits = 1000)
        {
            var m = new GameModel(new GameState { credits = credits }, seed: 11, dayLengthSeconds: 60) { RandomEventsEnabled = false };
            m.BuyAgent(0, 10);
            m.ClockIn();
            return m;
        }

        /// <summary>Plays out the rest of today with these earnings and starts the next day.</summary>
        static DayReview EndDay(GameModel m, double earned)
        {
            m.State.earnedToday = earned;
            var r = m.ClockOut();
            m.GoHome();
            m.StartNextDay();
            m.ClockIn();
            return r;
        }

        [Test]
        public void EachWayADayEndsIsRecorded()
        {
            var m = Working();
            EndDay(m, 500);                       // the player clocks out
            Assert.AreEqual(1, m.State.history.Count);
            Assert.AreEqual(1, m.State.history[0].day);
            Assert.AreEqual(500, m.State.history[0].earned);
            Assert.AreEqual(150, m.State.history[0].quota);
            Assert.IsTrue(m.State.history[0].Met);

            m.State.dayMinutes = GameDatabase.WorkdayMinutes + 1;   // the autopilot clocks out
            DayAutopilot.Apply(m, AutopilotAction.ClockOut);
            Assert.AreEqual(2, m.State.history.Count);
            Assert.AreEqual(2, m.State.history[1].day);
            DayAutopilot.Apply(m, AutopilotAction.GoHome);
            DayAutopilot.Apply(m, AutopilotAction.ClockIn);
            m.ClockIn();

            // the building closes at the end of overtime
            m.State.dayMinutes = (GameDatabase.OvertimeEndHour - GameDatabase.DayStartHour) * 60f - 0.01f;
            m.Tick(1f);
            Assert.AreEqual(GamePhase.Review, m.Phase);
            Assert.AreEqual(3, m.State.history.Count);
            Assert.AreEqual(3, m.State.history[2].day);
        }

        [Test]
        public void OnlyTheLastThirtyDaysAreKept()
        {
            var m = Working();
            for (int d = 1; d <= 40; d++) EndDay(m, 1000 * d);
            Assert.AreEqual(DayHistory.Kept, m.State.history.Count);
            Assert.AreEqual(11, m.State.history[0].day);
            Assert.AreEqual(40, m.State.history[DayHistory.Kept - 1].day);
        }

        [Test]
        public void TheReviewSaysHowTheDayCompares()
        {
            var m = Working();
            Assert.AreEqual("", EndDay(m, 1000).Trend, "nothing to compare day 1 with");
            Assert.AreEqual("Best day yet · +62% on yesterday", EndDay(m, 1620).Trend);
            Assert.AreEqual("38% below yesterday", EndDay(m, 1004.4).Trend);
            Assert.AreEqual("+30% on yesterday", EndDay(m, 1305.72).Trend, "better than yesterday, not the best");
            Assert.AreEqual("Best day yet · x20 on yesterday", EndDay(m, 1305.72 * 20).Trend);
            Assert.AreEqual("the same as yesterday", EndDay(m, 1305.72 * 20).Trend);
        }

        [Test]
        public void TheChartShowsRecentDaysThenToday()
        {
            var m = Working();
            for (int d = 1; d <= 20; d++) EndDay(m, 100 * d * d);
            var bars = DayHistory.Bars(m.State);
            Assert.AreEqual(DayHistory.Shown, bars.Count);
            Assert.IsTrue(bars[bars.Count - 1].Today);
            Assert.AreEqual(21, bars[bars.Count - 1].Day);
            Assert.AreEqual(8, bars[0].Day, "13 finished days and today");
            m.State.earnedToday = 100 * 21 * 21;
            m.ClockOut();
            bars = DayHistory.Bars(m.State);
            Assert.IsFalse(bars[bars.Count - 1].Today, "after clocking out today is a recorded day");
            Assert.AreEqual(21, bars[bars.Count - 1].Day);

            DayHistory.Range(bars, out double lo, out double hi);
            Assert.Less(lo, 100 * 8 * 8 * 0.9);
            float prev = -1;
            foreach (var b in bars)
            {
                float h = DayHistory.Height(b.Earned, lo, hi);
                Assert.That(h, Is.InRange(0.05f, 1f));
                if (!b.Today) Assert.Greater(h, prev, "heights follow the values");
                prev = h;
            }
            Assert.AreEqual(0f, DayHistory.Height(0, lo, hi));
        }

        [Test]
        public void AReorgStartsAFreshChart()
        {
            var m = Working(GameDatabase.FactoryCost);
            EndDay(m, 500);
            EndDay(m, 900);
            for (int i = 0; i < GameDatabase.CoreAgentCount; i++) m.State.agentCounts[i] = 5;
            m.State.office.Add("recliner");
            m.State.allTimeEarned = m.State.lifetimeEarned = 1e15;
            m.State.credits = GameDatabase.FactoryCost;
            m.Load(m.State);
            m.ClockIn();
            Assert.IsTrue(m.BuildFactory());
            Assert.Greater(m.Reorg(), 0);
            Assert.AreEqual(0, m.State.history.Count);
        }

        [Test]
        public void TheRecordSurvivesTheSaveAndOldSavesLoad()
        {
            var m = Working();
            EndDay(m, 500);
            EndDay(m, 90);
            var back = SaveSystem.FromJson(SaveSystem.ToJson(m.State));
            Assert.AreEqual(2, back.history.Count);
            Assert.AreEqual(90, back.history[1].earned);
            Assert.IsFalse(back.history[1].Met);

            // a save from before round 11 has no history field
            string json = SaveSystem.ToJson(new GameState { day = 7, introSeen = true });
            json = System.Text.RegularExpressions.Regex.Replace(json, "\"history\":\\s*\\[[^\\]]*\\],?", "");
            Assert.IsFalse(json.Contains("history"));
            var old = SaveSystem.FromJson(json);
            Assert.IsNotNull(old.history);
            Assert.AreEqual(0, old.history.Count);
            Assert.AreEqual(0, DayHistory.Bars(old).Count, "an empty chart until a day ends (not working)");
        }
    }
}
