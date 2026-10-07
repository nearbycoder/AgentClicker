using System;

namespace AgentClicker.Core
{
    /// <summary>
    /// What happened between the player's last input and their return: idle work days the game ran by itself, and time
    /// the game didn't run at all (closed, a hidden browser tab, a sleeping laptop), credited at the offline rate.
    /// </summary>
    public sealed class AwaySummary
    {
        public double Seconds, Credits;
        public int Days, QuotasMet, QuotasMissed, CallsMissed, DropsMissed, FromDay, ToDay;

        /// <summary>Time the game wasn't running, what it earned at the offline rate, and the time past the cap that didn't count.</summary>
        public double OfflineSeconds, OfflineCredits, OfflineUncounted, OfflineRate, OfflineCapSeconds;
        /// <summary>The game was running but stopped (a hidden tab or sleep), rather than closed.</summary>
        public bool OfflinePaused, OfflineClosed;

        /// <summary>Gaps shorter than this aren't worth a card (a quick restart, a glance at another tab).</summary>
        public const double MinOfflineSeconds = 60;

        public bool HasDays => Days > 0;
        public bool HasOffline => OfflineSeconds >= MinOfflineSeconds && OfflineCredits > 0;

        /// <summary>Adds a stretch the game didn't run: <paramref name="credits"/> is what <see cref="GameModel.ApplyOffline"/> paid for it.</summary>
        public void AddOffline(double seconds, double credits, double rate, double capSeconds, bool paused)
        {
            if (!(seconds > 0)) return;
            OfflineSeconds += seconds;
            OfflineCredits += Math.Max(0, credits);
            OfflineUncounted += Math.Max(0, seconds - capSeconds);
            OfflineRate = rate;
            OfflineCapSeconds = capSeconds;
            if (paused) OfflinePaused = true; else OfflineClosed = true;
        }

        /// <summary>Adds the other summary's offline time to this one.</summary>
        public void MergeOffline(AwaySummary o)
        {
            if (o == null) return;
            OfflineSeconds += o.OfflineSeconds;
            OfflineCredits += o.OfflineCredits;
            OfflineUncounted += o.OfflineUncounted;
            OfflineRate = o.OfflineRate;
            OfflineCapSeconds = o.OfflineCapSeconds;
            OfflinePaused |= o.OfflinePaused;
            OfflineClosed |= o.OfflineClosed;
        }

        public string OfflineLabel => OfflineClosed && OfflinePaused ? "Game not running" : OfflinePaused ? "Game paused" : "Game closed";

        /// <summary>"10% for up to 1 hour" (of the normal production rate).</summary>
        public string OfflineRateText => $"{NumberFormat.Percent(OfflineRate)} for up to {Hours(OfflineCapSeconds)}";

        /// <summary>Said only when part of the time didn't count: "Only the first 1 hour counted; the other 2h 00m didn't."</summary>
        public string OfflineCapNote => OfflineUncounted >= 1
            ? $"Only the first {Hours(OfflineCapSeconds)} counted; the other {NumberFormat.Duration(OfflineUncounted)} didn't."
            : null;

        /// <summary>The caps are whole hours (1, 4, 24): "1 hour", "4 hours".</summary>
        static string Hours(double seconds) =>
            seconds >= 3600 && seconds % 3600 == 0 ? $"{seconds / 3600:0} hour{(seconds == 3600 ? "" : "s")}" : NumberFormat.Duration(seconds);
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
