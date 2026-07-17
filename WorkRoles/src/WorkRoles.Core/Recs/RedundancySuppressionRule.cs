using System.Collections.Generic;
using System.Linq;

namespace WorkRoles.Core.Recs
{
    /// Rule 10: a combo candidate beats its parts — with two escapes. A
    /// covered role that sits ABOVE its coverer in a shared path (higher band
    /// min) survives, and the lower-band partner stays alongside it. A NEW
    /// coverer whose job order would reshuffle the pawn's existing priorities
    /// never folds them: it is dropped instead (order-compatible folds only).
    public sealed class RedundancySuppressionRule : RecRule
    {
        public override string Id => "redundancy";
        public override RuleKind Kind => RuleKind.PerPawn;

        public override void Apply(EngineContext context, int pawnIndex)
        {
            var byRole = context.Candidates[pawnIndex];
            var existing = context.Colony.Pawns[pawnIndex].Existing;

            foreach (int id in byRole.Keys.ToList())
            {
                var coverer = context.RoleOf(id);
                if (coverer == null || coverer.Blocker) continue;
                if (existing.Any(a => a.RoleId == id)) continue;
                if (!OrderCompatible(context, pawnIndex, coverer))
                    context.RemoveCandidate(pawnIndex, id);
            }

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
                    if (!CoverageMath.MakesRedundant(other.Coverage, other.Id,
                            role.Coverage, role.Id)) continue;
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

        // A coverer folds held roles only when relative order matches its own job order.
        private static bool OrderCompatible(EngineContext context, int pawnIndex, RoleView coverer)
        {
            if (coverer.OrderedCoverage == null) return true;
            int last = -1;
            foreach (var a in context.Colony.Pawns[pawnIndex].Existing)
            {
                if (a.Pinned) continue;
                var held = context.RoleOf(a.RoleId);
                if (held == null || held.Id == coverer.Id) continue;
                if (!CoverageMath.MakesRedundant(coverer.Coverage, coverer.Id,
                        held.Coverage, held.Id)) continue;
                int first = FirstCoveredIndex(coverer, held);
                if (first < 0) continue;
                if (first < last) return false;
                last = first;
            }
            return true;
        }

        private static int FirstCoveredIndex(RoleView coverer, RoleView held)
        {
            for (int i = 0; i < coverer.OrderedCoverage.Count; i++)
                if (held.Coverage.Contains(coverer.OrderedCoverage[i])) return i;
            return -1;
        }
    }
}
