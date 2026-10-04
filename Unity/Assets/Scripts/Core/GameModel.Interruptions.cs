using System;
using System.Collections.Generic;
using System.Linq;

namespace AgentClicker.Core
{
    public sealed class ActiveCall
    {
        public CallDef Def;
        public float Ring;
        public bool Answered;
    }

    /// <summary>Focus, relationships, phone-call interruptions and daily asks.</summary>
    public sealed partial class GameModel
    {
        // ---------------------------------------------------------------- focus
        public const float FocusPerClick = 0.03f, FocusDecayPerSecond = 0.22f, FocusMaxBonus = 2f;

        /// <summary>0..1. Builds while you click steadily, decays when you stop; phone calls break it.</summary>
        public float Focus { get; private set; }
        public double FocusMult => 1 + FocusMaxBonusNow * Focus;
        /// <summary>x3 at full Focus, x4 with the Deep Work perk.</summary>
        public float FocusMaxBonusNow => HasBoardPerk("deep_work") ? 3f : FocusMaxBonus;
        double _lastManualClick = -10;

        void BuildFocus()
        {
            Focus = Math.Min(1f, Focus + FocusPerClick * (HasBoardPerk("deep_work") ? 2 : 1));
            _lastManualClick = State.playSeconds;
        }

        void UpdateFocus(float dt)
        {
            if (Focus <= 0 || State.playSeconds - _lastManualClick < 0.8) return;
            Focus = Math.Max(0f, Focus - FocusDecayPerSecond * dt);
        }

        public void BreakFocus(float keep = 0f) => Focus *= keep;

        // ---------------------------------------------------------------- relationships
        public const int RapportMin = -5, RapportMax = 5, PerkThreshold = 3;

        public int Rapport(Person p) => State.rapport[(int)p];
        public bool HasPerk(Person p) => State.rapport[(int)p] >= PerkThreshold;

        public void AddRapport(Person p, int delta)
        {
            if (delta == 0) return;
            int before = State.rapport[(int)p];
            int after = Math.Max(RapportMin, Math.Min(RapportMax, before + delta));
            State.rapport[(int)p] = after;
            _dirty = true;
            RapportChanged?.Invoke(p, after - before);
            if (before < PerkThreshold && after >= PerkThreshold) PerkUnlocked?.Invoke(p);
        }

        /// <summary>Dana's opinion of you nudges tomorrow's quota.</summary>
        public double QuotaRapportFactor =>
            Rapport(Person.Dana) >= PerkThreshold ? 0.85 : Rapport(Person.Dana) <= -PerkThreshold ? 1.15 : 1.0;

        public event Action<Person, int> RapportChanged;
        public event Action<Person> PerkUnlocked;

        // ---------------------------------------------------------------- phone calls
        public ActiveCall ActiveCall { get; private set; }
        float _callTimer;
        string _lastRandomCall;

        public event Action<ActiveCall> CallIncoming;
        public event Action<ActiveCall> CallAnswered;
        /// <summary>Raised when a call ends: (call, chosen option or null if declined/missed, summary text).</summary>
        public event Action<CallDef, CallChoice, string> CallEnded;
        public event Action<CallDef> CallMissed;

        void UpdateCalls(float dt)
        {
            if (ActiveCall != null)
            {
                if (ActiveCall.Answered) return;
                ActiveCall.Ring -= dt;
                if (ActiveCall.Ring <= 0) MissCall();
                return;
            }
            // nobody calls in the last half hour of the day, or before you've hired anyone
            if (TotalAgents == 0 || State.dayMinutes > GameDatabase.WorkdayMinutes - 30 || !RandomEventsEnabled) return;
            _callTimer -= dt;
            if (_callTimer > 0) return;
            _callTimer = Rand(CallDatabase.MinDelay, CallDatabase.MaxDelay);
            var def = PickCall();
            if (def != null) RingPhone(def);
        }

        /// <summary>Story calls take priority; otherwise a weighted random interruption.</summary>
        public CallDef PickCall()
        {
            int chapter = Chapter;
            foreach (var c in CallDatabase.Calls)
                if (c.Story && !State.callsDone.Contains(c.Id) && (c.Trigger == null || c.Trigger(this)))
                    return c;

            var pool = new List<CallDef>();
            float total = 0;
            foreach (var c in CallDatabase.Calls)
            {
                if (c.Story || c.Id == _lastRandomCall || chapter < c.MinChapter || chapter > c.MaxChapter) continue;
                if (c.Trigger != null && !c.Trigger(this)) continue;
                pool.Add(c);
                total += c.Weight;
            }
            if (pool.Count == 0) return null;
            double roll = _rng.NextDouble() * total;
            foreach (var c in pool)
            {
                roll -= c.Weight;
                if (roll <= 0) return c;
            }
            return pool[pool.Count - 1];
        }

        public ActiveCall RingPhone(CallDef def)
        {
            if (ActiveCall != null || def == null) return null;
            if (!def.Story) _lastRandomCall = def.Id;
            ActiveCall = new ActiveCall { Def = def, Ring = CallDatabase.RingSeconds };
            CallIncoming?.Invoke(ActiveCall);
            return ActiveCall;
        }

        public void AnswerCall()
        {
            if (ActiveCall == null || ActiveCall.Answered) return;
            ActiveCall.Answered = true;
            State.callsAnswered++;
            State.callsToday++;
            BreakFocus(0.5f); // picking up the phone is an interruption
            CallAnswered?.Invoke(ActiveCall);
        }

        /// <summary>Resolves an answered call with one of its choices. Returns a summary of what happened.</summary>
        public string ChooseCallOption(int index)
        {
            if (ActiveCall == null || !ActiveCall.Answered) return null;
            var def = ActiveCall.Def;
            var choice = def.Choices[Math.Max(0, Math.Min(index, def.Choices.Length - 1))];
            ActiveCall = null;
            if (def.Story) State.callsDone.Add(def.Id);
            string summary = ApplyChoice(choice);
            CallEnded?.Invoke(def, choice, summary);
            return summary;
        }

        public void DeclineCall() => EndUnanswered(missed: false);
        void MissCall() => EndUnanswered(missed: true);

        void EndUnanswered(bool missed)
        {
            if (ActiveCall == null) return;
            var def = ActiveCall.Def;
            ActiveCall = null;
            string summary = null;
            if (def.Story)
            {
                // story calls only ring once; ignoring someone is noticed
                State.callsDone.Add(def.Id);
                if (def.Offended is Person p)
                {
                    AddRapport(p, -1);
                    summary = $"{CallDatabase.PeopleNames[(int)p].Split(' ')[0]} -1";
                }
            }
            if (missed)
            {
                State.callsMissed++;
                CallMissed?.Invoke(def);
            }
            CallEnded?.Invoke(def, null, summary);
        }

        string ApplyChoice(CallChoice c)
        {
            var parts = new List<string>();
            void Rap(Person p, int d)
            {
                if (d == 0) return;
                AddRapport(p, d);
                parts.Add($"{CallDatabase.PeopleNames[(int)p].Split(' ')[0]} {(d > 0 ? "+" : "")}{d}");
            }
            Rap(Person.Dana, c.Dana);
            Rap(Person.Priya, c.Priya);
            Rap(Person.Gary, c.Gary);
            Rap(Person.Rex, c.Rex);

            if (!c.KeepsFocus) BreakFocus();

            if (c.LoseMinutes > 0)
            {
                // a meeting: the clock jumps ahead, but your agents kept working
                double seconds = c.LoseMinutes * DayLengthSeconds / GameDatabase.WorkdayMinutes;
                Earn(Math.Floor(RawCps * seconds));
                State.dayMinutes += c.LoseMinutes;
                parts.Add($"{c.LoseMinutes} min meeting");
            }
            if (c.CpsSeconds > 0 || c.MinCredits > 0)
            {
                double credits = Math.Floor(Math.Max(c.MinCredits, RawCps * c.CpsSeconds));
                if (credits > 0)
                {
                    Earn(credits);
                    parts.Add($"+{NumberFormat.Credits(credits)}");
                }
            }
            if (c.HypeMult > 0)
            {
                AddBuff(BuffKind.Hype, "Buzz", c.HypeMult, c.HypeSeconds);
                parts.Add($"x{c.HypeMult:0} output for {c.HypeSeconds:0}s");
            }
            if (c.CaffeineSeconds > 0)
            {
                AddBuff(BuffKind.Caffeine, "Coffee", GameDatabase.CaffeineMult, c.CaffeineSeconds * (float)_caffeineMult);
                parts.Add("caffeinated");
            }
            if (c.Discount > 0)
            {
                State.agentDiscount = Math.Max(State.agentDiscount, c.Discount);
                parts.Add($"{c.Discount * 100:0}% off your next agent order");
            }
            if (c.StartOutage && ActiveOutage == null) StartOutage();
            if (c.PreventOutage)
            {
                if (ActiveOutage != null) EndOutage(true);
                _outageTimer += 240f;
                parts.Add("outage avoided");
            }
            return string.Join(" · ", parts);
        }

        // ---------------------------------------------------------------- daily asks
        public event Action<AskState, double> AskCompleted;
        float _askTimer;

        public void GenerateAsks()
        {
            State.asksDay = State.day;
            State.asks.Clear();
            IEnumerable<AskDef> picks;
            if (State.day == 1)
                picks = new[] { AskDatabase.ById("ship"), AskDatabase.ById("hire") };
            else
            {
                var options = AskDatabase.Asks.Where(a => a.Available(this)).OrderBy(_ => _rng.Next()).ToList();
                picks = options.Take(2);
            }
            foreach (var a in picks)
            {
                double target = a.Target(this);
                // progress-from-zero asks would be done instantly if already met (e.g. production level)
                if (a.Progress(this) >= target && a.Id != "cps") continue;
                State.asks.Add(new AskState { id = a.Id, target = target });
            }
        }

        public double AskProgress(AskState s)
        {
            var def = AskDatabase.ById(s.id);
            return def == null ? 0 : Math.Min(1, def.Progress(this) / Math.Max(1e-9, s.target));
        }

        public static double AskRewardFor(GameModel m) => Math.Floor(Math.Max(50.0 * m.State.day, m.RawCps * 30));

        void UpdateAsks(float dt)
        {
            _askTimer -= dt;
            if (_askTimer > 0) return;
            _askTimer = 0.5f;
            bool allDoneBefore = AllAsksDone();
            foreach (var a in State.asks)
            {
                if (a.done || AskProgress(a) < 1) continue;
                a.done = true;
                State.asksCompleted++;
                double reward = AskRewardFor(this);
                Earn(reward);
                AskCompleted?.Invoke(a, reward);
            }
            if (!allDoneBefore && AllAsksDone())
                AddRapport(Person.Dana, 1); // Dana notices a clean sweep
        }

        bool AllAsksDone()
        {
            if (State.asks.Count == 0) return false;
            foreach (var a in State.asks) if (!a.done) return false;
            return true;
        }
    }
}
