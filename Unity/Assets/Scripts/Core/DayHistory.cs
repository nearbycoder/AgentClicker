using System;
using System.Collections.Generic;

namespace AgentClicker.Core
{
    /// <summary>One finished work day of this division: what was shipped against the quota. Saved.</summary>
    [Serializable]
    public class DayRecord
    {
        public int day;
        public double earned, quota;

        public bool Met => earned >= quota;
    }

    /// <summary>One bar of the Stats tab's "Last 14 days" chart.</summary>
    public struct DayBar
    {
        public int Day;
        public double Earned, Quota;
        public bool Met, Today;
    }

    /// <summary>
    /// The division's recent days (kept in the save, at most <see cref="Kept"/>), how today compares with them on the
    /// performance review, and the bars of the Stats tab's chart.
    /// </summary>
    public static class DayHistory
    {
        public const int Kept = 30;
        public const int Shown = 14;

        /// <summary>Adds a finished day (replacing one with the same number) and drops the oldest beyond <see cref="Kept"/>.</summary>
        public static void Record(List<DayRecord> history, int day, double earned, double quota)
        {
            if (history.Count > 0 && history[history.Count - 1].day == day) history.RemoveAt(history.Count - 1);
            history.Add(new DayRecord { day = day, earned = earned, quota = quota });
            if (history.Count > Kept) history.RemoveRange(0, history.Count - Kept);
        }

        /// <summary>
        /// How the last recorded day compares: "Best day yet · +62% on yesterday", "+62% on yesterday", "38% below
        /// yesterday", or "" for a division's first day (or a gap in the record).
        /// </summary>
        public static string Trend(List<DayRecord> history)
        {
            int n = history.Count;
            if (n < 2) return "";
            var today = history[n - 1];
            var before = history[n - 2];
            if (before.day != today.day - 1) return "";
            bool best = true;
            for (int i = 0; i < n - 1; i++) if (history[i].earned >= today.earned) { best = false; break; }
            string change;
            if (before.earned <= 0) change = today.earned > 0 ? "up from nothing yesterday" : "the same as yesterday";
            else
            {
                double r = today.earned / before.earned - 1;
                change = Math.Abs(r) < 0.005 ? "the same as yesterday"
                       : r > 0 ? $"{(r < 9.995 ? "+" : "")}{Pct(r)} on yesterday"
                       : $"{Pct(-r)} below yesterday";
            }
            return best ? $"Best day yet · {change}" : change;
        }

        // "+62%" up to ten times as much, then "x20" ("x1.50K" past a thousand)
        static string Pct(double r) => r < 9.995 ? $"{Math.Round(r * 100):0}%"
                                     : r + 1 < 1000 ? $"x{Math.Round(r + 1, 1).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)}"
                                     : $"x{NumberFormat.Short(r + 1)}";

        /// <summary>
        /// The chart's bars, oldest first: the last recorded days, then today while the day is running (it's recorded when
        /// the player clocks out).
        /// </summary>
        public static List<DayBar> Bars(GameState s, int max = Shown)
        {
            bool live = s.Phase == GamePhase.Working;
            var bars = new List<DayBar>();
            int take = Math.Max(0, max - (live ? 1 : 0));
            var h = s.history;
            for (int i = Math.Max(0, h.Count - take); i < h.Count; i++)
                bars.Add(new DayBar { Day = h[i].day, Earned = h[i].earned, Quota = h[i].quota, Met = h[i].Met });
            if (live) bars.Add(new DayBar { Day = s.day, Earned = s.earnedToday, Quota = s.quotaToday, Met = s.earnedToday >= s.quotaToday, Today = true });
            return bars;
        }

        /// <summary>
        /// Where a value sits between the chart's floor and top on a log scale (0..1). The range covers every bar and quota,
        /// so growth of x1000 over two weeks still shows each day; zero is the floor.
        /// </summary>
        public static float Height(double v, double lo, double hi)
        {
            if (!(v > 0)) return 0f;
            if (!(hi > lo)) return 1f;
            double t = (Math.Log10(v) - Math.Log10(lo)) / (Math.Log10(hi) - Math.Log10(lo));
            return (float)Math.Max(0, Math.Min(1, t));
        }

        /// <summary>The chart's log range: from a bit under the smallest positive bar or quota to the largest.</summary>
        public static void Range(List<DayBar> bars, out double lo, out double hi)
        {
            lo = double.MaxValue;
            hi = 0;
            foreach (var b in bars)
                foreach (var v in new[] { b.Earned, b.Quota })
                    if (v > 0) { lo = Math.Min(lo, v); hi = Math.Max(hi, v); }
            if (hi <= 0) { lo = 1; hi = 10; return; }
            // the smallest bar keeps a stub: the floor sits a fifth of the range (at least x2) below it
            double span = Math.Max(Math.Log10(hi) - Math.Log10(lo), Math.Log10(2));
            lo = Math.Pow(10, Math.Log10(lo) - Math.Max(span * 0.2, Math.Log10(2)));
            if (hi <= lo) hi = lo * 10;
        }
    }
}
