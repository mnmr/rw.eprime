using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Multiplayer.API;
using RimShared.Common;
using RimWorld;
using Verse;
using WorkRoles.Core;

namespace WorkRoles
{
    /// Game-side adapter for the Core scope engine: enumerates the player's
    /// locations (ships and settlements) and places pawns in them. Engine code
    /// (RoleRules, RoleIO) consumes it, so it lives outside the UI layer.
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

        private sealed class LocationSnapshot : IReadOnlyList<LocationInfo>
        {
            private readonly List<LocationInfo> locations;

            internal LocationSnapshot(List<LocationInfo> locations)
            {
                this.locations = locations;
            }

            internal bool ContentEquals(List<LocationInfo> other)
            {
                if (other == null || locations.Count != other.Count)
                    return false;
                for (int i = 0; i < locations.Count; i++)
                {
                    LocationInfo left = locations[i];
                    LocationInfo right = other[i];
                    if (!string.Equals(left.Id, right.Id,
                            System.StringComparison.Ordinal)
                        || !string.Equals(left.Label, right.Label,
                            System.StringComparison.Ordinal)
                        || left.IsShip != right.IsShip
                        || left.IsActive != right.IsActive)
                        return false;
                }
                return true;
            }

            public int Count => locations.Count;
            public LocationInfo this[int index] => locations[index];
            public IEnumerator<LocationInfo> GetEnumerator() =>
                locations.GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        private sealed class LocationSnapshotEntry
        {
            internal int Stamp = -1;
            internal LocationSnapshot? Snapshot;
        }

        private static readonly IReadOnlyList<LocationInfo> NoLocations =
            new LocationInfo[0];

        // Owner: process, partitioned by the active map set. Key: canonical Map
        // reference identity. Value: a private classification projection; its
        // game-owned references are observed but never mutated. Dependencies:
        // map spawn/removal, parent kind/ownership, and grav-engine lifecycle;
        // stable map/engine identity strings are created only while rebuilding.
        // Refresh: event-driven by the exact lifecycle patches below. Equality:
        // a cache hit preserves the private value; rebuilt identity is not
        // published outside ColonyScope. Teardown: ReleaseSnapshot clears all
        // map entries and releases the canonical-floor-map owner state.
        private static readonly VersionedSnapshotCache<Map, MapClassification>
            mapClassifications = new VersionedSnapshotCache<Map, MapClassification>(
                BuildMapClassification);

        // Owner: process, partitioned by the current map set. Key: Faction
        // reference identity. Value: an immutable published location projection;
        // its producer-owned List is transferred without copying and never
        // mutated after publication. Mutable dependency stamps stay private in
        // the unpublished cache entry.
        // Dependencies: map-classification revision, map-set membership, faction,
        // language, and the sole current landed/traveling Gravship engine identity
        // and state. Refresh: immediate on the next Locations read after the
        // existing grav-engine/map transition events invalidate it; no polling.
        // Equality: an exact equal rebuild preserves snapshot identity; changed
        // contents publish a new snapshot. Teardown: ReleaseSnapshot/language or
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

        private static Faction ViewFaction
        {
            get
            {
                if (!MP.enabled) return Faction.OfPlayer;
                var faction = realPlayerFactionGetter?.Invoke();
                return faction ?? Faction.OfPlayer;
            }
        }

        internal static IReadOnlyList<LocationInfo> Locations() =>
            Locations(ViewFaction);

        internal static IReadOnlyList<LocationInfo> Locations(Faction faction)
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
                List<LocationInfo> rebuilt = BuildLocations(faction);
                if (entry.Snapshot == null
                    || !entry.Snapshot.ContentEquals(rebuilt))
                    entry.Snapshot = new LocationSnapshot(rebuilt);
            }
            return entry.Snapshot;
        }

        private static List<LocationInfo> BuildLocations(Faction faction)
        {
            var result = new List<LocationInfo>();
            var seen = new HashSet<string>();
            foreach (var map in Find.Maps)
            {
                var place = PlaceOf(map, faction, out var gravEngine,
                    out string? shipLocationId);
                if (!place.IsSettlement && !place.IsShip) continue;
                if (place.IsShip)
                {
                    AddShipLocation(result, seen, gravEngine,
                        shipLocationId, isActive: true);
                    continue;
                }

                // Floor maps canonicalize to their ground map's id: one
                // location per stack.
                string? locationId = place.LocationId;
                if (locationId == null || !seen.Add(locationId)) continue;
                result.Add(new LocationInfo(locationId,
                    map.Parent?.LabelCap.ToString() ?? "?", isShip: false));
                // A ship parked at a settlement is inactive there, but remains
                // visible and removable in the role picker under its own stable
                // identity.
                if (gravEngine != null)
                    AddShipLocation(result, seen, gravEngine,
                        shipLocationId, isActive: false);
            }

            // During flight the landing map is gone, but the same engine remains
            // attached to the game's singular Gravship world object.
            RimWorld.Planet.Gravship? travelingShip = Current.Game?.Gravship;
            Building_GravEngine? travelingEngine = travelingShip?.Engine;
            if (travelingEngine != null && travelingShip!.Faction == faction)
                AddShipLocation(result, seen, travelingEngine,
                    travelingEngine.ThingID, isActive: false);
            return result;
        }

        private static void AddShipLocation(List<LocationInfo> result,
            HashSet<string> seen, Building_GravEngine? engine,
            string? shipLocationId, bool isActive)
        {
            if (engine == null || shipLocationId.NullOrEmpty()
                || !seen.Add(shipLocationId!))
                return;
            // Unnamed ships fall back to a short label — the map parent's
            // ("Gravship landing site") overflows every dropdown.
            string label = !engine.nameHidden
                ? engine.RenamableLabel
                : "WR_ShipFallback".Translate().ToString();
            result.Add(new LocationInfo(
                shipLocationId!, label, isShip: true, isActive: isActive));
        }

        /// Authoritative load migration must not depend on ViewFaction (which
        /// is client-local in multifaction Multiplayer). Collect every
        /// player-owned settlement plus the game's singular player Gravship
        /// from cached invariant classifications instead.
        internal static string? CollectLocationMigrationFacts(
            ISet<string> liveSettlementTokens)
        {
            string? stableShipToken = null;
            foreach (var sourceMap in Find.Maps)
            {
                Map? map = FloorMaps.Canonical(sourceMap);
                if (map == null) continue;
                MapClassification classification = mapClassifications.Get(map);
                if (classification.OwnerFaction?.IsPlayer != true) continue;
                PawnPlace place = FactionLocationClassifier.Classify(
                    classification.MapLocationId!, // Classify tolerates null ids; its params predate nullable annotations
                    classification.ShipLocationId!,
                    ownedByFaction: true,
                    spawnedViaGravship: classification.SpawnedViaGravship,
                    parentCanBePlayerHome: classification.ParentCanBePlayerHome,
                    parentIsSettlement: classification.ParentIsSettlement,
                    hasGravEngine: classification.GravEngine != null);
                if (place.IsSettlement
                    && !classification.MapLocationId.NullOrEmpty())
                    liveSettlementTokens?.Add(
                        LocationRules.SettlementPrefix
                        + classification.MapLocationId);
                if (stableShipToken == null
                    && classification.GravEngine != null
                    && !classification.ShipLocationId.NullOrEmpty())
                    stableShipToken = LocationRules.ShipPrefix
                        + classification.ShipLocationId;
            }

            RimWorld.Planet.Gravship? travelingShip = Current.Game?.Gravship;
            Building_GravEngine? travelingEngine = travelingShip?.Engine;
            if (stableShipToken == null
                && travelingShip?.Faction?.IsPlayer == true)
                stableShipToken = LocationRules.ShipPrefix
                    + travelingEngine!.ThingID; // a traveling gravship always has its engine
            return stableShipToken;
        }

        /// Deterministic colony-location test for simulation-driven work
        /// (AutoOptimizer): classifies with Faction.OfPlayer — the ticking
        /// map's faction context under multiplayer — never the client-local
        /// view faction.
        internal static bool IsColonyLocationForSimulation(Map map)
        {
            PawnPlace place = PlaceOf(map, Faction.OfPlayer);
            return place.IsSettlement || place.IsShip;
        }

        /// A gravship map that isn't parked at a settlement — a ship landed at
        /// one of the player's settlements counts as that settlement.
        internal static bool IsShipMap(Map map) => PlaceOf(map, ViewFaction).IsShip;

        internal static bool IsSettlementMap(Map map) =>
            PlaceOf(map, ViewFaction).IsSettlement;

        /// The pawn's place for Core location-rule matching: settlement = home,
        /// not ship.
        internal static PawnPlace PlaceOf(Pawn pawn) =>
            pawn == null ? new PawnPlace() : PlaceOf(pawn.MapHeld, pawn.Faction);

        private static PawnPlace PlaceOf(Map? map, Faction? faction) =>
            PlaceOf(map, faction, out _);

        private static PawnPlace PlaceOf(
            Map? map, Faction? faction, out Building_GravEngine? gravEngine)
            => PlaceOf(map, faction, out gravEngine, out _);

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
        /// the remainder of that tick; compiled snapshots need the post-event
        /// state, so mirror the vanilla lookup without that temporal cache.
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
        /// root-holder move can change a map's location-rule classification.
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

        internal static string? LocationId(Map map) =>
            PlaceOf(map, ViewFaction).LocationId;

        /// Off-map pawns (caravans) report the location they departed from.
        internal static string? LocationIdOf(Pawn pawn) =>
            PawnLocationTracker.EffectiveLocationId(pawn);

        internal static string CurrentLocationId() => LocationId(Find.CurrentMap) ?? "";

        internal static List<Pawn> PawnsOnMap(Map map)
        {
            var result = new List<Pawn>();
            var faction = ViewFaction;
            if (map == null || faction == null) return result;
            foreach (var pawn in map.mapPawns.AllPawnsSpawned)
                if (IsFactionColonist(pawn, faction)) result.Add(pawn);
            return result;
        }

        private static bool IsFactionColonist(Pawn pawn, Faction faction) =>
            pawn?.Faction == faction
            && (pawn.IsFreeColonist || pawn.IsSlaveOfColony);

        /// Colonists and slaves within the scope (no babies): spawned map pawns,
        /// plus pawns travelling in player caravans under All.
        internal static List<Pawn> PawnsIn(ScopeOption scope)
        {
            var result = new List<Pawn>();
            var faction = ViewFaction;
            if (faction == null) return result;
            string currentId = CurrentLocationId();
            foreach (var map in Find.Maps)
            {
                if (!ScopeEngine.Matches(scope, LocationId(map), currentId)) continue;
                foreach (var pawn in map.mapPawns.AllPawnsSpawned)
                    if (IsFactionColonist(pawn, faction)) result.Add(pawn);
            }
            // Caravan pawns list under Everywhere and under the location they
            // departed from; rule matching still classifies them as caravanning.
            foreach (var caravan in Find.WorldObjects.Caravans)
            {
                if (caravan.Faction != faction) continue;
                foreach (var pawn in caravan.PawnsListForReading)
                {
                    if (!IsFactionColonist(pawn, faction)) continue;
                    if (scope.Kind == ScopeKind.All)
                    {
                        result.Add(pawn);
                        continue;
                    }
                    string? lastId = PawnLocationTracker.EffectiveLocationId(pawn);
                    if (lastId != null && ScopeEngine.Matches(scope, lastId, currentId))
                        result.Add(pawn);
                }
            }
            return result
                .Where(p => !p.DevelopmentalStage.Baby())
                .Distinct()
                .ToList();
        }

        internal static string LabelOf(ScopeOption option)
        {
            if (option.Kind == ScopeKind.All) return "WR_ScopeAll".Translate().ToString();
            if (option.Kind != ScopeKind.CurrentLocation) return option.Label ?? "";
            // The current location folds its name in ("Rimosa (current
            // location)"), so the menu carries no separate named entry for it.
            string currentId = CurrentLocationId();
            foreach (var location in Locations())
                if (location.Id == currentId)
                    return "WR_ScopeCurrentNamed".Translate(location.Label).ToString();
            return "WR_ScopeCurrent".Translate().ToString();
        }
    }
}
