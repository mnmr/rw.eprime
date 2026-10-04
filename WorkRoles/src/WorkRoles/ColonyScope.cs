using System.Collections;
using System.Collections.Generic;
using System.Linq;
using RimShared.Common;
using RimShared.GameLib;
using RimWorld;
using Verse;
using WorkRoles.Core;

namespace WorkRoles
{
    /// Game-side adapter for the Core scope engine: enumerates the player's
    /// locations (ships and settlements) and places pawns in them. Engine code
    /// (RoleRules, RoleIO) consumes it, so it lives outside the UI layer. The
    /// map classification core, FloorMaps and the transition patches are
    /// shared (RimShared.GameLib).
    internal static class ColonyScope
    {
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

        // Owner: process, partitioned by the current map set. Key: Faction
        // reference identity. Value: an immutable published location projection;
        // its producer-owned List is transferred without copying and never
        // mutated after publication. Mutable dependency stamps stay private in
        // the unpublished cache entry.
        // Dependencies: map-classification revision
        // (MapClassifications.LocationRevision), map-set membership, faction,
        // language, and the sole current landed/traveling Gravship engine identity
        // and state. Refresh: immediate on the next Locations read after the
        // existing grav-engine/map transition events invalidate it; no polling.
        // Equality: an exact equal rebuild preserves snapshot identity; changed
        // contents publish a new snapshot. Teardown:
        // MapClassifications.ReleaseSnapshot, language, classification or
        // map-set invalidation (through InvalidateLocationSnapshots) clears
        // faction entries and their owned buffers.
        private static readonly Dictionary<Faction, LocationSnapshotEntry>
            locationSnapshots = new Dictionary<Faction, LocationSnapshotEntry>(
                ReferenceIdentityComparer<Faction>.Instance);

        internal static void InvalidateLocationSnapshots()
        {
            locationSnapshots.Clear();
        }

        internal static IReadOnlyList<LocationInfo> Locations() =>
            Locations(PlayerFactions.ViewFaction);

        internal static IReadOnlyList<LocationInfo> Locations(Faction faction)
        {
            MapClassifications.ObserveMapCount(Find.Maps.Count);
            if (faction == null) return NoLocations;
            if (!locationSnapshots.TryGetValue(faction, out var entry))
            {
                entry = new LocationSnapshotEntry();
                locationSnapshots.Add(faction, entry);
            }
            int revision = MapClassifications.LocationRevision;
            if (entry.Snapshot == null
                || entry.Stamp != revision)
            {
                entry.Stamp = revision;
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
                var place = MapClassifications.PlaceOf(map, faction,
                    out var gravEngine, out string? shipLocationId);
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
                MapClassification classification = MapClassifications.Get(map);
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
            PawnPlace place = MapClassifications.PlaceOf(map, Faction.OfPlayer);
            return place.IsSettlement || place.IsShip;
        }

        /// A gravship map that isn't parked at a settlement — a ship landed at
        /// one of the player's settlements counts as that settlement.
        internal static bool IsShipMap(Map map) =>
            MapClassifications.PlaceOf(map, PlayerFactions.ViewFaction).IsShip;

        internal static bool IsSettlementMap(Map map) =>
            MapClassifications.PlaceOf(map, PlayerFactions.ViewFaction).IsSettlement;

        /// The pawn's place for Core location-rule matching: settlement = home,
        /// not ship.
        internal static PawnPlace PlaceOf(Pawn pawn) =>
            pawn == null
                ? new PawnPlace()
                : MapClassifications.PlaceOf(pawn.MapHeld, pawn.Faction);

        internal static string? LocationId(Map map) =>
            MapClassifications.PlaceOf(map, PlayerFactions.ViewFaction).LocationId;

        /// Off-map pawns (caravans) report the location they departed from.
        internal static string? LocationIdOf(Pawn pawn) =>
            PawnLocationTracker.EffectiveLocationId(pawn);

        internal static string CurrentLocationId() => LocationId(Find.CurrentMap) ?? "";

        internal static List<Pawn> PawnsOnMap(Map map)
        {
            var result = new List<Pawn>();
            var faction = PlayerFactions.ViewFaction;
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
            var faction = PlayerFactions.ViewFaction;
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
