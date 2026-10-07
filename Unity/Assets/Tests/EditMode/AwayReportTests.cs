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

        [Test]
        public void TimeTheGameWasClosedPastTheCapSaysWhatDidntCount()
        {
            var m = new GameModel(new GameState { credits = 1000 }, seed: 13);
            m.BuyAgent(0, 10);
            double before = m.State.credits;
            double gain = m.ApplyOffline(2 * 3600);
            Assert.AreEqual(System.Math.Floor(m.RawCps * 3600 * 0.1), gain, 1e-9, "10% for at most an hour");
            Assert.AreEqual(before + gain, m.State.credits, 1e-9);

            var a = new AwaySummary();
            a.AddOffline(2 * 3600, gain, m.OfflineEfficiency, m.OfflineCapSeconds, paused: false);
            Assert.IsTrue(a.HasOffline);
            Assert.IsFalse(a.HasDays);
            Assert.AreEqual(gain, a.OfflineCredits, 1e-9, "the card shows what was credited");
            Assert.AreEqual(3600, a.OfflineUncounted, 1e-9);
            Assert.AreEqual("Game closed", a.OfflineLabel);
            Assert.AreEqual("10% for up to 1 hour", a.OfflineRateText);
            Assert.AreEqual("Only the first 1 hour counted; the other 1h 00m didn't.", a.OfflineCapNote);
        }

        [Test]
        public void UnderTheCapTheCardDoesntMentionIt()
        {
            var a = new AwaySummary();
            a.AddOffline(600, 42, 0.1, 3600, paused: true);
            Assert.IsTrue(a.HasOffline);
            Assert.AreEqual(0, a.OfflineUncounted);
            Assert.IsNull(a.OfflineCapNote);
            Assert.AreEqual("Game paused", a.OfflineLabel);
        }

        [Test]
        public void AShortGapOrNothingEarnedIsntWorthACard()
        {
            var a = new AwaySummary();
            a.AddOffline(AwaySummary.MinOfflineSeconds - 1, 42, 0.1, 3600, paused: false);
            Assert.IsFalse(a.HasOffline, "under a minute");
            var b = new AwaySummary();
            b.AddOffline(7200, 0, 0.1, 3600, paused: false);
            Assert.IsFalse(b.HasOffline, "no agents, nothing earned");
        }

        [Test]
        public void RemoteWorkRaisesTheRateAndCapTheCardQuotes()
        {
            var s = new GameState { credits = 1000 };
            s.perks.Add("remote_work");
            var m = new GameModel(s, seed: 13);
            var a = new AwaySummary();
            a.AddOffline(3 * 3600, 1, m.OfflineEfficiency, m.OfflineCapSeconds, paused: false);
            Assert.AreEqual("25% for up to 4 hours", a.OfflineRateText);
            Assert.IsNull(a.OfflineCapNote, "3 hours fit in Remote Work's 4");
        }

        [Test]
        public void ClosedThenPausedAddsUpAndJoinsTheIdleDays()
        {
            var closed = new AwaySummary();
            closed.AddOffline(5400, 100, 0.1, 3600, paused: false);
            var paused = new AwaySummary();
            paused.AddOffline(600, 20, 0.1, 3600, paused: true);
            var days = new AwaySummary { Days = 2, Seconds = 700, Credits = 5000 };
            days.MergeOffline(closed);
            days.MergeOffline(paused);
            Assert.IsTrue(days.HasDays && days.HasOffline);
            Assert.AreEqual(6000, days.OfflineSeconds, 1e-9);
            Assert.AreEqual(120, days.OfflineCredits, 1e-9);
            Assert.AreEqual(1800, days.OfflineUncounted, 1e-9, "only the closed stretch went past the cap");
            Assert.AreEqual("Game not running", days.OfflineLabel);
        }
    }
}
