using System;
using System.Collections.Generic;

namespace WorkRoles.Core
{
    /// Coalesces synchronous floor transfers before publishing a departure.
    /// Owner: a store; key: pawn identity; value: pending canonical location.
    /// Dependencies: explicit despawn/spawn notifications only. Refresh: drain
    /// after the transfer settles. Equality: a return to the pending canonical
    /// location is silent. Teardown: Clear; removed pawns use StopTracking.
    public sealed class CanonicalDepartureTracker<TOwner> where TOwner : class
    {
        private readonly Dictionary<TOwner, string?> pending;

        public CanonicalDepartureTracker(IEqualityComparer<TOwner>? comparer = null)
        {
            pending = new Dictionary<TOwner, string?>(comparer ?? EqualityComparer<TOwner>.Default);
        }

        public int PendingCount => pending.Count;

        public void Despawned(TOwner owner, string? canonicalLocation)
        {
            if (owner != null && !pending.ContainsKey(owner))
                pending.Add(owner, canonicalLocation);
        }

        /// Returns whether this arrival changed roster/location facts.
        public bool Spawned(TOwner owner, string? canonicalLocation)
        {
            if (owner == null) return false;
            bool departing = pending.TryGetValue(owner, out string? previous);
            pending.Remove(owner);
            return !departing || previous != canonicalLocation;
        }

        public void Drain(Func<TOwner, bool> stillOffMap, Action<TOwner> departed)
        {
            foreach (TOwner owner in pending.Keys)
                if (stillOffMap(owner)) departed(owner);
            pending.Clear();
        }

        public void StopTracking(TOwner owner)
        {
            if (owner != null) pending.Remove(owner);
        }

        public void Clear() => pending.Clear();
    }
}
