using System;
using System.Collections.Generic;

namespace AgentClicker.Core
{
    /// <summary>All the jokes live here.</summary>
    public static class FlavorText
    {
        public const string Company = "Synergex Corp.";
        public const string EmployeeName = "Sam Rivera";
        public const string EmployeeId = "Employee #4471";

        static readonly Dictionary<string, string> Families = new Dictionary<string, string>
        {
            { "hallucin8", "Fib" }, { "paperclip", "Maximizer" }, { "gradient", "Summit" }, { "nimbus", "Cumulus" },
            { "deeppocket", "Baron" }, { "lumen", "Prism" }, { "singularity", "Heirloom" }, { "omnisapient", "Omni" },
        };

        static readonly string[] ModelSuffixes =
            { "", " Turbo", " Pro", " Ultra", "o", " Max", "-preview", " (Thinking)", " Mini", " Instant", " Deep", " Nano" };

        public static string ModelName(LabDef lab, Random rng)
        {
            string fam = Families.TryGetValue(lab.Id, out var f) ? f : "Model";
            int major = rng.Next(3, 13);
            string minor = rng.NextDouble() < 0.4 ? "." + rng.Next(1, 10) : "";
            return $"{fam}-{major}{minor}{ModelSuffixes[rng.Next(ModelSuffixes.Length)]}";
        }

        static readonly string[] CodeLines =
        {
            "def ship_it(): return True",
            "if (bug) { bug = false; }",
            "// TODO: ask the agent",
            "const synergy = leverage(paradigm);",
            "git commit -m \"fix\"",
            "git commit -m \"fix fix\"",
            "git push --force-with-lease",
            "while (!done) { prompt(); }",
            "SELECT * FROM tickets WHERE mine;",
            "npm install left-pad",
            "assert(agent.is_working)",
            "return await vibes();",
            "catch (e) { /* lol */ }",
            "kubectl apply -f everything.yaml",
            "let tokens = budget * hope;",
            "// this should never happen",
            "sudo make me-a-sandwich",
            "import antigravity",
            "if (friday) deploy();",
            "throw new NotMyProblemException();",
            "for agent in fleet: agent.go()",
            "rm -rf node_modules && pray",
            "docker compose up --vibe",
            "terraform apply -auto-approve",
            "fn main() { println!(\"ship\"); }",
            "@ai please make this good",
        };

        public static string CodeLine(Random rng) => CodeLines[rng.Next(CodeLines.Length)];

        public static string AgentLog(AgentDef agent, int instance, Random rng)
        {
            string task = agent.Tasks[rng.Next(agent.Tasks.Length)].Replace("{n}", rng.Next(1000, 9999).ToString());
            return $"{agent.Model} #{instance}: {task}";
        }

        static readonly string[] Weekdays = { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday" };
        public static string Weekday(int day) => Weekdays[(day - 1) % 5];

        /// <summary>Dana writes the reviews, until the Heirloom PM agent replaces her.</summary>
        public static string ManagerName(GameModel m) =>
            m.AgentCount(GameDatabase.AgentIndex("pm")) > 0 ? "Heirloom PM" : "Dana Whitfield";

        public static string ManagerReview(DayReview r, GameModel m, Random rng)
        {
            if (m.AgentCount(GameDatabase.AgentIndex("pm")) > 0)
            {
                string[] agentGood =
                {
                    "Exceeds expectations. I have generated new, higher expectations.",
                    "Quota met. I have written a 6-pager celebrating you. It is in your inbox. It is 40 pages.",
                    "Performance: optimal. Vibes: also optimal. I measured both.",
                };
                string[] agentBad =
                {
                    "Below quota. This is a learning opportunity. For you. I already learned.",
                    "Missed target. I have scheduled 3 syncs to discuss. And a sync to discuss the syncs.",
                };
                var p = r.Met ? agentGood : agentBad;
                return p[rng.Next(p.Length)];
            }
            string[] good =
            {
                "Great velocity today. Let's take this offline and celebrate.",
                "You crushed the quota. I'm putting you on a slide.",
                "Fantastic output. The VP noticed. (The VP is also an agent.)",
                "Strong numbers! Keep circling back like that.",
                "Above quota again. Have you considered becoming a thought leader?",
            };
            string[] bad =
            {
                "We missed quota. Let's sync about your syncs.",
                "Velocity is trending sideways. Have you tried more agents?",
                "Below target. I'm not mad, I'm just... aligned with disappointment.",
                "Quota missed. Tomorrow is a new sprint!",
            };
            var pool = r.Met ? good : bad;
            return pool[rng.Next(pool.Length)];
        }

        public static readonly string[] MorningEmails =
        {
            "Reminder: the 9:00 standup is now a 9:00 stand-still.",
            "Facilities: please stop microwaving fish in the AI lab.",
            "HR: 'Synergy Week' starts Monday. Attendance is mandatory and optional.",
            "IT: Your password expires in 3 days. And 2 days. And now.",
            "All-hands moved to the metaverse. Bring a headset.",
            "Legal: Do not let the agents sign contracts. Again.",
            "Finance: Compute budget approved! (Please stop asking.)",
            "CEO: We are an AI-first, people-also company.",
            "Reminder: Lunch & Learn: 'Prompting Your Prompt Engineer'.",
            "The coffee machine has been upgraded to v2.1-preview.",
        };

        public static readonly string[] DayTicker =
        {
            "Manager: Quick sync? (It won't be quick.)",
            "Slack: 47 unread messages in #agents-going-rogue",
            "Calendar: 'Focus Time' was overwritten by 'Alignment Huddle'",
            "Coworker: Is your agent the one ordering pizzas?",
            "IT: Please do not give the agents admin rights.",
            "Manager: Love the energy. Can it be more agentic?",
            "Security: The Orchestrator requested a badge.",
            "Slack: @channel who deployed on a Friday?",
            "Calendar: 'Q3 Planning' (now Q4 planning)",
        };

        public static string PromotionLine(TitleDef t) => $"Promoted to {t.Name}! {t.Perk}";

        public static string OfficeInstalled(OfficeItemDef o) => $"Facilities installed your {o.Name}.";

        public static string DayChange(int day)
        {
            switch (day)
            {
                case 2: return "Someone left sticky notes on your monitor. They say 'use more agents'.";
                case 3: return "You framed a photo. Your family misses you.";
                case 4: return "A motivational poster appeared. Nobody knows who hung it.";
                case 5: return "Friday pizza! (The boxes will stay for a while.)";
                case 7: return "A second poster. The office is getting cozy.";
                case 9: return "Facilities finally took the pizza boxes.";
                default: return null;
            }
        }
    }
}
