using System.Collections.Generic;
using EPrimeReadouts.Core;
using RimWorld;
using Verse;

namespace EPrimeReadouts
{
    /// Builds the count payload used by GameRenderData. The caller owns the
    /// 204-tick stock cadence; this class performs one deterministic stock pass
    /// and merges the independently cached planned-work contribution.
    /// When the map belongs to a MultiFloors stack, the pass covers every
    /// level map in ascending level order so readouts show stack totals.
    public static class GameCounts
    {
        internal static RenderCountSnapshot BuildSnapshot(
            Map map,
            int tick,
            CountSnapshotOptions options)
        {
            var accumulator = new CountAccumulator();
            Dictionary<int, Map>? levels = LevelStacks.LevelsOf(map);
            if (levels == null)
            {
                AccumulateMap(map, tick, accumulator, options);
                return accumulator.ToSnapshot();
            }

            // Ascending level order keeps the pass deterministic; the queried
            // map is accumulated directly if the controller omits it.
            var order = new List<int>(levels.Keys);
            order.Sort();
            bool sawQueriedMap = false;
            for (int i = 0; i < order.Count; i++)
            {
                Map level = levels[order[i]];
                if (level == null || level.Disposed) continue;
                if (ReferenceEquals(level, map)) sawQueriedMap = true;
                AccumulateMap(level, tick, accumulator, options);
            }
            if (!sawQueriedMap)
                AccumulateMap(map, tick, accumulator, options);
            return accumulator.ToSnapshot();
        }

        private static void AccumulateMap(
            Map map,
            int tick,
            CountAccumulator accumulator,
            CountSnapshotOptions options)
        {
            // Zero-valued entries are skipped: consumers resolve a missing
            // key as zero, and the search candidate universe comes from
            // GameResourceCatalog.SearchableDefNames rather than zero-seeded
            // snapshot entries. This keeps the published dictionary sized by
            // what actually exists, not by the loaded def database.
            foreach (var pair in map.resourceCounter.AllCountedAmounts)
                if (pair.Value != 0)
                    accumulator.Add(pair.Key.defName, pair.Key.shortHash, pair.Value);

            // Stored pass, mirroring ResourceCounter.UpdateResourceCounts:
            // haul destinations, inner-of-minified, fresh, not fogged. Extra
            // counted defs feed the group-count basis exactly as vanilla's
            // counter would if it knew them; every stored stack additionally
            // feeds the search breakdown with its forbidden flag (read from
            // the outer thing — a minified wrapper carries the comp).
            var groups = map.haulDestinationManager.AllGroupsListForReading;
            for (int i = 0; i < groups.Count; i++)
            {
                foreach (Thing held in groups[i].HeldThings)
                {
                    var inner = held.GetInnerIfMinified();
                    bool extra = GameResourceCatalog.IsExtraCountedDef(inner.def);
                    if (!extra && !inner.def.CountAsResource) continue;
                    if (inner.IsNotFresh()) continue;
                    if (inner.SpawnedOrAnyParentSpawned && inner.PositionHeld.Fogged(inner.MapHeld))
                        continue;
                    if (extra)
                        accumulator.Add(inner.def.defName, inner.def.shortHash, inner.stackCount);
                    accumulator.AddSearch(inner.def.defName, inner.def.shortHash,
                        inner.stackCount, stored: true,
                        forbidden: options.InspectForbidden
                            && held.IsForbidden(Faction.OfPlayer));
                }
            }

            // Scattered pass: spawned haulables outside any slot group. It is
            // omitted entirely for the storage-only basis; otherwise these
            // stacks widen the shared count breakdown to map-wide totals.
            if (options.IncludeScattered)
            {
                var things = map.listerThings.ThingsInGroup(
                    ThingRequestGroup.HaulableEver);
                for (int i = 0; i < things.Count; i++)
                {
                    Thing thing = things[i];
                    if (thing.IsInAnyStorage()) continue;
                    var inner = thing.GetInnerIfMinified();
                    if (!inner.def.CountAsResource
                        && !GameResourceCatalog.IsExtraCountedDef(inner.def)) continue;
                    if (inner.IsNotFresh()) continue;
                    if (thing.Position.Fogged(map)) continue;
                    accumulator.AddSearch(inner.def.defName, inner.def.shortHash,
                        inner.stackCount, stored: false,
                        forbidden: options.InspectForbidden
                            && thing.IsForbidden(Faction.OfPlayer));
                }
            }

            // The expensive bill/buildable walk has its own 1020-tick cache.
            // Replay its compact immutable result into every 204-tick stock
            // refresh so debt remains present between planned-work scans.
            if (options.PlannedWork.Any)
                GamePlannedWorkData.Get(map, tick,
                    options.PlannedWork).AccumulateInto(accumulator);
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
