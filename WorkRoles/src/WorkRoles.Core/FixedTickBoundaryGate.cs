using System;

namespace WorkRoles.Core
{
    /// <summary>
    /// One-comparison steady-state gate for work scheduled on exact fixed tick
    /// boundaries. Each simulation context owns its own instance.
    /// </summary>
    public sealed class FixedTickBoundaryGate
    {
        private readonly int interval;
        private int nextTick;
        private bool initialized;

        public FixedTickBoundaryGate(int interval)
        {
            if (interval <= 0) throw new ArgumentOutOfRangeException(nameof(interval));
            this.interval = interval;
        }

        public bool ShouldRun(int now)
        {
            if (initialized && now < nextTick) return false;
            int remainder = now % interval;
            if (remainder < 0) remainder += interval;
            nextTick = now + interval - remainder;
            initialized = true;
            return true;
        }
    }
}
