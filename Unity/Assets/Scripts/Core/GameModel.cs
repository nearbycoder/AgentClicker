using System;
using System.Collections.Generic;
using System.Linq;

namespace AgentClicker.Core
{
    public struct ClickResult
    {
        public double Amount;
        public bool Crit;
        public bool Auto;
    }

    public enum BuffKind { Hype, Caffeine }

    public sealed class Buff
    {
        public BuffKind Kind;
        public string Name;
        public double Mult;
        public float Duration, Remaining;
    }

    public sealed class ModelDrop
    {
        public string LabId, LabName, ModelName;
        public float Remaining;
    }

    public enum DropOutcome { Hype, Funding, Caffeine }

    public struct DropResult
    {
        public DropOutcome Outcome;
        public double Credits;
        public string Headline;
    }

    public sealed class Outage
    {
        public string LabName;
        public float Remaining;
        public int ClicksLeft;
    }

    public sealed class DayReview
    {
        public int Day;
        public double Earned, Quota, Bonus;
        public bool Met;
        public int Stars;
        public string Trend;   // "Best day yet · +62% on yesterday", or "" with nothing to compare
        public string ManagerName, ManagerSays;
    }

    public enum PurchaseKind { Agent, Upgrade, Office, Factory }

    public struct Purchase
    {
        public PurchaseKind Kind;
        public string Id;
        public int Count;
        public double Cost;
    }

    /// <summary>
    /// The whole game simulation, independent of Unity scenes. Drive it with <see cref="Tick"/> and
    /// the player actions (Click, Buy*, ClockIn/ClockOut ...). UI and the 3D office listen to its events.
    /// </summary>
    public sealed partial class GameModel
    {
        public GameState State { get; private set; }
        /// <summary>Random drops, outages and phone calls. Scripted runs (the demo video) turn these off.</summary>
        public bool RandomEventsEnabled { get; set; } = true;
        public float DayLengthSeconds { get; set; }

        readonly Random _rng;
        readonly HashSet<string> _upgrades = new HashSet<string>();
        readonly HashSet<string> _office = new HashSet<string>();

        public readonly List<Buff> Buffs = new List<Buff>();
        public ModelDrop ActiveDrop { get; private set; }
        public Outage ActiveOutage { get; private set; }

        float _dropTimer, _outageTimer;
        double _autoClickAcc, _clickAccum, _emaAuto, _emaClick, _emaAutoBoost;
        bool _dayEndAnnounced;
        float _storyTimer, _mailCooldown;
        int _lastChapter;

        // ---------------------------------------------------------------- events
        public event Action<Purchase> Purchased;
        public event Action<int> Promoted;               // new title index
        public event Action DayEndReached;               // 17:00
        public event Action<DayReview> ClockedOut;
        public event Action<ModelDrop> DropSpawned;
        public event Action DropExpired;
        public event Action<DropResult> DropClaimed;
        public event Action<Outage> OutageStarted;
        public event Action<bool> OutageEnded;           // true if fixed by the player
        public event Action BuffsChanged;
        public event Action<ClickResult> Clicked;
        public event Action FactoryBuilt;
        public event Action<MailDef> MailReceived;
        public event Action<int> ChapterChanged;

        // ---------------------------------------------------------------- cached stats
        bool _dirty = true, _upgradesDirty = true;
        double _rawCps, _cps, _clickPower, _globalMult;
        double _officeCpsPct, _clickFlat, _clickMult, _clickCpsPct, _crit, _outageShorten, _dropFreq,
               _caffeineMult, _autoClicks, _nightBonus, _architectMult;
        readonly double[] _unitCps = new double[GameDatabase.Agents.Length];
        // upgrade aggregates, rebuilt only when the set of owned upgrades changes
        readonly int[] _tiers = new int[GameDatabase.Agents.Length];
        readonly double[] _labMult = new double[GameDatabase.Labs.Length];
        readonly List<double> _cloutFactors = new List<double>();
        double _upClickMult, _upClickPct, _researchMult;
        double _prestigeMult = 1, _cloutMult = 1;

        public GameModel(GameState state = null, int seed = 0, float dayLengthSeconds = GameDatabase.DefaultDayLengthSeconds)
        {
            _rng = seed == 0 ? new Random() : new Random(seed);
            DayLengthSeconds = dayLengthSeconds;
            Load(state ?? new GameState());
        }

        public void Load(GameState state)
        {
            State = state;
            State.Normalize();
            _upgrades.Clear();
            _office.Clear();
            foreach (var u in State.upgrades) _upgrades.Add(u);
            foreach (var o in State.office) _office.Add(o);
            LoadCareer();
            Buffs.Clear();
            ActiveDrop = null;
            ActiveOutage = null;
            ActiveCall = null;
            Focus = 0;
            _lastChapter = 0;
            _emaAuto = _emaClick = _emaAutoBoost = _clickAccum = _autoClickAcc = 0;
            _dayEndAnnounced = State.dayMinutes >= GameDatabase.WorkdayMinutes;
            ResetEventTimers();
            _dirty = _upgradesDirty = true;
        }

        void ResetEventTimers()
        {
            _callTimer = State.callsDone.Count == 0 && State.day == 1 ? CallDatabase.FirstCallDelay : Rand(CallDatabase.MinDelay, CallDatabase.MaxDelay);
            _dropTimer = State.day == 1 && State.dayMinutes < 1 ? GameDatabase.FirstDropDelay : NextDropDelay();
            _outageTimer = Rand(GameDatabase.OutageMinDelay, GameDatabase.OutageMaxDelay);
        }

        // ---------------------------------------------------------------- queries
        public bool HasUpgrade(string id) => _upgrades.Contains(id);
        public bool HasOffice(string id) => _office.Contains(id);
        public int AgentCount(int index) => State.agentCounts[index];
        public int TotalAgents
        {
            get
            {
                int n = 0;
                var c = State.agentCounts;
                for (int i = 0; i < c.Length; i++) n += c[i];
                return n;
            }
        }

        public int LabAgentCount(string labId)
        {
            int n = 0;
            for (int i = 0; i < GameDatabase.Agents.Length; i++)
                if (GameDatabase.Agents[i].LabId == labId) n += State.agentCounts[i];
            return n;
        }

        public double Cps { get { Recompute(); return _cps; } }
        /// <summary>Credits/sec without temporary buffs or outages.</summary>
        public double RawCps { get { Recompute(); return _rawCps; } }
        /// <summary>Click power including the current Focus multiplier (what your next click earns).</summary>
        public double ClickPower { get { Recompute(); return _clickPower * FocusMult; } }
        /// <summary>Click power without Focus.</summary>
        public double BaseClickPower { get { Recompute(); return _clickPower; } }
        public double GlobalMultiplier { get { Recompute(); return _globalMult; } }
        public double CritChance { get { Recompute(); return _crit; } }
        public double AutoClicksPerSecond { get { Recompute(); return _autoClicks; } }
        public double NightBonus { get { Recompute(); return _nightBonus; } }
        public double UnitCps(int agentIndex) { Recompute(); return _unitCps[agentIndex] * _globalMult; }
        public double AgentTypeCps(int agentIndex) => UnitCps(agentIndex) * State.agentCounts[agentIndex];

        public double CpsBuffMult => BuffMult(BuffKind.Hype);
        public double ClickBuffMult => BuffMult(BuffKind.Caffeine);

        double BuffMult(BuffKind kind)
        {
            double m = 1;
            for (int i = 0; i < Buffs.Count; i++) if (Buffs[i].Kind == kind) m *= Buffs[i].Mult;
            return m;
        }

        /// <summary>0..1 share of recent income produced without your hands on the keyboard.</summary>
        public double Automation
        {
            get
            {
                if (State.factoryBuilt) return 1.0;
                double total = _emaAuto + _emaClick;
                return total <= 1e-9 ? 0.0 : _emaAuto / total;
            }
        }

        public TitleDef Title => GameDatabase.Titles[State.titleIndex];
        public TitleDef NextTitle => State.titleIndex + 1 < GameDatabase.Titles.Length ? GameDatabase.Titles[State.titleIndex + 1] : null;
        public GamePhase Phase => State.Phase;
        public bool IsWorking => State.Phase == GamePhase.Working;
        public bool PastFiveOClock => State.dayMinutes >= GameDatabase.WorkdayMinutes;

        /// <summary>Hours on a 24h clock, e.g. 13.5 for 1:30 PM.</summary>
        public float ClockHours => GameDatabase.DayStartHour + State.dayMinutes / 60f;
        public float DayProgress => Math.Min(1f, State.dayMinutes / GameDatabase.WorkdayMinutes);

        // ---------------------------------------------------------------- costs
        public static double CostFor(double baseCost, int owned, int count)
        {
            double r = GameDatabase.CostGrowth;
            return baseCost * Math.Pow(r, owned) * (Math.Pow(r, count) - 1) / (r - 1);
        }

        /// <summary>Price factor on agents: a sales-call discount on the next order and the Preferred Vendor perk.</summary>
        double AgentPriceFactor => (1 - State.agentDiscount) * (HasBoardPerk("preferred_vendor") ? 0.9 : 1.0);

        /// <summary>Cost of the next `count` agents, including discounts.</summary>
        public double AgentCost(int index, int count = 1) =>
            CostFor(GameDatabase.Agents[index].BaseCost, State.agentCounts[index], Math.Max(1, count)) * AgentPriceFactor;

        public int MaxAffordable(int index)
        {
            double r = GameDatabase.CostGrowth;
            double first = GameDatabase.Agents[index].BaseCost * Math.Pow(r, State.agentCounts[index]) * AgentPriceFactor;
            if (!(State.credits >= first) || double.IsInfinity(first)) return 0;
            double n = Math.Floor(Math.Log(State.credits * (r - 1) / first + 1) / Math.Log(r));
            int max = (int)Math.Min(n, 100000);
            // floating-point: make sure the bulk price really is affordable
            while (max > 1 && AgentCost(index, max) > State.credits) max--;
            return Math.Max(0, max);
        }

        public double UpgradeCost(UpgradeDef u) => u.Cost * (HasBoardPerk("expense_account") ? 0.75 : 1.0);
        public double OfficeCost(OfficeItemDef o) => o.Cost * (HasBoardPerk("expense_account") ? 0.75 : 1.0);
        public double FactoryCost =>
            (State.divisionFactoryCost > 0 ? State.divisionFactoryCost : GameDatabase.FactoryCost) * (HasBoardPerk("blueprints") ? 0.1 : 1.0);
        public int FactoryOrchestratorsNeeded => HasBoardPerk("blueprints") ? 1 : GameDatabase.FactoryOrchestrators;

        // ---------------------------------------------------------------- availability
        public bool IsAgentRevealed(int index)
        {
            if (State.agentCounts[index] > 0) return true;
            // frontier agents exist once you've built a Software Factory (in any division)
            if (GameDatabase.IsFrontier(index) && !FrontierUnlocked) return false;
            if (index <= 1) return true;
            if (State.agentCounts[index - 1] > 0) return true;
            return State.lifetimeEarned >= GameDatabase.Agents[index].BaseCost * 0.35;
        }

        public bool FrontierUnlocked => State.factoryBuilt || State.factoriesBuilt > 0;

        public bool IsUpgradeAvailable(UpgradeDef u) => !HasUpgrade(u.Id) && u.Unlocked(this);

        public IEnumerable<UpgradeDef> AvailableUpgrades() =>
            GameDatabase.Upgrades.Where(IsUpgradeAvailable).OrderBy(u => u.Cost);

        readonly List<UpgradeDef> _availableCache = new List<UpgradeDef>();
        int _availableStamp = -1;
        float _availableAge;

        /// <summary>
        /// Allocation-free view of the available upgrades for UI refreshes. Rebuilt after purchases and at most
        /// a couple of times per second otherwise (unlock conditions depend on continuous stats).
        /// </summary>
        public List<UpgradeDef> AvailableUpgradesCached
        {
            get
            {
                int stamp = State.upgrades.Count * 1000 + TotalAgents;
                if (stamp != _availableStamp || State.playSeconds - _availableAge > 0.5f)
                {
                    _availableStamp = stamp;
                    _availableAge = (float)State.playSeconds;
                    _availableCache.Clear();
                    foreach (var u in GameDatabase.Upgrades) if (IsUpgradeAvailable(u)) _availableCache.Add(u);
                    _availableCache.Sort((a, b) => a.Cost.CompareTo(b.Cost));
                }
                return _availableCache;
            }
        }

        public bool IsOfficeAvailable(OfficeItemDef o) =>
            !HasOffice(o.Id) && (o.Requires == null || HasOffice(o.Requires));

        public bool IsOfficeRevealed(OfficeItemDef o) =>
            HasOffice(o.Id) || State.lifetimeEarned >= o.Cost * 0.1;

        // ---------------------------------------------------------------- actions
        public ClickResult Click(bool auto = false)
        {
            Recompute();
            double amount = _clickPower;
            if (!auto) amount *= FocusMult;
            bool crit = _crit > 0 && _rng.NextDouble() < _crit;
            if (crit) amount *= 10;
            State.clicks++;
            State.allTimeClicks++;
            if (!auto)
            {
                State.handmadeTotal += amount;
                State.handmadeToday += amount;
                State.allTimeHandmade += amount;
                BuildFocus();
            }
            Earn(amount);
            if (auto) _emaAutoBoost += amount; else _clickAccum += amount;
            var result = new ClickResult { Amount = amount, Crit = crit, Auto = auto };
            Clicked?.Invoke(result);
            return result;
        }

        public bool BuyAgent(int index, int count = 1)
        {
            count = Math.Max(1, count);
            double cost = AgentCost(index, count);
            if (State.credits < cost) return false;
            State.credits -= cost;
            State.agentCounts[index] += count;
            State.hiresToday += count;
            State.agentDiscount = 0;
            _dirty = true;
            Purchased?.Invoke(new Purchase { Kind = PurchaseKind.Agent, Id = GameDatabase.Agents[index].Id, Count = count, Cost = cost });
            return true;
        }

        public bool BuyUpgrade(string id)
        {
            var u = GameDatabase.Upgrade(id);
            if (u == null || !IsUpgradeAvailable(u)) return false;
            double cost = UpgradeCost(u);
            if (State.credits < cost) return false;
            State.credits -= cost;
            _upgrades.Add(id);
            State.upgrades.Add(id);
            State.upgradesToday++;
            _dirty = _upgradesDirty = true;
            Purchased?.Invoke(new Purchase { Kind = PurchaseKind.Upgrade, Id = id, Count = 1, Cost = cost });
            return true;
        }

        public bool BuyOffice(string id)
        {
            var o = GameDatabase.Office(id);
            if (o == null || !IsOfficeAvailable(o)) return false;
            double cost = OfficeCost(o);
            if (State.credits < cost) return false;
            State.credits -= cost;
            _office.Add(id);
            State.office.Add(id);
            State.officeToday++;
            _dirty = true;
            Purchased?.Invoke(new Purchase { Kind = PurchaseKind.Office, Id = id, Count = 1, Cost = cost });
            return true;
        }

        public List<(string label, bool met)> FactoryRequirements()
        {
            var list = new List<(string, bool)>();
            bool allTypes = true;
            for (int i = 0; i < GameDatabase.CoreAgentCount; i++) allTypes &= State.agentCounts[i] > 0;
            list.Add(("At least one of every agent type", allTypes));
            int orch = State.agentCounts[GameDatabase.OrchestratorIndex], need = FactoryOrchestratorsNeeded;
            list.Add((need == 1 ? $"An Orchestrator Cluster ({Math.Min(orch, 1)}/1)" : $"{need} Orchestrator Clusters ({Math.Min(orch, need)}/{need})",
                      orch >= need));
            list.Add(("A Zero-Gravity Recliner (you'll need it)", HasOffice(GameDatabase.FactoryRequiredOffice)));
            list.Add((NumberFormat.Credits(FactoryCost), State.credits >= FactoryCost));
            return list;
        }

        public bool CanBuildFactory
        {
            get
            {
                if (State.factoryBuilt || State.credits < FactoryCost || !HasOffice(GameDatabase.FactoryRequiredOffice)) return false;
                if (State.agentCounts[GameDatabase.OrchestratorIndex] < FactoryOrchestratorsNeeded) return false;
                for (int i = 0; i < GameDatabase.CoreAgentCount; i++) if (State.agentCounts[i] == 0) return false;
                return true;
            }
        }

        public bool BuildFactory()
        {
            if (!CanBuildFactory) return false;
            double cost = FactoryCost;
            State.credits -= cost;
            State.factoryBuilt = true;
            State.factoryDay = State.day;
            State.factoriesBuilt++;
            State.bestFactoryDay = State.bestFactoryDay <= 0 ? State.day : Math.Min(State.bestFactoryDay, State.day);
            _dirty = true;
            Purchased?.Invoke(new Purchase { Kind = PurchaseKind.Factory, Id = "factory", Count = 1, Cost = cost });
            FactoryBuilt?.Invoke();
            return true;
        }

        // ---------------------------------------------------------------- day cycle
        public void ClockIn()
        {
            if (State.Phase != GamePhase.Login) return;
            State.Phase = GamePhase.Working;
            _dayEndAnnounced = State.dayMinutes >= GameDatabase.WorkdayMinutes;
            _mailCooldown = 1.5f;
            if (State.asksDay != State.day) GenerateAsks();
        }

        public DayReview ClockOut()
        {
            if (State.Phase != GamePhase.Working) return null;
            var review = new DayReview
            {
                Day = State.day,
                Earned = State.earnedToday,
                Quota = State.quotaToday,
                Met = State.earnedToday >= State.quotaToday,
            };
            if (review.Met)
            {
                review.Bonus = Math.Floor(State.earnedToday * (HasPerk(Person.Rex) ? 0.15 : GameDatabase.ReviewBonus));
                State.stars++;
                State.totalStars++;
                Earn(review.Bonus, countToday: false);
                _dirty = true;
            }
            review.Stars = State.stars;
            DayHistory.Record(State.history, State.day, State.earnedToday, State.quotaToday);
            review.Trend = DayHistory.Trend(State.history);
            review.ManagerName = FlavorText.ManagerName(this);
            review.ManagerSays = FlavorText.ManagerReview(review, this, _rng);
            State.Phase = GamePhase.Review;
            ClearTransientEvents();
            ClockedOut?.Invoke(review);
            return review;
        }

        /// <summary>Leave the office. Returns what the agents earned overnight.</summary>
        public double GoHome()
        {
            if (State.Phase != GamePhase.Review) return 0;
            State.Phase = GamePhase.Night;
            double night = Math.Floor(NightShiftEstimate);
            Earn(night, countToday: false);
            return night;
        }

        /// <summary>What the agents will earn overnight.</summary>
        public double NightShiftEstimate =>
            RawCps * GameDatabase.NightShiftSeconds * (1 + NightBonus) * (HasBoardPerk("night_owl") ? 4 : 1);

        public void StartNextDay()
        {
            if (State.Phase != GamePhase.Night) return;
            State.day++;
            State.totalDays++;
            State.dayMinutes = 0;
            State.yesterdayEarned = State.earnedToday;
            State.earnedToday = 0;
            State.quotaToday = QuotaFor(State.day, State.yesterdayEarned) * QuotaRapportFactor;
            State.handmadeToday = 0;
            State.hiresToday = State.upgradesToday = State.dropsToday = State.callsToday = State.officeToday = 0;
            State.Phase = GamePhase.Login;
            _dayEndAnnounced = false;
            ResetEventTimers();
        }

        public static double QuotaFor(int day, double yesterday)
        {
            double q = Math.Max(150.0 * day, 0.9 * yesterday);
            return NumberFormat.RoundSignificant(q, 3);
        }

        /// <summary>Credit for time the game was closed. Returns the amount granted.</summary>
        public double ApplyOffline(double secondsAway)
        {
            if (secondsAway < 60) return 0;
            double secs = Math.Min(secondsAway, OfflineCapSeconds);
            double gain = Math.Floor(RawCps * secs * OfflineEfficiency);
            Earn(gain, countToday: false);
            return gain;
        }

        public double OfflineEfficiency =>
            HasBoardPerk("unlimited_pto") ? 0.5 : HasBoardPerk("remote_work") ? 0.25 : GameDatabase.OfflineEfficiency;
        public double OfflineCapSeconds =>
            HasBoardPerk("unlimited_pto") ? 24 * 3600 : HasBoardPerk("remote_work") ? 4 * 3600 : GameDatabase.OfflineCapSeconds;

        // ---------------------------------------------------------------- simulation
        public void Tick(float dt)
        {
            if (State.Phase != GamePhase.Working || dt <= 0) return;
            State.playSeconds += dt;

            State.dayMinutes += dt * GameDatabase.WorkdayMinutes / DayLengthSeconds;
            if (!_dayEndAnnounced && State.dayMinutes >= GameDatabase.WorkdayMinutes)
            {
                _dayEndAnnounced = true;
                DayEndReached?.Invoke();
            }

            UpdateBuffs(dt);

            double gain = Cps * dt;
            Earn(gain);

            _autoClickAcc += AutoClicksPerSecond * dt;
            while (_autoClickAcc >= 1)
            {
                _autoClickAcc -= 1;
                Click(auto: true);
            }

            UpdateDrops(dt);
            UpdateOutage(dt);
            UpdateStory(dt);
            UpdateFocus(dt);
            UpdateCalls(dt);
            UpdateAsks(dt);
            UpdateChiefOfStaff();
            UpdateAchievements(dt);

            // automation share: exponential moving averages of agent vs. hand-made income per second
            double k = 1 - Math.Exp(-dt / 20.0);
            _emaAuto += ((gain + _emaAutoBoost) / dt - _emaAuto) * k;
            _emaClick += (_clickAccum / dt - _emaClick) * k;
            _clickAccum = 0;
            _emaAutoBoost = 0;

            if ((State.dayMinutes / 60f + GameDatabase.DayStartHour) >= GameDatabase.OvertimeEndHour)
            {
                State.lastOneOut = true; // the building closes; security walks you out
                ClockOut();
            }
        }

        void Earn(double amount, bool countToday = true)
        {
            if (!(amount > 0)) return; // also rejects NaN
            State.credits = Saturate(State.credits + amount);
            State.lifetimeEarned = Saturate(State.lifetimeEarned + amount);
            State.allTimeEarned = Saturate(State.allTimeEarned + amount);
            if (countToday) State.earnedToday = Saturate(State.earnedToday + amount);
            while (State.titleIndex + 1 < GameDatabase.Titles.Length &&
                   State.lifetimeEarned >= GameDatabase.Titles[State.titleIndex + 1].Threshold)
            {
                State.titleIndex++;
                Promoted?.Invoke(State.titleIndex);
            }
        }

        /// <summary>Numbers stop at the largest double instead of becoming Infinity (and breaking saves).</summary>
        static double Saturate(double v) => v < double.MaxValue ? v : double.MaxValue;

        void UpdateBuffs(float dt)
        {
            bool changed = false;
            for (int i = Buffs.Count - 1; i >= 0; i--)
            {
                Buffs[i].Remaining -= dt;
                if (Buffs[i].Remaining <= 0) { Buffs.RemoveAt(i); changed = true; }
            }
            if (changed) { _dirty = true; BuffsChanged?.Invoke(); }
        }

        void AddBuff(BuffKind kind, string name, double mult, float duration)
        {
            Buffs.RemoveAll(b => b.Kind == kind);
            Buffs.Add(new Buff { Kind = kind, Name = name, Mult = mult, Duration = duration, Remaining = duration });
            _dirty = true;
            BuffsChanged?.Invoke();
        }

        float NextDropDelay()
        {
            Recompute();
            return Rand(GameDatabase.DropMinDelay, GameDatabase.DropMaxDelay) / (float)(1 + _dropFreq) / (HasBoardPerk("early_access") ? 1.5f : 1f);
        }

        void UpdateDrops(float dt)
        {
            if (ActiveDrop != null)
            {
                ActiveDrop.Remaining -= dt;
                if (ActiveDrop.Remaining <= 0)
                {
                    ActiveDrop = null;
                    DropExpired?.Invoke();
                }
                return;
            }
            if (!RandomEventsEnabled) return;
            _dropTimer -= dt;
            if (_dropTimer > 0) return;
            _dropTimer = NextDropDelay();
            SpawnDrop();
        }

        public ModelDrop SpawnDrop()
        {
            var lab = GameDatabase.Labs[_rng.Next(GameDatabase.Labs.Length)];
            ActiveDrop = new ModelDrop
            {
                LabId = lab.Id, LabName = lab.Name, ModelName = FlavorText.ModelName(lab, _rng),
                Remaining = DropLifetime,
            };
            DropSpawned?.Invoke(ActiveDrop);
            return ActiveDrop;
        }

        public float DropLifetime => GameDatabase.DropLifetime * (HasBoardPerk("early_access") ? 2f : 1f);
        public float HypeMult => HasBoardPerk("hype_machine") ? 12f : GameDatabase.HypeMult;

        public DropResult ClaimDrop()
        {
            if (ActiveDrop == null) return default;
            var drop = ActiveDrop;
            ActiveDrop = null;
            State.dropsClaimed++;
            State.dropsToday++;
            double roll = _rng.NextDouble();
            var result = new DropResult();
            if (roll < 0.5)
            {
                result.Outcome = DropOutcome.Hype;
                result.Headline = $"Benchmark Hype! {drop.ModelName} tops the leaderboard. Credits/sec x{HypeMult} for {GameDatabase.HypeDuration:0}s";
                AddBuff(BuffKind.Hype, "Benchmark Hype", HypeMult, GameDatabase.HypeDuration);
            }
            else if (roll < 0.85)
            {
                result.Outcome = DropOutcome.Funding;
                double amount = Math.Floor(Math.Min(State.credits * 0.15, RawCps * 900) + 13);
                result.Credits = amount;
                result.Headline = $"Funding Round! {drop.LabName} shares the love: +{NumberFormat.Credits(amount)}";
                Earn(amount);
            }
            else
            {
                result.Outcome = DropOutcome.Caffeine;
                Recompute();
                float dur = (float)(GameDatabase.CaffeineDuration * _caffeineMult * (HasBoardPerk("hype_machine") ? 1.5 : 1.0));
                result.Headline = $"Caffeine Rush! Click power x{GameDatabase.CaffeineMult} for {dur:0}s. Click like your job depends on it!";
                AddBuff(BuffKind.Caffeine, "Caffeine Rush", GameDatabase.CaffeineMult, dur);
            }
            DropClaimed?.Invoke(result);
            return result;
        }

        void UpdateOutage(float dt)
        {
            if (ActiveOutage != null)
            {
                ActiveOutage.Remaining -= dt;
                if (ActiveOutage.Remaining <= 0) EndOutage(false);
                return;
            }
            if (State.day < 2 || TotalAgents < 5 || !RandomEventsEnabled) return;
            _outageTimer -= dt;
            if (_outageTimer > 0) return;
            _outageTimer = Rand(GameDatabase.OutageMinDelay, GameDatabase.OutageMaxDelay) * (HasPerk(Person.Gary) ? 1.66f : 1f);
            StartOutage();
        }

        public Outage StartOutage()
        {
            Recompute();
            var lab = GameDatabase.Labs[_rng.Next(GameDatabase.Labs.Length)];
            ActiveOutage = new Outage
            {
                LabName = lab.Name,
                Remaining = GameDatabase.OutageDuration * (float)(1 - _outageShorten) * (HasPerk(Person.Priya) ? 0.7f : 1f),
                ClicksLeft = GameDatabase.OutageClicks,
            };
            State.outagesSeen++;
            _dirty = true;
            OutageStarted?.Invoke(ActiveOutage);
            return ActiveOutage;
        }

        /// <summary>Player clicks "fail over". Returns true when the outage is resolved.</summary>
        public bool ClickOutage()
        {
            if (ActiveOutage == null) return false;
            ActiveOutage.ClicksLeft--;
            if (ActiveOutage.ClicksLeft > 0) return false;
            EndOutage(true);
            return true;
        }

        void EndOutage(bool fixedByPlayer)
        {
            ActiveOutage = null;
            if (fixedByPlayer) State.outagesFixed++;
            _dirty = true;
            OutageEnded?.Invoke(fixedByPlayer);
        }

        void ClearTransientEvents()
        {
            if (ActiveCall != null) { var c = ActiveCall.Def; ActiveCall = null; CallEnded?.Invoke(c, null, null); }
            if (ActiveDrop != null) { ActiveDrop = null; DropExpired?.Invoke(); }
            if (ActiveOutage != null) EndOutage(false);
            if (Buffs.Count > 0) { Buffs.Clear(); _dirty = true; BuffsChanged?.Invoke(); }
        }

        float Rand(float a, float b) => a + (float)_rng.NextDouble() * (b - a);

        // ---------------------------------------------------------------- story mail
        public int UnreadMail => State.mail.Count(id => !State.mailRead.Contains(id));
        public int Chapter => StoryDatabase.ChapterFor(this);

        public void MarkRead(string id)
        {
            if (State.mail.Contains(id) && !State.mailRead.Contains(id)) State.mailRead.Add(id);
        }

        /// <summary>Delivers at most one new story email every few seconds while working.</summary>
        void UpdateStory(float dt)
        {
            _mailCooldown -= dt;
            _storyTimer -= dt;
            if (_storyTimer > 0 || _mailCooldown > 0) return;
            _storyTimer = 1f;
            int chapter = Chapter;
            if (chapter > _lastChapter)
            {
                // the story's chapters are told once; later divisions have their own memos
                if (_lastChapter > 0 && State.reorgs == 0) ChapterChanged?.Invoke(chapter);
                _lastChapter = chapter;
            }
            if (DeliverNextMail() != null) _mailCooldown = 8f;
        }

        /// <summary>Delivers the first pending email whose trigger is satisfied. Returns it, or null.</summary>
        public MailDef DeliverNextMail()
        {
            foreach (var mail in StoryDatabase.Mail)
            {
                if (State.mail.Contains(mail.Id) || !mail.Trigger(this)) continue;
                State.mail.Add(mail.Id);
                MailReceived?.Invoke(mail);
                return mail;
            }
            return null;
        }

        // ---------------------------------------------------------------- stats
        public void MarkDirty() => _dirty = true;

        void Recompute()
        {
            if (!_dirty) return;
            _dirty = false;
            if (_upgradesDirty) RecomputeUpgrades();

            _officeCpsPct = 0; _clickFlat = 0; _clickMult = 1; _clickCpsPct = 0; _crit = 0; _outageShorten = 0;
            _dropFreq = 0; _caffeineMult = 1; _autoClicks = 0; _nightBonus = 0; _architectMult = 1;
            var items = GameDatabase.OfficeItems;
            for (int k = 0; k < items.Length; k++)
            {
                var o = items[k];
                if (!_office.Contains(o.Id)) continue;
                _officeCpsPct += o.CpsPercent;
                _clickFlat += o.ClickFlat;
                _clickMult *= o.ClickMult;
                _clickCpsPct += o.ClickCpsPercent;
                _crit += o.CritChance;
                _outageShorten = Math.Max(_outageShorten, o.OutageShorten);
                _dropFreq += o.DropFrequency;
                _caffeineMult *= o.CaffeineDuration;
                _autoClicks += o.AutoClicksPerSec;
                _nightBonus += o.NightBonus;
                _architectMult *= o.ArchitectMult;
            }
            _clickMult *= _upClickMult;
            _clickCpsPct += _upClickPct;

            double baseCps = 0;
            var agents = GameDatabase.Agents;
            int architect = GameDatabase.ArchitectIndex;
            for (int i = 0; i < agents.Length; i++)
            {
                var a = agents[i];
                double unit = a.BaseCps * Math.Pow(2, _tiers[i]) * _labMult[a.LabIndex];
                if (i == architect) unit *= _architectMult;
                _unitCps[i] = unit;
                baseCps += unit * State.agentCounts[i];
            }

            // career multipliers: stock options, board seats, clout from trophies
            _prestigeMult = 1 + OptionValue * State.optionsEarned;
            double clout = Clout;
            _cloutMult = 1;
            for (int i = 0; i < _cloutFactors.Count; i++) _cloutMult *= 1 + clout * _cloutFactors[i];

            _globalMult = (1 + _officeCpsPct) * (1 + GameDatabase.StarCpsBonus * State.stars) * (State.factoryBuilt ? 2 : 1)
                          * _researchMult * _prestigeMult * _cloutMult * Math.Pow(1.1, State.boardSeats);
            _rawCps = Saturate(baseCps * _globalMult);
            double hype = BuffMult(BuffKind.Hype);
            double caffeine = BuffMult(BuffKind.Caffeine);
            _cps = Saturate(_rawCps * hype * (ActiveOutage != null ? 0.5 : 1.0));
            _clickPower = Saturate(((1 + _clickFlat) * _clickMult + _clickCpsPct * _rawCps) * caffeine);
        }

        void RecomputeUpgrades()
        {
            _upgradesDirty = false;
            Array.Clear(_tiers, 0, _tiers.Length);
            for (int i = 0; i < _labMult.Length; i++) _labMult[i] = 1;
            _cloutFactors.Clear();
            _upClickMult = 1; _upClickPct = 0; _researchMult = 1;
            foreach (var id in _upgrades)
            {
                var u = GameDatabase.Upgrade(id);
                if (u == null) continue;
                switch (u.Kind)
                {
                    case UpgradeKind.AgentTier: _tiers[u.AgentIndex]++; break;
                    case UpgradeKind.ClickMult: _upClickMult *= u.Value; break;
                    case UpgradeKind.ClickCpsPercent: _upClickPct += u.Value; break;
                    case UpgradeKind.LabContract: _labMult[u.LabIndex] *= u.Value; break;
                    case UpgradeKind.Research: _researchMult *= u.Value; break;
                    case UpgradeKind.Clout: _cloutFactors.Add(u.Value); break;
                }
            }
        }

        public double ResearchMultiplier { get { Recompute(); return _researchMult; } }
        public double PrestigeMultiplier { get { Recompute(); return _prestigeMult; } }
        public double CloutMultiplier { get { Recompute(); return _cloutMult; } }

        // ---------------------------------------------------------------- what-if helpers (balance sim / UI hints)
        /// <summary>
        /// Income gained per second (agents + clicks at `clicksPerSecond` + macro pad) by buying an item.
        /// Temporarily applies the purchase and reverts it; nothing is spent.
        /// </summary>
        public double GainFrom(PurchaseKind kind, string id, double clicksPerSecond)
        {
            double Value() => RawCps + (clicksPerSecond + AutoClicksPerSecond) * ClickPower;
            double before = Value(), after = before;
            switch (kind)
            {
                case PurchaseKind.Agent:
                    int idx = GameDatabase.AgentIndex(id);
                    State.agentCounts[idx]++; _dirty = true;
                    after = Value();
                    State.agentCounts[idx]--;
                    break;
                case PurchaseKind.Upgrade:
                    var def = GameDatabase.Upgrade(id);
                    // tier and lab upgrades do nothing for agents you don't own: skip the what-if
                    if (def != null && def.Kind == UpgradeKind.AgentTier && State.agentCounts[def.AgentIndex] == 0) return 0;
                    if (def != null && def.Kind == UpgradeKind.LabContract && LabAgentCount(def.LabId) == 0) return 0;
                    _upgrades.Add(id); _dirty = _upgradesDirty = true;
                    after = Value();
                    _upgrades.Remove(id);
                    _upgradesDirty = true;
                    break;
                case PurchaseKind.Office:
                    _office.Add(id); _dirty = true;
                    after = Value();
                    _office.Remove(id);
                    break;
            }
            _dirty = true;
            return after - before;
        }
    }
}
