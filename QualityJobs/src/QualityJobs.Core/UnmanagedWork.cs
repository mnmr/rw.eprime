namespace QualityJobs.Core
{
    /// <summary>Hand-back rule for tracked items whose management ended: a
    /// recipe Quality Jobs no longer manages (checked on load) or a bill that
    /// was unmanaged (its checkbox, the "manage new bills" default, or the
    /// API). Only work the gate locked (paused,
    /// or dispatched to a finisher) is released; shared work never depended
    /// on management.</summary>
    public static class UnmanagedWork
    {
        public static bool Releases(bool managed, WorkItemState state)
            => !managed && state != WorkItemState.Shared;
    }
}
