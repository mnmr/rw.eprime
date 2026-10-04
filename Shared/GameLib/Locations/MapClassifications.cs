using System.Collections.Generic;
using RimShared.Common;
using RimWorld;
using Verse;

namespace RimShared.GameLib
{
    /// Faction-invariant facts about one canonical map, captured when its
    /// classification is built. Game-owned references are observed, never
    /// mutated.
    internal sealed class MapClassification
    {
        internal Building_GravEngine? GravEngine;
        internal string? MapLocationId;
        internal string? ShipLocationId;
        internal Faction? OwnerFaction;
        internal bool SpawnedViaGravship;
        internal bool ParentCanBePlayerHome;
        internal bool ParentIsSettlement;
    }

    /// Label-free map classification behind each mod's ColonyScope: places a
    /// map (settlement, ship, or elsewhere) for a faction. The transition
    /// patches in Patch_LocationTransitions keep it current; each mod projects
    /// its own location list from it.
    internal static partial class MapClassifications
    {
        // Owner: process (one per mod assembly), partitioned by the active map
        // set. Key: canonical Map reference identity. Value: a classification
        // projection; its game-owned references are observed but never
        // mutated. Dependencies: map spawn/removal, parent kind/ownership, and
        // grav-engine lifecycle; stable map/engine identity strings are created
        // only while rebuilding. Refresh: event-driven by the exact lifecycle
        // patches in Patch_LocationTransitions, plus the read-side map-count
        // guard (ObserveMapCount). Equality: a cache hit preserves the value;
        // rebuilt identity is not published outside the mod's ColonyScope.
        // Teardown: ReleaseSnapshot clears all map entries and the mod's
        // location snapshots, and releases the canonical-floor-map owner state.
        private static readonly VersionedSnapshotCache<Map, MapClassification>
            mapClassifications = new VersionedSnapshotCache<Map, MapClassification>(
                BuildMapClassification);
        private static int locationsMapCount = -1;
        [System.ThreadStatic] private static List<Thing>? gravEngineSearch;

        internal static int LocationRevision => mapClassifications.Revision;

        /// Each mod's ColonyScope drops the location snapshots it projects from
        /// these classifications. Required: a mod without it does not compile.
        private static partial void InvalidateLocationSnapshots();

        internal static void InvalidateClassification(Map? map)
        {
            map = FloorMaps.Canonical(map);
            if (map == null) return;
            mapClassifications.Invalidate(map);
            InvalidateLocationSnapshots();
        }

        internal static void InvalidateMapSet()
        {
            FloorMaps.ReleaseForTeardown();
            mapClassifications.Clear();
            InvalidateLocationSnapshots();
            locationsMapCount = Find.Maps?.Count ?? -1;
        }

        /// Location reads pass the live map count; a count other than the one
        /// last observed invalidates the map set.
        internal static void ObserveMapCount(int mapCount)
        {
            if (locationsMapCount != mapCount)
                InvalidateMapSet();
        }

        internal static void ReleaseSnapshot()
        {
            InvalidateLocationSnapshots();
            mapClassifications.Clear();
            locationsMapCount = -1;
            gravEngineSearch = null;
            FloorMaps.ReleaseForTeardown();
        }

        /// The classification of a map already canonicalized by FloorMaps.
        internal static MapClassification Get(Map canonicalMap) =>
            mapClassifications.Get(canonicalMap);

        internal static PawnPlace PlaceOf(Map? map, Faction? faction) =>
            PlaceOf(map, faction, out _, out _);

        internal static PawnPlace PlaceOf(
            Map? map, Faction? faction, out Building_GravEngine? gravEngine,
            out string? shipLocationId)
        {
            // Floor maps classify as their ground map: grav machinery must sit
            // in the ground substructure footprint, so the engine search stays
            // single-map.
            map = FloorMaps.Canonical(map);
            if (map == null)
            {
                gravEngine = null;
                shipLocationId = null;
                return new PawnPlace();
            }
            MapClassification classification = mapClassifications.Get(map);
            gravEngine = classification.GravEngine;
            shipLocationId = classification.ShipLocationId;
            return FactionLocationClassifier.Classify(
                classification.MapLocationId,
                classification.ShipLocationId,
                faction != null && classification.OwnerFaction == faction,
                classification.SpawnedViaGravship,
                classification.ParentCanBePlayerHome,
                classification.ParentIsSettlement,
                gravEngine != null);
        }

        private static MapClassification BuildMapClassification(Map map)
        {
            Building_GravEngine? gravEngine = FindGravEngineFresh(map);
            return new MapClassification
            {
                GravEngine = gravEngine,
                MapLocationId = map?.uniqueID.ToStringCached(),
                ShipLocationId = gravEngine?.ThingID,
                OwnerFaction = map?.Parent?.Faction ?? gravEngine?.Faction,
                SpawnedViaGravship = map?.wasSpawnedViaGravShipLanding == true,
                ParentCanBePlayerHome = map?.Parent?.def.canBePlayerHome == true,
                ParentIsSettlement = map?.Parent is RimWorld.Planet.Settlement,
            };
        }

        /// RimWorld's public grav-engine query caches by game tick. A spawn,
        /// despawn or holder transfer can therefore return the old answer for
        /// the remainder of that tick; snapshots need the post-event state, so
        /// mirror the vanilla lookup without that temporal cache.
        private static Building_GravEngine? FindGravEngineFresh(Map map)
        {
            if (!ModsConfig.OdysseyActive || map == null) return null;

            var engineDef = ThingDefOf.GravEngine;
            var engines = map.listerThings.ThingsOfDef(engineDef);
            for (int i = 0; i < engines.Count; i++)
                if (engines[i] is Building_GravEngine engine)
                    return engine;

            var minifiedDef = engineDef.minifiedDef;
            var minified = map.listerThings.ThingsOfDef(minifiedDef);
            for (int i = 0; i < minified.Count; i++)
                if (minified[i].GetInnerIfMinified()
                    is Building_GravEngine engine)
                    return engine;

            var search = gravEngineSearch
                ?? (gravEngineSearch = new List<Thing>());
            search.Clear();
            try
            {
                ThingOwnerUtility.GetAllThingsRecursively(
                    map, ThingRequest.ForDef(minifiedDef), search,
                    true, null, false);
                for (int i = 0; i < search.Count; i++)
                    if (search[i].GetInnerIfMinified()
                        is Building_GravEngine engine)
                        return engine;
                return null;
            }
            finally
            {
                // The reusable buffer may retain capacity, never world things.
                search.Clear();
            }
        }

        /// Transition patches use the same definition test to decide whether a
        /// root-holder move can change a map's classification.
        internal static bool ContainsGravEngine(Thing thing)
        {
            if (!ModsConfig.OdysseyActive || thing == null) return false;

            var engineDef = ThingDefOf.GravEngine;
            if (thing.def == engineDef
                || (thing.def == engineDef.minifiedDef
                    && thing.GetInnerIfMinified()?.def == engineDef))
                return true;
            if (!(thing is IThingHolder holder)) return false;

            var search = gravEngineSearch
                ?? (gravEngineSearch = new List<Thing>());
            search.Clear();
            try
            {
                ThingOwnerUtility.GetAllThingsRecursively(
                    holder, search, true, null);
                for (int i = 0; i < search.Count; i++)
                {
                    var held = search[i];
                    if (held.def == engineDef
                        || (held.def == engineDef.minifiedDef
                            && held.GetInnerIfMinified()?.def == engineDef))
                        return true;
                }
                return false;
            }
            finally
            {
                search.Clear();
            }
        }
    }
}
