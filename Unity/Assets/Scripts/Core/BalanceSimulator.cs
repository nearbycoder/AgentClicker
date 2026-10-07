using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AgentClicker.Core
{
    /// <summary>
    /// A greedy "reasonable player" bot used to tune pacing. It clicks at a configurable rate,
    /// catches model drops, fixes outages, clocks out at 17:00 and always buys the purchase
    /// with the best payback time.
    /// </summary>
    public static class BalanceSimulator
    {
        public sealed class Result
        {
            public bool Finished;
            public double Seconds;
            public int Days;
            public readonly List<string> Log = new List<string>();

            public override string ToString()
            {
                var sb = new StringBuilder();
                sb.AppendLine($"Finished={Finished} after {NumberFormat.Duration(Seconds)} of play, day {Days}");
                foreach (var l in Log) sb.AppendLine("  " + l);
                return sb.ToString();
            }
        }

        public static Result Run(double earlyClicksPerSecond = 4, double lateClicksPerSecond = 1,
                                 double maxSeconds = 8 * 3600, bool catchDrops = true, int seed = 1,
                                 Action<GameModel, double> observe = null)
            => Run(new GameModel(new GameState(), seed), earlyClicksPerSecond, lateClicksPerSecond, maxSeconds, catchDrops, observe);

        /// <summary>
        /// Plays one division from its first morning until the Factory is built (or time runs out).
        /// <paramref name="observe"/> sees the model after every simulated second (tests use it; it must not change anything).
        /// </summary>
        public static Result Run(GameModel model, double earlyClicksPerSecond, double lateClicksPerSecond, double maxSeconds, bool catchDrops,
                                 Action<GameModel, double> observe = null)
        {
            var result = new Result();
            var seenAgents = new HashSet<int>();
            int lastTitle = 0;
            model.ClockIn();
            double t = 0, clickAcc = 0;
            const float dt = 1f;

            while (t < maxSeconds)
            {
                double cps = t < 1800 ? earlyClicksPerSecond : lateClicksPerSecond;
                clickAcc += cps * dt;
                while (clickAcc >= 1) { model.Click(); clickAcc -= 1; }

                model.Tick(dt);
                t += dt;

                if (catchDrops && model.ActiveDrop != null && model.ActiveDrop.Remaining < GameDatabase.DropLifetime - 2)
                    model.ClaimDrop();
                if (model.ActiveCall != null)
                {
                    model.AnswerCall();
                    model.ChooseCallOption(0);
                }
                if (model.ActiveOutage != null)
                    for (int i = 0; i < GameDatabase.OutageClicks; i++) model.ClickOutage();

                if (model.PastFiveOClock || model.Phase != GamePhase.Working)
                {
                    if (model.Phase == GamePhase.Working) model.ClockOut();
                    model.GoHome();
                    model.StartNextDay();
                    model.ClockIn();
                }

                observe?.Invoke(model, t);
                if (model.CanBuildFactory)
                {
                    model.BuildFactory();
                    result.Finished = true;
                    result.Log.Add($"{NumberFormat.Duration(t)} day {model.State.day}: SOFTWARE FACTORY BUILT");
                    break;
                }

                // shopping is the expensive part of the sim; a real player doesn't buy every second either
                if (((int)t % 2) == 0) BuyGreedy(model, cps);

                for (int i = 0; i < GameDatabase.Agents.Length; i++)
                    if (model.State.agentCounts[i] > 0 && seenAgents.Add(i))
                        result.Log.Add($"{NumberFormat.Duration(t)} day {model.State.day}: first {GameDatabase.Agents[i].Name} (cps {NumberFormat.Short(model.RawCps)})");
                if (model.State.titleIndex != lastTitle)
                {
                    lastTitle = model.State.titleIndex;
                    result.Log.Add($"{NumberFormat.Duration(t)} day {model.State.day}: promoted to {model.Title.Name}");
                }
            }

            result.Seconds = t;
            result.Days = model.State.day;
            result.Log.Add($"final: cps {NumberFormat.Short(model.RawCps)}, agents {model.TotalAgents}, " +
                           $"upgrades {model.State.upgrades.Count}, office {model.State.office.Count}, stars {model.State.stars}");
            return result;
        }

        /// <summary>
        /// The endless game: plays several divisions in a row. In each one the bot builds the Factory, keeps playing
        /// for <paramref name="postFactorySeconds"/> (frontier agents), reorgs, and spends its options on perks.
        /// <paramref name="observeLap"/> sees the model every second of those laps, with the time into the lap.
        /// </summary>
        public static CareerResult RunCareer(int divisions, double postFactorySeconds = 1800, double maxSecondsPerDivision = 8 * 3600, int seed = 1,
                                             Action<GameModel, double> observeLap = null)
        {
            var model = new GameModel(new GameState(), seed);
            var career = new CareerResult();
            for (int d = 0; d < divisions; d++)
            {
                if (d > 0) model.ClockIn();
                var run = Run(model, 4, 1, maxSecondsPerDivision, true);
                var div = new DivisionResult
                {
                    Name = model.DivisionName, FactorySeconds = run.Seconds, FactoryDay = model.State.day, Finished = run.Finished,
                    OptionsBefore = model.State.optionsEarned,
                };
                if (!run.Finished)
                {
                    div.Log = run.ToString() + $"\n  factory cost {NumberFormat.Short(model.FactoryCost)}, credits {NumberFormat.Short(model.State.credits)}, " +
                              $"cps {NumberFormat.Short(model.RawCps)}, reqs: {string.Join(" | ", model.FactoryRequirements().Select(r => r.label + (r.met ? " ✓" : " ✗")))}";
                    career.Divisions.Add(div);
                    break;
                }

                // victory lap: frontier agents, research, clout
                double t = 0;
                while (t < postFactorySeconds)
                {
                    model.Click();
                    model.Tick(1f);
                    t += 1;
                    if (model.ActiveDrop != null) model.ClaimDrop();
                    if (model.ActiveCall != null) { model.AnswerCall(); model.ChooseCallOption(0); }
                    if (model.ActiveOutage != null) for (int i = 0; i < GameDatabase.OutageClicks; i++) model.ClickOutage();
                    if (model.PastFiveOClock || model.Phase != GamePhase.Working)
                    {
                        if (model.Phase == GamePhase.Working) model.ClockOut();
                        model.GoHome();
                        model.StartNextDay();
                        model.ClockIn();
                    }
                    if (((int)t % 3) == 0) BuyGreedy(model, 1);
                    observeLap?.Invoke(model, t);
                }
                div.FinalCps = model.RawCps;
                div.EarnedHere = model.State.lifetimeEarned;
                div.Options = model.PendingOptions;
                div.Trophies = model.AchievementCount;
                career.Divisions.Add(div);

                if (model.Phase == GamePhase.Working) model.ClockOut();
                if (model.Phase == GamePhase.Review) model.GoHome();
                model.Reorg();
                BuyPerks(model);
            }
            career.Final = model;
            return career;
        }

        public sealed class DivisionResult
        {
            public string Name;
            public bool Finished;
            public double FactorySeconds, FinalCps, EarnedHere, Options, OptionsBefore;
            public int FactoryDay, Trophies;
            public string Log;
            public override string ToString() =>
                $"{Name,-20} factory {(Finished ? NumberFormat.Duration(FactorySeconds) : "DNF"),-8} (day {FactoryDay,2})  " +
                $"cps after lap {NumberFormat.Short(FinalCps),-8} earned {NumberFormat.Short(EarnedHere),-8} +{NumberFormat.Short(Options)} options  trophies {Trophies}";
        }

        public sealed class CareerResult
        {
            public readonly List<DivisionResult> Divisions = new List<DivisionResult>();
            public GameModel Final;
            public override string ToString()
            {
                var sb = new StringBuilder();
                foreach (var d in Divisions)
                {
                    sb.AppendLine(d.ToString());
                    if (d.Log != null) sb.AppendLine(d.Log);
                }
                if (Final != null)
                    sb.AppendLine($"perks: {string.Join(", ", Final.State.perks)} · seats {Final.State.boardSeats} · options left {NumberFormat.Short(Final.State.options)}");
                return sb.ToString();
            }
        }

        /// <summary>Cheapest useful perk first, then board seats with whatever is left.</summary>
        static void BuyPerks(GameModel model)
        {
            bool bought = true;
            while (bought)
            {
                bought = false;
                PerkDef best = null;
                foreach (var p in GameDatabase.Perks)
                    if (model.IsPerkAvailable(p) && model.State.options >= model.PerkCost(p) && (best == null || model.PerkCost(p) < model.PerkCost(best)))
                        best = p;
                if (best != null) bought = model.BuyPerk(best.Id);
            }
        }

        static void BuyGreedy(GameModel model, double clickRate)
        {
            // Once everything but the money is in place, save up for the factory (if it's within reach; otherwise keep investing).
            if (!model.State.factoryBuilt)
            {
                var reqs = model.FactoryRequirements();
                double income = Math.Max(model.RawCps + clickRate * model.ClickPower, 1e-6);
                if (reqs.Take(reqs.Count - 1).All(r => r.met) && (model.FactoryCost - model.State.credits) / income < 600) return;
            }

            for (int guard = 0; guard < 50; guard++)
            {
                double income = Math.Max(model.RawCps + clickRate * model.ClickPower, 1e-6);
                double bestScore = double.MaxValue, bestCost = 0;
                Action buy = null;

                void Consider(double cost, double gain, Action action, double bias = 1)
                {
                    if (gain <= 0) return;
                    double wait = Math.Max(0, cost - model.State.credits) / income;
                    double score = (wait + cost / gain) * bias;
                    if (score < bestScore) { bestScore = score; bestCost = cost; buy = action; }
                }

                for (int i = 0; i < GameDatabase.Agents.Length; i++)
                {
                    int idx = i;
                    double bias = 1;
                    // Gate requirements are worth a little extra.
                    if (model.State.agentCounts[i] == 0) bias = 0.7;
                    if (i == GameDatabase.OrchestratorIndex && model.State.agentCounts[i] < model.FactoryOrchestratorsNeeded) bias = 0.8;
                    if (!model.IsAgentRevealed(i)) continue;
                    Consider(model.AgentCost(i), model.GainFrom(PurchaseKind.Agent, GameDatabase.Agents[i].Id, clickRate),
                             () => model.BuyAgent(idx), bias);
                }
                foreach (var u in model.AvailableUpgrades().ToList())
                {
                    var id = u.Id;
                    Consider(model.UpgradeCost(u), model.GainFrom(PurchaseKind.Upgrade, id, clickRate), () => model.BuyUpgrade(id));
                }
                foreach (var o in GameDatabase.OfficeItems.Where(model.IsOfficeAvailable).ToList())
                {
                    var id = o.Id;
                    double gain = model.GainFrom(PurchaseKind.Office, id, clickRate);
                    // Items without direct income (fridge, whiteboard prerequisites, crit...) get a nominal value.
                    if (gain <= 0) gain = income * 0.03;
                    if (id == GameDatabase.FactoryRequiredOffice) gain *= 3;
                    Consider(model.OfficeCost(o), gain, () => model.BuyOffice(id));
                }

                if (buy == null || bestCost > model.State.credits) return;
                buy();
            }
        }
    }
}
