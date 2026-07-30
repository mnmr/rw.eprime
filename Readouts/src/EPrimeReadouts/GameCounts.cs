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
            Map map)
        {
            var counts = new Dictionary<string, int>();
            long fp = 17;
            foreach (var pair in map.resourceCounter.AllCountedAmounts)
            {
                counts[pair.Key.defName] = pair.Value;
                fp = fp * 31 + pair.Key.shortHash;
                fp = fp * 31 + pair.Value;
            }

            int extraDefCount = GameResourceCatalog.ExtraCountedDefCount;
            for (int i = 0; i < extraDefCount; i++)
                counts[GameResourceCatalog.ExtraCountedDefAt(i).defName] = 0;

            // Mirror ResourceCounter.UpdateResourceCounts: stored things only
            // (haul destinations), inner-of-minified, fresh, not fogged.
            var groups = map.haulDestinationManager.AllGroupsListForReading;
            for (int i = 0; i < groups.Count; i++)
            {
                foreach (Thing held in groups[i].HeldThings)
                {
                    var inner = held.GetInnerIfMinified();
                    if (!GameResourceCatalog.IsExtraCountedDef(inner.def)) continue;
                    if (inner.IsNotFresh()) continue;
                    if (inner.SpawnedOrAnyParentSpawned && inner.PositionHeld.Fogged(inner.MapHeld))
                        continue;
                    counts[inner.def.defName] += inner.stackCount;
                }
            }

            for (int i = 0; i < extraDefCount; i++)
            {
                var def = GameResourceCatalog.ExtraCountedDefAt(i);
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

    }
}
