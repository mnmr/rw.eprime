namespace EPrimeReadouts.Core
{
    /// Tick-only schedule for a periodic GPU check, separated from count and
    /// buffer work. The owner replaces this state when its panel/map resets.
    public sealed class PanelHealthSchedule
    {
        public const int WorkOffsetTicks = 30;
        private readonly int intervalTicks;
        private bool observed;
        private bool issued;
        private int lastObservedTick;
        private int lastIssuedTick;
        private bool rewound;
        private int rewindTick;

        public PanelHealthSchedule(int intervalTicks)
        {
            if (intervalTicks < WorkOffsetTicks * 2)
                throw new System.ArgumentOutOfRangeException(nameof(intervalTicks));
            this.intervalTicks = intervalTicks;
        }

        public bool TryBegin(int tick, int countRefreshTick, int bufferWorkTick)
        {
            if (observed && tick < lastObservedTick)
            {
                issued = false;
                rewound = true;
                rewindTick = tick;
            }
            observed = true;
            lastObservedTick = tick;
            // A prior clock epoch must neither block checks indefinitely nor
            // make every paused update appear due. Use widened subtraction.
            if (rewound && (long)tick - rewindTick < WorkOffsetTicks)
                return false;
            long countAge = (long)tick - countRefreshTick;
            if (countAge >= 0 && (countAge < WorkOffsetTicks
                || countAge > intervalTicks - WorkOffsetTicks))
                return false;
            if (bufferWorkTick <= tick
                && (long)tick - bufferWorkTick < WorkOffsetTicks)
                return false;
            if (issued && (long)tick - lastIssuedTick < intervalTicks)
                return false;
            issued = true;
            lastIssuedTick = tick;
            return true;
        }
    }
}
