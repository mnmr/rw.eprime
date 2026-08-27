using System;
using System.Collections.Generic;

namespace RimShared.Common
{
    /// <summary>
    /// Deduplicates despawn notifications for explicitly managed owners. A
    /// respawn cancels the pending departure, and the drain predicate performs
    /// the authoritative managed/off-map check after the transition settles.
    /// </summary>
    public sealed class ManagedDepartureTracker<TOwner> where TOwner : class
    {
        private readonly HashSet<TOwner> pending;

        public ManagedDepartureTracker(IEqualityComparer<TOwner>? comparer = null)
        {
            pending = new HashSet<TOwner>(
                comparer ?? EqualityComparer<TOwner>.Default);
        }

        public int PendingCount => pending.Count;

        public bool Spawned(TOwner owner, bool managed)
        {
            if (owner == null) return false;
            pending.Remove(owner);
            return managed;
        }

        public bool Despawned(TOwner owner, bool managed)
        {
            return owner != null && managed && pending.Add(owner);
        }

        public bool StopTracking(TOwner owner) =>
            owner != null && pending.Remove(owner);

        public void Drain(Func<TOwner, bool> shouldInvalidate,
            Action<TOwner> invalidate)
        {
            if (shouldInvalidate == null)
                throw new ArgumentNullException(nameof(shouldInvalidate));
            if (invalidate == null)
                throw new ArgumentNullException(nameof(invalidate));

            foreach (TOwner owner in pending)
                if (shouldInvalidate(owner))
                    invalidate(owner);
            pending.Clear();
        }

        public void Clear() => pending.Clear();
    }
}
