using System.Collections.Generic;
using System.Linq;
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
        // Open-window snapshot (UiVersion + map count): several draw paths hit
        // this per row/pass, and each rebuild walks all maps with gravship
        // lookups. Callers must never mutate the returned list.
        private static List<LocationInfo> locationsCache;
        private static int locationsStamp = -1;
        private static int locationsMapCount = -1;

        internal static List<LocationInfo> Locations()
        {
            var maps = Find.Maps;
            if (locationsCache == null || locationsStamp != UiVersion.Current
                || locationsMapCount != maps.Count)
            {
                locationsStamp = UiVersion.Current;
                locationsMapCount = maps.Count;
                locationsCache = BuildLocations();
            }
            return locationsCache;
        }

        private static List<LocationInfo> BuildLocations()
        {
            var result = new List<LocationInfo>();
            foreach (var map in Find.Maps)
            {
                if (!map.IsPlayerHome) continue;
                bool ship = IsShipMap(map);
                // Unnamed ships fall back to a short label — the map parent's
                // ("Gravship landing site") overflows every dropdown.
                string label = ship
                    ? (GravshipUtility.TryGetNameOfGravshipOnMap(map, out var shipName)
                        ? shipName : "WR_ShipFallback".Translate().ToString())
                    : map.Parent?.LabelCap.ToString() ?? "?";
                result.Add(new LocationInfo { Id = LocationId(map), Label = label, IsShip = ship });
            }
            return result;
        }

        /// A gravship map that isn't parked at a settlement — a ship landed at
        /// one of the player's settlements counts as that settlement.
        internal static bool IsShipMap(Map map) =>
            GravshipUtility.PlayerHasGravEngine(map)
            && !(map.Parent is RimWorld.Planet.Settlement);

        internal static bool IsSettlementMap(Map map) => map.IsPlayerHome && !IsShipMap(map);

        /// The pawn's place, for Core location-rule matching. IsShipMap is
        /// evaluated once (it hits GravshipUtility): settlement = home, not ship.
        internal static PawnPlace PlaceOf(Pawn pawn)
        {
            var map = pawn.MapHeld;
            bool ship = map != null && IsShipMap(map);
            return new PawnPlace
            {
                LocationId = LocationId(map),
                IsSettlement = map != null && map.IsPlayerHome && !ship,
                IsShip = ship,
            };
        }

        internal static string LocationId(Map map) => map?.uniqueID.ToStringCached();

        internal static string LocationIdOf(Pawn pawn) => LocationId(pawn.MapHeld);

        internal static string CurrentLocationId() => LocationId(Find.CurrentMap);

        /// Colonists and slaves within the scope (no babies): spawned map pawns,
        /// plus pawns travelling in player caravans under All.
        internal static List<Pawn> PawnsIn(ScopeOption scope)
        {
            var result = new List<Pawn>();
            string currentId = CurrentLocationId();
            foreach (var map in Find.Maps)
            {
                if (!ScopeEngine.Matches(scope, LocationId(map), currentId)) continue;
                result.AddRange(map.mapPawns.FreeColonistsSpawned);
                result.AddRange(map.mapPawns.SlavesOfColonySpawned);
            }
            if (scope.Kind == ScopeKind.All)
                foreach (var caravan in Find.WorldObjects.Caravans)
                {
                    if (!caravan.IsPlayerControlled) continue;
                    foreach (var pawn in caravan.PawnsListForReading)
                        if (pawn.IsFreeColonist || pawn.IsSlaveOfColony)
                            result.Add(pawn);
                }
            return result
                .Where(p => !p.DevelopmentalStage.Baby())
                .Distinct()
                .ToList();
        }

        internal static string LabelOf(ScopeOption option) =>
            option.Kind == ScopeKind.All ? "WR_ScopeAll".Translate().ToString()
            : option.Kind == ScopeKind.CurrentLocation ? "WR_ScopeCurrent".Translate().ToString()
            : option.Label;
    }
}
