using System.Collections.Generic;
using EPrimeReadouts.Core;
using RimWorld;
using Verse;

namespace EPrimeReadouts
{
    /// Builds the count payload used by GameRenderData. The caller owns the
    /// 204-tick cadence; this class performs one complete, deterministic pass.
    /// When the map belongs to a MultiFloors stack, the pass covers every
    /// level map in ascending level order so readouts show stack totals.
    public static class GameCounts
    {
        internal static RenderCountSnapshot BuildSnapshot(
            Map map,
            PlannedWorkOptions plannedWork,
            QualityJobsPlannedWorkSnapshot qualityJobs)
        {
            var accumulator = new CountAccumulator();
            Dictionary<int, Map>? levels = LevelStacks.LevelsOf(map);
            if (levels == null)
            {
                AccumulateMap(map, accumulator, plannedWork, qualityJobs);
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
                AccumulateMap(level, accumulator, plannedWork, qualityJobs);
            }
            if (!sawQueriedMap)
                AccumulateMap(map, accumulator, plannedWork, qualityJobs);
            return accumulator.ToSnapshot();
        }

        private static void AccumulateMap(
            Map map,
            CountAccumulator accumulator,
            PlannedWorkOptions plannedWork,
            QualityJobsPlannedWorkSnapshot qualityJobs)
        {
            foreach (var pair in map.resourceCounter.AllCountedAmounts)
                accumulator.Add(pair.Key.defName, pair.Key.shortHash, pair.Value);

            int extraDefCount = GameResourceCatalog.ExtraCountedDefCount;
            for (int i = 0; i < extraDefCount; i++)
            {
                ThingDef def = GameResourceCatalog.ExtraCountedDefAt(i);
                accumulator.AddZero(def.defName, def.shortHash);
            }

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
                        forbidden: held.IsForbidden(Faction.OfPlayer));
                }
            }

            // Scattered pass: spawned haulables outside any slot group. The
            // group-count basis stays storage-only (vanilla behavior); these
            // stacks only widen the search breakdown so the search options can
            // include or exclude loose items.
            var things = map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver);
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
                    forbidden: thing.IsForbidden(Faction.OfPlayer));
            }

            // Planned-work reservations share this map walk and this cadence, so
            // the debt a counter shows always belongs to the same instant as the
            // stock it was subtracted from. No-op when every option is off.
            if (plannedWork.Any)
                PlannedWorkCounts.Accumulate(
                    map, accumulator, plannedWork, qualityJobs);
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
