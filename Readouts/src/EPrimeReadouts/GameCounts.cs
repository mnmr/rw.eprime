using System.Collections.Generic;
using EPrimeReadouts.Core;
using RimWorld;
using Verse;

namespace EPrimeReadouts
{
    /// Snapshots and fingerprints the vanilla per-map resource counter, plus
    /// an extra counting pass for defs the vanilla counter ignores but the
    /// player's pools/groups reference (PlayerAcquirable without
    /// CountAsResource — e.g. stone chunks). The extra pass mirrors
    /// ResourceCounter's semantics exactly (stored things only, fresh,
    /// unfogged) and recounts on the same 204-tick cadence.
    public static class GameCounts
    {
        private const int ExtraCountIntervalTicks = 204; // vanilla ResourceCounter cadence

        private static readonly Dictionary<ThingDef, int> extraCounts =
            new Dictionary<ThingDef, int>();
        private static readonly List<ThingDef> extraDefs = new List<ThingDef>();
        private static readonly HashSet<ThingDef> extraDefSet = new HashSet<ThingDef>();
        private static int extraDefsVersion = -1;
        private static Map extraMap;
        private static int extraLastTick = -999999;

        /// Cheap change probe: order-sensitive hash over the counter dict plus
        /// the extra-count pass. Enumeration order is stable because
        /// ResourceCounter rebuilds its dict from a statically ordered list on
        /// every recount and extraDefs keeps a fixed order, so identical
        /// contents always produce identical fingerprints.
        public static long Fingerprint(Map map, ReadoutStore store)
        {
            EnsureExtra(map, store);
            long fp = 17;
            foreach (var pair in map.resourceCounter.AllCountedAmounts)
            {
                fp = fp * 31 + pair.Key.shortHash;
                fp = fp * 31 + pair.Value;
            }
            for (int i = 0; i < extraDefs.Count; i++)
            {
                fp = fp * 31 + extraDefs[i].shortHash;
                fp = fp * 31 + (extraCounts.TryGetValue(extraDefs[i], out int c) ? c : 0);
            }
            return fp;
        }

        /// defName-keyed snapshot for the Core layout engine. Includes
        /// resourceReadoutAlwaysShow defs at their (possibly zero) counts,
        /// matching what vanilla surfaces, plus the extra-counted defs.
        public static Dictionary<string, int> Snapshot(Map map, ReadoutStore store)
        {
            EnsureExtra(map, store);
            var counts = new Dictionary<string, int>();
            foreach (var pair in map.resourceCounter.AllCountedAmounts)
                counts[pair.Key.defName] = pair.Value;
            foreach (var pair in extraCounts)
                counts[pair.Key.defName] = pair.Value;
            return counts;
        }

        /// Current count for a single def — vanilla counter value, or the
        /// extra-count value for defs vanilla ignores. Used by hover tooltips.
        public static int LiveCount(Map map, ReadoutStore store, ThingDef def)
        {
            if (def.CountAsResource) return map.resourceCounter.GetCount(def);
            EnsureExtra(map, store);
            return extraCounts.TryGetValue(def, out int c) ? c : 0;
        }

        private static void EnsureExtra(Map map, ReadoutStore store)
        {
            if (store == null || map == null) return;
            if (store.Version != extraDefsVersion)
            {
                RebuildExtraDefs(store);
                extraDefsVersion = store.Version;
                extraLastTick = -999999; // force a recount for the new def set
            }
            if (extraDefs.Count == 0)
            {
                extraCounts.Clear();
                extraMap = map;
                return;
            }
            int tick = Find.TickManager.TicksGame;
            if (map == extraMap && tick - extraLastTick < ExtraCountIntervalTicks) return;
            extraMap = map;
            extraLastTick = tick;

            extraCounts.Clear();
            for (int i = 0; i < extraDefs.Count; i++) extraCounts[extraDefs[i]] = 0;

            // Mirror ResourceCounter.UpdateResourceCounts: stored things only
            // (haul destinations), inner-of-minified, fresh, not fogged.
            var groups = map.haulDestinationManager.AllGroupsListForReading;
            for (int i = 0; i < groups.Count; i++)
            {
                foreach (var held in groups[i].HeldThings)
                {
                    var inner = held.GetInnerIfMinified();
                    if (!extraDefSet.Contains(inner.def)) continue;
                    if (inner.IsNotFresh()) continue;
                    if (inner.SpawnedOrAnyParentSpawned && inner.PositionHeld.Fogged(inner.MapHeld))
                        continue;
                    extraCounts[inner.def] += inner.stackCount;
                }
            }
        }

        /// Defs referenced by the store's groups/pools that vanilla never
        /// counts. Rebuilt only when the store version changes.
        private static void RebuildExtraDefs(ReadoutStore store)
        {
            extraDefs.Clear();
            extraDefSet.Clear();
            var snapshot = PoolSnapshot.Build(store.Model.Pools, GameResourceCatalog.Instance);
            foreach (var group in store.Model.Groups)
                foreach (var tier in group.Tiers)
                    foreach (var token in tier)
                    {
                        if (SlotToken.IsPoolRef(token))
                        {
                            if (!snapshot.TryGet(SlotToken.PoolId(token),
                                    out var members, out _, out _)) continue;
                            for (int i = 0; i < members.Count; i++)
                                AddExtraDef(members[i]);
                        }
                        else if (!SlotToken.IsPool(token))
                        {
                            AddExtraDef(SlotToken.MemberName(token));
                        }
                    }
        }

        private static void AddExtraDef(string defName)
        {
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def == null || def.CountAsResource || !def.PlayerAcquirable) return;
            if (extraDefSet.Add(def)) extraDefs.Add(def);
        }
    }
}
