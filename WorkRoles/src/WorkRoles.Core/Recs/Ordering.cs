using System.Collections.Generic;
using System.Linq;

namespace WorkRoles.Core.Recs
{
    /// Position math for the recommendation order. BasePositions is the
    /// pawn-independent skeleton; SortKeys (per pawn) lands with OrderingRule.
    public static class Ordering
    {
        public const long Slot = 1_000_000;

        /// Template members: index * Slot. Unlisted roles: after the last
        /// template entry of equal-or-higher work-type priority (before the
        /// whole template when none is).
        public static Dictionary<int, long> BasePositions(
            IReadOnlyList<RoleView> roles, IReadOnlyList<int> template)
        {
            var index = new Dictionary<int, int>();
            for (int i = 0; i < template.Count; i++)
                index[template[i]] = i;
            var byId = roles.ToDictionary(r => r.Id);
            var positions = new Dictionary<int, long>();
            foreach (var role in roles)
                positions[role.Id] = index.TryGetValue(role.Id, out int at)
                    ? at * Slot
                    : NaturalSlot(role, index, byId);
            ApplyPreservedOrderFallbacks(roles, template, index, byId, positions);
            return positions;
        }

        private static void ApplyPreservedOrderFallbacks(
            IReadOnlyList<RoleView> roles,
            IReadOnlyList<int> template,
            IReadOnlyDictionary<int, int> templateIndex,
            IReadOnlyDictionary<int, RoleView> byId,
            IDictionary<int, long> positions)
        {
            var unlisted = roles
                .Where(role => role.PreserveRecommendationOrder
                    && !templateIndex.ContainsKey(role.Id))
                .ToList();
            if (unlisted.Count == 0) return;

            int trailingUnskilledStart = template.Count;
            while (trailingUnskilledStart > 0
                && byId.TryGetValue(template[trailingUnskilledStart - 1], out var trailing)
                && trailing.Unskilled)
                trailingUnskilledStart--;
            long boundary = trailingUnskilledStart < template.Count
                ? trailingUnskilledStart * Slot
                : template.Count * Slot;

            var derivedIndex = OrderTemplate.DeriveTemplate(roles)
                .Select((roleId, at) => (roleId, at))
                .ToDictionary(pair => pair.roleId, pair => pair.at);
            var ordered = unlisted
                .OrderBy(role => derivedIndex.TryGetValue(role.Id, out int at)
                    ? at : int.MaxValue)
                .ThenByDescending(role => role.NaturalPriority)
                .ThenBy(role => role.Id)
                .ToList();
            for (int i = 0; i < ordered.Count; i++)
                positions[ordered[i].Id] = boundary - (ordered.Count - i) * 1000;
        }

        private static long NaturalSlot(RoleView role, Dictionary<int, int> templateIndex,
            Dictionary<int, RoleView> byId)
        {
            int lastHigher = -1;
            foreach (var entry in templateIndex)
                if (entry.Value > lastHigher && byId.TryGetValue(entry.Key, out var anchor)
                    && anchor.NaturalPriority >= role.NaturalPriority)
                    lastHigher = entry.Value;
            return lastHigher < 0 ? -Slot / 10 : lastHigher * Slot + Slot * 9 / 10;
        }

        /// Per-pawn sort keys: base positions; applicable training paths
        /// replace their members' keys (band-min DESCENDING inside the block;
        /// same-anchor blocks rank by the pawn's strongest bucket in the
        /// block, then the block's smallest base position, then path id);
        /// then the unlisted-Hunter and fire-safety overrides. Anchors CHAIN:
        /// a block reads its anchor role at the position the role's own path
        /// resolved it to (see GroupOrder). A training assignment uses its
        /// selected path; an unplaced multi-path member follows the path it
        /// tops; an otherwise ambiguous role keeps its configured base
        /// position.
        public static Dictionary<int, long> SortKeys(EngineContext context, int pawnIndex)
        {
            var colony = context.Colony;
            var baseKeys = context.BasePositions();
            var keys = context.CopyBasePositions();
            var anchoredKeys = new Dictionary<int, Dictionary<int, long>>();

            var groups = colony.Paths
                .Where(p => p.AnchorRoleId != -1 && baseKeys.ContainsKey(p.AnchorRoleId))
                .GroupBy(p => (p.AnchorRoleId, p.AnchorBefore))
                .ToList();
            var groupIndexByPathId = new Dictionary<int, int>();
            for (int i = 0; i < groups.Count; i++)
                foreach (var path in groups[i])
                    groupIndexByPathId[path.Id] = i;

            foreach (int groupIndex in GroupOrder(
                         context, pawnIndex, groups, groupIndexByPathId, baseKeys))
            {
                var group = groups[groupIndex];
                long anchorKey = ResolvedRoleKey(
                    context, pawnIndex, group.Key.AnchorRoleId, baseKeys, anchoredKeys);
                var blocks = group
                    .Select(path => (path,
                        strength: BlockStrength(context, pawnIndex, path),
                        basePos: path.RoleIds.Where(baseKeys.ContainsKey)
                            .Select(id => baseKeys[id]).DefaultIfEmpty(long.MaxValue).Min()))
                    .OrderByDescending(b => b.strength)
                    .ThenBy(b => b.basePos)
                    .ThenBy(b => b.path.Id)
                    .ToList();
                for (int rank = 0; rank < blocks.Count; rank++)
                {
                    var path = blocks[rank].path;
                    var members = OrderedMembers(context, pawnIndex, path);
                    var pathKeys = new Dictionary<int, long>();
                    for (int m = 0; m < members.Count; m++)
                    {
                        if (!baseKeys.ContainsKey(members[m])) continue;
                        // Before-offsets sit 1 off a multiple of 1000 so a
                        // chained before+after pair can never cancel to an
                        // exact tie with the anchor's own key.
                        pathKeys[members[m]] = group.Key.AnchorBefore
                            ? anchorKey - (blocks.Count - rank) * 1000 + 1 + m
                            : anchorKey + (rank + 1) * 1000 + m;
                    }
                    anchoredKeys[path.Id] = pathKeys;
                }
            }

            foreach (int roleId in context.Candidates[pawnIndex].Keys)
            {
                if (context.RoleOf(roleId)?.PreserveRecommendationOrder == true)
                    continue;
                int? pathId = ApplicablePathId(context, pawnIndex, roleId);
                if (!pathId.HasValue) continue;
                if (!context.PathsById.TryGetValue(pathId.Value, out var path)) continue;
                if (anchoredKeys.TryGetValue(path.Id, out var pathKeys)
                    && pathKeys.TryGetValue(roleId, out long anchoredKey))
                {
                    keys[roleId] = anchoredKey;
                    continue;
                }
                if (TryUnanchoredKey(context, pawnIndex, path, roleId,
                        baseKeys, out long unanchoredKey))
                    keys[roleId] = unanchoredKey;
            }

            int hunterId = colony.HunterRoleId;
            if (hunterId != -1 && keys.ContainsKey(hunterId)
                && !colony.OrderTemplate.Contains(hunterId))
            {
                int tier = context.HunterTiers[pawnIndex];
                if (tier >= 0) keys[hunterId] = HunterPosition(colony, context.RolesById, tier);
            }
            if (colony.FireBlockerRoleId != -1 && keys.ContainsKey(colony.FireBlockerRoleId))
                keys[colony.FireBlockerRoleId] = long.MinValue;   // veto must lead the list
            return keys;
        }

        private static int? ApplicablePathId(EngineContext context, int pawnIndex, int roleId)
        {
            if (context.TrainingPathPlacements[pawnIndex].TryGetValue(
                    roleId, out var placement))
                return placement.PathId;
            // Multi-path members without a placement follow the path they
            // TOP; pure trainer roles stay ambiguous and keep base position.
            return (context.SoloPathOf(roleId) ?? context.TargetPathOf(roleId))?.Id;
        }

        /// Anchored groups process in dependency order so a block reads its
        /// anchor role at the position the role's OWN path resolved it to
        /// (anchors chain). Cycles break deterministically: the stuck group
        /// whose anchor sits lowest in the template goes first, reading
        /// whatever has resolved so far.
        private static List<int> GroupOrder(EngineContext context, int pawnIndex,
            List<IGrouping<(int AnchorRoleId, bool AnchorBefore), PathView>> groups,
            IReadOnlyDictionary<int, int> groupIndexByPathId,
            IReadOnlyDictionary<int, long> baseKeys)
        {
            int count = groups.Count;
            var dependsOn = new int[count];
            for (int i = 0; i < count; i++)
            {
                dependsOn[i] = -1;
                int anchorId = groups[i].Key.AnchorRoleId;
                if (context.RoleOf(anchorId)?.PreserveRecommendationOrder == true) continue;
                int? pathId = ApplicablePathId(context, pawnIndex, anchorId);
                if (pathId.HasValue
                    && groupIndexByPathId.TryGetValue(pathId.Value, out int at) && at != i)
                    dependsOn[i] = at;
            }

            var order = new List<int>(count);
            var emitted = new bool[count];
            while (order.Count < count)
            {
                bool progress = false;
                for (int i = 0; i < count; i++)
                {
                    if (emitted[i] || (dependsOn[i] != -1 && !emitted[dependsOn[i]]))
                        continue;
                    emitted[i] = true;
                    order.Add(i);
                    progress = true;
                }
                if (progress) continue;
                int cycleBreaker = Enumerable.Range(0, count)
                    .Where(i => !emitted[i])
                    .OrderBy(i => baseKeys[groups[i].Key.AnchorRoleId])
                    .ThenBy(i => groups[i].Key.AnchorRoleId)
                    .ThenBy(i => groups[i].Key.AnchorBefore)
                    .First();
                emitted[cycleBreaker] = true;
                order.Add(cycleBreaker);
            }
            return order;
        }

        /// The role's position after its own applicable path placed it; base
        /// position when unplaced (or when the role preserves its slot).
        private static long ResolvedRoleKey(EngineContext context, int pawnIndex,
            int roleId, IReadOnlyDictionary<int, long> baseKeys,
            Dictionary<int, Dictionary<int, long>> anchoredKeys)
        {
            if (context.RoleOf(roleId)?.PreserveRecommendationOrder != true)
            {
                int? pathId = ApplicablePathId(context, pawnIndex, roleId);
                if (pathId.HasValue
                    && anchoredKeys.TryGetValue(pathId.Value, out var pathKeys)
                    && pathKeys.TryGetValue(roleId, out long key))
                    return key;
            }
            return baseKeys[roleId];
        }

        private static bool TryUnanchoredKey(EngineContext context, int pawnIndex,
            PathView path, int roleId, IReadOnlyDictionary<int, long> baseKeys,
            out long key)
        {
            key = 0;
            var members = OrderedMembers(context, pawnIndex, path);
            int memberIndex = members.IndexOf(roleId);
            if (memberIndex < 0) return false;

            int targetRoleId = -1;
            if (context.TrainingPathPlacements[pawnIndex].TryGetValue(
                    roleId, out var placement) && placement.PathId == path.Id)
                targetRoleId = placement.TargetRoleId;
            if (!baseKeys.ContainsKey(targetRoleId))
                targetRoleId = DefaultTargetRoleId(path, baseKeys);
            if (!baseKeys.TryGetValue(targetRoleId, out long targetKey)) return false;

            key = targetKey + memberIndex;
            return true;
        }

        private static int DefaultTargetRoleId(PathView path,
            IReadOnlyDictionary<int, long> baseKeys)
        {
            return Enumerable.Range(0, path.RoleIds.Count)
                .Where(i => baseKeys.ContainsKey(path.RoleIds[i]))
                .OrderByDescending(i => path.BandMins[i])
                .ThenBy(i => baseKeys[path.RoleIds[i]])
                .ThenBy(i => i)
                .Select(i => path.RoleIds[i])
                .DefaultIfEmpty(-1)
                .First();
        }

        private static List<int> OrderedMembers(EngineContext context, int pawnIndex,
            PathView path)
        {
            return Enumerable.Range(0, path.RoleIds.Count)
                .OrderByDescending(i => path.BandMins[i])
                .ThenBy(i => PathRoleReadiness(context, pawnIndex, path.RoleIds[i]))
                .ThenBy(i => i)
                .Select(i => path.RoleIds[i])
                .ToList();
        }

        private static int PathRoleReadiness(EngineContext context, int pawnIndex, int roleId)
        {
            var role = context.RoleOf(roleId);
            if (role == null) return int.MaxValue;
            return context.RequiredSkills(role)
                .Select(skill => context.SkillLevel(pawnIndex, skill.SkillDefName))
                .DefaultIfEmpty(int.MaxValue)
                .Min();
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

        internal static long HunterPosition(ColonyView colony,
            Dictionary<int, RoleView> byId, int tier)
        {
            if (tier >= 3) return long.MaxValue;
            int lowAnchor = LowHunterAnchor(colony, byId);
            if (tier == 0) return AfterTemplateIndex(lowAnchor);

            var workIndices = Enumerable.Range(0, colony.OrderTemplate.Count)
                .Where(i => IsWorkRole(byId, colony.OrderTemplate[i]))
                .ToList();
            if (workIndices.Count == 0) return AfterTemplateIndex(lowAnchor);
            return AfterTemplateIndex(tier == 1 ? workIndices[0] : workIndices[workIndices.Count - 1]);
        }

        /// Tier 0 follows the template's BasicWorker role plus immediately
        /// following Childcare/Warden roles. Without that anchor, it follows
        /// the template's leading auto-assigned roles.
        private static int LowHunterAnchor(ColonyView colony, Dictionary<int, RoleView> byId)
        {
            int basics = -1;
            for (int i = 0; i < colony.OrderTemplate.Count; i++)
            {
                var role = RoleAt(colony, byId, i);
                if (role != null && role.WorkTypes.Contains("BasicWorker"))
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
                    var next = RoleAt(colony, byId, anchor + 1);
                    if (next == null || (!next.WorkTypes.Contains("Childcare")
                                         && !next.WorkTypes.Contains("Warden")))
                        break;
                    anchor++;
                }
                return anchor;
            }

            int lastLeadingAuto = -1;
            while (lastLeadingAuto + 1 < colony.OrderTemplate.Count)
            {
                var next = RoleAt(colony, byId, lastLeadingAuto + 1);
                if (next == null || !next.AutoAssign) break;
                lastLeadingAuto++;
            }
            return lastLeadingAuto;
        }

        private static bool IsWorkRole(Dictionary<int, RoleView> byId, int roleId)
        {
            return byId.TryGetValue(roleId, out var role)
                && !role.AutoAssign && role.PrimarySkill != null
                && role.PrimarySkill != "Medicine" && role.PrimarySkill != "Social";
        }

        private static RoleView RoleAt(ColonyView colony, Dictionary<int, RoleView> byId,
            int templateIndex)
        {
            int roleId = colony.OrderTemplate[templateIndex];
            return byId.TryGetValue(roleId, out var role) ? role : null;
        }

        private static long AfterTemplateIndex(int templateIndex)
            => templateIndex < 0 ? -Slot / 2 : templateIndex * Slot + Slot / 2;

        private static SignalBucket BlockStrength(EngineContext context, int pawnIndex, PathView path)
        {
            var best = SignalBucket.Awful;
            foreach (int id in path.RoleIds)
                if (context.Candidates[pawnIndex].TryGetValue(id, out var candidate)
                    && ApplicablePathId(context, pawnIndex, id) == path.Id
                    && candidate.Strength > best)
                    best = candidate.Strength;
            return best;
        }
    }
}
