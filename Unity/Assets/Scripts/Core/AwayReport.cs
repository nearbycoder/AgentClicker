using System;

namespace AgentClicker.Core
{
    /// <summary>What happened between the player's last input and their return.</summary>
    public sealed class AwaySummary
    {
        public double Seconds, Credits;
        public int Days, QuotasMet, QuotasMissed, CallsMissed, DropsMissed, FromDay, ToDay;
    }

    /// <summary>
    /// Keeps score while nobody is at the keyboard (the day runs itself, see <see cref="DayAutopilot"/>), so a
    /// returning player can be told what they missed. Pure C#: feed it the model's events, real time and input.
    /// </summary>
    public sealed class AwayReport
    {
        /// <summary>Only worth a report after a real absence that spanned at least one end of day.</summary>
        public double MinSeconds = 90;

        double _seconds, _earnedAtStart;
        int _days, _met, _missed, _calls, _drops, _dayAtStart;

        public AwayReport(GameModel m = null) { if (m != null) Restart(m); }

        public double AwaySeconds => _seconds;

        public void Tick(double realSeconds) => _seconds += Math.Max(0, realSeconds);
        public void OnReview(DayReview r) { _days++; if (r.Met) _met++; else _missed++; }
        public void OnCallMissed() => _calls++;
        public void OnDropExpired() => _drops++;

        /// <summary>The player is back: returns a summary if the absence is worth one, and starts counting afresh.</summary>
        public AwaySummary Return(GameModel m)
        {
            AwaySummary s = null;
            if (_seconds >= MinSeconds && _days > 0)
                s = new AwaySummary
                {
                    Seconds = _seconds, Credits = Math.Max(0, m.State.allTimeEarned - _earnedAtStart),
                    Days = _days, QuotasMet = _met, QuotasMissed = _missed, CallsMissed = _calls, DropsMissed = _drops,
                    FromDay = _dayAtStart, ToDay = m.State.day,
                };
            Restart(m);
            return s;
        }

        public void Restart(GameModel m)
        {
            _seconds = 0;
            _days = _met = _missed = _calls = _drops = 0;
            _earnedAtStart = m.State.allTimeEarned;
            _dayAtStart = m.State.day;
        }
    }
}
