using System;
using RimShared.Common;
using Verse;
using WorkRoles.Core;

namespace WorkRoles
{
    /// Tracks managed pawns' scribed last locations and coalesces map transfers
    /// for every roster-relevant pawn, including those without assignments.
    internal static class PawnLocationTracker
    {
        // Store-owned pending events, keyed by pawn identity; immutable canonical
        // departure ids exist only until respawn or the next component tick.
        // Spawn/despawn events are the only inputs. Same-canonical transfers
        // publish nothing. Store replacement/teardown and destruction release
        // entries; no pawn graph survives in a completed departure.
        private static readonly CanonicalDepartureTracker<Pawn> departures =
            new CanonicalDepartureTracker<Pawn>(ReferenceIdentityComparer<Pawn>.Instance);
        private static RoleStore? owner;
        private static readonly Func<Pawn, bool> StillOffMap = IsStillOffMap;
        private static readonly Action<Pawn> PublishDeparture = PublishLocationChange;

        private static RoleStore? CurrentStore()
        {
            var store = RoleStore.Current;
            if (!ReferenceEquals(owner, store))
            {
                departures.Clear();
                owner = store;
            }
            return store;
        }

        internal static void NotifySpawned(Pawn pawn)
        {
            if (pawn == null) return;
            var store = CurrentStore();
            if (store == null || !ExternalPawnFacts.IsRelevant(pawn)) return;
            string? id = ColonyScope.PlaceOf(pawn).LocationId;
            bool changed = departures.Spawned(pawn, id);
            if (store.IsManaged(pawn)) RecordCurrentLocation(store, pawn);
            if (changed) PublishLocationChange(pawn);
        }

        /// A pawn's first assignment starts location tracking immediately; this
        /// is needed when the assignment happens after SpawnSetup.
        internal static void NotifyManaged(Pawn pawn)
        {
            var store = CurrentStore();
            if (pawn == null || store?.IsManaged(pawn) != true) return;
            RecordCurrentLocation(store, pawn);
        }

        /// Scribed departure history belongs to managed assignments. A colonist
        /// remains in the roster transition tracker after becoming unmanaged.
        internal static void NotifyUnmanaged(Pawn pawn)
        {
            if (pawn == null) return;
            var store = CurrentStore();
            if (!ExternalPawnFacts.IsRelevant(pawn)) departures.StopTracking(pawn);
            if (store?.lastLocationIds.Remove(pawn) == true && pawn.MapHeld == null)
                UiVersion.RosterChanged();
        }

        private static void RecordCurrentLocation(RoleStore store, Pawn pawn)
        {
            string? id = ColonyScope.PlaceOf(pawn).LocationId;
            if (store.lastLocationIds.TryGetValue(pawn, out string previous)
                && previous == id)
                return;
            if (id == null) store.lastLocationIds.Remove(pawn);
            else store.lastLocationIds[pawn] = id;
        }

        internal static string? CaptureDeparture(Pawn pawn) =>
            pawn?.Spawned == true && CurrentStore() != null
                && ExternalPawnFacts.IsRelevant(pawn)
                    ? ColonyScope.PlaceOf(pawn).LocationId : null;

        internal static void NotifyDespawned(Pawn pawn, string? previousLocation)
        {
            CurrentStore();
            if (previousLocation != null && pawn?.MapHeld == null)
                departures.Despawned(pawn!, previousLocation);
        }

        /// Drains from the game-component tick: a pawn that respawned was
        /// already handled by NotifySpawned (or skipped for a same-location
        /// floor hop); one still off-map left the map world (caravan, world
        /// storage) and invalidates once so Caravans-rule gating engages. Its
        /// last-location entry deliberately survives.
        internal static void ProcessPendingDepartures()
        {
            var store = CurrentStore();
            if (store == null || departures.PendingCount == 0) return;
            using var batch = store.Revisions.Batch();
            departures.Drain(StillOffMap, PublishDeparture);
        }

        private static bool IsStillOffMap(Pawn pawn) =>
            !pawn.Destroyed && pawn.MapHeld == null && ExternalPawnFacts.IsRelevant(pawn);

        private static void PublishLocationChange(Pawn pawn)
        {
            using var batch = RoleStore.Current?.Revisions.Batch();
            UiVersion.RosterChanged();
            ExternalPawnFacts.Invalidate(pawn);
            CompiledJobOrders.Invalidate(pawn);
        }

        internal static void NotifyDestroyed(Pawn pawn)
        {
            if (pawn == null) return;
            var store = CurrentStore();
            departures.StopTracking(pawn);
            store?.lastLocationIds.Remove(pawn);
        }

        internal static void ReleaseForTeardown()
        {
            departures.Clear();
            owner = null;
        }

        /// Canonical location id of the pawn's map, or the last one it
        /// departed from while off-map; null when unknown.
        internal static string? EffectiveLocationId(Pawn pawn)
        {
            if (pawn == null) return null;
            var held = pawn.MapHeld;
            if (held != null) return ColonyScope.PlaceOf(pawn).LocationId;
            var store = CurrentStore();
            return store != null
                && store.lastLocationIds.TryGetValue(pawn, out string id)
                ? id : null;
        }

        /// One-time load normalization refreshes spawned managed pawns after
        /// legacy numeric departure ids have been read.
        internal static void RefreshManagedLocations()
        {
            var store = CurrentStore();
            if (store == null) return;
            foreach (var pawn in store.pawnSets.Keys)
                if (pawn?.MapHeld != null)
                    RecordCurrentLocation(store, pawn);
        }
    }
}
