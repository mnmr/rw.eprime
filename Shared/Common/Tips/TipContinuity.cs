namespace RimShared.Common
{
    /// Frame-continuity stand-in for a tooltip-closed callback: the presenter
    /// observes a hovered region on every rendered frame, so a gap of more
    /// than one frame starts a new display session.
    public static class TipContinuity
    {
        /// Sentinel for "never invoked".
        public const int NoFrame = int.MinValue;

        public static bool IsBroken(int lastFrame, int currentFrame) =>
            lastFrame == NoFrame || currentFrame - lastFrame > 1;
    }
}
