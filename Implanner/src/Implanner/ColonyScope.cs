using System.Collections;
using System.Collections.Generic;
using Multiplayer.API;
using Implanner.Core;
using RimShared.Common;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Implanner
{
    // Copied from WorkRoles and adapted for Implanner (an intentional
    // independent copy; divergence from WorkRoles is expected). Adaptations:
    // free colonists only (no slaves), mutants that can receive no implants
    // are excluded, and there is no origin/last-location tracking — caravans
    // are their own presentation groupings and their pawns are Away.

    /// Game-side adapter for serviceable-location discovery: enumerates the
    /// player's locations (ships and settlements) and places pawns in them.
    internal static class ColonyScope
    {
        private sealed class MapClassification
        {
            internal Building_GravEngine? GravEngine;
            internal string? MapLocationId;
            internal string? ShipLocationId;
            internal Faction? OwnerFaction;
            internal bool SpawnedViaGravship;
            internal bool ParentCanBePlayerHome;
            internal bool ParentIsSettlement;
        }

        private sealed class LocationSnapshot : IReadOnlyList<GroupingInfo>
        {
            private readonly List<GroupingInfo> locations;

            internal LocationSnapshot(List<GroupingInfo> locations)
            {
                this.locations = locations;
            }

            internal bool ContentEquals(List<GroupingInfo> other)
            {
                if (other == null || locations.Count != other.Count)
                    return false;
                for (int i = 0; i < locations.Count; i++)
                {
                    GroupingInfo left = locations[i];
                    GroupingInfo right = other[i];
                    if (!string.Equals(left.Id, right.Id,
                            System.StringComparison.Ordinal)
                        || !string.Equals(left.Label, right.Label,
                            System.StringComparison.Ordinal)
                        || left.IsShip != right.IsShip
                        || left.IsCaravan != right.IsCaravan)
                        return false;
                }
                return true;
            }

            public int Count => locations.Count;
            public GroupingInfo this[int index] => locations[index];
            public IEnumerator<GroupingInfo> GetEnumerator() =>
                locations.GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        private sealed class LocationSnapshotEntry
        {
            internal int Stamp = -1;
            internal LocationSnapshot? Snapshot;
        }

        private static readonly IReadOnlyList<GroupingInfo> NoLocations =
            new GroupingInfo[0];

        // Owner: process, partitioned by the active map set. Key: canonical Map
        // reference identity. Value: a private classification projection; its
        // game-owned references are observed but never mutated. Dependencies:
        // map spawn/removal, parent kind/ownership, and grav-engine lifecycle;
        // stable map/engine identity strings are created only while rebuilding.
        // Refresh: event-driven by the exact lifecycle patches in
        // Patch_LocationTransitions. Equality: a cache hit preserves the
        // private value; rebuilt identity is not published outside ColonyScope.
        // Teardown: ReleaseSnapshot clears all map entries and releases the
        // canonical-floor-map owner state.
        private static readonly VersionedSnapshotCache<Map, MapClassification>
            mapClassifications = new VersionedSnapshotCache<Map, MapClassification>(
                BuildMapClassification);

        // Owner: process, partitioned by the current map set. Key: Faction
        // reference identity. Value: an immutable published map-location
        // projection (settlements and ships only; caravan groupings are built
        // by the overview snapshot, which owns their volatility). The
        // producer-owned List is transferred without copying and never mutated
        // after publication. Dependencies: map-classification revision,
        // map-set membership, faction, language, and the sole current
        // landed/traveling Gravship engine identity and state. Refresh:
        // immediate on the next Locations read after the transition events
        // invalidate it; no polling. Equality: an exact equal rebuild
        // preserves snapshot identity. Teardown: ReleaseSnapshot/language or
        // map-set invalidation clears faction entries and their owned buffers.
        private static readonly Dictionary<Faction, LocationSnapshotEntry>
            locationSnapshots = new Dictionary<Faction, LocationSnapshotEntry>(
                ReferenceIdentityComparer<Faction>.Instance);
        private static int locationsMapCount = -1;
        [System.ThreadStatic] private static List<Thing>? gravEngineSearch;

        internal static int LocationRevision => mapClassifications.Revision;

        internal static void InvalidateLanguageCaches()
        {
            locationSnapshots.Clear();
        }

        internal static void InvalidateClassification(Map? map)
        {
            map = FloorMaps.Canonical(map);
            if (map == null) return;
            mapClassifications.Invalidate(map);
            locationSnapshots.Clear();
        }

        internal static void InvalidateMapSet()
        {
            FloorMaps.ReleaseForTeardown();
            mapClassifications.Clear();
            locationSnapshots.Clear();
            locationsMapCount = Find.Maps?.Count ?? -1;
        }

        internal static void ReleaseSnapshot()
        {
            InvalidateLanguageCaches();
            mapClassifications.Clear();
            locationsMapCount = -1;
            gravEngineSearch = null;
            FloorMaps.ReleaseForTeardown();
        }

        // MP.RealPlayerFaction exists only in Multiplayer API 0.5+, but the
        // first Multiplayer.API assembly in mod-list order wins resolution, so
        // an older stub shipped by another mod would make a direct call throw
        // MissingMethodException at JIT time. Bind via reflection once instead.
        private static readonly System.Func<Faction>? realPlayerFactionGetter =
            ResolveRealPlayerFactionGetter();

        private static System.Func<Faction>? ResolveRealPlayerFactionGetter()
        {
            var getter = typeof(MP).GetProperty(
                "RealPlayerFaction",
                System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.Static)?.GetGetMethod();
            if (getter == null || getter.ReturnType != typeof(Faction))
                return null;
            return (System.Func<Faction>)System.Delegate.CreateDelegate(
                typeof(System.Func<Faction>), getter);
        }

        /// The faction presentation code shows: the LOCAL client's player
        /// faction. Per-client by design — it must never feed synchronized
        /// mutations.
        internal static Faction ViewFaction
        {
            get
            {
                if (!MP.enabled) return Faction.OfPlayer;
                var faction = realPlayerFactionGetter?.Invoke();
                return faction ?? Faction.OfPlayer;
            }
        }

        /// The faction the deterministic tick path and synced commands use.
        /// Faction.OfPlayer is identical on every multiplayer client during
        /// synchronized execution (Multiplayer swaps it under the synced
        /// faction context), unlike MP.RealPlayerFaction, which is the local
        /// client's faction and diverges in multifaction sessions.
        internal static Faction AuthoritativeFaction => Faction.OfPlayer;

        /// Settlement and ship groupings only; caravans are appended by the
        /// overview snapshot builder.
        internal static IReadOnlyList<GroupingInfo> Locations() =>
            Locations(ViewFaction);

        internal static IReadOnlyList<GroupingInfo> Locations(Faction faction)
        {
            var maps = Find.Maps;
            if (locationsMapCount != maps.Count)
                InvalidateMapSet();
            if (faction == null) return NoLocations;
            if (!locationSnapshots.TryGetValue(faction, out var entry))
            {
                entry = new LocationSnapshotEntry();
                locationSnapshots.Add(faction, entry);
            }
            if (entry.Snapshot == null
                || entry.Stamp != mapClassifications.Revision)
            {
                entry.Stamp = mapClassifications.Revision;
                locationsMapCount = maps.Count;
                List<GroupingInfo> rebuilt = BuildLocations(faction);
                if (entry.Snapshot == null
                    || !entry.Snapshot.ContentEquals(rebuilt))
                    entry.Snapshot = new LocationSnapshot(rebuilt);
            }
            return entry.Snapshot;
        }

        private static List<GroupingInfo> BuildLocations(Faction faction)
        {
            var result = new List<GroupingInfo>();
            var seen = new HashSet<string>();
            foreach (var map in Find.Maps)
            {
                var place = PlaceOf(map, faction, out var gravEngine,
                    out string? shipLocationId);
                if (!place.IsSettlement && !place.IsShip) continue;
                if (place.IsShip)
                {
                    AddShipLocation(result, seen, gravEngine,
                        shipLocationId);
                    continue;
                }

                // Floor maps canonicalize to their ground map's id: one
                // location per stack.
                string? locationId = place.LocationId;
                if (locationId == null || !seen.Add(locationId)) continue;
                result.Add(new GroupingInfo(locationId,
                    map.Parent?.LabelCap.ToString() ?? "?",
                    isShip: false, isCaravan: false));
            }
            return result;
        }

        private static void AddShipLocation(List<GroupingInfo> result,
            HashSet<string> seen, Building_GravEngine? engine,
            string? shipLocationId)
        {
            if (engine == null || shipLocationId.NullOrEmpty()
                || !seen.Add(shipLocationId!))
                return;
            // Unnamed ships fall back to a short label — the map parent's
            // ("Gravship landing site") overflows every dropdown.
            string label = !engine.nameHidden
                ? engine.RenamableLabel
                : "IMP_ShipFallback".Translate().ToString();
            result.Add(new GroupingInfo(
                shipLocationId!, label, isShip: true, isCaravan: false));
        }

        /// The pawn's place. Serviceable (settlement or ship) locations can
        /// execute automation; everywhere else is Away.
        internal static PawnPlace PlaceOf(Pawn pawn) =>
            pawn == null ? new PawnPlace() : PlaceOf(pawn.MapHeld, pawn.Faction);

        private static PawnPlace PlaceOf(Map? map, Faction? faction) =>
            PlaceOf(map, faction, out _, out _);

        private static PawnPlace PlaceOf(
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

        /// Presentation overload: resolves against the local view faction.
        internal static string? LocationId(Map map) =>
            PlaceOf(map, ViewFaction).LocationId;

        /// Deterministic overload for the reconcile path and synced commands.
        internal static string? LocationId(Map map, Faction faction) =>
            PlaceOf(map, faction).LocationId;

        /// The pawn's grouping id: serviceable map location, caravan token,
        /// or null (nowhere — listed only under All).
        internal static string? GroupingIdOf(Pawn pawn)
        {
            if (pawn == null) return null;
            if (pawn.MapHeld != null)
                return PlaceOf(pawn).LocationId;
            var caravan = pawn.GetCaravan();
            return caravan != null
                ? LocationGrouping.CaravanPrefix + caravan.ID.ToStringCached()
                : null;
        }

        internal static string CurrentLocationId() => LocationId(Find.CurrentMap) ?? "";

        /// A planable colonist: an adult free colonist of the viewed faction
        /// that can, in principle, receive implants. Mutants that
        /// categorically cannot (Anomaly ghouls) are excluded entirely, as
        /// are children and babies.
        internal static bool IsPlanableColonist(Pawn pawn, Faction faction) =>
            pawn?.Faction == faction
            && pawn.IsFreeColonist
            && !pawn.IsMutant
            && pawn.DevelopmentalStage == DevelopmentalStage.Adult;

        /// Presentation overload: the local view faction's colonists.
        internal static List<Pawn> AllPlanableColonists() =>
            AllPlanableColonists(ViewFaction);

        /// Every planable colonist in the world: spawned map pawns plus pawns
        /// travelling in player caravans. Builder path only, never per-frame.
        /// Deterministic paths pass AuthoritativeFaction.
        internal static List<Pawn> AllPlanableColonists(Faction faction)
        {
            var result = new List<Pawn>();
            if (faction == null) return result;
            foreach (var map in Find.Maps)
                foreach (var pawn in map.mapPawns.AllPawnsSpawned)
                    if (IsPlanableColonist(pawn, faction))
                        result.Add(pawn);
            foreach (var caravan in Find.WorldObjects.Caravans)
            {
                if (caravan.Faction != faction) continue;
                foreach (var pawn in caravan.PawnsListForReading)
                    if (IsPlanableColonist(pawn, faction))
                        result.Add(pawn);
            }
            return result;
        }
    }
}
