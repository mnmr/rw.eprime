using System.Collections.Generic;
using Verse;

namespace WorkRoles
{
    /// Tracks each pawn's last canonical location (RoleStore-scribed) and
    /// dedups map-transition invalidation: hops between floors of one stack
    /// change nothing location-relevant, so they no longer recompile or bump
    /// the UI, while off-map pawns keep the location they departed from for
    /// scope listing and recommendations.
    internal static class PawnLocationTracker
    {
        private static readonly HashSet<Pawn> pendingDepartures = new HashSet<Pawn>();

        internal static void NotifySpawned(Pawn pawn)
        {
            if (pawn == null) return;
            pendingDepartures.Remove(pawn);
            var store = RoleStore.Current;
            if (store == null)
            {
                CompiledJobOrders.Invalidate(pawn);
                return;
            }
            int id = FloorMaps.Canonical(pawn.Map)?.uniqueID ?? -1;
            if (store.lastLocationMapIds.TryGetValue(pawn, out int previous)
                && previous == id)
                return;
            store.lastLocationMapIds[pawn] = id;
            CompiledJobOrders.Invalidate(pawn);
        }

        internal static void NotifyDespawned(Pawn pawn)
        {
            if (pawn != null) pendingDepartures.Add(pawn);
        }

        /// Drains from the game-component tick: a pawn that respawned was
        /// already handled by NotifySpawned (or skipped for a same-location
        /// floor hop); one still off-map left the map world (caravan, world
        /// storage) and invalidates once so Caravans-rule gating engages. Its
        /// last-location entry deliberately survives.
        internal static void ProcessPendingDepartures()
        {
            if (pendingDepartures.Count == 0) return;
            foreach (var pawn in pendingDepartures)
                if (!pawn.Destroyed && pawn.MapHeld == null)
                    CompiledJobOrders.Invalidate(pawn);
            pendingDepartures.Clear();
        }

        internal static void NotifyDestroyed(Pawn pawn)
        {
            if (pawn == null) return;
            pendingDepartures.Remove(pawn);
            RoleStore.Current?.lastLocationMapIds.Remove(pawn);
        }

        internal static void ReleaseForTeardown() => pendingDepartures.Clear();

        /// Canonical location id of the pawn's map, or the last one it
        /// departed from while off-map; null when unknown.
        internal static string EffectiveLocationId(Pawn pawn)
        {
            if (pawn == null) return null;
            var held = pawn.MapHeld;
            if (held != null) return ColonyScope.LocationId(held);
            var store = RoleStore.Current;
            return store != null
                && store.lastLocationMapIds.TryGetValue(pawn, out int id)
                && id >= 0
                ? id.ToStringCached()
                : null;
        }
    }
}
