namespace WorkRoles.Core.Recs
{
    /// Existing unskilled chores (Hauler, Cleaner, ...) survive the rebuild;
    /// skilled roles must re-earn their place through the other rules.
    /// Protected assignments re-enter later (ProtectedReentryRule).
    public sealed class RetentionRule : RecRule
    {
        public override string Id => "retention";
        public override RuleKind Kind => RuleKind.PerPawn;

        public override void Apply(EngineContext context, int pawnIndex)
        {
            foreach (var existing in context.Colony.Pawns[pawnIndex].Existing)
            {
                var role = context.RoleOf(existing.RoleId);
                if (role == null || !role.Unskilled || role.AutoAssign) continue;
                if (existing.Pinned || role.HasRules || role.Blocker) continue;
                if (context.Vetoed.Contains(role.Id)) continue;
                context.AddCandidate(pawnIndex, role.Id,
                    new Reason { RuleId = Id, TowardRoleId = -1 }, SignalBucket.Neutral);
            }
        }
    }
}
