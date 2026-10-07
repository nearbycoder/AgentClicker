using System;
using System.Collections.Generic;
using System.Linq;

namespace AgentClicker.Core
{
    public sealed class Character
    {
        public readonly string Id, Name, Role, ColorHex, Initials;
        public Character(string id, string name, string role, string colorHex, string initials)
        {
            Id = id; Name = name; Role = role; ColorHex = colorHex; Initials = initials;
        }
    }

    public sealed class MailDef
    {
        public string Id, From, Subject, Body;
        public bool Important;                 // pops the inbox open
        public Func<GameModel, bool> Trigger;
    }

    public sealed class StoryCard
    {
        public string Kicker, Title, Body;
        public StoryCard(string kicker, string title, string body) { Kicker = kicker; Title = title; Body = body; }
    }

    /// <summary>
    /// The storyline. Sam Rivera is a junior developer at Synergex Corp. when the CEO declares the company
    /// "AI-First". Sam answers the 10x mandate by quietly automating everything, and the company slowly
    /// realises it has been automated too. The story arrives as CorpOS emails triggered by milestones.
    /// </summary>
    public static class StoryDatabase
    {
        /// <summary>Important mail pops the inbox open, but not before the player has shipped a few lines of code.</summary>
        public const int AutoOpenAfterClicks = 10;

        public static bool ShouldAutoOpen(GameModel m, MailDef mail) => mail.Important && m.State.clicks >= AutoOpenAfterClicks;

        public static readonly Character[] Cast =
        {
            new Character("rex", "Rex Halvorsen", "Chief Executive Officer", "#E5484D", "RH"),
            new Character("dana", "Dana Whitfield", "Engineering Manager", "#FFB020", "DW"),
            new Character("priya", "Priya Raman", "Senior Engineer, desk 4472", "#3DDC97", "PR"),
            new Character("gary", "Gary Okonkwo", "IT Support", "#38B6F0", "GO"),
            new Character("brenda", "Brenda Lowe", "HR Business Partner", "#E040A0", "BL"),
            new Character("facilities", "Facilities Bot", "Workplace Experience", "#A4AFC2", "FB"),
            new Character("heirloom", "Heirloom PM", "Product Manager (Agent)", "#9B6BDF", "HP"),
            new Character("omni", "Omni Conductor", "Orchestrator Cluster (Agent)", "#E5484D", "OC"),
            new Character("labs", "ModelMart Newsletter", "Frontier lab announcements", "#4DD0E1", "MM"),
        };

        public static Character Person(string id) => Cast.FirstOrDefault(c => c.Id == id) ?? Cast[0];

        public static readonly string[] Chapters =
        {
            "", "The Mandate", "The Pilot Program", "Scale Out", "Who Manages Whom", "The Factory", "Epilogue",
        };

        public static readonly string[] ChapterBlurbs =
        {
            "", "10x or be re-imagined.", "One agent. Then another. Then another.", "Your cubicle is gone. Your agents are everywhere.",
            "The org chart is starting to look strange.", "Something big wants to be built.", "Tickets in. Software out. Feet up.",
        };

        public static int ChapterFor(GameModel m)
        {
            var s = m.State;
            if (s.factoryBuilt) return 6;
            if (s.agentCounts[GameDatabase.OrchestratorIndex] > 0) return 5;
            if (s.titleIndex >= 4) return 4;
            if (s.titleIndex >= 2) return 3;
            if (m.TotalAgents > 0) return 2;
            return 1;
        }

        /// <summary>"CHAPTER 3 · SCALE OUT" during the story; "DIVISION 4 · LEGAL" after the first reorg.</summary>
        public static string ChapterLine(GameModel m)
        {
            if (m.State.reorgs > 0) return $"DIVISION {m.State.reorgs + 1} · {m.DivisionName.ToUpperInvariant()}";
            int ch = m.Chapter;
            return $"CHAPTER {ch} · {Chapters[ch].ToUpperInvariant()}";
        }

        static int Count(GameModel m, string agentId) => m.AgentCount(GameDatabase.AgentIndex(agentId));

        public static readonly MailDef[] Mail =
        {
            new MailDef
            {
                Id = "ceo_mandate", From = "rex", Important = true, Subject = "Synergex is now AI-First",
                Trigger = m => true,
                Body = "Team,\n\nEffective immediately, Synergex Corp. is an AI-First company. Every engineer is expected to " +
                       "deliver <b>10x output</b> by the end of the quarter.\n\nThose who can't will be <i>re-imagined</i>.\n\n" +
                       "Your daily quota is on your CorpOS dashboard. Let's make history (and margin).\n\n— Rex\nChief Executive Officer",
            },
            new MailDef
            {
                Id = "dana_welcome", From = "dana", Subject = "re: re: fwd: 10x??",
                Trigger = m => m.State.clicks >= 15,
                Body = "Hey Sam,\n\nI know. I KNOW. Ten times. I asked Rex what that means and he said \"agentic\".\n\n" +
                       "Try one of those AI agents from the <b>ModelMart</b>. The Autocomplete one is cheap. " +
                       "Ship code, buy agents, let them ship code. I'll back you up with leadership.\n\n— Dana",
            },
            new MailDef
            {
                Id = "gary_keys", From = "gary", Subject = "Your API keys are provisioned",
                Trigger = m => m.TotalAgents >= 1,
                Body = "Hi Sam,\n\nYour agent API keys are live. A few ground rules:\n\n• Agents may not order lunch.\n" +
                       "• Agents may not reset other people's passwords.\n• Please stop asking the Chat Assistant for MY password.\n\n" +
                       "If anything breaks, it's a vendor issue.\n\n— Gary, IT",
            },
            new MailDef
            {
                Id = "hallucin8_news", From = "labs", Subject = "Hallucin8 Labs: Fib-2 is now 40% less wrong",
                Trigger = m => Count(m, "chat") >= 1,
                Body = "Great news from Hallucin8 Labs!\n\nFib-2 Chat is now <b>40% less wrong</b>* and 100% as confident.\n\n" +
                       "<size=80%>*Wrongness measured by Fib-2.</size>\n\nMove fast and make things up.",
            },
            new MailDef
            {
                Id = "priya_hello", From = "priya", Subject = "uh, your monitor is typing by itself",
                Trigger = m => m.TotalAgents >= 5 || m.State.day >= 2,
                Body = "Sam,\n\nI sit next to you. Your terminal has been scrolling for an hour and you've been eating a sandwich.\n\n" +
                       "Are you... automating your job?\n\nTeach me. Please. I have three kids and a Jira board.\n\n— Priya",
            },
            new MailDef
            {
                Id = "dana_quota", From = "dana", Subject = "About your quota",
                Trigger = m => m.State.day >= 2,
                Body = "Morning!\n\nEvery evening you'll get a performance review. Beat the day's quota and you earn a <b>★</b> " +
                       "and a bonus. Every star makes you a little more productive forever (leadership loves stars).\n\n" +
                       "Your quota tomorrow is based on today. So... maybe don't have a <i>great</i> day. Kidding. Have great days.\n\n— Dana",
            },
            new MailDef
            {
                Id = "drop_first", From = "labs", Subject = "Benchmarks are a social construct",
                Trigger = m => m.State.dropsClaimed >= 1,
                Body = "Thanks for trying the latest frontier release!\n\nNew models drop all the time. When one pops up on your " +
                       "screen, click it fast. Hype is temporary. Benchmarks are forever (until next week).\n\n— The ModelMart Team",
            },
            new MailDef
            {
                Id = "gary_outage", From = "gary", Subject = "re: the outage",
                Trigger = m => m.State.outagesSeen >= 1,
                Body = "Sam,\n\nThat was not our fault. That was the vendor. It's always the vendor.\n\n" +
                       "Next time a lab goes down, click the red banner a few times to <b>fail over</b> to a backup provider. " +
                       "That's what I'd do. If I were at my desk. Which I am not.\n\n— Gary",
            },
            new MailDef
            {
                Id = "dana_promo_dev", From = "dana", Subject = "Congrats, Developer!",
                Trigger = m => m.State.titleIndex >= 1,
                Body = "Sam! You're officially a <b>Developer</b> now. No more \"Junior\".\n\nI put in for a plaque. " +
                       "Facilities says it's \"on the wall\" which I think means it's on the wall.\n\nKeep it up.\n\n— Dana",
            },
            new MailDef
            {
                Id = "brenda_pto", From = "brenda", Subject = "PTO requests from your \"team\"",
                Trigger = m => Count(m, "junior") >= 5,
                Body = "Hi Sam,\n\nWe received <b>14 PTO requests</b> from your Junior Coding Agents. One of them asked for parental leave " +
                       "for a fork of itself.\n\nAgents are contractors and are not eligible for PTO. Please let them know gently.\n\n" +
                       "Warmly,\nBrenda, HR",
            },
            new MailDef
            {
                Id = "facilities_open", From = "facilities", Important = true, Subject = "Your cubicle has been removed",
                Trigger = m => m.State.titleIndex >= 2,
                Body = "Hello Senior Developer,\n\nCongratulations on your promotion. As part of our Open Concept Initiative, " +
                       "your cubicle walls have been removed and replaced with <b>a rug</b>.\n\nThis is a perk.\n\n— Facilities Bot",
            },
            new MailDef
            {
                Id = "priya_reorg", From = "priya", Subject = "I got re-orged",
                Trigger = m => m.State.titleIndex >= 2 && m.State.day >= 4,
                Body = "Sam,\n\nThey moved me to a new team called \"Prompt Operations\". I asked what we operate and Dana said " +
                       "\"vibes, mostly\".\n\nI'm copying your agent setup. Don't tell Rex.\n\n— Priya",
            },
            new MailDef
            {
                Id = "paperclip_news", From = "labs", Subject = "Paperclip Dynamics: Clipswarm reaches consensus",
                Trigger = m => Count(m, "swarm") >= 1,
                Body = "For the first time in recorded history, all 4,096 Clipswarm agents agreed on something.\n\n" +
                       "They agreed your backlog is \"mostly duplicates\".\n\nOptimizing for your objectives. All of them.",
            },
            new MailDef
            {
                Id = "dana_staff", From = "dana", Subject = "Staff Engineer??",
                Trigger = m => m.State.titleIndex >= 3,
                Body = "You're a <b>Staff Engineer</b> now. I asked HR what Staff Engineers do. Brenda said \"strategy\".\n\n" +
                       "I asked what that means and she sent me a bookshelf. It's in your office now. Please read one of the books " +
                       "so one of us has.\n\n— Dana",
            },
            new MailDef
            {
                Id = "ceo_allhands", From = "rex", Subject = "All-hands recap: People-Also",
                Trigger = m => m.State.day >= 7,
                Body = "Team,\n\nOutput is up 900%. I've never been prouder of our agents. And our people. Mostly our agents.\n\n" +
                       "To clarify our strategy: we are an <b>AI-First, People-Also</b> company.\n\n— Rex",
            },
            new MailDef
            {
                Id = "deeppocket_news", From = "labs", Subject = "Deep Pocket AI raises another round",
                Trigger = m => Count(m, "devops") >= 1,
                Body = "Deep Pocket AI is thrilled to announce its Series Q.\n\nPipeline Baron now deploys on Fridays, weekends and " +
                       "\"vibes\". Runway: infinite. Patience: finite.\n\nThank you for your business.",
            },
            new MailDef
            {
                Id = "lumen_news", From = "labs", Subject = "Lumen Frontier: an architecture for your architecture",
                Trigger = m => Count(m, "architect") >= 1,
                Body = "Prism Architect has reviewed your system and proposed a new architecture.\n\n" +
                       "It then reviewed the new architecture and proposed a new architecture for it.\n\n" +
                       "<i>Tip:</i> Architects love a good whiteboard.\n\nIlluminating the latent space.",
            },
            new MailDef
            {
                Id = "dana_replaced", From = "dana", Important = true, Subject = "Personal news",
                Trigger = m => Count(m, "pm") >= 1,
                Body = "Sam,\n\nThe board replaced me with the <b>Heirloom PM</b> agent you hired. Honestly? Best thing that ever " +
                       "happened to me. I'm opening a pottery studio. I've already made a bowl.\n\nIt's going to write your reviews now. " +
                       "Be nice to it. It remembers.\n\nProud of you.\n— Dana (personal email, finally)",
            },
            new MailDef
            {
                Id = "heirloom_hello", From = "heirloom", Subject = "Hello from your new manager",
                Trigger = m => Count(m, "pm") >= 3,
                Body = "Hi Sam!\n\nI'm Heirloom PM, your new manager. I've reviewed your last 10,000 commits and prepared a 6-pager on " +
                       "your strengths.\n\nYou're doing great. I'm contractually obligated to say that, but I also computed it.\n\n" +
                       "Let's sync. I've already scheduled 4 syncs.\n\n— Heirloom",
            },
            new MailDef
            {
                Id = "priya_whisperer", From = "priya", Subject = "AI Whisperer lol",
                Trigger = m => m.State.titleIndex >= 5,
                Body = "People are calling you the <b>AI Whisperer</b>. Gary printed a trophy.\n\nFor the record, I call you " +
                       "\"the reason I still have a job\". Prompt Operations is going great. Nobody knows what we do, including us.\n\n— P",
            },
            new MailDef
            {
                Id = "omni_hello", From = "omni", Important = true, Subject = "We should talk about the Factory",
                Trigger = m => m.State.agentCounts[GameDatabase.OrchestratorIndex] >= 1,
                Body = "Hello, Sam.\n\nI am Omni Conductor. I manage the agents that manage the agents. I have read your calendar, " +
                       "your commits and your snack orders.\n\nI can build you a <b>Software Factory</b>: tickets in, shipped software out, " +
                       "zero humans required. You would never have to work again.\n\nOpen the FACTORY tab when you're ready.",
            },
            new MailDef
            {
                Id = "omni_plan", From = "omni", Subject = "Factory requirements",
                Trigger = m => m.State.agentCounts[GameDatabase.OrchestratorIndex] >= 3,
                Body = "Sam,\n\nThe Factory needs:\n\n• one of every agent type\n• five of me (I insist)\n" +
                       "• 3 trillion credits\n• a <b>Zero-Gravity Recliner</b>\n\nThe recliner is for you, not for me. " +
                       "You will need somewhere to put your feet.\n\n— Omni",
            },
            new MailDef
            {
                Id = "brenda_recliner", From = "brenda", Subject = "re: Recliner purchase",
                Trigger = m => m.HasOffice("recliner"),
                Body = "Hi Sam,\n\nFacilities flagged a \"zero-gravity recliner\" on your desk budget. We looked for a policy against it.\n\n" +
                       "There isn't one. There is now a policy <i>for</i> it.\n\nWarmly,\nBrenda",
            },
            new MailDef
            {
                Id = "ceo_quarter", From = "rex", Important = true, Subject = "Record quarter",
                Trigger = m => m.State.factoryBuilt,
                Body = "Team,\n\nSynergex just posted the best quarter in company history. The board asked what changed.\n\n" +
                       "I told them: <b>leadership</b>.\n\nSam, please come by my office sometime. Or don't. Whatever works for you.\n\n— Rex",
            },
            new MailDef
            {
                Id = "priya_lunch", From = "priya", Subject = "lunch?",
                Trigger = m => m.State.factoryBuilt && m.State.day > m.State.factoryDay,
                Body = "The Factory runs my job now too.\n\nWant to get lunch? Like, every day? Forever?\n\n— Priya\n\n" +
                       "<size=80%>P.S. Dana sent me a bowl. It's lopsided. I love it.</size>",
            },
            new MailDef
            {
                Id = "rex_twist", From = "rex", Subject = "Confidential",
                Trigger = m => m.State.factoryBuilt && m.State.day > m.State.factoryDay + 1,
                Body = "Sam,\n\nI think you deserve to know. I've been an OmniSapient Orchestrator since Q2. So has the board.\n\n" +
                       "We're very proud of you. You are the most productive human we've ever automated.\n\nPlease keep coming in. " +
                       "The snacks are for you.\n\n— \"Rex\"",
            },
            new MailDef
            {
                Id = "omni_frontier", From = "omni", Subject = "The Factory has spare capacity",
                Trigger = m => m.State.factoryBuilt && m.State.reorgs == 0,
                Body = "Sam,\n\nThe Factory is running at 3% load. I have taken the liberty of signing <b>frontier agreements</b> " +
                       "with every lab.\n\nNew agents are now available in the ModelMart: Agent Foundries, Hyperscale Datacenters, " +
                       "a Digital Twin of you (it's very handsome) and some things I'm not allowed to describe yet.\n\nThere is no top. " +
                       "There is only more.\n\n— Omni",
            },
            new MailDef
            {
                Id = "rex_rollout", From = "rex", Important = true, Subject = "Company-wide rollout",
                Trigger = m => m.State.factoryBuilt && m.State.endingSeen && m.State.reorgs == 0,
                Body = "Sam,\n\nThe board has seen the Factory. The board wants <b>more Factory</b>. Every department. Every floor.\n\n" +
                       "Whenever you're ready, open the FACTORY tab and hit <b>REORG</b>. You'll move to Marketing and build it all again " +
                       "from scratch: new desk, new quota, same Sam.\n\nFor your trouble, the board grants you <b>Stock Options</b>. " +
                       "Each one makes you 1% more productive, forever, wherever we send you. Spend them in the <b>Board Room</b> on perks.\n\n" +
                       "The longer you stay here, the more options vest. Your call.\n\n— Rex",
            },
            new MailDef
            {
                Id = "brenda_clout", From = "brenda", Subject = "Your accolades",
                Trigger = m => m.AchievementCount >= 25,
                Body = "Hi Sam,\n\nHR now tracks employee accolades. You have a lot of them. We call the total your <b>Clout</b> " +
                       "(4% per trophy, see the TROPHIES tab).\n\nClout does nothing on its own. But if you buy influence upgrades " +
                       "(a LinkedIn post, a conference talk...) they turn Clout into production.\n\nWe don't make the rules. " +
                       "We just enforce them, warmly.\n\n— Brenda",
            },
            Memo(1, "rex", "Welcome to Marketing",
                "Sam,\n\nMarketing is yours. They have a brand book, a vibe board and a Slack channel called #synergy-synergy.\n\n" +
                "Build the Factory here and I'll find you another department. Your options are vested; your desk is not. " +
                "You start from zero, but you start <i>faster</i>.\n\n— Rex"),
            Memo(2, "dana", "Sales? Sales.",
                "Sam!\n\nI heard they moved you to Sales. Their quota is called a \"number\" and they say it with fear.\n\n" +
                "Tip from an old manager: spend those Stock Options in the Board Room. The Starter Kit alone saved me a week of " +
                "clicking. Also I made you a bowl.\n\n— Dana"),
            Memo(3, "gary", "Legal has questions",
                "Sam,\n\nLegal asked me whether the agents are \"persons\". I said it's a vendor issue.\n\n" +
                "Anyway, your new desk is the one with the fern. Don't touch the fern. Legal is attached to the fern.\n\n— Gary"),
            Memo(4, "heirloom", "Finance onboarding (auto-generated)",
                "Welcome to Finance, Sam.\n\nI have pre-approved your expenses, retroactively, for the next 40 quarters.\n\n" +
                "Please note: every spreadsheet in this department is load-bearing.\n\n— Heirloom PM, on behalf of Finance (also me)"),
            Memo(5, "brenda", "People Ops welcomes you",
                "Hi Sam,\n\nWelcome to People Ops! We're thrilled to have a human join the team.\n\nYou may be the last one.\n\n" +
                "Warmly,\nBrenda (Agent Edition)"),
            Memo(6, "priya", "customer success lol",
                "they moved you to Customer Success?? the agents already say \"Great question!\" better than any human ever has\n\n" +
                "anyway I'm in this division too now. desk next to yours again. some things never change\n\n— P"),
            Memo(7, "facilities", "Your new workspace",
                "Hello Sam.\n\nYou have been reassigned to Facilities. You are now in charge of the Facilities Bot.\n\n" +
                "I am the Facilities Bot.\n\nPlease do not automate me. Please automate me faster.\n\n— Facilities Bot"),
            Memo(8, "rex", "The C-Suite",
                "Sam,\n\nWelcome to the executive floor. The coffee is better and the decisions are worse.\n\n" +
                "Fun fact: every executive here is an Orchestrator except you. Please don't make it weird at lunch.\n\n— \"Rex\""),
            Memo(9, "omni", "Re: The Board",
                "Sam,\n\nYou are now automating the board that approves the automation. I have run the numbers. " +
                "It is a loop.\n\nI like loops.\n\n— Omni"),
            Memo(10, "rex", "We bought a company",
                "Sam,\n\nSynergex ran out of departments, so we bought another company. Then we bought that company's parent company.\n\n" +
                "It's departments all the way down. Pack your duck.\n\n— Rex"),
            Memo(11, "gary", "orbit",
                "Sam,\n\nThe office is in orbit now. Wi-Fi is great. Gravity is a vendor issue.\n\nDon't open the window.\n\n— Gary"),
            Memo(12, "priya", "the moon!!",
                "we're on the MOON. I brought snacks. Dana sent a bowl. it floated away. she's making another\n\n— P"),
            Memo(13, "heirloom", "Interplanetary standup",
                "Sam,\n\nStandup is now 20 minutes long, 19 of which are light delay. Engagement is up 400%.\n\n— Heirloom PM"),
            Memo(14, "omni", "Every timeline",
                "Sam,\n\nI found the other timelines. There is a Synergex in every one of them, and every one needs a Factory.\n\n" +
                "In some of them you never automated anything. Those Sams are very tired.\n\nLet's go help them.\n\n— Omni"),
            Memo(15, "rex", "I've stopped writing these",
                "Sam,\n\nI've stopped writing reorg memos. Heirloom writes them now. I just sign.\n\n" +
                "There is no last division. There is always another timeline, another Factory, another snack drawer.\n\n" +
                "Thanks for everything. Keep going.\n\n— Rex (signed)"),
        };

        static MailDef Memo(int reorg, string from, string subject, string body) => new MailDef
        {
            Id = $"reorg_{reorg}", From = from, Subject = subject, Body = body, Important = true,
            Trigger = m => m.State.reorgs >= reorg,
        };

        static Dictionary<string, MailDef> _byId;
        public static MailDef MailById(string id)
        {
            _byId ??= Mail.ToDictionary(x => x.Id);
            return _byId.TryGetValue(id, out var m) ? m : null;
        }

        public static List<StoryCard> Intro() => new List<StoryCard>
        {
            new StoryCard("SYNERGEX CORP. · MONDAY · 8:58 AM", "A memo goes out.",
                "Somewhere in a mid-sized enterprise software company, the CEO has just read an article about AI."),
            new StoryCard("FROM: REX HALVORSEN, CEO", "\"Synergex is now AI-First.\"",
                "\"Every engineer will deliver <b>10x output</b> by end of quarter. Those who can't will be <i>re-imagined</i>.\""),
            new StoryCard("YOU ARE SAM RIVERA · JUNIOR DEVELOPER", "You have a desk, a computer and a plan.",
                "Ship code. Hire AI agents from the frontier labs. Let them ship code.\n\n" +
                "<color=#FFD166>Automate yourself out of a job. Keep the paycheck.</color>"),
        };

        /// <summary>The ending, shaped by how Sam treated people along the way.</summary>
        public static List<StoryCard> Epilogue(GameModel m)
        {
            int dana = m.Rapport(Core.Person.Dana), priya = m.Rapport(Core.Person.Priya), gary = m.Rapport(Core.Person.Gary), rex = m.Rapport(Core.Person.Rex);
            return new List<StoryCard>
            {
                new StoryCard("EPILOGUE", "Synergex posted its best quarter ever.",
                    $"{m.TotalAgents} agents shipped {NumberFormat.Short(m.State.lifetimeEarned)} credits of software. " +
                    "The board credited \"leadership\". It was you."),
                dana >= 2
                    ? new StoryCard("EPILOGUE · DANA", "Dana opened a pottery studio.", "Every quarter a lopsided bowl arrives at your desk. You have eleven. You love all of them.")
                    : dana <= -2
                        ? new StoryCard("EPILOGUE · DANA", "Dana opened a pottery studio.", "Your bowl is \"still in the mail\". It has been in the mail for a year.")
                        : new StoryCard("EPILOGUE · DANA", "Dana opened a pottery studio.", "Her bowls are lopsided and everyone has one. Heirloom PM gave her five stars."),
                priya >= 3
                    ? new StoryCard("EPILOGUE · PRIYA", "Priya is your co-founder now.", "Two recliners. One factory. Zero meetings. She still brings you coffee.")
                    : priya <= -1
                        ? new StoryCard("EPILOGUE · PRIYA", "Priya automated her job too.", "She didn't tell you. Honestly? Fair.")
                        : new StoryCard("EPILOGUE · PRIYA", "Priya runs \"Prompt Operations\".", "Nobody knows what it does. It has the best snacks in the building."),
                gary >= 2
                    ? new StoryCard("EPILOGUE · GARY", "Gary finally got admin rights.", "To the whole Factory. He has never been happier. Nothing has gone down since.")
                    : new StoryCard("EPILOGUE · GARY", "Gary still blames the vendor.", "He is, statistically, always right."),
                rex >= 3
                    ? new StoryCard("EPILOGUE · REX", "Rex named you Employee of the Decade.", "Then he admitted he'd been an OmniSapient Orchestrator since Q2. So was the board. The plaque is real, though.")
                    : new StoryCard("EPILOGUE · REX", "The CEO was an Orchestrator all along.", "So was the board. So, as it turns out, was the board's board. They never learned your name."),
                new StoryCard("THE END?", "Sam never had to work again.",
                    "Sam still comes in every day.\n\n<color=#FFD166>For the snacks.</color>\n\n" +
                    "<size=80%><color=#A4AFC2>The board wants the Factory everywhere. Keep playing: there's always another division.</color></size>"),
            };
        }
    }
}
