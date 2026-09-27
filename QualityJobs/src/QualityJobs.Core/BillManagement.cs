namespace QualityJobs.Core
{
    /// <summary>Per-bill key writes for one API management command.</summary>
    public readonly struct BillManagementChange
    {
        public readonly bool WritesManaged;
        public readonly bool Managed;
        public readonly bool WritesTarget;
        public readonly int TargetQuality;
        /// <summary>The effective managed flag flipped (gate, dispatch and
        /// status eligibility follow it).</summary>
        public readonly bool EligibilityChanged;

        public BillManagementChange(bool writesManaged, bool managed,
            bool writesTarget, int targetQuality, bool eligibilityChanged)
        {
            WritesManaged = writesManaged;
            Managed = managed;
            WritesTarget = writesTarget;
            TargetQuality = targetQuality;
            EligibilityChanged = eligibilityChanged;
        }

        public bool IsNoOp => !WritesManaged && !WritesTarget;
    }

    /// <summary>State transitions behind QualityJobsApi.ManageBill and
    /// UnmanageBill. Both pin explicit per-bill keys (a missing key tracks the
    /// per-save default, which the player may change later), write only when
    /// the pinned value differs, and never touch the skill-gate, inspiration,
    /// specialist, or auto-best keys, which keep following the defaults.</summary>
    public static class BillManagement
    {
        public static BillManagementChange Manage(bool? managedOverride,
            bool managedDefault, int? targetOverride, int requestedTarget)
        {
            bool effective = managedOverride ?? managedDefault;
            bool writesManaged = managedOverride != true;
            bool writesTarget = targetOverride != requestedTarget;
            return new BillManagementChange(writesManaged, true, writesTarget,
                requestedTarget, eligibilityChanged: !effective);
        }

        public static BillManagementChange Unmanage(bool? managedOverride,
            bool managedDefault)
        {
            bool effective = managedOverride ?? managedDefault;
            return new BillManagementChange(managedOverride != false, false,
                writesTarget: false, targetQuality: 0,
                eligibilityChanged: effective);
        }

        public static bool IsValidTargetQuality(int value)
            => ConfigurationLimits.Quality(value) == value;
    }
}
