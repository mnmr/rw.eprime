using System.Collections.Generic;
using System.Linq;

namespace WorkRoles.Core.Recs
{
    /// Rule 10: a combo candidate beats its parts, with two escapes. A
    /// covered role that sits ABOVE its coverer in a shared path (higher band
    /// min) survives, and the lower-band partner stays alongside it. Blocker
    /// roles neither suppress nor are suppressed.
    public sealed class RedundancySuppressionRule : RecRule
    {
        public override string Id => "redundancy";
        public override RuleKind Kind => RuleKind.PerPawn;

        public override void Apply(EngineContext context, int pawnIndex)
        {
            var byRole = context.Candidates[pawnIndex];

            // Checked against the full list, like the retired engine: a
            // suppressed coverer still suppresses its own parts.
            var ids = byRole.Keys.ToList();
            var drop = new List<int>();
            foreach (int id in ids)
            {
                var role = context.RoleOf(id);
                if (role == null || role.Blocker) continue;
                foreach (int otherId in ids)
                {
                    var other = context.RoleOf(otherId);
                    if (other == null || other.Blocker || otherId == id) continue;
                    if (!context.Redundant(other.Id, role.Id)) continue;
                    if (SurvivesAsPathTarget(context, role, other)) continue;
                    drop.Add(id);
                    break;
                }
            }
            foreach (int id in drop)
                context.RemoveCandidate(pawnIndex, id);
        }

        private static bool SurvivesAsPathTarget(EngineContext context,
            RoleView covered, RoleView coverer)
        {
            foreach (var path in context.Colony.Paths)
            {
                int coveredAt = path.RoleIds.IndexOf(covered.Id);
                int covererAt = path.RoleIds.IndexOf(coverer.Id);
                if (coveredAt >= 0 && covererAt >= 0
                    && path.BandMins[coveredAt] > path.BandMins[covererAt])
                    return true;
            }
            return false;
        }
    }
}
