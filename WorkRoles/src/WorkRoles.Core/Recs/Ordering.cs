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
        /// then the hunter-tier and fire-safety overrides. A role in several
        /// anchored paths takes the LAST path's key (paths list order).
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
            if (hunterId != -1 && keys.ContainsKey(hunterId))
            {
                int tier = context.HunterTiers[pawnIndex];
                if (tier == 0) keys[hunterId] = -Slot / 20;       // food before skilled work
                else if (tier == 2) keys[hunterId] = long.MaxValue;
            }
            if (colony.FireBlockerRoleId != -1 && keys.ContainsKey(colony.FireBlockerRoleId))
                keys[colony.FireBlockerRoleId] = long.MinValue;   // veto must lead the list
            return keys;
        }

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
