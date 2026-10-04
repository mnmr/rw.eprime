using System.Collections;
using System.Collections.Generic;
using Implanner.Core;
using RimShared.Common;
using RimShared.GameLib;
using RimShared.UiLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Implanner
{
    // Adapted from WorkRoles' ColonyScope (an intentional independent copy;
    // divergence from WorkRoles is expected); the map classification core,
    // FloorMaps and the transition patches are shared (RimShared.GameLib).
    // Adaptations: free colonists only (no slaves), mutants that can receive
    // no implants are excluded, and there is no origin/last-location
    // tracking — caravans are their own presentation groupings and their
    // pawns are Away.

    /// Game-side adapter for serviceable-location discovery: enumerates the
    /// player's locations (ships and settlements) and places pawns in them.
    internal static class ColonyScope
    {
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
            // Labels come from translated parent labels and the ship
            // fallback string, so the entry also observes the language.
            internal int LanguageStamp = -1;
            internal LocationSnapshot? Snapshot;
        }

        private static readonly IReadOnlyList<GroupingInfo> NoLocations =
            new GroupingInfo[0];

        // Owner: process, partitioned by the current map set. Key: Faction
        // reference identity. Value: an immutable published map-location
        // projection (settlements and ships only; caravan groupings are built
        // by the overview snapshot, which owns their volatility). The
        // producer-owned List is transferred without copying and never mutated
        // after publication. Dependencies: map-classification revision
        // (MapClassifications.LocationRevision), map-set membership, faction,
        // language, and the sole current landed/traveling Gravship engine
        // identity and state. Refresh: immediate on the next Locations read
        // after the transition events invalidate it; no polling. Equality: an
        // exact equal rebuild preserves snapshot identity. Teardown:
        // MapClassifications.ReleaseSnapshot, classification or map-set
        // invalidation (through InvalidateLocationSnapshots) clears faction
        // entries and their owned buffers.
        private static readonly Dictionary<Faction, LocationSnapshotEntry>
            locationSnapshots = new Dictionary<Faction, LocationSnapshotEntry>(
                ReferenceIdentityComparer<Faction>.Instance);

        internal static void InvalidateLocationSnapshots()
        {
            locationSnapshots.Clear();
        }

        /// Settlement and ship groupings only; caravans are appended by the
        /// overview snapshot builder.
        internal static IReadOnlyList<GroupingInfo> Locations() =>
            Locations(PlayerFactions.ViewFaction);

        internal static IReadOnlyList<GroupingInfo> Locations(Faction faction)
        {
            MapClassifications.ObserveMapCount(Find.Maps.Count);
            if (faction == null) return NoLocations;
            if (!locationSnapshots.TryGetValue(faction, out var entry))
            {
                entry = new LocationSnapshotEntry();
                locationSnapshots.Add(faction, entry);
            }
            int language = UiRevision.LanguageCurrent;
            int revision = MapClassifications.LocationRevision;
            if (entry.Snapshot == null
                || entry.Stamp != revision
                || entry.LanguageStamp != language)
            {
                entry.Stamp = revision;
                entry.LanguageStamp = language;
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
                var place = MapClassifications.PlaceOf(map, faction,
                    out var gravEngine, out string? shipLocationId);
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

        /// Whether automation can reach the pawn where they are: spawned on
        /// a map, or carried by another pawn (a rescue in progress). A pawn
        /// sealed in a building (cryptosleep casket, biosculpter pod, landed
        /// transporter) is alive and keeps every record, but is Away: it
        /// can neither operate nor be operated on, and must not set the
        /// doctor floor or take a surgery slot.
        internal static bool IsOperable(Pawn pawn) =>
            pawn.Spawned || pawn.ParentHolder is Pawn_CarryTracker;

        /// The pawn's place. Serviceable (settlement or ship) locations can
        /// execute automation; everywhere else, including inside a building
        /// that holds the pawn, is Away.
        internal static PawnPlace PlaceOf(Pawn pawn) =>
            pawn == null || !IsOperable(pawn)
                ? new PawnPlace()
                : MapClassifications.PlaceOf(pawn.MapHeld, pawn.Faction);

        /// Presentation overload: resolves against the local view faction.
        internal static string? LocationId(Map map) =>
            MapClassifications.PlaceOf(map, PlayerFactions.ViewFaction).LocationId;

        /// Deterministic overload for the reconcile path and synced commands.
        internal static string? LocationId(Map map, Faction faction) =>
            MapClassifications.PlaceOf(map, faction).LocationId;

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
            && !pawn.Dead
            && pawn.IsFreeColonist
            && !pawn.IsMutant
            && pawn.DevelopmentalStage == DevelopmentalStage.Adult;

        /// Presentation overload: the local view faction's colonists.
        internal static List<Pawn> AllPlanableColonists() =>
            AllPlanableColonists(PlayerFactions.ViewFaction);

        /// Every planable colonist alive anywhere: pawns on any map, spawned
        /// or held (carried, in a cryptosleep casket, inside a landed
        /// transporter), plus pawns in player caravans, in transporters in
        /// flight, and aboard a gravship in flight — the reach of
        /// PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive,
        /// filtered to planable colonists. Being alive but off every
        /// serviceable map reads as Away (PlaceOf): state is kept, no new
        /// work is issued. Builder path only, never per-frame. Deterministic
        /// paths pass PlayerFactions.AuthoritativeFaction.
        internal static List<Pawn> AllPlanableColonists(Faction faction)
        {
            var result = new List<Pawn>();
            if (faction == null) return result;
            List<Map> maps = Find.Maps;
            for (int m = 0; m < maps.Count; m++)
            {
                // Spawned plus held (AllPawnsUnspawned walks the map's
                // holder graph and already drops the dead).
                List<Pawn> pawns = maps[m].mapPawns.AllPawns;
                for (int i = 0; i < pawns.Count; i++)
                    if (IsPlanableColonist(pawns[i], faction))
                        result.Add(pawns[i]);
            }
            List<Caravan> caravans = Find.WorldObjects.Caravans;
            for (int c = 0; c < caravans.Count; c++)
            {
                Caravan caravan = caravans[c];
                if (caravan.Faction != faction) continue;
                List<Pawn> pawns = caravan.PawnsListForReading;
                for (int i = 0; i < pawns.Count; i++)
                    if (IsPlanableColonist(pawns[i], faction))
                        result.Add(pawns[i]);
            }
            List<TravellingTransporters> transporters =
                Find.WorldObjects.TravellingTransporters;
            for (int t = 0; t < transporters.Count; t++)
                foreach (Pawn pawn in transporters[t].Pawns)
                    if (IsPlanableColonist(pawn, faction))
                        result.Add(pawn);
            // A gravship in flight holds its crew off every map; pawns
            // still spawned somewhere (the ship has landed and the holder
            // graph is being re-spawned) are already listed above.
            Gravship? gravship = Find.CurrentGravship;
            if (gravship != null)
                foreach (Pawn pawn in gravship.Pawns)
                    if (!pawn.SpawnedOrAnyParentSpawned
                        && IsPlanableColonist(pawn, faction))
                        result.Add(pawn);
            return result;
        }
    }
}
