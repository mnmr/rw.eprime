using System.Collections.Generic;
using EPrimeReadouts.Core;
using RimWorld;
using Verse;

namespace EPrimeReadouts
{
    /// Builds the count payload used by GameRenderData. The caller owns the
    /// 204-tick cadence; this class performs one complete, deterministic pass.
    public static class GameCounts
    {
        internal static RenderCountSnapshot BuildSnapshot(
            Map map,
            ReadoutStore store,
            PoolSnapshot pools)
        {
            var counts = new Dictionary<string, int>();
            long fp = 17;
            foreach (var pair in map.resourceCounter.AllCountedAmounts)
            {
                counts[pair.Key.defName] = pair.Value;
                fp = fp * 31 + pair.Key.shortHash;
                fp = fp * 31 + pair.Value;
            }

            var extraDefs = new List<ThingDef>();
            var extraDefSet = new HashSet<ThingDef>();
            CollectExtraDefs(store, pools, extraDefs, extraDefSet);
            for (int i = 0; i < extraDefs.Count; i++) counts[extraDefs[i].defName] = 0;

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
                    counts[inner.def.defName] += inner.stackCount;
                }
            }

            for (int i = 0; i < extraDefs.Count; i++)
            {
                var def = extraDefs[i];
                int count = counts[def.defName];
                fp = fp * 31 + def.shortHash;
                fp = fp * 31 + count;
            }
            return new RenderCountSnapshot(counts, fp);
        }

        /// Current count for a single def from the shared render snapshot.
        public static int LiveCount(Map map, ReadoutStore store, ThingDef def)
        {
            if (map == null || store == null || def == null) return 0;
            var snapshot = GameRenderData.Get(map, store).Counts;
            return snapshot.Counts.TryGetValue(def.defName, out int count) ? count : 0;
        }

        private static void CollectExtraDefs(
            ReadoutStore store,
            PoolSnapshot pools,
            List<ThingDef> extraDefs,
            HashSet<ThingDef> extraDefSet)
        {
            foreach (var group in store.Model.Groups)
                foreach (var tier in group.Tiers)
                    foreach (var token in tier)
                    {
                        if (SlotToken.IsPoolRef(token))
                        {
                            if (!pools.TryGet(SlotToken.PoolId(token),
                                    out var members, out _, out _)) continue;
                            for (int i = 0; i < members.Count; i++)
                                AddExtraDef(members[i], extraDefs, extraDefSet);
                        }
                        else if (!SlotToken.IsPool(token))
                        {
                            AddExtraDef(SlotToken.MemberName(token), extraDefs, extraDefSet);
                        }
                    }
        }

        private static void AddExtraDef(
            string defName,
            List<ThingDef> extraDefs,
            HashSet<ThingDef> extraDefSet)
        {
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def == null || def.CountAsResource || !def.PlayerAcquirable) return;
            if (extraDefSet.Add(def)) extraDefs.Add(def);
        }
    }
}
