using System;
using System.Collections.Generic;
using System.Linq;

namespace AgentClicker.Core
{
    /// <summary>A daily request from your manager. Saved per day.</summary>
    [Serializable]
    public class AskState
    {
        public string id;
        public double target;
        public bool done;
    }

    public sealed class AskDef
    {
        public string Id;
        public Func<GameModel, bool> Available;
        public Func<GameModel, double> Target;
        public Func<GameModel, double> Progress;
        public Func<double, string> Text;
    }

    /// <summary>"Today's asks": two small goals each morning, so every day has a shape.</summary>
    public static class AskDatabase
    {
        static double Nice(double v) => Math.Max(1, NumberFormat.RoundSignificant(v, 2));

        public static readonly AskDef[] Asks =
        {
            new AskDef
            {
                Id = "ship", Available = m => true,
                Target = m => Nice(Math.Max(60, m.BaseClickPower * 140)),
                Progress = m => m.State.handmadeToday,
                Text = t => $"Ship {NumberFormat.Short(t)} credits of code by hand",
            },
            new AskDef
            {
                Id = "hire", Available = m => true,
                Target = m => 3 + m.Chapter * 2,
                Progress = m => m.State.hiresToday,
                Text = t => $"Hire {t:0} agents",
            },
            new AskDef
            {
                Id = "upgrade", Available = m => m.TotalAgents >= 5,
                Target = m => m.Chapter >= 3 ? 2 : 1,
                Progress = m => m.State.upgradesToday,
                Text = t => t > 1 ? $"Buy {t:0} upgrades" : "Buy an upgrade",
            },
            new AskDef
            {
                Id = "drop", Available = m => m.State.day >= 2,
                Target = m => 1,
                Progress = m => m.State.dropsToday,
                Text = t => "Try a new model drop",
            },
            new AskDef
            {
                Id = "cps", Available = m => m.RawCps >= 5,
                Target = m => Nice(m.RawCps * 1.6),
                Progress = m => m.RawCps,
                Text = t => $"Reach {NumberFormat.Rate(t)} production",
            },
            new AskDef
            {
                Id = "overdeliver", Available = m => m.State.day >= 3,
                Target = m => Nice(m.State.quotaToday * 1.3),
                Progress = m => m.State.earnedToday,
                Text = t => $"Over-deliver: ship {NumberFormat.Short(t)} today",
            },
            new AskDef
            {
                Id = "calls", Available = m => m.State.day >= 2,
                Target = m => 2,
                Progress = m => m.State.callsToday,
                Text = t => "Answer 2 phone calls",
            },
            new AskDef
            {
                Id = "gadget", Available = m => GameDatabase.OfficeItems.Any(o => m.IsOfficeAvailable(o) && o.Cost <= Math.Max(m.State.credits, m.RawCps * 200)),
                Target = m => 1,
                Progress = m => m.State.officeToday,
                Text = t => "Install a new office gadget",
            },
        };

        static Dictionary<string, AskDef> _byId;
        public static AskDef ById(string id)
        {
            _byId ??= Asks.ToDictionary(a => a.Id);
            return id != null && _byId.TryGetValue(id, out var a) ? a : null;
        }
    }
}
