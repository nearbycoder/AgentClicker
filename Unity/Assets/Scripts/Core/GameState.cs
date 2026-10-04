using System;
using System.Collections.Generic;

namespace AgentClicker.Core
{
    public enum GamePhase
    {
        Login = 0,    // morning: the CorpOS login screen is up
        Working = 1,  // the clock is running
        Review = 2,   // clocked out, reading the performance review
        Night = 3,    // at home; agents work the night shift
    }

    /// <summary>Everything that gets saved. Plain serializable data (JsonUtility-friendly).</summary>
    [Serializable]
    public class GameState
    {
        public const int CurrentVersion = 2;
        public int version = CurrentVersion;

        public double credits;
        public double lifetimeEarned;
        public double handmadeTotal;
        public long clicks;

        public int day = 1;
        public float dayMinutes;           // minutes since 09:00
        public double earnedToday;
        public double yesterdayEarned;
        public double quotaToday = 150;
        public int stars;
        public int titleIndex;
        public int phase;                  // GamePhase

        public int[] agentCounts = new int[GameDatabase.Agents.Length];
        public List<string> upgrades = new List<string>();
        public List<string> office = new List<string>();

        public bool factoryBuilt;
        public int factoryDay;
        public bool endingSeen;

        // story & tutorial
        public List<string> mail = new List<string>();
        public List<string> mailRead = new List<string>();
        public int dropsClaimed;
        public int outagesSeen;
        public int tutorialStep;
        public bool introSeen;

        // relationships (Dana, Priya, Gary, Rex), phone calls, daily asks
        public int[] rapport = new int[4];
        public List<string> callsDone = new List<string>();
        public int callsAnswered, callsMissed;
        public float agentDiscount;
        public int asksDay;
        public List<AskState> asks = new List<AskState>();
        public int asksCompleted;

        // per-day counters (reset each morning)
        public double handmadeToday;
        public int hiresToday, upgradesToday, dropsToday, callsToday, officeToday;
        public double playSeconds;
        public long lastSaveUnix;

        // career: survives reorgs (the prestige reset)
        public int reorgs;
        public double allTimeEarned, allTimeHandmade;
        public long allTimeClicks;
        public int totalDays, totalStars, factoriesBuilt, bestFactoryDay, outagesFixed;
        public double options;             // unspent Stock Options
        public double optionsEarned;       // every option ever granted: +1% production each
        public int boardSeats;
        public double divisionFactoryCost;  // set at each reorg; 0 = the first Factory's price
        public List<string> perks = new List<string>();
        public List<string> achievements = new List<string>();
        public bool lastOneOut;

        public GamePhase Phase
        {
            get => (GamePhase)phase;
            set => phase = (int)value;
        }

        /// <summary>Repairs data from older saves (e.g. new agents added).</summary>
        public void Normalize()
        {
            if (agentCounts == null) agentCounts = new int[GameDatabase.Agents.Length];
            if (agentCounts.Length != GameDatabase.Agents.Length)
                Array.Resize(ref agentCounts, GameDatabase.Agents.Length);
            upgrades ??= new List<string>();
            office ??= new List<string>();
            mail ??= new List<string>();
            if (rapport == null || rapport.Length != 4) Array.Resize(ref rapport, 4);
            callsDone ??= new List<string>();
            asks ??= new List<AskState>();
            mailRead ??= new List<string>();
            perks ??= new List<string>();
            achievements ??= new List<string>();
            if (day < 1) day = 1;

            // v1 saves had no career totals: the current run is the whole career
            allTimeEarned = Math.Max(allTimeEarned, lifetimeEarned);
            allTimeHandmade = Math.Max(allTimeHandmade, handmadeTotal);
            allTimeClicks = Math.Max(allTimeClicks, clicks);
            if (reorgs == 0)
            {
                totalDays = Math.Max(totalDays, day);
                totalStars = Math.Max(totalStars, stars);
                if (factoryBuilt) factoriesBuilt = Math.Max(factoriesBuilt, 1);
            }
            totalDays = Math.Max(totalDays, 1);
            if (double.IsNaN(credits) || double.IsInfinity(credits)) credits = double.MaxValue;
            if (double.IsNaN(options) || options < 0) options = 0;
            version = CurrentVersion;
        }
    }
}
