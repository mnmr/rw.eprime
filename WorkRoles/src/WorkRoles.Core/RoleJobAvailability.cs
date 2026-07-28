namespace WorkRoles.Core
{
    /// <summary>Cached capability decoration for one colonist-role pair.</summary>
    public enum RoleJobAvailability
    {
        Available,
        SomeUnavailable,
        AllUnavailable,
    }

    public static class RoleJobAvailabilitySummary
    {
        public static RoleJobAvailability FromCounts(int totalJobs, int unavailableJobs)
        {
            if (totalJobs <= 0 || unavailableJobs <= 0)
                return RoleJobAvailability.Available;
            return unavailableJobs >= totalJobs
                ? RoleJobAvailability.AllUnavailable
                : RoleJobAvailability.SomeUnavailable;
        }
    }

    /// <summary>The single prefix decoration rendered for an assignment chip.</summary>
    public enum RoleAssignmentWarningSeverity
    {
        None,
        Caution,
        Critical,
    }

    public static class RoleAssignmentWarningSummary
    {
        /// Veto signals (work-type Awful, primary-skill Awful) are Critical;
        /// a dampened signal (Awful on a non-primary skill) is only Caution,
        /// matching the recommendation engine's primary-only disqualification.
        public static RoleAssignmentWarningSeverity From(
            RoleJobAvailability availability,
            bool hasVetoSignal,
            bool hasDampenedSignal = false)
        {
            if (hasVetoSignal || availability == RoleJobAvailability.AllUnavailable)
                return RoleAssignmentWarningSeverity.Critical;
            return hasDampenedSignal || availability == RoleJobAvailability.SomeUnavailable
                ? RoleAssignmentWarningSeverity.Caution
                : RoleAssignmentWarningSeverity.None;
        }
    }

}
