using System.Linq;

namespace WorkRoles.Core.Recs
{
    /// Rule 12: rule-carrying and blocker assignments skip normal placement
    /// and re-enter at their original index with toggles kept; pinned
    /// assignments the ordering no longer carries re-enter the same way.
    public sealed class ProtectedReentryRule : RecRule
    {
        public override string Id => "protected";
        public override RuleKind Kind => RuleKind.PerPawn;

        public override void Apply(EngineContext context, int pawnIndex)
        {
            var result = context.Results[pawnIndex];
            var existing = context.Colony.Pawns[pawnIndex].Existing;
            for (int i = 0; i < existing.Count; i++)
            {
                var role = context.RoleOf(existing[i].RoleId);
                if (role == null) continue;
                if (!role.HasRules && !role.Blocker && !existing[i].Pinned) continue;
                if (result.Assignments.Any(a => a.RoleId == existing[i].RoleId)) continue;
                result.Assignments.Insert(
                    System.Math.Min(i, result.Assignments.Count), existing[i]);
            }
        }
    }
}
