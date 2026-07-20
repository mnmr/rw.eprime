using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace WorkRoles.Core
{
    public sealed class ReferenceIdentityComparer<T> : IEqualityComparer<T>
        where T : class
    {
        public static readonly ReferenceIdentityComparer<T> Instance =
            new ReferenceIdentityComparer<T>();

        private ReferenceIdentityComparer() { }

        public bool Equals(T x, T y) => ReferenceEquals(x, y);

        public int GetHashCode(T obj) =>
            obj == null ? 0 : RuntimeHelpers.GetHashCode(obj);
    }

    public static class IdentityKeySweepPlanner
    {
        public static IReadOnlyList<TKey> StaleKeys<TKey>(
            IEnumerable<TKey> storedKeys,
            IEnumerable<TKey> liveKeys)
            where TKey : class
        {
            if (storedKeys == null) throw new ArgumentNullException(nameof(storedKeys));
            if (liveKeys == null) throw new ArgumentNullException(nameof(liveKeys));

            if (liveKeys is HashSet<TKey> candidate
                && ReferenceEquals(candidate.Comparer, ReferenceIdentityComparer<TKey>.Instance))
                return StaleKeysAgainstSet(storedKeys, candidate);

            var live = new HashSet<TKey>(ReferenceIdentityComparer<TKey>.Instance);
            foreach (TKey key in liveKeys)
                if (key != null)
                    live.Add(key);

            return StaleKeysAgainstSet(storedKeys, live);
        }

        private static IReadOnlyList<TKey> StaleKeysAgainstSet<TKey>(
            IEnumerable<TKey> storedKeys,
            HashSet<TKey> live)
            where TKey : class
        {
            var stale = new List<TKey>();
            foreach (TKey key in storedKeys)
                if (key == null || !live.Contains(key))
                    stale.Add(key);
            return stale;
        }
    }
}
