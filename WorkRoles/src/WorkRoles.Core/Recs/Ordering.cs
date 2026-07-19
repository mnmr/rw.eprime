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
            return positions;
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

        /// Per-pawn sort keys: base positions; anchored path blocks replace
        /// their members' keys (band-min DESCENDING inside the block;
        /// same-anchor blocks rank by the pawn's strongest bucket in the
        /// block, then the block's smallest base position, then path id);
        /// then the unlisted-Hunter and fire-safety overrides. A role in
        /// several anchored paths takes the LAST path's key (paths list order).
        public static Dictionary<int, long> SortKeys(EngineContext context, int pawnIndex)
        {
            var colony = context.Colony;
            var keys = BasePositions(colony.Roles, colony.OrderTemplate);

            foreach (var group in colony.Paths
                         .Where(p => p.AnchorRoleId != -1 && keys.ContainsKey(p.AnchorRoleId))
                         .GroupBy(p => (p.AnchorRoleId, p.AnchorBefore)))
            {
                long anchorKey = keys[group.Key.AnchorRoleId];
                var blocks = group
                    .Select(path => (path,
                        strength: BlockStrength(context, pawnIndex, path),
                        basePos: path.RoleIds.Where(keys.ContainsKey)
                            .Select(id => keys[id]).DefaultIfEmpty(long.MaxValue).Min()))
                    .OrderByDescending(b => b.strength)
                    .ThenBy(b => b.basePos)
                    .ThenBy(b => b.path.Id)
                    .ToList();
                for (int rank = 0; rank < blocks.Count; rank++)
                {
                    var path = blocks[rank].path;
                    var members = Enumerable.Range(0, path.RoleIds.Count)
                        .OrderByDescending(i => path.BandMins[i])
                        .ThenBy(i => PathRoleReadiness(context, pawnIndex, path.RoleIds[i]))
                        .ThenBy(i => i)
                        .Select(i => path.RoleIds[i])
                        .ToList();
                    for (int m = 0; m < members.Count; m++)
                    {
                        if (!keys.ContainsKey(members[m])) continue;
                        keys[members[m]] = group.Key.AnchorBefore
                            ? anchorKey - (blocks.Count - rank) * 1000 + m
                            : anchorKey + (rank + 1) * 1000 + m;
                    }
                }
            }

            int hunterId = colony.HunterRoleId;
            if (hunterId != -1 && keys.ContainsKey(hunterId)
                && !colony.OrderTemplate.Contains(hunterId))
            {
                int tier = context.HunterTiers[pawnIndex];
                if (tier >= 0) keys[hunterId] = HunterPosition(colony, tier);
            }
            if (colony.FireBlockerRoleId != -1 && keys.ContainsKey(colony.FireBlockerRoleId))
                keys[colony.FireBlockerRoleId] = long.MinValue;   // veto must lead the list
            return keys;
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

        private static long HunterPosition(ColonyView colony, int tier)
        {
            if (tier >= 3) return long.MaxValue;
            int lowAnchor = LowHunterAnchor(colony);
            if (tier == 0) return AfterTemplateIndex(lowAnchor);

            var workIndices = Enumerable.Range(0, colony.OrderTemplate.Count)
                .Where(i => IsWorkRole(colony, colony.OrderTemplate[i]))
                .ToList();
            if (workIndices.Count == 0) return AfterTemplateIndex(lowAnchor);
            return AfterTemplateIndex(tier == 1 ? workIndices[0] : workIndices[workIndices.Count - 1]);
        }

        /// Tier 0 follows the template's BasicWorker role plus immediately
        /// following Childcare/Warden roles. Without that anchor, it follows
        /// the template's leading auto-assigned roles.
        private static int LowHunterAnchor(ColonyView colony)
        {
            int basics = -1;
            for (int i = 0; i < colony.OrderTemplate.Count; i++)
            {
                var role = RoleAt(colony, i);
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
                    var next = RoleAt(colony, anchor + 1);
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
                var next = RoleAt(colony, lastLeadingAuto + 1);
                if (next == null || !next.AutoAssign) break;
                lastLeadingAuto++;
            }
            return lastLeadingAuto;
        }

        private static bool IsWorkRole(ColonyView colony, int roleId)
        {
            var role = colony.Roles.FirstOrDefault(r => r.Id == roleId);
            return role != null && !role.AutoAssign && role.PrimarySkill != null
                && role.PrimarySkill != "Medicine" && role.PrimarySkill != "Social";
        }

        private static RoleView RoleAt(ColonyView colony, int templateIndex)
        {
            int roleId = colony.OrderTemplate[templateIndex];
            return colony.Roles.FirstOrDefault(r => r.Id == roleId);
        }

        private static long AfterTemplateIndex(int templateIndex)
            => templateIndex < 0 ? -Slot / 2 : templateIndex * Slot + Slot / 2;

        private static SignalBucket BlockStrength(EngineContext context, int pawnIndex, PathView path)
        {
            var best = SignalBucket.Awful;
            foreach (int id in path.RoleIds)
                if (context.Candidates[pawnIndex].TryGetValue(id, out var candidate)
                    && candidate.Strength > best)
                    best = candidate.Strength;
            return best;
        }
    }
}
