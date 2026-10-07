using System;

namespace AgentClicker.Core
{
    public enum GoalKind { Agent, Orchestrators, Office, Factory, Option }

    /// <summary>One thing worth saving up for, with how far along you are.</summary>
    public sealed class Goal
    {
        public GoalKind Kind;
        public string Title;
        /// <summary>Where the progress bar starts, where you are and where it ends (credits, or all-time credits for options).</summary>
        public double From, Have, Need;
        public int AgentIndex = -1;
        public string OfficeId;

        public double Progress
        {
            get
            {
                double span = Need - From; // infinite or NaN at the very top of the number range
                if (!(span > 0) || double.IsInfinity(span)) return Have >= Need ? 1 : 0;
                return Math.Max(0, Math.Min(1, (Have - From) / span));
            }
        }
        public bool Reached => Have >= Need;

        /// <summary>Seconds until it's reached at <paramref name="cps"/>; 0 if it already is, infinity if nothing is earning.</summary>
        public double SecondsAt(double cps) => Reached ? 0 : cps > 0 ? (Need - Have) / cps : double.PositiveInfinity;

        /// <summary>Identifies the goal regardless of progress (for change detection).</summary>
        public string Key => $"{Kind}:{AgentIndex}:{OfficeId}:{Title}";
    }

    /// <summary>
    /// Picks the next goal for the CorpOS desktop. Before this division's Factory: the cheapest story agent type you
    /// don't own yet, then the Orchestrator Clusters the Factory needs, then the recliner, then the Factory itself.
    /// After it: the next frontier agent type, then a reorg that doubles your Stock Options; if that agent costs more than
    /// an hour of production and a reorg is already worth taking, the reorg. Display only; it never changes the economy.
    /// </summary>
    public static class NextGoal
    {
        /// <summary>
        /// A frontier agent that costs more than this many seconds of production (without buffs) gives way to a reorg that's worth
        /// taking. Its list price, not the time left or a discounted price: savings rise and fall with every purchase and a sales
        /// call's discount comes and goes, so the card would flip back and forth.
        /// </summary>
        public const double FarAgentSeconds = 3600;

        public static Goal Pick(GameModel m)
        {
            var s = m.State;
            if (!s.factoryBuilt)
            {
                for (int i = 0; i < GameDatabase.CoreAgentCount; i++)
                    if (s.agentCounts[i] == 0) return AgentGoal(m, i, $"Hire your first {GameDatabase.Agents[i].Name}");

                int orch = s.agentCounts[GameDatabase.OrchestratorIndex], need = m.FactoryOrchestratorsNeeded;
                if (orch < need)
                {
                    var g = AgentGoal(m, GameDatabase.OrchestratorIndex, $"Orchestrator Clusters for the Factory: {orch}/{need}");
                    g.Kind = GoalKind.Orchestrators;
                    return g;
                }

                var recliner = GameDatabase.Office(GameDatabase.FactoryRequiredOffice);
                if (!m.HasOffice(recliner.Id))
                    return new Goal
                    {
                        Kind = GoalKind.Office, Title = $"{recliner.Name} (the Factory needs it)", OfficeId = recliner.Id,
                        Have = s.credits, Need = m.OfficeCost(recliner),
                    };

                return new Goal { Kind = GoalKind.Factory, Title = "Build the Software Factory", Have = s.credits, Need = m.FactoryCost };
            }

            var reorg = ReorgGoal(m);
            for (int i = GameDatabase.CoreAgentCount; i < GameDatabase.Agents.Length; i++)
                if (s.agentCounts[i] == 0)
                {
                    var agent = AgentGoal(m, i, $"Hire your first {GameDatabase.Agents[i].Name}");
                    // a frontier agent hours (or years) away is no goal when a reorg would already pay off
                    return reorg.Reached && !(GameDatabase.Agents[i].BaseCost <= m.RawCps * FarAgentSeconds) ? reorg : agent;
                }
            // every agent type is on the payroll
            return reorg;
        }

        /// <summary>A reorg worth taking: one that at least doubles your Stock Options (or vests your first).</summary>
        static Goal ReorgGoal(GameModel m)
        {
            var s = m.State;
            double earned = s.optionsEarned, pending = m.PendingOptions;
            return new Goal
            {
                Kind = GoalKind.Option,
                Title = earned < 1
                    ? pending >= 1 ? $"Reorg for your first Stock Options (+{NumberFormat.Short(pending)})" : "Vest your first Stock Option"
                    : $"Double your Stock Options (+{NumberFormat.Short(pending)} of +{NumberFormat.Short(earned)} vested)",
                From = GameModel.EarningsForOptions(earned), Have = s.allTimeEarned,
                Need = GameModel.EarningsForOptions(Math.Max(1, 2 * earned)),
            };
        }

        static Goal AgentGoal(GameModel m, int index, string title) => new Goal
        {
            Kind = GoalKind.Agent, Title = title, AgentIndex = index, Have = m.State.credits, Need = m.AgentCost(index, 1),
        };
    }
}
