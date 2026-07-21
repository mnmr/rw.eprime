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
        public static RoleAssignmentWarningSeverity From(
            RoleJobAvailability availability,
            bool hasAwfulSignal)
        {
            if (hasAwfulSignal || availability == RoleJobAvailability.AllUnavailable)
                return RoleAssignmentWarningSeverity.Critical;
            return availability == RoleJobAvailability.SomeUnavailable
                ? RoleAssignmentWarningSeverity.Caution
                : RoleAssignmentWarningSeverity.None;
        }
    }

}
