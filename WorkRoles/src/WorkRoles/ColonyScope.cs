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
        [System.ThreadStatic] private static List<Thing> gravEngineSearch;

        internal static void InvalidateLanguageCaches()
        {
            locationsCache = null;
            locationsStamp = -1;
            locationsMapCount = -1;
        }

        internal static void ReleaseSnapshot()
        {
            InvalidateLanguageCaches();
            gravEngineSearch = null;
        }

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
                var place = PlaceOf(map, out var gravEngine);
                if (!place.IsSettlement && !place.IsShip) continue;
                // Unnamed ships fall back to a short label — the map parent's
                // ("Gravship landing site") overflows every dropdown.
                string label = place.IsShip
                    ? (!gravEngine.nameHidden
                        ? gravEngine.RenamableLabel
                        : "WR_ShipFallback".Translate().ToString())
                    : map.Parent?.LabelCap.ToString() ?? "?";
                result.Add(new LocationInfo
                {
                    Id = place.LocationId,
                    Label = label,
                    IsShip = place.IsShip
                });
            }
            return result;
        }

        /// A gravship map that isn't parked at a settlement — a ship landed at
        /// one of the player's settlements counts as that settlement.
        internal static bool IsShipMap(Map map) => PlaceOf(map).IsShip;

        internal static bool IsSettlementMap(Map map) => PlaceOf(map).IsSettlement;

        /// The pawn's place for Core location-rule matching: settlement = home,
        /// not ship.
        internal static PawnPlace PlaceOf(Pawn pawn) => PlaceOf(pawn.MapHeld);

        private static PawnPlace PlaceOf(Map map) =>
            PlaceOf(map, out _);

        private static PawnPlace PlaceOf(
            Map map, out Building_GravEngine gravEngine)
        {
            gravEngine = FindGravEngineFresh(map);
            bool hasEngine = gravEngine != null;
            bool ship = hasEngine
                && !(map?.Parent is RimWorld.Planet.Settlement);
            bool playerHome = map != null
                && (map.wasSpawnedViaGravShipLanding
                    || (map.Parent != null
                        && map.Parent.Faction == Faction.OfPlayer
                        && map.Parent.def.canBePlayerHome)
                    || hasEngine);
            return new PawnPlace
            {
                LocationId = LocationId(map),
                IsSettlement = playerHome && !ship,
                IsShip = ship,
            };
        }

        /// RimWorld's public grav-engine query caches by game tick. A spawn,
        /// despawn or holder transfer can therefore return the old answer for
        /// the remainder of that tick; compiled snapshots need the post-event
        /// state, so mirror the vanilla lookup without that temporal cache.
        private static Building_GravEngine FindGravEngineFresh(Map map)
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
