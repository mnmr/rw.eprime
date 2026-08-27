namespace WorkRoles.Core
{
    /// Per-pawn side of the role toggle pair. Enabled defers to the role's
    /// global toggle; ForceOn runs the role on this pawn even when the role
    /// is globally off. Persisted by value: members must keep their numbers.
    public enum AssignmentState
    {
        Enabled = 0,
        Disabled = 1,
        ForceOn = 2,
    }

    /// The one effective-enablement predicate for a role assignment. Every
    /// consumer (compile gate, coverage math, chip state) must route through
    /// this so the tri-state semantics can never diverge between sites.
    public static class RoleActivation
    {
        public static bool IsActive(bool roleEnabled, AssignmentState state) =>
            state == AssignmentState.ForceOn
            || (roleEnabled && state == AssignmentState.Enabled);

        /// Chip click order. Disabling stays a single click; re-enabling passes
        /// through ForceOn, which is harmless and visibly distinct.
        public static AssignmentState Next(AssignmentState state) =>
            state == AssignmentState.Enabled ? AssignmentState.Disabled
            : state == AssignmentState.Disabled ? AssignmentState.ForceOn
            : AssignmentState.Enabled;
    }
}
