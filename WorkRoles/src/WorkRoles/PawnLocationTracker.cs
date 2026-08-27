using System.Collections.Generic;
using RimShared.Common;
using Verse;
using WorkRoles.Core;

namespace WorkRoles
{
    /// Tracks each managed pawn's last canonical location (RoleStore-scribed) and
    /// dedups map-transition invalidation: hops between floors of one stack
    /// change nothing location-relevant, so they no longer recompile or bump
    /// the UI, while off-map pawns keep the location they departed from for
    /// scope listing and recommendations.
    internal static class PawnLocationTracker
    {
        private static readonly ManagedDepartureTracker<Pawn> departures =
            new ManagedDepartureTracker<Pawn>(ReferenceIdentityComparer<Pawn>.Instance);

        internal static void NotifySpawned(Pawn pawn)
        {
            if (pawn == null) return;
            var store = RoleStore.Current;
            if (!departures.Spawned(pawn, store?.IsManaged(pawn) == true)) return;
            RecordCurrentLocation(store!, pawn, invalidateOnChange: true); // Spawned only reports managed pawns, which implies a store
        }

        /// A pawn's first assignment starts location tracking immediately; this
        /// is needed when the assignment happens after SpawnSetup.
        internal static void NotifyManaged(Pawn pawn)
        {
            var store = RoleStore.Current;
            if (pawn == null || store?.IsManaged(pawn) != true) return;
            RecordCurrentLocation(store, pawn, invalidateOnChange: false);
        }

        /// The final assignment owns the lifetime of both transient and scribed
        /// location state.
        internal static void NotifyUnmanaged(Pawn pawn)
        {
            if (pawn == null) return;
            departures.StopTracking(pawn);
            RoleStore.Current?.lastLocationIds.Remove(pawn);
        }

        private static void RecordCurrentLocation(RoleStore store, Pawn pawn,
            bool invalidateOnChange)
        {
            string? id = ColonyScope.PlaceOf(pawn).LocationId;
            if (store.lastLocationIds.TryGetValue(pawn, out string previous)
                && previous == id)
                return;
            if (id == null) store.lastLocationIds.Remove(pawn);
            else store.lastLocationIds[pawn] = id;
            if (!invalidateOnChange) return;
            if (ExternalPawnFacts.IsRelevant(pawn))
                ExternalPawnFacts.Invalidate(pawn);
            CompiledJobOrders.Invalidate(pawn);
        }

        internal static void NotifyDespawned(Pawn pawn)
        {
            departures.Despawned(pawn,
                RoleStore.Current?.IsManaged(pawn) == true);
        }

        /// Drains from the game-component tick: a pawn that respawned was
        /// already handled by NotifySpawned (or skipped for a same-location
        /// floor hop); one still off-map left the map world (caravan, world
        /// storage) and invalidates once so Caravans-rule gating engages. Its
        /// last-location entry deliberately survives.
        internal static void ProcessPendingDepartures()
        {
            if (departures.PendingCount == 0) return;
            var store = RoleStore.Current;
            departures.Drain(
                pawn => store?.IsManaged(pawn) == true
                    && !pawn.Destroyed && pawn.MapHeld == null,
                pawn =>
                {
                    if (ExternalPawnFacts.IsRelevant(pawn))
                        ExternalPawnFacts.Invalidate(pawn);
                    CompiledJobOrders.Invalidate(pawn);
                });
        }

        internal static void NotifyDestroyed(Pawn pawn)
        {
            if (pawn == null) return;
            departures.StopTracking(pawn);
            RoleStore.Current?.lastLocationIds.Remove(pawn);
        }

        internal static void ReleaseForTeardown() => departures.Clear();

        /// Canonical location id of the pawn's map, or the last one it
        /// departed from while off-map; null when unknown.
        internal static string? EffectiveLocationId(Pawn pawn)
        {
            if (pawn == null) return null;
            var held = pawn.MapHeld;
            if (held != null) return ColonyScope.PlaceOf(pawn).LocationId;
            var store = RoleStore.Current;
            return store != null
                && store.lastLocationIds.TryGetValue(pawn, out string id)
                ? id : null;
        }

        /// One-time load normalization refreshes spawned managed pawns after
        /// legacy numeric departure ids have been read.
        internal static void RefreshManagedLocations()
        {
            var store = RoleStore.Current;
            if (store == null) return;
            foreach (var pawn in store.pawnSets.Keys)
                if (pawn?.MapHeld != null)
                    RecordCurrentLocation(store, pawn,
                        invalidateOnChange: false);
        }
    }
}
