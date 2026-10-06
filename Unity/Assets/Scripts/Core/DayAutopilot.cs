using System;

namespace AgentClicker.Core
{
    public enum AutopilotAction { None, ClockOut, GoHome, ClockIn, LogIn }

    /// <summary>
    /// Runs the work day while nobody is at the keyboard, so an idle game keeps producing: after 17:00 it
    /// clocks out, files the review, goes home, clocks in and logs in, each after a short quiet spell.
    /// The quiet timer restarts on any player input, whenever the phase changes and while something
    /// needs the player (a call, a menu, the story). Pure C#: the caller performs the returned action.
    /// </summary>
    public sealed class DayAutopilot
    {
        public float ClockOutAfter = 30f, GoHomeAfter = 15f, ClockInAfter = 8f, LogInAfter = 10f;

        GamePhase _phase = (GamePhase)(-1);
        bool _pastFive;
        float _quiet;

        /// <summary>Seconds without input in the current phase.</summary>
        public float QuietSeconds => _quiet;

        public void Reset() => _quiet = 0;

        /// <param name="dt">Real seconds since the last call.</param>
        /// <param name="input">The player touched the mouse or keyboard since the last call.</param>
        /// <param name="blocked">Something needs the player (call, menu, story cards, ending): never act.</param>
        /// <param name="holdClockOut">The player chose to work late today: let the overtime run.</param>
        public AutopilotAction Tick(GameModel m, float dt, bool input, bool blocked, bool holdClockOut = false)
        {
            var phase = m.Phase;
            bool pastFive = m.PastFiveOClock;
            if (phase != _phase || pastFive != _pastFive)
            {
                _phase = phase;
                _pastFive = pastFive;
                _quiet = 0;
            }
            if (input || blocked)
            {
                _quiet = 0;
                return AutopilotAction.None;
            }
            AutopilotAction action;
            float need;
            switch (phase)
            {
                case GamePhase.Working when pastFive && !holdClockOut && m.ActiveCall == null:
                    action = AutopilotAction.ClockOut; need = ClockOutAfter; break;
                case GamePhase.Review: action = AutopilotAction.GoHome; need = GoHomeAfter; break;
                case GamePhase.Night: action = AutopilotAction.ClockIn; need = ClockInAfter; break;
                case GamePhase.Login: action = AutopilotAction.LogIn; need = LogInAfter; break;
                default: _quiet = 0; return AutopilotAction.None; // nothing pending: the countdown starts when it is
            }
            _quiet += Math.Max(0f, dt);
            if (_quiet < need) return AutopilotAction.None;
            _quiet = 0;
            return action;
        }

        /// <summary>Applies an action straight to the model (what the buttons do, minus the presentation).</summary>
        public static void Apply(GameModel m, AutopilotAction action)
        {
            switch (action)
            {
                case AutopilotAction.ClockOut: m.ClockOut(); break;
                case AutopilotAction.GoHome: m.GoHome(); break;
                case AutopilotAction.ClockIn: m.StartNextDay(); break;
                case AutopilotAction.LogIn: m.ClockIn(); break;
            }
        }
    }
}
