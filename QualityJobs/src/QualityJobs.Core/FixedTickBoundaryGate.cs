using System;

namespace QualityJobs.Core
{
    /// <summary>
    /// Fires once when a game-tick interval is first observed, when its fixed
    /// boundary changes, or when the clock moves backwards during load.
    /// Repeated observations of the same paused tick never fire again.
    /// </summary>
    public sealed class FixedTickBoundaryGate
    {
        private readonly int interval;
        private int lastTick;
        private int boundary;
        private bool initialized;

        public FixedTickBoundaryGate(int interval)
        {
            if (interval <= 0) throw new ArgumentOutOfRangeException(nameof(interval));
            this.interval = interval;
        }

        public bool Observe(int tick)
        {
            int currentBoundary = tick / interval;
            if (!initialized || tick < lastTick || currentBoundary != boundary)
            {
                initialized = true;
                lastTick = tick;
                boundary = currentBoundary;
                return true;
            }

            lastTick = tick;
            return false;
        }

        public void Reset()
        {
            initialized = false;
            lastTick = 0;
            boundary = 0;
        }
    }
}
