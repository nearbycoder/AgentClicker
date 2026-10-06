using AgentClicker.Core;
using NUnit.Framework;

namespace AgentClicker.Tests
{
    public class AwayReportTests
    {
        static (GameModel m, AwayReport r) Setup()
        {
            var m = new GameModel(new GameState { credits = 1000 }, seed: 13);
            m.BuyAgent(0, 10);
            m.ClockIn();
            var r = new AwayReport(m);
            m.ClockedOut += r.OnReview;
            m.CallMissed += _ => r.OnCallMissed();
            m.DropExpired += r.OnDropExpired;
            return (m, r);
        }

        [Test]
        public void AnIdleHourIsReportedOnReturn()
        {
            var (m, r) = Setup();
            var pilot = new DayAutopilot();
            double before = m.State.allTimeEarned;
            int day = m.State.day;
            const float dt = 0.5f;
            for (float t = 0; t < 3600; t += dt)
            {
                m.Tick(dt);
                DayAutopilot.Apply(m, pilot.Tick(m, dt, input: false, blocked: m.ActiveCall != null));
                r.Tick(dt);
            }
            var s = r.Return(m);
            Assert.IsNotNull(s);
            Assert.AreEqual(3600, s.Seconds, 1);
            Assert.GreaterOrEqual(s.Days, 6);
            Assert.AreEqual(s.Days, s.QuotasMet + s.QuotasMissed);
            Assert.AreEqual(day, s.FromDay);
            Assert.AreEqual(m.State.day, s.ToDay);
            Assert.AreEqual(m.State.allTimeEarned - before, s.Credits, 1e-6);
            Assert.Greater(s.CallsMissed, 0, "nobody answered the phone for an hour");
            Assert.IsNull(r.Return(m), "reported once");
        }

        [Test]
        public void AnActivePlayerGetsNoReport()
        {
            var (m, r) = Setup();
            m.State.dayMinutes = GameDatabase.WorkdayMinutes + 1;
            m.Tick(0.1f);
            r.Tick(20);           // read the 5 PM prompt, then clocked out by hand
            m.ClockOut();
            r.Tick(5);
            Assert.IsNull(r.Return(m), "a day ended, but the player never left");
        }

        [Test]
        public void ALongBreakWithoutAnEndOfDayGetsNoReport()
        {
            var (m, r) = Setup();
            r.Tick(600);
            Assert.IsNull(r.Return(m), "the agents just kept working; the toast is enough");
        }
    }
}
