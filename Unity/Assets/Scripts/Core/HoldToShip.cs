namespace AgentClicker.Core
{
    /// <summary>
    /// Settings → Gameplay → Hold to keep shipping (an accessibility option, off by default): a held SHIP CODE, Space, Enter,
    /// RT or X ships once when pressed and then <see cref="Rate"/> times a second, about what steady clicking does, so Focus
    /// builds as usual. One of these per input (keys, gamepad, pointer); the caller ships once on the press itself.
    /// </summary>
    public sealed class HoldToShip
    {
        /// <summary>Lines of code a second while held.</summary>
        public const float Rate = 6f;

        /// <summary>Most lines shipped in one frame, so a hitch doesn't turn into a burst.</summary>
        public const int MaxPerTick = 3;

        float _held = -1f;
        int _shipped;

        public bool Holding => _held >= 0f;

        /// <summary>The press, which the caller has just shipped once for.</summary>
        public void Press()
        {
            _held = 0f;
            _shipped = 1;
        }

        public void Release() => _held = -1f;

        /// <summary>Time passes while held; returns how many more lines to ship now.</summary>
        public int Tick(float dt)
        {
            if (_held < 0f || dt <= 0f) return 0;
            _held += dt;
            int due = 1 + (int)(_held * Rate);
            int n = due - _shipped;
            if (n > MaxPerTick)
            {
                // drop what a long frame owed rather than catching up all at once
                _shipped = due - MaxPerTick;
                n = MaxPerTick;
            }
            _shipped += n;
            return n;
        }
    }
}
