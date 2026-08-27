namespace QualityJobs.Core
{
    /// <summary>Spec §4. Shared = idle in-progress work anyone may take (creator kept);
    /// Paused = zero-work quality-locked (creator cleared); Dispatched =
    /// assigned to a finisher via a one-shot bill.</summary>
    public enum WorkItemState
    {
        Paused = 0,
        Dispatched = 1,
        Shared = 2,
    }

    public static class WorkItemStates
    {
        /// <summary>Entry-preserving transitions only. Shared entries are REMOVED on
        /// handover (the item goes active again); a later pause creates a new
        /// Paused entry — so Shared has no legal in-place transitions.</summary>
        public static bool CanTransition(WorkItemState from, WorkItemState to)
            => (from == WorkItemState.Paused && to == WorkItemState.Dispatched)
               || (from == WorkItemState.Dispatched && to == WorkItemState.Paused);
    }
}
