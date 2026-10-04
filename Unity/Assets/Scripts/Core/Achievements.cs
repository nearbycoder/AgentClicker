using System;
using System.Collections.Generic;

namespace AgentClicker.Core
{
    public enum TrophyCategory { Earnings, Production, Agents, Fleet, Craft, Career, Events, Story }

    public sealed class AchievementDef
    {
        public string Id, Name, Description;
        public TrophyCategory Category;
        public Func<GameModel, bool> Check;
    }

    /// <summary>
    /// Trophies. Hundreds of them, most generated from ladders (earn 1 million, 1 billion ... 1 centillion), so there is
    /// always another one ahead. Each trophy adds Clout, which the influence upgrades turn into production.
    /// </summary>
    public static class AchievementDatabase
    {
        public static readonly List<AchievementDef> All = Build();
        static Dictionary<string, AchievementDef> s_byId;

        public static AchievementDef ById(string id)
        {
            if (s_byId == null)
            {
                s_byId = new Dictionary<string, AchievementDef>();
                foreach (var a in All) s_byId[a.Id] = a;
            }
            return id != null && s_byId.TryGetValue(id, out var d) ? d : null;
        }

        public static readonly string[] CategoryNames = { "Earnings", "Production", "Agents", "Fleet", "Craft", "Career", "Events", "Story" };
        public static readonly string[] CategoryColors = { "#FFD166", "#4DD0E1", "#7C4DFF", "#9B6BDF", "#3DDC97", "#FF8A3D", "#E040A0", "#38B6F0" };

        static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        static List<AchievementDef> Build()
        {
            var list = new List<AchievementDef>();
            void Add(TrophyCategory c, string id, string name, string desc, Func<GameModel, bool> check) =>
                list.Add(new AchievementDef { Id = id, Name = name, Description = desc, Category = c, Check = check });

            // ---- earnings, all-time: thousand ... centillion
            string[] earnNames = { "Thousandaire", "Millionaire", "Billionaire", "Trillionaire" };
            for (int k = 1; k < NumberFormat.NamedTiers; k++)
            {
                double need = Math.Pow(10, 3 * k);
                string word = NumberFormat.LongName(k);
                Add(TrophyCategory.Earnings, $"earn_{k}", k <= earnNames.Length ? earnNames[k - 1] : Cap(word) + "aire",
                    $"Earn 1 {word} credits, across every division.", m => m.State.allTimeEarned >= need);
            }

            // ---- production
            Add(TrophyCategory.Production, "cps_0", "Steady Trickle", "Produce 1 credit per second.", m => m.RawCps >= 1);
            for (int k = 1; k < NumberFormat.NamedTiers; k++)
            {
                double need = Math.Pow(10, 3 * k);
                string word = NumberFormat.LongName(k);
                Add(TrophyCategory.Production, $"cps_{k}", $"{Cap(word)} per Second", $"Produce 1 {word} credits per second.",
                    m => m.RawCps >= need);
            }

            // ---- agents of each type
            (int n, string fmt)[] ladder =
            {
                (1, "First {0}"), (50, "{0} Team"), (100, "{0} Department"), (150, "{0} Division"), (200, "{0} Company"),
                (300, "{0} Conglomerate"), (400, "{0} Nation"), (500, "{0} Planet"), (600, "{0} Galaxy"), (700, "{0} Multiverse"),
            };
            for (int i = 0; i < GameDatabase.Agents.Length; i++)
            {
                var a = GameDatabase.Agents[i];
                int idx = i;
                foreach (var (n, fmt) in ladder)
                {
                    int need = n;
                    Add(TrophyCategory.Agents, $"agent_{a.Id}_{n}", string.Format(fmt, a.Name),
                        n == 1 ? $"Hire your first {a.Name}." : $"Have {n} {a.Name} agents at once.",
                        m => m.State.agentCounts[idx] >= need);
                }
            }

            // ---- the whole fleet
            (int n, string name)[] fleet =
            {
                (10, "Small Team"), (100, "Startup"), (250, "Scale-Up"), (500, "Unicorn"), (1000, "Enterprise"),
                (2000, "Megacorp"), (4000, "Agent Nation"), (8000, "Agent Civilization"), (16000, "Kardashev Workforce"),
            };
            foreach (var (n, name) in fleet)
            {
                int need = n;
                Add(TrophyCategory.Fleet, $"fleet_{n}", name, $"Have {n:N0} agents at once.", m => m.TotalAgents >= need);
            }
            (int n, string name)[] owned =
            {
                (10, "Tinkerer"), (25, "Power User"), (50, "Feature Flagged"), (100, "Fully Upgraded"), (200, "Upgrade Hoarder"), (300, "Patch Notes"),
            };
            foreach (var (n, name) in owned)
            {
                int need = n;
                Add(TrophyCategory.Fleet, $"upgrades_{n}", name, $"Own {n} upgrades in one division.", m => m.State.upgrades.Count >= need);
            }
            Add(TrophyCategory.Fleet, "office_full", "Battlestation", "Install every office gadget in one division.",
                m => m.State.office.Count >= GameDatabase.OfficeItems.Length);
            Add(TrophyCategory.Fleet, "frontier", "Frontier Access", "Hire a frontier agent.", m =>
            {
                for (int i = GameDatabase.CoreAgentCount; i < GameDatabase.Agents.Length; i++) if (m.State.agentCounts[i] > 0) return true;
                return false;
            });
            Add(TrophyCategory.Fleet, "all_twenty", "Full Roster", "Own every kind of agent at once.", m =>
            {
                for (int i = 0; i < GameDatabase.Agents.Length; i++) if (m.State.agentCounts[i] == 0) return false;
                return true;
            });

            // ---- craft: clicking by hand
            (long n, string name)[] clicks =
            {
                (1, "Hello, World"), (100, "Warm Hands"), (1000, "Clicker"), (10000, "Carpal Tunnel"), (100000, "Mechanical Soul"), (1000000, "One With the Keyboard"),
            };
            foreach (var (n, name) in clicks)
            {
                long need = n;
                Add(TrophyCategory.Craft, $"clicks_{n}", name, n == 1 ? "Ship code by hand." : $"Click {n:N0} times.", m => m.State.allTimeClicks >= need);
            }
            string[] craftNames = { "Artisanal Code", "Hand-Crafted", "Bespoke Engineering", "Small-Batch Commits", "Locally Sourced Logic" };
            for (int k = 1; k <= 15; k++)
            {
                double need = Math.Pow(10, 3 * k);
                string word = NumberFormat.LongName(k);
                Add(TrophyCategory.Craft, $"hand_{k}", k <= craftNames.Length ? craftNames[k - 1] : $"Hand-Shipped {Cap(word)}",
                    $"Ship 1 {word} credits of code by hand.", m => m.State.allTimeHandmade >= need);
            }
            Add(TrophyCategory.Craft, "flow", "Flow State", "Max out your Focus.", m => m.Focus >= 0.999f);

            // ---- career
            (int n, string name)[] days =
            {
                (5, "First Week"), (10, "Two Weeks' Notice"), (25, "Probation Over"), (50, "Regular"), (100, "Furniture"),
                (250, "Lifer"), (500, "Load-Bearing Employee"), (1000, "Part of the Building"),
            };
            foreach (var (n, name) in days)
            {
                int need = n;
                Add(TrophyCategory.Career, $"days_{n}", name, $"Work {n} days.", m => m.State.totalDays >= need);
            }
            (int n, string name)[] stars = { (10, "Gold Star"), (50, "Star Employee"), (100, "Constellation"), (250, "Galaxy of Stars"), (500, "Supernova") };
            foreach (var (n, name) in stars)
            {
                int need = n;
                Add(TrophyCategory.Career, $"stars_{n}", name, $"Meet your quota {n} times.", m => m.State.totalStars >= need);
            }
            (int n, string name)[] factories = { (1, "Feet Up"), (5, "Serial Automator"), (10, "Factory Franchise"), (25, "Factory Factory"), (50, "Industrial Revolution") };
            foreach (var (n, name) in factories)
            {
                int need = n;
                Add(TrophyCategory.Career, $"factories_{n}", name, n == 1 ? "Build the Software Factory." : $"Build {n} Software Factories.",
                    m => m.State.factoriesBuilt >= need);
            }
            (int n, string name)[] speed = { (10, "Fast Track"), (5, "Speedrun"), (2, "Any% Factory") };
            foreach (var (n, name) in speed)
            {
                int need = n;
                Add(TrophyCategory.Career, $"factory_by_{n}", name, $"Build a Factory by day {n} of a division.",
                    m => m.State.bestFactoryDay > 0 && m.State.bestFactoryDay <= need);
            }
            (int n, string name)[] reorgs =
            {
                (1, "Reorg'd"), (2, "Restructured"), (3, "Synergized"), (5, "Right-Sized"), (10, "Matrix Management"),
                (15, "Interdimensional Transfer"), (25, "Corporate Nomad"), (50, "The Reorg Is Coming From Inside the House"), (100, "Org Chart Fractal"),
            };
            foreach (var (n, name) in reorgs)
            {
                int need = n;
                Add(TrophyCategory.Career, $"reorgs_{n}", name, n == 1 ? "Roll out the Factory to a new division." : $"Reorg {n} times.",
                    m => m.State.reorgs >= need);
            }
            (double n, string name)[] options =
            {
                (100, "Vested"), (1000, "Paper Millionaire"), (1e4, "Stock Option Hoarder"), (1e5, "Majority Stakeholder"),
                (1e6, "Cap Table Overflow"), (1e8, "You Are the Market"), (1e10, "Share Split Singularity"),
            };
            foreach (var (n, name) in options)
            {
                double need = n;
                Add(TrophyCategory.Career, $"options_{n:0}", name, $"Earn {NumberFormat.Short(n)} Stock Options in total.", m => m.State.optionsEarned >= need);
            }
            Add(TrophyCategory.Career, "perks_5", "Executive Lounge", "Own 5 Board Room perks.", m => m.State.perks.Count >= 5);
            Add(TrophyCategory.Career, "perks_all", "Golden Handcuffs", "Own every Board Room perk.", m =>
            {
                foreach (var p in GameDatabase.Perks) if (!p.Repeatable && !m.State.perks.Contains(p.Id)) return false;
                return true;
            });
            Add(TrophyCategory.Career, "seats_10", "The Whole Table", "Hold 10 Board Seats.", m => m.State.boardSeats >= 10);
            Add(TrophyCategory.Career, "top_title", "Top of the Ladder", "Reach the highest title in a division.",
                m => m.State.titleIndex >= GameDatabase.Titles.Length - 1);

            // ---- events
            (int n, string name)[] drops = { (1, "Early Adopter"), (10, "Beta Tester"), (50, "Benchmark Chaser"), (100, "Hype Cycle"), (250, "Release Notes Reader"), (1000, "Model Collector") };
            foreach (var (n, name) in drops)
            {
                int need = n;
                Add(TrophyCategory.Events, $"drops_{n}", name, n == 1 ? "Catch a model drop." : $"Catch {n} model drops.", m => m.State.dropsClaimed >= need);
            }
            (int n, string name)[] fixes = { (1, "Have You Tried Failing Over?"), (10, "On Call"), (50, "Incident Commander"), (100, "Five Nines") };
            foreach (var (n, name) in fixes)
            {
                int need = n;
                Add(TrophyCategory.Events, $"fixes_{n}", name, n == 1 ? "Fail over during an outage." : $"Fail over {n} outages.", m => m.State.outagesFixed >= need);
            }
            (int n, string name)[] calls = { (1, "Hello?"), (10, "Phone Person"), (50, "Switchboard"), (100, "Call Center"), (250, "Headset Permanently Attached") };
            foreach (var (n, name) in calls)
            {
                int need = n;
                Add(TrophyCategory.Events, $"calls_{n}", name, n == 1 ? "Answer a phone call." : $"Answer {n} phone calls.", m => m.State.callsAnswered >= need);
            }
            Add(TrophyCategory.Events, "missed_10", "Do Not Disturb", "Let 10 calls ring out.", m => m.State.callsMissed >= 10);
            (int n, string name)[] asks = { (10, "Team Player"), (50, "Reliable"), (100, "Go-To Person"), (250, "Manager's Favorite"), (500, "The Asks Are Coming From Inside the House") };
            foreach (var (n, name) in asks)
            {
                int need = n;
                Add(TrophyCategory.Events, $"asks_{n}", name, $"Complete {n} asks from your manager.", m => m.State.asksCompleted >= need);
            }
            Add(TrophyCategory.Events, "overachiever", "Overachiever", "Ship 10x your quota in a single day.",
                m => m.State.quotaToday > 0 && m.State.earnedToday >= m.State.quotaToday * 10);
            Add(TrophyCategory.Events, "last_one_out", "Last One Out", "Stay until security walks you out at 11:30 PM.", m => m.State.lastOneOut);

            // ---- story
            Add(TrophyCategory.Story, "epilogue", "The End?", "See the epilogue.", m => m.State.endingSeen);
            Add(TrophyCategory.Story, "inbox_zero", "Inbox Zero", "Read every email (at least 20 of them).",
                m => m.State.mail.Count >= 20 && m.State.mailRead.Count >= m.State.mail.Count);
            Add(TrophyCategory.Story, "best_friend", "Work Bestie", "Max out your rapport with someone.", m =>
            {
                foreach (var r in m.State.rapport) if (r >= GameModel.RapportMax) return true;
                return false;
            });
            Add(TrophyCategory.Story, "burned", "Burned Bridge", "Hit rock bottom with someone.", m =>
            {
                foreach (var r in m.State.rapport) if (r <= GameModel.RapportMin) return true;
                return false;
            });
            Add(TrophyCategory.Story, "favorite", "Office Favorite", "Unlock all four coworker perks.", m =>
            {
                foreach (var r in m.State.rapport) if (r < GameModel.PerkThreshold) return false;
                return true;
            });
            return list;
        }
    }
}
