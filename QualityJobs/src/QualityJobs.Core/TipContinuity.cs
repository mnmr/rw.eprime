namespace QualityJobs.Core
{
    /// Frame-continuity stand-in for the "tooltip closed" callback vanilla
    /// never provides: a displayed tooltip invokes its text getter on every
    /// rendered frame (possibly more than once per frame), so a gap of more
    /// than one frame between invocations means the tip closed and the next
    /// invocation starts a new display session.
    /// (Ported from EPrimeReadouts; keep in lockstep.)
    public static class TipContinuity
    {
        /// Sentinel for "never invoked".
        public const int NoFrame = int.MinValue;

        public static bool IsBroken(int lastFrame, int currentFrame) =>
            lastFrame == NoFrame || currentFrame - lastFrame > 1;
    }
}
