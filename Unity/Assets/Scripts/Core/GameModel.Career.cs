using System;
using System.Collections.Generic;

namespace AgentClicker.Core
{
    /// <summary>
    /// The endless game: reorgs (prestige), Stock Options, Board Room perks and trophies. Everything here
    /// survives a reorg; the run-scoped economy lives in GameModel.cs.
    /// </summary>
    public sealed partial class GameModel
    {
        readonly HashSet<string> _perks = new HashSet<string>();
        readonly HashSet<string> _achieved = new HashSet<string>();
        readonly List<AchievementDef> _locked = new List<AchievementDef>();
        float _achievementTimer;

        public event Action<List<AchievementDef>> AchievementsUnlocked;
        public event Action<PerkDef> PerkBought;
        /// <summary>Raised after a reorg with the number of options granted.</summary>
        public event Action<double> Reorged;

        void LoadCareer()
        {
            _perks.Clear();
            foreach (var p in State.perks) _perks.Add(p);
            _achieved.Clear();
            foreach (var a in State.achievements) _achieved.Add(a);
            _locked.Clear();
            foreach (var a in AchievementDatabase.All) if (!_achieved.Contains(a.Id)) _locked.Add(a);
            _achievementTimer = 0.5f;
        }

        // ---------------------------------------------------------------- stock options
        public static double OptionsFor(double allTimeEarned) =>
            allTimeEarned <= 0 ? 0 : Math.Floor(Math.Cbrt(allTimeEarned / GameDatabase.OptionsDivisor) + 1e-9);

        /// <summary>All-time credits needed before the next option vests.</summary>
        public static double EarningsForOptions(double options) => Math.Pow(options, 3) * GameDatabase.OptionsDivisor;

        /// <summary>Options a reorg would grant right now.</summary>
        public double PendingOptions => Math.Max(0, OptionsFor(State.allTimeEarned) - State.optionsEarned);

        /// <summary>Production bonus per option earned: 1%, or more with the vesting perks.</summary>
        public double OptionValue =>
            HasBoardPerk("founder_shares") ? 0.02 : HasBoardPerk("accelerated_vesting") ? 0.015 : GameDatabase.OptionValue;

        /// <summary>You can reorg once this division's Factory is online and at least one new option has vested.</summary>
        public bool CanReorg => State.factoryBuilt && PendingOptions >= 1;
        /// <summary>The reorg system has been introduced (first Factory built).</summary>
        public bool CareerUnlocked => State.factoriesBuilt > 0 || State.reorgs > 0;

        public string DivisionName => GameDatabase.DivisionName(State.reorgs);
        public string NextDivisionName => GameDatabase.DivisionName(State.reorgs + 1);

        /// <summary>
        /// Rolls the Factory out to the next division: the run resets (credits, agents, upgrades, office, day, title),
        /// the career carries on (options, perks, trophies, story and relationships). Returns the options granted.
        /// </summary>
        public double Reorg()
        {
            if (!CanReorg) return 0;
            double gained = PendingOptions;
            var s = State;
            s.options += gained;
            s.optionsEarned += gained;
            s.reorgs++;

            s.credits = 0;
            s.lifetimeEarned = 0;
            s.handmadeTotal = 0;
            s.clicks = 0;
            s.day = 1;
            s.dayMinutes = 0;
            s.earnedToday = s.yesterdayEarned = 0;
            s.quotaToday = QuotaFor(1, 0);
            s.stars = 0;
            s.titleIndex = 0;
            s.Phase = GamePhase.Login;
            Array.Clear(s.agentCounts, 0, s.agentCounts.Length);
            s.upgrades.Clear();
            if (!HasBoardPerk("pack_your_desk")) s.office.Clear();
            s.factoryBuilt = false;
            s.factoryDay = 0;
            // each division's Factory is sized to the career so far, so it stays a goal instead of an afterthought
            s.divisionFactoryCost = NumberFormat.RoundSignificant(
                Math.Max(GameDatabase.FactoryCost, s.allTimeEarned * GameDatabase.FactoryCareerShare), 2);
            s.agentDiscount = 0;
            s.asksDay = 0;
            s.asks.Clear();
            s.handmadeToday = 0;
            s.hiresToday = s.upgradesToday = s.dropsToday = s.callsToday = s.officeToday = 0;
            if (HasBoardPerk("starter_kit")) GrantStarterKit();
            Load(s);
            Reorged?.Invoke(gained);
            return gained;
        }

        // ---------------------------------------------------------------- board room perks
        public bool HasBoardPerk(string id) => _perks.Contains(id);

        public double PerkCost(PerkDef p) => p.Repeatable ? p.Cost * Math.Pow(2, State.boardSeats) : p.Cost;

        public bool IsPerkAvailable(PerkDef p) =>
            (p.Repeatable || !HasBoardPerk(p.Id)) && (p.Requires == null || HasBoardPerk(p.Requires));

        public bool BuyPerk(string id)
        {
            var p = GameDatabase.Perk(id);
            if (p == null || !IsPerkAvailable(p)) return false;
            double cost = PerkCost(p);
            if (State.options < cost) return false;
            State.options -= cost;
            if (p.Repeatable) State.boardSeats++;
            else
            {
                _perks.Add(id);
                State.perks.Add(id);
            }
            // bought on the first morning of a fresh division: the kit arrives right away
            if (id == "starter_kit" && State.reorgs > 0 && State.day == 1 && State.lifetimeEarned < 1e4) GrantStarterKit();
            _dirty = true;
            PerkBought?.Invoke(p);
            return true;
        }

        void GrantStarterKit()
        {
            State.agentCounts[0] = Math.Max(State.agentCounts[0], 10);
            State.agentCounts[1] = Math.Max(State.agentCounts[1], 5);
            _dirty = true;
        }

        // ---------------------------------------------------------------- chief of staff
        void UpdateChiefOfStaff()
        {
            if (!HasBoardPerk("chief_of_staff")) return;
            if (ActiveDrop != null && ActiveDrop.Remaining < DropLifetime - 2.5f) ClaimDrop();
            if (ActiveOutage != null && ActiveOutage.Remaining < GameDatabase.OutageDuration - 4f) EndOutage(true);
        }

        // ---------------------------------------------------------------- trophies
        public int AchievementCount => _achieved.Count;
        public static int AchievementTotal => AchievementDatabase.All.Count;
        public bool HasAchievement(string id) => _achieved.Contains(id);

        /// <summary>Clout: 4% per trophy (5% with Personal Brand). Influence upgrades turn it into production.</summary>
        public double Clout => _achieved.Count * GameDatabase.CloutPerAchievement * (HasBoardPerk("personal_brand") ? 1.25 : 1.0);

        void UpdateAchievements(float dt)
        {
            _achievementTimer -= dt;
            if (_achievementTimer > 0) return;
            _achievementTimer = 1f;
            CheckAchievements();
        }

        /// <summary>Unlocks every trophy whose condition is met. Returns how many were new.</summary>
        public int CheckAchievements()
        {
            List<AchievementDef> unlocked = null;
            for (int i = _locked.Count - 1; i >= 0; i--)
            {
                var a = _locked[i];
                if (!a.Check(this)) continue;
                _locked.RemoveAt(i);
                _achieved.Add(a.Id);
                State.achievements.Add(a.Id);
                (unlocked ??= new List<AchievementDef>()).Add(a);
            }
            if (unlocked == null) return 0;
            unlocked.Reverse(); // database order
            _dirty = true;      // clout changed
            AchievementsUnlocked?.Invoke(unlocked);
            return unlocked.Count;
        }
    }
}
