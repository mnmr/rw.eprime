namespace RimShared.Common
{
    public enum TipRefresh
    {
        /// Gather once and keep until an external reset (static translations).
        Pinned,
        /// Gather once per continuous hover; a frame gap ends the session.
        PerSession,
    }

    /// <summary>
    /// Decides when a lazily gathered tooltip must rebuild its text. Tooltip
    /// content freezes while the pointer stays on the region (frame
    /// continuity); leaving and re-hovering regathers session tips.
    /// </summary>
    public static class TipGatherPolicy
    {
        public static bool ShouldGather(TipRefresh refresh, bool hasText,
            int frame, int lastObservedFrame)
        {
            if (!hasText) return true;
            if (refresh == TipRefresh.Pinned) return false;
            return frame - lastObservedFrame > 1;
        }
    }
}
