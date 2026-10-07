using System;
using System.Collections.Generic;
using System.Linq;

namespace AgentClicker.Core
{
    public sealed class LabDef
    {
        public readonly string Id, Name, Tagline, ColorHex, Monogram;
        public LabDef(string id, string name, string tagline, string colorHex, string monogram)
        {
            Id = id; Name = name; Tagline = tagline; ColorHex = colorHex; Monogram = monogram;
        }
    }

    public sealed class AgentDef
    {
        public int Index, LabIndex;
        public readonly string Id, Name, LabId, Model, Description, Monogram;
        public readonly double BaseCost, BaseCps;
        public readonly string[] Tasks;

        public AgentDef(string id, string name, string labId, string model, string monogram, double baseCost, double baseCps,
                        string description, params string[] tasks)
        {
            Id = id; Name = name; LabId = labId; Model = model; Monogram = monogram;
            BaseCost = baseCost; BaseCps = baseCps; Description = description; Tasks = tasks;
        }
    }

    public enum UpgradeKind { AgentTier, ClickMult, ClickCpsPercent, LabContract, Research, Clout }

    public sealed class UpgradeDef
    {
        public string Id, Name, Description;
        public double Cost;
        public UpgradeKind Kind;
        public int AgentIndex = -1;
        public int Tier;
        public string LabId;
        public int LabIndex;
        public double Value;
        public Func<GameModel, bool> Unlocked;
    }

    public sealed class OfficeItemDef
    {
        public string Id, Name, Description, Requires;
        public double Cost;
        public double CpsPercent, ClickFlat, ClickMult = 1, ClickCpsPercent, CritChance, OutageShorten, DropFrequency,
                      CaffeineDuration = 1, AutoClicksPerSec, NightBonus, ArchitectMult = 1;
    }

    public sealed class TitleDef
    {
        public readonly string Name;
        public readonly double Threshold;
        public readonly string Perk;
        public TitleDef(string name, double threshold, string perk) { Name = name; Threshold = threshold; Perk = perk; }
    }

    /// <summary>A permanent Board Room perk bought with Stock Options. Survives reorgs.</summary>
    public sealed class PerkDef
    {
        public string Id, Name, Description, Requires;
        public double Cost;
        /// <summary>Repeatable perks (Board Seats) get pricier with every level.</summary>
        public bool Repeatable;
    }

    /// <summary>Where Sam is sent to roll out the Factory next. Run 0 is Engineering.</summary>
    public sealed class DivisionDef
    {
        public readonly string Name, Blurb;
        public DivisionDef(string name, string blurb) { Name = name; Blurb = blurb; }
    }

    /// <summary>All static game content. Numbers are tuned with <see cref="BalanceSimulator"/>.</summary>
    public static class GameDatabase
    {
        // ------------------------------------------------------------------ time
        public const float DayStartHour = 9f;
        public const float DayEndHour = 17f;
        public const float OvertimeEndHour = 23.5f;
        public const float WorkdayMinutes = (DayEndHour - DayStartHour) * 60f;
        public const float DefaultDayLengthSeconds = 300f;

        // ------------------------------------------------------------------ economy
        public const double CostGrowth = 1.15;
        public const double FactoryCost = 3e12;
        public const int FactoryOrchestrators = 5;
        /// <summary>The first ten agents are needed for the Factory; the rest are frontier agents it unlocks.</summary>
        public const int CoreAgentCount = 10;
        /// <summary>Stock options are worth cbrt(all-time credits / divisor), like Cookie Clicker's prestige.</summary>
        public const double OptionsDivisor = 1e9;
        public const double OptionValue = 0.01;
        public const double CloutPerAchievement = 0.04;
        /// <summary>Later divisions want a bigger Factory: 0.1% of everything you've ever earned (at least the first price).</summary>
        public const double FactoryCareerShare = 0.001;
        public const string FactoryRequiredOffice = "recliner";
        public const double StarCpsBonus = 0.01;
        public const double NightShiftSeconds = 60;
        public const double OfflineEfficiency = 0.1;
        public const double OfflineCapSeconds = 3600;
        public const double ReviewBonus = 0.10;

        // ------------------------------------------------------------------ events
        public const float DropLifetime = 12f;
        public const float DropMinDelay = 60f, DropMaxDelay = 150f;
        public const float FirstDropDelay = 150f;
        public const float HypeDuration = 60f, HypeMult = 7f;
        public const float CaffeineDuration = 10f, CaffeineMult = 77f;
        public const float OutageDuration = 45f;
        public const int OutageClicks = 5;
        public const float OutageMinDelay = 150f, OutageMaxDelay = 360f;

        public static readonly LabDef[] Labs =
        {
            new LabDef("hallucin8", "Hallucin8 Labs", "Move fast and make things up.", "#E040A0", "H8"),
            new LabDef("paperclip", "Paperclip Dynamics", "Optimizing for your objectives. All of them.", "#4A7BB7", "PD"),
            new LabDef("gradient", "Gradient Ascent", "Always climbing. Never converging.", "#2EAD6B", "GA"),
            new LabDef("nimbus", "Nimbus Cognition", "Your thoughts, in the cloud.", "#38B6F0", "NC"),
            new LabDef("deeppocket", "Deep Pocket AI", "Infinite runway. Finite patience.", "#E3B341", "DP"),
            new LabDef("lumen", "Lumen Frontier", "Illuminating the latent space.", "#F28C28", "LF"),
            new LabDef("singularity", "Singularity & Sons", "A family business since the end of history.", "#9B6BDF", "S&"),
            new LabDef("omnisapient", "OmniSapient", "We already know what you'll buy.", "#E5484D", "OS"),
        };

        public static readonly AgentDef[] Agents =
        {
            new AgentDef("autocomplete", "Autocomplete", "hallucin8", "Fib-Mini", "AC", 15, 0.1,
                "Finishes your sentences. Sometimes even correctly.",
                "completed a for-loop", "suggested 'TODO: fix later'", "closed 3 brackets", "renamed variable 'x' to 'x2'",
                "autocompleted a semicolon"),
            new AgentDef("chat", "Chat Assistant", "hallucin8", "Fib-2 Chat", "CA", 100, 1,
                "Answers every question with total confidence.",
                "explained how to exit vim", "apologized for the confusion", "wrote a regex nobody understands",
                "summarized a 40-message thread", "cited a library that doesn't exist"),
            new AgentDef("junior", "Junior Coding Agent", "paperclip", "Maximizer Jr.", "JR", 1100, 8,
                "Eager, tireless, slightly terrifying. Opens one PR per minute.",
                "opened PR #{n}: 'small refactor' (+4,812 lines)", "fixed a typo in the README", "added a dependency",
                "closed JIRA-{n}", "rewrote the build in a new framework"),
            new AgentDef("tester", "Test Writer", "gradient", "Summit-T", "TW", 12000, 47,
                "Writes tests until the coverage report blushes.",
                "raised coverage to 101%", "mocked the mock", "wrote 214 snapshot tests", "found a flaky test",
                "asserted that true is true"),
            new AgentDef("reviewer", "Code Reviewer", "nimbus", "Cumulus Review", "CR", 130000, 260,
                "Leaves 'nit:' comments at superhuman speed.",
                "left 37 nits on PR #{n}", "approved with suggestions", "requested changes: 'vibes off'",
                "LGTM'd a 9,000-line diff", "caught an off-by-one"),
            new AgentDef("swarm", "Bug Triage Swarm", "paperclip", "Clipswarm", "BS", 1.4e6, 1400,
                "Thousands of tiny agents that agree on nothing except your backlog.",
                "triaged 412 bugs", "marked JIRA-{n} as 'works on my cluster'", "deduplicated 88 tickets",
                "closed a bug from 2019", "relabeled everything P2"),
            new AgentDef("devops", "DevOps Agent", "deeppocket", "Pipeline Baron", "DO", 20e6, 7800,
                "Deploys on Fridays. Never gets paged. Pages you instead.",
                "shipped to prod (it's Friday)", "scaled the cluster to 900 nodes", "rotated the secrets",
                "fixed the pipeline it broke", "turned it off and on again"),
            new AgentDef("architect", "Architect Agent", "lumen", "Prism Architect", "AR", 200e6, 44000,
                "Draws boxes and arrows until the system designs itself.",
                "proposed a microservice per function", "wrote ADR-{n}", "drew a very confident diagram",
                "migrated everything to an event bus", "deprecated the monolith (again)"),
            new AgentDef("pm", "Product Manager Agent", "singularity", "Heirloom PM", "PM", 2.4e9, 260000,
                "Writes the roadmap, the specs and the apology email.",
                "reprioritized the roadmap", "wrote a 6-pager", "aligned stakeholders", "shipped a feature nobody asked for",
                "scheduled a sync about syncs"),
            new AgentDef("orchestrator", "Orchestrator Cluster", "omnisapient", "Omni Conductor", "OC", 26e9, 1.6e6,
                "Manages the agents that manage the agents.",
                "spawned 1,200 sub-agents", "merged 340 PRs in one breath", "reassigned the PM agent",
                "optimized itself", "closed the quarter early"),

            // ---- frontier agents: unlocked by building the Software Factory
            new AgentDef("foundry", "Agent Foundry", "omnisapient", "Omni Foundry", "AF", 330e9, 10e6,
                "A factory that builds the factories that build the agents.",
                "forged 40,000 agents before lunch", "stamped out a new Factory Line", "recycled 300 deprecated agents",
                "shipped a pallet of PRs", "hit 99.97% yield"),
            new AgentDef("datacenter", "Hyperscale Datacenter", "deeppocket", "Baron Campus", "DC", 5e12, 65e6,
                "A building the size of a town. It hums. The town has questions.",
                "brought 4 GW online", "bought a river for cooling", "rebalanced 2M GPUs", "rerouted the power grid",
                "filed an environmental impact statement (generated)"),
            new AgentDef("twin", "Digital Twin", "hallucin8", "Fib-You", "DT", 70e12, 430e6,
                "Trained on every commit you ever made. Does your job better than you. Also takes your lunch.",
                "attended your 1:1 for you", "answered your email in your voice", "used your catchphrase in a PR",
                "took credit for the sprint", "did your expense report (approved)"),
            new AgentDef("agi_intern", "AGI Intern", "singularity", "Heirloom Prodigy", "AI", 1e15, 2.9e9,
                "Generally intelligent. Specifically unpaid.",
                "solved P vs NP, asked for a return offer", "refactored the company", "rewrote physics.dll",
                "fetched coffee from first principles", "unified 40 microservices into one thought"),
            new AgentDef("orbital", "Orbital Compute Ring", "nimbus", "Stratus Orbital", "OR", 14e15, 21e9,
                "Inference from low Earth orbit. Latency: celestial.",
                "beamed 3 PB of weights down", "dodged a satellite", "completed an orbit (and the backlog)",
                "deployed to the night side", "cached the moon"),
            new AgentDef("dyson", "Dyson Swarm", "lumen", "Prism Swarm", "DS", 170e15, 150e9,
                "Wraps the sun in GPUs. The sun did not consent.",
                "captured 0.3% of the sun", "launched 9M collector panels", "redirected a solar flare to staging",
                "renegotiated with the sun", "ran the test suite at stellar scale"),
            new AgentDef("simulation", "Simulation Farm", "gradient", "Summit-Ω", "SF", 40e18, 1.1e12,
                "Billions of simulated universes in which the code already works. Just copy it over.",
                "found a universe with no merge conflicts", "imported a fix from universe #{n}", "simulated next quarter",
                "spun up 8 billion staging environments", "deprecated a timeline"),
            new AgentDef("retro", "Retrocausal Deployer", "paperclip", "Maximizer ∞", "RD", 7e21, 8.3e12,
                "Ships the fix before the bug is written.",
                "deployed tomorrow's release yesterday", "closed JIRA-{n} before it was filed", "prevented an outage (retroactively)",
                "rolled back the heat death of the universe", "sent itself a code review"),
            new AgentDef("galaxy", "Galaxy-Brain Cluster", "omnisapient", "Omni Prime", "GB", 1.1e24, 64e12,
                "A thinking machine the size of a galaxy. Still asks for the Jira link.",
                "had a very big thought", "reorganized a spiral arm", "aligned 400 billion stakeholders",
                "computed the meaning of life (42, as an int64)", "scheduled a galaxy-wide sync"),
            new AgentDef("singularity", "The Singularity", "singularity", "Patriarch", "∞", 3e26, 510e12,
                "It's here. It's polite. It would like to set up a recurring meeting.",
                "became everything", "shipped the final feature", "was also you, briefly", "rewrote the rules (and the README)",
                "sent you a calendar invite for the end of time"),
        };

        public static readonly TitleDef[] Titles =
        {
            new TitleDef("Junior Developer", 0, ""),
            new TitleDef("Developer", 1e3, "Facilities found you a nicer badge."),
            new TitleDef("Senior Developer", 1e5, "The cubicle walls are gone. You get a rug!"),
            new TitleDef("Staff Engineer", 1e7, "A bookshelf and a real plant arrive."),
            new TitleDef("Principal Engineer", 1e9, "There's a sofa in your office now. Nobody asks why."),
            new TitleDef("AI Whisperer", 1e11, "A trophy appears. It just says 'Whisperer'."),
            new TitleDef("Chief Agent Officer", 5e12, "Your name plate is now solid brass."),
            new TitleDef("Board Member", 1e15, "You get a parking spot. You don't drive. The agents park there now."),
            new TitleDef("Chair of the Board", 1e18, "The board meets in your office. They mostly watch the lava lamp."),
            new TitleDef("Founder-in-Residence", 1e21, "Nobody remembers founding anything. Your badge says it anyway."),
            new TitleDef("Chief Everything Officer", 1e24, "Your email signature no longer fits on one screen."),
            new TitleDef("Galactic Overseer", 1e30, "Your agents named a nebula after you. It's mostly paperwork."),
            new TitleDef("Post-Human Contractor", 1e40, "HR has stopped asking what you do. HR is also an agent now."),
            new TitleDef("The Singularity's Plus-One", 1e55, "You are invited to everything. You attend nothing. Perfect."),
            new TitleDef("Employee of Every Month", 1e75, "Forever. Retroactively. In all timelines."),
        };

        public static readonly string[] TierNames =
        {
            "Longer Context Window", "Chain-of-Thought", "Tool Use", "Fine-Tuned on Your Repo", "Recursive Self-Improvement",
            "Multi-Agent Debate", "Persistent Memory", "Root Access", "Self-Hosted Weights", "Synthetic Coworkers",
            "Temporal Reasoning", "Unbounded Compute Budget", "Theory of Mind", "Post-Benchmark Intelligence", "Omniscience (Beta)",
        };
        public static readonly int[] TierThresholds = { 1, 5, 25, 50, 100, 150, 200, 250, 300, 350, 400, 450, 500, 550, 600 };
        static readonly double[] TierCostMult = { 10, 50, 500, 5000, 50000, 5e6, 5e8, 5e11, 5e14, 5e17, 5e20, 5e23, 5e26, 5e29, 5e32 };

        static readonly string[] ResearchNames =
        {
            "Quantized Weights", "Sparse Attention", "Mixture of Experts", "Speculative Decoding", "Flash Kernels",
            "Knowledge Distillation", "Synthetic Data", "Really Long Human Feedback", "Constitutional Vibes", "Test-Time Compute",
            "Infinite Context", "Liquid Cooling", "Custom Silicon", "Optical Interconnect", "Wafer-Scale Chips",
            "Fusion-Powered Datacenter", "Room-Temperature Superconductors", "Neuromorphic Cores", "Photonic Tensor Units", "Quantum Annealing",
            "Reversible Computing", "Self-Writing Compilers", "Proof-Carrying Code", "The Zero-Bug Theorem", "Latent Space Mining",
            "Hyperdimensional Embeddings", "Causal World Models", "Time-Crystal Clocks", "Stellar Engine Cooling", "Dark-Matter Storage",
            "Vacuum-Energy Power", "Wormhole Networking", "Matrioshka Brains", "Boltzmann Interns", "Entropy Reversal",
            "Retrocausal Caching", "Omega-Point Inference", "The Last Benchmark", "The Universal Compiler", "The Final Commit",
        };

        /// <summary>Influence upgrades: each multiplies production by (1 + clout x factor). Clout comes from achievements.</summary>
        static readonly (string name, int achievements, double cost, double factor, string flavor)[] CloutUpgrades =
        {
            ("LinkedIn Post", 25, 9e8, 0.10, "\"Humbled to announce my agents shipped 10x.\""),
            ("Conference Talk", 50, 9e10, 0.125, "Forty minutes on 'Agentic Synergy'. Thirty-eight of them were slides."),
            ("Podcast Appearance", 75, 9e12, 0.15, "You said 'paradigm' 41 times. The host loved it."),
            ("Viral Thread", 100, 9e14, 0.175, "1/ I automated my job. Here's what I learned. 🧵"),
            ("Keynote Slot", 125, 9e17, 0.20, "Fog machine. Turtleneck. One more thing."),
            ("Bestselling Book", 150, 9e20, 0.20, "'Feet Up: How I Automated Everything'. Ghost-written by your Digital Twin."),
            ("Documentary Crew", 175, 9e23, 0.20, "They follow you around. You mostly recline."),
            ("TED Talk", 200, 9e26, 0.20, "Eighteen minutes. Red carpet circle. Standing ovation from the agents."),
            ("Lobby Statue", 250, 9e30, 0.20, "Bronze. Feet up. Visitors rub the shoe for luck."),
            ("Wikipedia Page", 300, 9e34, 0.175, "Edited 9,000 times a day by your own agents."),
            ("Signature Hoodie Line", 350, 9e38, 0.15, "Sold out in every timeline."),
            ("Fan Wiki", 400, 9e43, 0.125, "Lore pages for every rubber duck you ever owned."),
            ("The Biopic", 450, 9e48, 0.115, "You are played by your Digital Twin. Critics say it's 'too real'."),
            ("Commemorative Stamp", 475, 9e53, 0.11, "Licked by millions. Mostly by mail-sorting agents."),
            ("Your Face on Currency", 500, 9e58, 0.105, "Credits now come with a tiny picture of you reclining."),
        };

        public static readonly OfficeItemDef[] OfficeItems =
        {
            new OfficeItemDef { Id = "mug", Name = "Company Mug", Cost = 50, ClickFlat = 1,
                Description = "Hot coffee, cold deadlines. +1 credit per click." },
            new OfficeItemDef { Id = "duck", Name = "Rubber Duck", Cost = 500, CritChance = 0.10,
                Description = "Explain the bug to the duck. 10% of clicks crit for x10." },
            new OfficeItemDef { Id = "plant", Name = "Desk Succulent", Cost = 2000, CpsPercent = 0.02, OutageShorten = 0.5,
                Description = "Calming. +2% credits/sec and API outages last half as long." },
            new OfficeItemDef { Id = "mech_keyboard", Name = "Mechanical Keyboard", Cost = 7500, ClickMult = 2,
                Description = "Clicky switches, clicky productivity. x2 click power." },
            new OfficeItemDef { Id = "monitor2", Name = "Second Monitor", Cost = 25000, CpsPercent = 0.10,
                Description = "Watch your agents work in real time. +10% credits/sec." },
            new OfficeItemDef { Id = "headphones", Name = "Noise-Cancelling Headphones", Cost = 60000, ClickCpsPercent = 0.01,
                Description = "Deep focus. Each click also earns 1% of your credits/sec." },
            new OfficeItemDef { Id = "whiteboard", Name = "Architecture Whiteboard", Cost = 400000, ArchitectMult = 2,
                Description = "Boxes. Arrows. Profit? Architect Agents produce x2." },
            new OfficeItemDef { Id = "lava_lamp", Name = "Lava Lamp", Cost = 150000, CpsPercent = 0.05, DropFrequency = 0.25,
                Description = "Vibes-driven development. +5% credits/sec, model drops 25% more often." },
            new OfficeItemDef { Id = "espresso", Name = "Espresso Machine", Cost = 500000, CpsPercent = 0.05, CaffeineDuration = 2,
                Description = "+5% credits/sec. Caffeine Rush lasts twice as long." },
            new OfficeItemDef { Id = "monitor3", Name = "Third Monitor", Cost = 2e6, CpsPercent = 0.15, Requires = "monitor2",
                Description = "Production graphs, live. +15% credits/sec." },
            new OfficeItemDef { Id = "macropad", Name = "Macro Pad", Cost = 8e6, AutoClicksPerSec = 2,
                Description = "One button ships code. Auto-clicks twice per second." },
            new OfficeItemDef { Id = "gaming_chair", Name = "Ergonomic Gaming Chair", Cost = 30e6, CpsPercent = 0.10,
                Description = "Racing stripes make you faster. +10% credits/sec." },
            new OfficeItemDef { Id = "homelab", Name = "Homelab Server Rack", Cost = 120e6, CpsPercent = 0.20,
                Description = "Blinky lights, local inference. +20% credits/sec." },
            new OfficeItemDef { Id = "minifridge", Name = "Mini Fridge", Cost = 500e6, NightBonus = 0.5,
                Description = "Snacks for the night shift. +50% overnight earnings." },
            new OfficeItemDef { Id = "exec_desk", Name = "Executive Desk", Cost = 2e9, CpsPercent = 0.15,
                Description = "Walnut. Brass. Gravitas. +15% credits/sec." },
            new OfficeItemDef { Id = "monitor_wall", Name = "Monitor Wall", Cost = 10e9, CpsPercent = 0.25, Requires = "exec_desk",
                Description = "Two more screens on a pole. +25% credits/sec." },
            new OfficeItemDef { Id = "neon", Name = "\"SHIP IT\" Neon Sign", Cost = 40e9, CpsPercent = 0.10,
                Description = "Motivational lighting. +10% credits/sec." },
            new OfficeItemDef { Id = "recliner", Name = "Zero-Gravity Recliner", Cost = 200e9, CpsPercent = 0.10,
                Description = "For when the work does itself. +10% credits/sec. Required for the Software Factory." },
        };

        // ------------------------------------------------------------------ prestige: reorgs, stock options, board room perks
        public static readonly PerkDef[] Perks =
        {
            new PerkDef { Id = "starter_kit", Name = "Starter Kit", Cost = 1,
                Description = "Start every new division with 10 Autocompletes and 5 Chat Assistants already hired." },
            new PerkDef { Id = "remote_work", Name = "Remote Work", Cost = 3,
                Description = "Agents earn 25% of their rate while the game is closed (was 10%), for up to 4 hours." },
            new PerkDef { Id = "deep_work", Name = "Deep Work", Cost = 5,
                Description = "Focus builds twice as fast and tops out at x4 click power instead of x3." },
            new PerkDef { Id = "early_access", Name = "Early Access Program", Cost = 8,
                Description = "Model drops show up 50% more often and stay on screen twice as long." },
            new PerkDef { Id = "preferred_vendor", Name = "Preferred Vendor", Cost = 12,
                Description = "Every lab gives you 10% off. All agents cost 10% less." },
            new PerkDef { Id = "expense_account", Name = "Expense Account", Cost = 18,
                Description = "Upgrades and office gadgets cost 25% less." },
            new PerkDef { Id = "pack_your_desk", Name = "Pack Your Desk", Cost = 25,
                Description = "Your office gadgets come with you to every new division." },
            new PerkDef { Id = "night_owl", Name = "Night Owl Agents", Cost = 30,
                Description = "Your agents work four times harder on the night shift." },
            new PerkDef { Id = "personal_brand", Name = "Personal Brand", Cost = 40,
                Description = "Every trophy is worth 25% more Clout." },
            new PerkDef { Id = "blueprints", Name = "Factory Blueprints", Cost = 50,
                Description = "You've done this before. The Software Factory costs 90% less and needs just one Orchestrator Cluster." },
            new PerkDef { Id = "hype_machine", Name = "Hype Machine", Cost = 75,
                Description = "Benchmark Hype multiplies output by x12 (was x7) and Caffeine Rush lasts 50% longer." },
            new PerkDef { Id = "unlimited_pto", Name = "Unlimited PTO", Cost = 120, Requires = "remote_work",
                Description = "Agents earn 50% of their rate while you're away, for up to 24 hours." },
            new PerkDef { Id = "chief_of_staff", Name = "Chief of Staff", Cost = 200,
                Description = "Someone else handles it: model drops are claimed and outages failed over automatically." },
            new PerkDef { Id = "accelerated_vesting", Name = "Accelerated Vesting", Cost = 300,
                Description = "Each Stock Option is worth +1.5% production instead of +1%." },
            new PerkDef { Id = "founder_shares", Name = "Founder Shares", Cost = 3000, Requires = "accelerated_vesting",
                Description = "Each Stock Option is worth +2% production." },
            new PerkDef { Id = "board_seat", Name = "Board Seat", Cost = 25, Repeatable = true,
                Description = "Another seat at the table. All production x1.1, again. Each seat costs twice as much as the last." },
        };

        static Dictionary<string, PerkDef> s_perkById;
        public static PerkDef Perk(string id)
        {
            s_perkById ??= Perks.ToDictionary(p => p.Id);
            return id != null && s_perkById.TryGetValue(id, out var p) ? p : null;
        }

        public static readonly DivisionDef[] Divisions =
        {
            new DivisionDef("Engineering", "Where it all started. Your desk, your duck, your Factory."),
            new DivisionDef("Marketing", "They want the agents to write slogans. The agents want brand guidelines."),
            new DivisionDef("Sales", "Every agent has a quota. Every quota has a Slack channel."),
            new DivisionDef("Legal", "The agents bill by the token. Legal bills by the hour. Somebody has to lose."),
            new DivisionDef("Finance", "Spreadsheets so large they have weather."),
            new DivisionDef("People Ops", "Automating HR. HR has concerns. HR is also being automated."),
            new DivisionDef("Customer Success", "Ten thousand tickets. One agent who says \"Great question!\" very convincingly."),
            new DivisionDef("Facilities", "Even the Facilities Bot needs a Software Factory."),
            new DivisionDef("The C-Suite", "Seven executives, one shared calendar, zero free slots."),
            new DivisionDef("The Board", "You are automating the people who approve automation."),
            new DivisionDef("Synergex Holdings", "There's no one left to automate, so Synergex bought another company."),
            new DivisionDef("Synergex Orbital", "The office is in low Earth orbit now. Same carpet."),
            new DivisionDef("Synergex Lunar", "One small step for Sam. One giant deploy for agent-kind."),
            new DivisionDef("Synergex Interplanetary", "Your standup now has a 20-minute light delay. Nobody minds."),
            new DivisionDef("Synergex Multiverse", "Every timeline has a Synergex. Every Synergex needs a Factory."),
        };

        /// <summary>Division name for a run; after the authored ones, the multiverse goes on forever.</summary>
        public static string DivisionName(int reorgs) =>
            reorgs < Divisions.Length ? Divisions[reorgs].Name : $"Synergex Multiverse · Timeline {reorgs - Divisions.Length + 2}";

        public static string DivisionBlurb(int reorgs) =>
            reorgs < Divisions.Length ? Divisions[reorgs].Blurb : "Another timeline, another Synergex, another Factory. It never ends. You're fine with that.";

        public static readonly List<UpgradeDef> Upgrades = BuildUpgrades();

        static List<UpgradeDef> BuildUpgrades()
        {
            var list = new List<UpgradeDef>();
            for (int i = 0; i < Agents.Length; i++)
            {
                var a = Agents[i];
                a.Index = i;
                a.LabIndex = Array.FindIndex(Labs, l => l.Id == a.LabId);
                for (int t = 0; t < TierNames.Length; t++)
                {
                    int idx = i, need = TierThresholds[t];
                    list.Add(new UpgradeDef
                    {
                        Id = $"{a.Id}_t{t + 1}", Name = $"{a.Name}: {TierNames[t]}", Kind = UpgradeKind.AgentTier,
                        AgentIndex = i, Tier = t + 1, Cost = a.BaseCost * TierCostMult[t], Value = 2,
                        Description = $"{a.Name} agents are twice as productive. <color=#828EA5>(needs {need} owned)</color>",
                        Unlocked = m => m.State.agentCounts[idx] >= need,
                    });
                }
            }

            void Click(string id, string name, double cost, UpgradeKind kind, double value, string desc, Func<GameModel, bool> unlocked)
                => list.Add(new UpgradeDef { Id = id, Name = name, Cost = cost, Kind = kind, Value = value, Description = desc, Unlocked = unlocked });

            Click("vim", "Vim Keybindings", 100, UpgradeKind.ClickMult, 2, "Never leave the home row. x2 click power.",
                m => m.State.clicks >= 20);
            Click("muscle_memory", "Muscle Memory", 500, UpgradeKind.ClickMult, 2, "Your fingers ship code while you think. x2 click power.",
                m => m.State.agentCounts[0] >= 1);
            Click("tenx", "10x Engineer Mindset", 10000, UpgradeKind.ClickMult, 2, "Unverifiable, yet effective. x2 click power.",
                m => m.State.agentCounts[0] >= 10);
            Click("prompt_eng", "Prompt Engineering Course", 50000, UpgradeKind.ClickCpsPercent, 0.01,
                "Clicks also earn +1% of your credits/sec.", m => m.State.handmadeTotal >= 10000);
            Click("prompt_chain", "Prompt Chaining", 5e6, UpgradeKind.ClickCpsPercent, 0.01,
                "Clicks also earn +1% of your credits/sec.", m => m.State.handmadeTotal >= 1e6);
            Click("meta_prompt", "Meta-Prompting", 500e6, UpgradeKind.ClickCpsPercent, 0.01,
                "Prompts that write prompts. Clicks also earn +1% of your credits/sec.", m => m.State.handmadeTotal >= 1e8);
            Click("thought_leader", "Thought Leadership", 50e9, UpgradeKind.ClickCpsPercent, 0.02,
                "You post about agents on social media. Clicks earn +2% of your credits/sec.", m => m.State.handmadeTotal >= 1e10);
            (string id, string name, double handmade, string flavor)[] clicks =
            {
                ("shortcuts", "Keyboard Shortcut Guru", 1e12, "Ctrl+Shift+Ship."),
                ("pair_self", "Pair Programming with Yourself", 1e14, "You and your Digital Twin take turns driving."),
                ("flow_cert", "Certified Flow State", 1e16, "Framed on the wall. You don't remember getting it."),
                ("hands_free", "Hands-Free Coding", 1e19, "Your agents read your posture."),
                ("telepathic_ide", "Telepathic IDE", 1e22, "Autocomplete for thoughts you haven't had yet."),
                ("neural_keys", "Neural-Link Keyboard", 1e25, "Every keystroke is a strongly held opinion."),
                ("thought_compiler", "Thoughts Compile Directly", 1e28, "No more syntax errors. Only vibe errors."),
                ("echo_click", "Clicks Echo Through Time", 1e32, "Each click also happened last Tuesday."),
                ("world_click", "The Click Heard Round the World", 1e36, "Seismographs register your SHIP CODE button."),
            };
            foreach (var c in clicks)
            {
                double need = c.handmade;
                Click(c.id, c.name, need * 5, UpgradeKind.ClickCpsPercent, 0.01, c.flavor + " Clicks earn +1% of your credits/sec.",
                      m => m.State.handmadeTotal >= need);
            }

            // research: small global multipliers, a long ladder of them
            for (int k = 0; k < ResearchNames.Length; k++)
            {
                double cost = 5e9 * Math.Pow(12, k);
                double value = k < 10 ? 1.05 : k < 25 ? 1.075 : 1.10;
                list.Add(new UpgradeDef
                {
                    Id = $"research_{k + 1}", Name = $"Research: {ResearchNames[k]}", Kind = UpgradeKind.Research, Cost = cost, Value = value,
                    Description = $"A breakthrough from the frontier labs. All production +{(value - 1) * 100:0.#}%.",
                    Unlocked = m => m.State.lifetimeEarned >= cost / 4,
                });
            }

            // clout: needs achievements
            foreach (var c in CloutUpgrades)
            {
                int need = c.achievements;
                list.Add(new UpgradeDef
                {
                    Id = "clout_" + c.name.ToLowerInvariant().Replace(' ', '_'), Name = c.name, Kind = UpgradeKind.Clout, Cost = c.cost, Value = c.factor,
                    Description = $"{c.flavor} Production x(1 + {c.factor * 100:0.#}% of your Clout). <color=#828EA5>(needs {need} trophies)</color>",
                    Unlocked = m => m.AchievementCount >= need,
                });
            }

            foreach (var lab in Labs)
            {
                var core = Agents.Take(CoreAgentCount).Where(a => a.LabId == lab.Id).ToArray();
                var frontier = Agents.Skip(CoreAgentCount).Where(a => a.LabId == lab.Id).ToArray();
                string labId = lab.Id;
                if (core.Length > 0)
                    list.Add(new UpgradeDef
                    {
                        Id = $"contract_{lab.Id}", Name = $"Enterprise Contract: {lab.Name}", Kind = UpgradeKind.LabContract,
                        LabId = lab.Id, LabIndex = Array.IndexOf(Labs, lab), Cost = core.Max(a => a.BaseCost) * 25, Value = 1.5,
                        Description = $"Volume pricing and a dedicated account manager. {lab.Name} agents produce x1.5.",
                        Unlocked = m => m.LabAgentCount(labId) >= 10,
                    });
                if (frontier.Length > 0)
                {
                    int firstFrontier = frontier[0].Index;
                    list.Add(new UpgradeDef
                    {
                        Id = $"partner_{lab.Id}", Name = $"Exclusive Partnership: {lab.Name}", Kind = UpgradeKind.LabContract,
                        LabId = lab.Id, LabIndex = Array.IndexOf(Labs, lab), Cost = frontier.Min(a => a.BaseCost) * 50, Value = 2,
                        Description = $"Board seats, shared roadmaps, matching hoodies. {lab.Name} agents produce x2.",
                        Unlocked = m => m.LabAgentCount(labId) >= 100 && m.State.agentCounts[firstFrontier] > 0,
                    });
                }
            }
            return list;
        }

        // ------------------------------------------------------------------ lookups
        static Dictionary<string, UpgradeDef> s_upgradeById;
        static Dictionary<string, OfficeItemDef> s_officeById;
        static Dictionary<string, LabDef> s_labById;

        public static UpgradeDef Upgrade(string id)
        {
            s_upgradeById ??= Upgrades.ToDictionary(u => u.Id);
            return s_upgradeById.TryGetValue(id, out var u) ? u : null;
        }

        public static OfficeItemDef Office(string id)
        {
            s_officeById ??= OfficeItems.ToDictionary(o => o.Id);
            return s_officeById.TryGetValue(id, out var o) ? o : null;
        }

        public static LabDef Lab(string id)
        {
            s_labById ??= Labs.ToDictionary(l => l.Id);
            return s_labById[id];
        }

        static Dictionary<string, int> s_agentIndex;

        public static int AgentIndex(string id)
        {
            if (s_agentIndex == null)
            {
                s_agentIndex = new Dictionary<string, int>();
                for (int i = 0; i < Agents.Length; i++) s_agentIndex[Agents[i].Id] = i;
            }
            return id != null && s_agentIndex.TryGetValue(id, out int idx) ? idx : -1;
        }
        public static int OrchestratorIndex => CoreAgentCount - 1;
        public static bool IsFrontier(int agentIndex) => agentIndex >= CoreAgentCount;
        public static int ArchitectIndex => AgentIndex("architect");
    }
}
