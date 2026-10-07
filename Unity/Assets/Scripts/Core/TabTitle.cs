namespace AgentClicker.Core
{
    /// <summary>
    /// The browser tab's title: the credits, with whatever needs the player in front, so a tab or window beside your work
    /// shows when to look ("★ Model drop! · 22.0Qa credits · Agent Clicker"). Plain "Agent Clicker" outside a game.
    /// </summary>
    public static class TabTitle
    {
        public const string Game = "Agent Clicker";

        public static string Compose(bool inGame, double credits, bool drop, bool ringing, bool outage, bool fivePm)
        {
            if (!inGame) return Game;
            string alert = drop ? "★ Model drop! · "
                         : ringing ? "☎ Phone ringing · "
                         : outage ? "⚠ API outage · "
                         : fivePm ? "5 PM: clock out? · "
                         : "";
            return $"{alert}{NumberFormat.Short(credits)} credits · {Game}";
        }
    }
}
