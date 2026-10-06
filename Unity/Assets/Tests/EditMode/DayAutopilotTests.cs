using System.Collections.Generic;
using AgentClicker.Core;
using NUnit.Framework;

namespace AgentClicker.Tests
{
    public class DayAutopilotTests
    {
        static GameModel AtFive()
        {
            var m = new GameModel(new GameState { credits = 1000 }, seed: 5, dayLengthSeconds: 60) { RandomEventsEnabled = false };
            m.BuyAgent(0, 10);
            m.ClockIn();
            m.State.dayMinutes = GameDatabase.WorkdayMinutes + 1;
            return m;
        }

        static AutopilotAction Idle(DayAutopilot p, GameModel m, float seconds, bool blocked = false, bool hold = false)
        {
            var a = AutopilotAction.None;
            for (float t = 0; t < seconds && a == AutopilotAction.None; t += 0.5f) a = p.Tick(m, 0.5f, false, blocked, hold);
            return a;
        }

        [Test]
        public void NothingHappensBeforeFive()
        {
            var m = AtFive();
            m.State.dayMinutes = 60;
            Assert.AreEqual(AutopilotAction.None, Idle(new DayAutopilot(), m, 600));
        }

        [Test]
        public void EachPhaseAdvancesAfterItsQuietSpell()
        {
            var m = AtFive();
            var p = new DayAutopilot();
            Assert.AreEqual(AutopilotAction.None, Idle(p, m, p.ClockOutAfter - 1));
            Assert.AreEqual(AutopilotAction.ClockOut, Idle(p, m, 2));
            DayAutopilot.Apply(m, AutopilotAction.ClockOut);
            Assert.AreEqual(GamePhase.Review, m.Phase);

            Assert.AreEqual(AutopilotAction.None, Idle(p, m, p.GoHomeAfter - 1));
            Assert.AreEqual(AutopilotAction.GoHome, Idle(p, m, 2));
            DayAutopilot.Apply(m, AutopilotAction.GoHome);
            Assert.AreEqual(GamePhase.Night, m.Phase);

            Assert.AreEqual(AutopilotAction.ClockIn, Idle(p, m, p.ClockInAfter + 1));
            DayAutopilot.Apply(m, AutopilotAction.ClockIn);
            Assert.AreEqual(GamePhase.Login, m.Phase);
            Assert.AreEqual(2, m.State.day);

            Assert.AreEqual(AutopilotAction.LogIn, Idle(p, m, p.LogInAfter + 1));
            DayAutopilot.Apply(m, AutopilotAction.LogIn);
            Assert.AreEqual(GamePhase.Working, m.Phase);
        }

        [Test]
        public void InputRestartsTheQuietTimer()
        {
            var m = AtFive();
            var p = new DayAutopilot();
            for (int i = 0; i < 200; i++)
            {
                // a player who touches the mouse every 20 s is never clocked out
                Assert.AreEqual(AutopilotAction.None, p.Tick(m, 1f, input: i % 20 == 0, blocked: false));
            }
        }

        [Test]
        public void BlockersAndWorkingLateHoldTheDay()
        {
            var m = AtFive();
            var p = new DayAutopilot();
            Assert.AreEqual(AutopilotAction.None, Idle(p, m, 300, blocked: true), "a call, menu or story card is up");
            Assert.AreEqual(AutopilotAction.None, Idle(p, m, 300, hold: true), "the player chose WORK LATE");
            // the timer starts from zero once the blocker is gone
            Assert.AreEqual(AutopilotAction.None, Idle(p, m, p.ClockOutAfter - 1));
        }

        [Test]
        public void AnIdleHourProducesInEveryDay()
        {
            // five-minute days, nobody at the keyboard for an hour, autopilot applying its actions to the model
            var m = new GameModel(new GameState { credits = 1000 }, seed: 9, dayLengthSeconds: GameDatabase.DefaultDayLengthSeconds);
            m.BuyAgent(0, 10);
            m.ClockIn();
            var p = new DayAutopilot();
            var earnedByDay = new Dictionary<int, double>();
            float notWorking = 0, longestStall = 0;
            const float dt = 0.5f;
            for (float t = 0; t < 3600; t += dt)
            {
                m.Tick(dt);
                DayAutopilot.Apply(m, p.Tick(m, dt, input: false, blocked: m.ActiveCall != null)); // nobody answers the phone
                if (m.Phase == GamePhase.Working) notWorking = 0;
                else longestStall = System.Math.Max(longestStall, notWorking += dt);
                earnedByDay[m.State.day] = m.State.earnedToday;
            }
            Assert.GreaterOrEqual(earnedByDay.Count, 6, "about one day every 5.6 minutes");
            foreach (var kv in earnedByDay)
                if (kv.Key < m.State.day) Assert.Greater(kv.Value, 0, $"day {kv.Key} produced nothing");
            Assert.LessOrEqual(longestStall, p.GoHomeAfter + p.ClockInAfter + p.LogInAfter + 2, "only the short hand-off between days");
        }
    }
}
