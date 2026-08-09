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
                && trailing.Unskilled)
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
                int previous = FindSurvivingNeighbor(
                    existing, existingIndex - 1, -1, ordered);
                int next = FindSurvivingNeighbor(
                    existing, existingIndex + 1, 1, ordered);
                int insertion = previous >= 0 && next >= 0
                    ? previous < next ? next : previous + 1
                    : previous >= 0 ? previous + 1
                    : next >= 0 ? next
                    : System.Math.Min(existingIndex, ordered.Count);
                ordered.Insert(insertion, roleId);
            }
            return ordered.ToArray();
        }

        private static int FindSurvivingNeighbor(
            IReadOnlyList<AssignmentView> existing,
            int start,
            int step,
            List<int> ordered)
        {
            for (int index = start;
                 index >= 0 && index < existing.Count;
                 index += step)
            {
                int found = ordered.IndexOf(existing[index].RoleId);
                if (found >= 0) return found;
            }
            return -1;
        }

        internal static long HunterPosition(
            ColonyView colony,
            Dictionary<int, RoleView> byId,
            int tier)
        {
            if (tier >= 3) return long.MaxValue;
            int lowAnchor = LowHunterAnchor(colony, byId);
            if (tier == 0) return AfterTemplateIndex(lowAnchor);

            List<int> workIndices = Enumerable.Range(
                    0, colony.OrderTemplate.Count)
                .Where(index => IsWorkRole(
                    byId, colony.OrderTemplate[index]))
                .ToList();
            if (workIndices.Count == 0)
                return AfterTemplateIndex(lowAnchor);
            return AfterTemplateIndex(tier == 1
                ? workIndices[0]
                : workIndices[workIndices.Count - 1]);
        }

        private static int LowHunterAnchor(
            ColonyView colony,
            Dictionary<int, RoleView> byId)
        {
            int basics = -1;
            for (int i = 0; i < colony.OrderTemplate.Count; i++)
            {
                RoleView role = RoleAt(colony, byId, i);
                if (role != null
                    && role.WorkTypes.Contains("BasicWorker"))
                {
                    basics = i;
                    break;
                }
            }
            if (basics >= 0)
            {
                int anchor = basics;
                while (anchor + 1 < colony.OrderTemplate.Count)
                {
                    RoleView next = RoleAt(
                        colony, byId, anchor + 1);
                    if (next == null
                        || !next.WorkTypes.Contains("Childcare")
                        && !next.WorkTypes.Contains("Warden"))
                        break;
                    anchor++;
                }
                return anchor;
            }

            int lastLeadingAuto = -1;
            while (lastLeadingAuto + 1 < colony.OrderTemplate.Count)
            {
                RoleView next = RoleAt(
                    colony, byId, lastLeadingAuto + 1);
                if (next == null || !next.AutoAssign) break;
                lastLeadingAuto++;
            }
            return lastLeadingAuto;
        }

        private static bool IsWorkRole(
            Dictionary<int, RoleView> byId,
            int roleId) =>
            byId.TryGetValue(roleId, out RoleView role)
            && !role.AutoAssign
            && role.PrimarySkill != null
            && role.PrimarySkill != "Medicine"
            && role.PrimarySkill != "Social";

        private static RoleView RoleAt(
            ColonyView colony,
            Dictionary<int, RoleView> byId,
            int templateIndex)
        {
            int roleId = colony.OrderTemplate[templateIndex];
            return byId.TryGetValue(roleId, out RoleView role)
                ? role
                : null;
        }

        private static long AfterTemplateIndex(int templateIndex) =>
            templateIndex < 0
                ? -Slot / 2
                : templateIndex * Slot + Slot / 2;
    }
}
