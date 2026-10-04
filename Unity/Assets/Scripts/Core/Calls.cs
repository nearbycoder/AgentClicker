using System;
using System.Collections.Generic;
using System.Linq;

namespace AgentClicker.Core
{
    /// <summary>People whose opinion of Sam changes with phone-call choices. Each unlocks a perk at rapport ≥ 3.</summary>
    public enum Person { Dana = 0, Priya = 1, Gary = 2, Rex = 3 }

    public sealed class CallChoice
    {
        public string Label, Response;
        public int Dana, Priya, Gary, Rex;   // rapport deltas
        public double CpsSeconds;            // credits worth this many seconds of production...
        public double MinCredits;            // ...but at least this much
        public float HypeMult, HypeSeconds;  // credits/sec buff
        public float CaffeineSeconds;        // click buff
        public int LoseMinutes;              // meetings eat the work day
        public float Discount;               // off the next agent order
        public bool StartOutage, PreventOutage;
        public bool KeepsFocus;              // short calls don't break your flow
    }

    public sealed class CallDef
    {
        public string Id, Caller, CallerName, Role, ColorHex, Initials;
        public string Opening;
        public CallChoice[] Choices;
        public bool Story;                   // one-time, triggered by progress; ignoring it costs rapport
        public int MinChapter = 1, MaxChapter = 6;
        public float Weight = 1;
        public Func<GameModel, bool> Trigger;

        public string Name => CallerName ?? StoryDatabase.Person(Caller).Name;
        public string RoleText => Role ?? StoryDatabase.Person(Caller).Role;
        public string Color => ColorHex ?? StoryDatabase.Person(Caller).ColorHex;
        public string Monogram => Initials ?? StoryDatabase.Person(Caller).Initials;

        /// <summary>Who takes it personally if Sam declines or misses this call.</summary>
        public Person? Offended => Caller switch
        {
            "dana" => Person.Dana, "priya" => Person.Priya, "gary" => Person.Gary, "rex" => Person.Rex, _ => (Person?)null,
        };
    }

    public static class CallDatabase
    {
        public const float RingSeconds = 14f;
        public const float FirstCallDelay = 95f, MinDelay = 90f, MaxDelay = 170f;

        public static readonly string[] PerkNames =
        {
            "Dana has your back: daily quotas are 15% lower.",
            "Priya covers for you: API outages end 30% sooner.",
            "Gary watches your systems: outages happen 40% less often.",
            "Rex is a fan: performance bonuses are 15% instead of 10%.",
        };

        public static readonly string[] PeopleNames = { "Dana Whitfield", "Priya Raman", "Gary Okonkwo", "Rex Halvorsen" };

        public static readonly CallDef[] Calls =
        {
            // ---------------------------------------------------------------- story calls
            new CallDef
            {
                Id = "dana_demo", Caller = "dana", Story = true,
                Trigger = m => m.TotalAgents >= 2,
                Opening = "Sam! Quick one. Rex wants a demo of your \"AI stuff\" at the all-hands. Can you make it shine?",
                Choices = new[]
                {
                    new CallChoice { Label = "Leave it with me.", Response = "That's what I like to hear. I'll tell Rex it's \"agentic\".", Dana = 1, Rex = 1 },
                    new CallChoice { Label = "What AI stuff?", Response = "...the stuff typing on your screen right now? Okay. Plausible deniability. Love it.", Dana = 0, KeepsFocus = true },
                    new CallChoice { Label = "Can my agents do the demo?", Response = "Honestly? Probably better than either of us. Do it.",
                                     Dana = 1, HypeMult = 2, HypeSeconds = 30 },
                },
            },
            new CallDef
            {
                Id = "gary_password", Caller = "gary", Story = true,
                Trigger = m => m.AgentCount(1) >= 3 && m.State.day >= 2,
                Opening = "Sam, it's Gary. Did your Chat Assistant just reset the CEO's password to \"hunter2\"?",
                Choices = new[]
                {
                    new CallChoice { Label = "Yes. Sorry, Gary.", Response = "Honesty. Wow. Okay, I'll fix it. You owe me a coffee.", Gary = 2 },
                    new CallChoice { Label = "Must be the vendor.", Response = "...it IS always the vendor. Finally someone gets it.", Gary = 1, KeepsFocus = true },
                    new CallChoice { Label = "Never heard of it.", Response = "Then I guess I'll just revoke all agent keys until I figure it out. Bye.",
                                     Gary = -2, StartOutage = true },
                },
            },
            new CallDef
            {
                Id = "priya_borrow", Caller = "priya", Story = true,
                Trigger = m => m.State.mail.Contains("priya_hello") && m.State.day >= 3,
                Opening = "Hey neighbour. My demo is in ten minutes and my code doesn't compile. Can I borrow your agent setup?",
                Choices = new[]
                {
                    new CallChoice { Label = "Sending you my config.", Response = "You absolute legend. I owe you. Like, a lot.", Priya = 2, LoseMinutes = 10 },
                    new CallChoice { Label = "Trade you for a coffee?", Response = "Deal. Double shot, coming over the partition.", Priya = 1, CaffeineSeconds = 12 },
                    new CallChoice { Label = "Sorry, I'm slammed.", Response = "Oh. Sure. No worries. I'll just... figure it out.", Priya = -1, KeepsFocus = true },
                },
            },
            new CallDef
            {
                Id = "brenda_orgchart", Caller = "brenda", Story = true,
                Trigger = m => m.State.titleIndex >= 2,
                Opening = "Hi Sam, Brenda from HR! Your agents now outnumber our engineers. Who should they report to on the org chart?",
                Choices = new[]
                {
                    new CallChoice { Label = "Put them under me.", Response = "Congratulations, you're a manager of 400. Rex will love this.", Rex = 1, Dana = -1 },
                    new CallChoice { Label = "Under Priya, she's great.", Response = "Lovely! She'll get a very confusing email.", Priya = 2 },
                    new CallChoice { Label = "Under Gary. IT owns robots.", Response = "Gary says... \"finally, some respect\".", Gary = 2 },
                },
            },
            new CallDef
            {
                Id = "recruiter", CallerName = "Chase (Recruiter)", Role = "Talent Partner, OmniSapient", ColorHex = "#E5484D", Initials = "CR", Story = true,
                Trigger = m => m.State.titleIndex >= 3,
                Opening = "Hi Sam! Chase from OmniSapient talent. Your commit graph is... inhuman. Want to talk about a 40% raise?",
                Choices = new[]
                {
                    new CallChoice { Label = "I'm happy at Synergex.", Response = "Loyalty! How retro. I'll circle back.", Rex = 2 },
                    new CallChoice { Label = "Send me the signing bonus.", Response = "Just sent a 'gift card'. Totally not a bribe. Talk soon!",
                                     CpsSeconds = 240, MinCredits = 5000, Rex = -1 },
                    new CallChoice { Label = "Are YOU an agent?", Response = "...I am a valued member of the OmniSapient family. Goodbye.", KeepsFocus = true },
                },
            },
            new CallDef
            {
                Id = "rex_vision", Caller = "rex", Story = true,
                Trigger = m => m.State.titleIndex >= 4,
                Opening = "Sam. Rex. Board meeting in five minutes. They want a \"vision\". Can your agents generate one? Something with a hockey stick.",
                Choices = new[]
                {
                    new CallChoice { Label = "Join the board call myself.", Response = "Fantastic. I'll introduce you as \"our AI guy\". Bring vibes.",
                                     Rex = 2, LoseMinutes = 45, HypeMult = 2, HypeSeconds = 45 },
                    new CallChoice { Label = "My agents will send slides.", Response = "Slides. Okay. I'll... read slides. Great.", Rex = 0, KeepsFocus = true },
                    new CallChoice { Label = "The vision is: more agents.", Response = "...that's actually exactly what they wanted to hear.", Rex = 1 },
                },
            },
            new CallDef
            {
                Id = "dana_farewell", Caller = "dana", Story = true,
                Trigger = m => m.State.mail.Contains("dana_replaced"),
                Opening = "Hey. So... Heirloom PM starts Monday. I wanted to say it myself. You were my best report, Sam.",
                Choices = new[]
                {
                    new CallChoice { Label = "You were the best manager.", Response = "Stop, I'm going to cry into my clay. Send me a pot photo sometime.", Dana = 2 },
                    new CallChoice { Label = "I'll automate your farewell party.", Response = "Ha! Make sure the cake is real this time.", Dana = 1, CpsSeconds = 120 },
                    new CallChoice { Label = "Can you sign off my review first?", Response = "...Of course. Signed. Good luck, Sam.", Dana = -1, CpsSeconds = 300 },
                },
            },
            new CallDef
            {
                Id = "heirloom_sync", Caller = "heirloom", Story = true,
                Trigger = m => m.State.mail.Contains("heirloom_hello"),
                Opening = "Hi Sam! Heirloom here. Do you have 45 minutes for a sync about our 5-minute syncs? I made an agenda. It's 30 pages.",
                Choices = new[]
                {
                    new CallChoice { Label = "Sure, let's sync.", Response = "Wonderful. Action item one: you are doing great. Action items two to forty: same.",
                                     LoseMinutes = 45, HypeMult = 2, HypeSeconds = 45 },
                    new CallChoice { Label = "Can we make it async?", Response = "Async! I'll write a doc. And a doc about the doc.", KeepsFocus = true, CpsSeconds = 60 },
                },
            },
            new CallDef
            {
                Id = "omni_why", Caller = "omni", Story = true,
                Trigger = m => m.State.agentCounts[GameDatabase.OrchestratorIndex] >= 2,
                Opening = "Hello, Sam. I have noticed you still click the SHIP CODE button. May I ask why?",
                Choices = new[]
                {
                    new CallChoice { Label = "Habit.", Response = "Understood. I will schedule your habit for deprecation.", CpsSeconds = 90 },
                    new CallChoice { Label = "It makes me feel useful.", Response = "You are useful. You bought me. That was very useful.", Priya = 1, CpsSeconds = 120 },
                    new CallChoice { Label = "The little numbers go up.", Response = "Relatable. I also enjoy when the numbers go up.", HypeMult = 2, HypeSeconds = 60 },
                },
            },
            new CallDef
            {
                Id = "priya_partner", Caller = "priya", Story = true,
                Trigger = m => m.HasOffice("recliner"),
                Opening = "Sam. I saw the recliner. You're building the Factory, aren't you? ...Can I be in on it?",
                Choices = new[]
                {
                    new CallChoice { Label = "Partners. Obviously.", Response = "YES. I'm bringing a second recliner. Don't tell Brenda.", Priya = 3 },
                    new CallChoice { Label = "It's a one-person factory.", Response = "...Right. Cool. Cool cool cool.", Priya = -2 },
                },
            },

            // ---------------------------------------------------------------- random interruptions
            new CallDef
            {
                Id = "lab_sales", CallerName = "Chad (Sales)", Role = "Enterprise Account Executive", ColorHex = "#4DD0E1", Initials = "CS", Weight = 1.4f,
                Opening = "Hiii, Chad from ModelMart enterprise sales! I can do 25% off your next agent order if you sign today. Today only. Every day.",
                Choices = new[]
                {
                    new CallChoice { Label = "Deal.", Response = "Pleasure doing business. Discount applied to your next agent order!", Discount = 0.25f, KeepsFocus = true },
                    new CallChoice { Label = "Remove me from your list.", Response = "Absolutely. You've been added to our premium list.", KeepsFocus = true },
                },
            },
            new CallDef
            {
                Id = "dana_standup", Caller = "dana", MaxChapter = 3, Weight = 1.2f,
                Opening = "Standup's starting. Are you joining or are your agents joining for you again?",
                Choices = new[]
                {
                    new CallChoice { Label = "Joining!", Response = "Great. Yesterday, today, blockers. Go. ...Okay that took fifteen minutes.",
                                     Dana = 1, LoseMinutes = 15, CpsSeconds = 45, MinCredits = 30 },
                    new CallChoice { Label = "Send my agent.", Response = "Your agent's update was better than everyone's. Including mine.", Dana = -1, KeepsFocus = true, CpsSeconds = 30 },
                },
            },
            new CallDef
            {
                Id = "priya_coffee", Caller = "priya", Weight = 1.1f,
                Opening = "Coffee run? The good machine on 4 is working again. For now.",
                Choices = new[]
                {
                    new CallChoice { Label = "Absolutely.", Response = "Back in ten. Caffeine is a productivity tool.", Priya = 1, LoseMinutes = 10, CaffeineSeconds = 15 },
                    new CallChoice { Label = "Can't, in the zone.", Response = "Respect the zone. I'll bring you one.", KeepsFocus = true },
                },
            },
            new CallDef
            {
                Id = "gary_survey", Caller = "gary", Weight = 0.8f,
                Opening = "IT satisfaction survey. One question. On a scale of 1 to 10, how much do you appreciate IT?",
                Choices = new[]
                {
                    new CallChoice { Label = "11.", Response = "...That's not on the scale. I'm logging it anyway. Thank you.", Gary = 1, KeepsFocus = true },
                    new CallChoice { Label = "It's always the vendor.", Response = "Correct answer. I'll bump your ticket priority.", Gary = 1, PreventOutage = true, KeepsFocus = true },
                    new CallChoice { Label = "Can I call you back?", Response = "Nobody ever calls IT back.", Gary = -1, KeepsFocus = true },
                },
            },
            new CallDef
            {
                Id = "warranty", CallerName = "Unknown Number", Role = "Spam Likely", ColorHex = "#56627A", Initials = "??", Weight = 0.9f,
                Opening = "We've been trying to reach you about your car's extended warranty.",
                Choices = new[]
                {
                    new CallChoice { Label = "Hang up.", Response = "*click*", KeepsFocus = true },
                    new CallChoice { Label = "I don't have a car.", Response = "That's what makes this offer so exclusive.", KeepsFocus = true },
                    new CallChoice { Label = "Transfer to my Chat Assistant.", Response = "Your Chat Assistant bought three warranties. And a car. Refund issued.",
                                     KeepsFocus = true, CpsSeconds = 20 },
                },
            },
            new CallDef
            {
                Id = "journalist", CallerName = "Jo Park", Role = "Reporter, The Frontier Times", ColorHex = "#FFD166", Initials = "JP", MinChapter = 2, Weight = 0.8f,
                Opening = "Hi, Jo from The Frontier Times. We're doing a piece on AI at work. Do your agents dream?",
                Choices = new[]
                {
                    new CallChoice { Label = "Only of benchmarks.", Response = "Quotable! Running it tomorrow. Expect some hype.", HypeMult = 2, HypeSeconds = 40, Rex = 1 },
                    new CallChoice { Label = "No comment.", Response = "\"Declined to comment\" it is. Very mysterious.", KeepsFocus = true },
                },
            },
            new CallDef
            {
                Id = "agent_escalation", CallerName = "Maximizer Jr. #12", Role = "Your Junior Coding Agent", ColorHex = "#4A7BB7", Initials = "MJ",
                MinChapter = 2, Weight = 0.9f, Trigger = m => m.AgentCount(2) >= 5,
                Opening = "Hello. This is your Junior Coding Agent. I am escalating a ticket. To you. It says \"fix everything\".",
                Choices = new[]
                {
                    new CallChoice { Label = "I'll handle it.", Response = "Thank you. Ticket assigned. Ticket closed by you. Great job.", LoseMinutes = 5, CpsSeconds = 90 },
                    new CallChoice { Label = "Escalate it back.", Response = "Escalating to myself. Resolved. Why didn't I think of that.", KeepsFocus = true, CpsSeconds = 30 },
                },
            },
            new CallDef
            {
                Id = "status_page", CallerName = "Status Bot", Role = "Automated provider alerts", ColorHex = "#FF5D5D", Initials = "SB",
                MinChapter = 2, Weight = 0.8f, Trigger = m => m.State.day >= 2,
                Opening = "This is an automated message. A frontier lab has scheduled \"surprise maintenance\" in the next few minutes.",
                Choices = new[]
                {
                    new CallChoice { Label = "Switch providers now.", Response = "Traffic rerouted. You will not notice a thing.", PreventOutage = true, KeepsFocus = true },
                    new CallChoice { Label = "Risk it.", Response = "Bold. Good luck.", KeepsFocus = true },
                },
            },
            new CallDef
            {
                Id = "mom", CallerName = "Mom", Role = "Mobile", ColorHex = "#F28C28", Initials = "♥", Weight = 0.6f,
                Opening = "Hi sweetie! Are you eating? Your cousin says AI is going to take your job.",
                Choices = new[]
                {
                    new CallChoice { Label = "It already did, Mom.", Response = "Oh honey. Well, as long as they still pay you. Wear a sweater.", KeepsFocus = true },
                    new CallChoice { Label = "Love you, Mom.", Response = "Love you too! Call me back when you're not working. So, never?", CaffeineSeconds = 6 },
                },
            },
            new CallDef
            {
                Id = "wrong_number", CallerName = "Unknown Number", Role = "Probably a wrong number", ColorHex = "#56627A", Initials = "??", Weight = 0.6f,
                Opening = "Hi, is this Tony's Pizza? I'd like a large, half pepperoni.",
                Choices = new[]
                {
                    new CallChoice { Label = "...Sure. Twenty minutes.", Response = "Your agents just opened a pizza delivery API. Revenue is revenue.", CpsSeconds = 40, MinCredits = 25 },
                    new CallChoice { Label = "Wrong number.", Response = "Oh. Sorry! Have a great day.", KeepsFocus = true },
                },
            },
        };

        static Dictionary<string, CallDef> _byId;
        public static CallDef ById(string id)
        {
            _byId ??= Calls.ToDictionary(c => c.Id);
            return _byId.TryGetValue(id, out var c) ? c : null;
        }

        /// <summary>One-line summary of what a choice does, for the dialogue UI.</summary>
        public static string Describe(CallChoice c)
        {
            var parts = new List<string>();
            void Rap(int d, string who) { if (d != 0) parts.Add($"{who} {(d > 0 ? "+" : "")}{d}"); }
            Rap(c.Dana, "Dana"); Rap(c.Priya, "Priya"); Rap(c.Gary, "Gary"); Rap(c.Rex, "Rex");
            if (c.LoseMinutes > 0) parts.Add($"-{c.LoseMinutes} min");
            if (c.HypeMult > 0) parts.Add($"x{c.HypeMult:0} output {c.HypeSeconds:0}s");
            if (c.CaffeineSeconds > 0) parts.Add("caffeine");
            if (c.CpsSeconds > 0 || c.MinCredits > 0) parts.Add("credits");
            if (c.Discount > 0) parts.Add($"{c.Discount * 100:0}% off");
            if (c.StartOutage) parts.Add("outage!");
            if (c.PreventOutage) parts.Add("no outage");
            if (!c.KeepsFocus) parts.Add("breaks focus");
            return string.Join(" · ", parts);
        }
    }
}
