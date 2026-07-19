using System;
using System.Collections.Generic;
using System.Linq;

namespace WorkRoles.Core.Recs
{
    /// Final ordering pass: existing assignments whose placement is controlled
    /// by the player or by role behavior keep their position relative to the
    /// nearest assignments that survive the recommendation pass.
    public sealed class AnchorPreservationRule : RecRule
    {
        public override string Id => "anchors";
        public override RuleKind Kind => RuleKind.PerPawn;

        public override void Apply(EngineContext context, int pawnIndex)
        {
            var existing = context.Colony.Pawns[pawnIndex].Existing;
            var result = context.Results[pawnIndex];
            if (existing.Count == 0 || result.Assignments.Count == 0) return;

            var targetByRole = result.Assignments
                .GroupBy(a => a.RoleId)
                .ToDictionary(group => group.Key, group => group.First());
            var anchoredRoleIds = new HashSet<int>();
            foreach (var assignment in existing)
            {
                if (!targetByRole.ContainsKey(assignment.RoleId)) continue;
                var role = context.RoleOf(assignment.RoleId);
                if (role != null && (assignment.Pinned
                                     || role.HasRules || role.Blocker))
                    anchoredRoleIds.Add(assignment.RoleId);
            }
            if (anchoredRoleIds.Count == 0) return;

            var ordered = result.Assignments
                .Where(a => !anchoredRoleIds.Contains(a.RoleId)).ToList();
            for (int existingIndex = 0; existingIndex < existing.Count; existingIndex++)
            {
                int roleId = existing[existingIndex].RoleId;
                if (!anchoredRoleIds.Contains(roleId)) continue;

                int previousIndex = FindSurvivingNeighbor(
                    existing, existingIndex - 1, -1, ordered);
                int nextIndex = FindSurvivingNeighbor(
                    existing, existingIndex + 1, 1, ordered);
                int insertIndex;
                if (previousIndex >= 0 && nextIndex >= 0)
                {
                    // When both relationships can be retained, place the role
                    // between them. If recommendation order inverted them,
                    // the preceding relationship wins as deterministic fallback.
                    insertIndex = previousIndex < nextIndex
                        ? nextIndex
                        : previousIndex + 1;
                }
                else if (previousIndex >= 0)
                    insertIndex = previousIndex + 1;
                else if (nextIndex >= 0)
                    insertIndex = nextIndex;
                else
                    insertIndex = Math.Min(existingIndex, ordered.Count);

                ordered.Insert(insertIndex, targetByRole[roleId]);
            }

            result.Assignments.Clear();
            result.Assignments.AddRange(ordered);
        }

        private static int FindSurvivingNeighbor(
            IReadOnlyList<AssignmentView> existing,
            int start,
            int step,
            IReadOnlyList<AssignmentView> ordered)
        {
            for (int i = start; i >= 0 && i < existing.Count; i += step)
            {
                int roleId = existing[i].RoleId;
                for (int j = 0; j < ordered.Count; j++)
                    if (ordered[j].RoleId == roleId)
                        return j;
            }
            return -1;
        }
    }
}
