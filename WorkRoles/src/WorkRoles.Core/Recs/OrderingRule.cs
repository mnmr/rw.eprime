using System.Linq;

namespace WorkRoles.Core.Recs
{
    /// Rule 11: materializes each pawn's candidates into the ordered
    /// assignment list (Ordering.SortKeys), keeping per-pawn toggles (and
    /// pins) from the existing assignments. Rule-carrying and blocker
    /// assignments are skipped here and re-seated by ProtectedReentryRule.
    public sealed class OrderingRule : RecRule
    {
        public override string Id => "ordering";
        public override RuleKind Kind => RuleKind.PerPawn;

        public override void Apply(EngineContext context, int pawnIndex)
        {
            var keys = Ordering.SortKeys(context, pawnIndex);
            var result = context.Results[pawnIndex];
            result.Assignments.Clear();
            result.Reasons.Clear();
            var existing = context.Colony.Pawns[pawnIndex].Existing;
            foreach (var candidate in context.Candidates[pawnIndex].Values
                         .OrderBy(c => keys[c.RoleId]).ThenBy(c => c.RoleId))
            {
                var role = context.RoleOf(candidate.RoleId);
                if (role == null) continue;
                bool enabled = true, pinned = false, isProtected = false;
                foreach (var a in existing)
                    if (a.RoleId == candidate.RoleId)
                    {
                        enabled = a.Enabled;
                        pinned = a.Pinned;
                        isProtected = role.HasRules || role.Blocker;
                        break;
                    }
                if (isProtected) continue;
                result.Assignments.Add(new AssignmentView
                { RoleId = candidate.RoleId, Enabled = enabled, Pinned = pinned });
                result.Reasons[candidate.RoleId] = candidate.Reason;
            }
        }
    }
}
