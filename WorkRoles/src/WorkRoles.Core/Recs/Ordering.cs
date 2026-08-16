using System.Collections.Generic;
using System.Linq;

namespace WorkRoles.Core.Recs
{
    /// Shared structural position math used by the production planner and the
    /// recommendation-order editor. Formula scoring lives in the planner's
    /// versioned formula engine.
    public static class Ordering
    {
        public const long Slot = 1_000_000;

        public static Dictionary<int, long> BasePositions(
            IReadOnlyList<RoleView> roles,
            IReadOnlyList<int> template)
        {
            var index = new Dictionary<int, int>();
            for (int i = 0; i < template.Count; i++)
                index[template[i]] = i;
            Dictionary<int, RoleView> byId = roles.ToDictionary(role => role.Id);
            var positions = new Dictionary<int, long>();
            foreach (RoleView role in roles)
                positions[role.Id] = index.TryGetValue(role.Id, out int at)
                    ? at * Slot
                    : NaturalSlot(role, index, byId);
            ApplyPreservedOrderFallbacks(
                roles, template, index, byId, positions);
            return positions;
        }

        private static void ApplyPreservedOrderFallbacks(
            IReadOnlyList<RoleView> roles,
            IReadOnlyList<int> template,
            IReadOnlyDictionary<int, int> templateIndex,
            IReadOnlyDictionary<int, RoleView> byId,
            IDictionary<int, long> positions)
        {
            List<RoleView> unlisted = roles
                .Where(role => role.PreserveRecommendationOrder
                    && !templateIndex.ContainsKey(role.Id))
                .ToList();
            if (unlisted.Count == 0) return;

            int trailingUnskilledStart = template.Count;
            while (trailingUnskilledStart > 0
                && byId.TryGetValue(
                    template[trailingUnskilledStart - 1], out RoleView trailing)
                && trailing.UseUnskilledPlacementRules)
                trailingUnskilledStart--;
            long boundary = trailingUnskilledStart < template.Count
                ? trailingUnskilledStart * Slot
                : template.Count * Slot;

            Dictionary<int, int> derivedIndex = OrderTemplate
                .DeriveTemplate(roles)
                .Select((roleId, at) => (roleId, at))
                .ToDictionary(pair => pair.roleId, pair => pair.at);
            List<RoleView> ordered = unlisted
                .OrderBy(role => derivedIndex.TryGetValue(
                    role.Id, out int at) ? at : int.MaxValue)
                .ThenByDescending(role => role.NaturalPriority)
                .ThenBy(role => role.Id)
                .ToList();
            for (int i = 0; i < ordered.Count; i++)
                positions[ordered[i].Id] =
                    boundary - (ordered.Count - i) * 1000;
        }

        private static long NaturalSlot(
            RoleView role,
            Dictionary<int, int> templateIndex,
            Dictionary<int, RoleView> byId)
        {
            int lastHigher = -1;
            foreach (KeyValuePair<int, int> entry in templateIndex)
                if (entry.Value > lastHigher
                    && byId.TryGetValue(entry.Key, out RoleView anchor)
                    && anchor.NaturalPriority >= role.NaturalPriority)
                    lastHigher = entry.Value;
            return lastHigher < 0
                ? -Slot / 10
                : lastHigher * Slot + Slot * 9 / 10;
        }

        internal static int[] PreserveProtectedOrder(
            EngineContext context,
            int pawnIndex,
            IReadOnlyList<int> recommended)
        {
            IReadOnlyList<AssignmentView> existing =
                context.Colony.Pawns[pawnIndex].Existing;
            if (existing.Count == 0 || recommended.Count == 0)
                return recommended.ToArray();

            var recommendedSet = new HashSet<int>(recommended);
            var anchored = new HashSet<int>();
            for (int index = 0; index < existing.Count; index++)
            {
                AssignmentView assignment = existing[index];
                RoleView role = context.RoleOf(assignment.RoleId);
                // Auto-assign roles are automatic and follow the default order;
                // only user-placed pins and rule/blocker roles keep their slot.
                if (recommendedSet.Contains(assignment.RoleId)
                    && role != null
                    && (assignment.Pinned || role.HasRules || role.Blocker))
                    anchored.Add(assignment.RoleId);
            }
            if (anchored.Count == 0) return recommended.ToArray();

            var ordered = new List<int>(recommended.Count);
            for (int index = 0; index < recommended.Count; index++)
                if (!anchored.Contains(recommended[index]))
                    ordered.Add(recommended[index]);
            for (int existingIndex = 0;
                 existingIndex < existing.Count;
                 existingIndex++)
            {
                int roleId = existing[existingIndex].RoleId;
                if (!anchored.Contains(roleId)) continue;
                // Bound by ALL surviving assignments, not adjacent neighbors only: a removed
                // or engine-reordered neighbor must not drag the pin away from the rest.
                int lower = -1;
                for (int index = 0; index < existingIndex; index++)
                {
                    int at = ordered.IndexOf(existing[index].RoleId);
                    if (at > lower) lower = at;
                }
                int upper = int.MaxValue;
                for (int index = existingIndex + 1; index < existing.Count; index++)
                {
                    int at = ordered.IndexOf(existing[index].RoleId);
                    if (at >= 0 && at < upper) upper = at;
                }
                // On an inverted window the upper bound wins: the pin never lands
                // below a role it was explicitly placed ahead of.
                int insertion = lower >= 0 && upper != int.MaxValue
                    ? (lower + 1 <= upper ? lower + 1 : upper)
                    : lower >= 0 ? lower + 1
                    : upper != int.MaxValue ? upper
                    : System.Math.Min(existingIndex, ordered.Count);
                ordered.Insert(insertion, roleId);
            }
            return ordered.ToArray();
        }
    }
}
