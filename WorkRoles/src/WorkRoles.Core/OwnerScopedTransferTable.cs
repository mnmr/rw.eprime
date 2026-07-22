using System;
using System.Runtime.CompilerServices;

namespace WorkRoles.Core
{
    /// Weak-key transient state that is valid only for the exact owning context.
    /// A lookup from a new owner (for example, after changing worlds) discards the
    /// stale value without retaining either the key or its former owner strongly.
    public sealed class OwnerScopedTransferTable<TKey, TOwner, TValue>
        where TKey : class
        where TOwner : class
    {
        private sealed class Entry
        {
            internal readonly WeakReference<TOwner> Owner;
            internal readonly TValue Value;

            internal Entry(TOwner owner, TValue value)
            {
                Owner = new WeakReference<TOwner>(owner);
                Value = value;
            }
        }

        private ConditionalWeakTable<TKey, Entry> entries =
            new ConditionalWeakTable<TKey, Entry>();

        /// Explicit owner-lifecycle release. Weak keys prevent object retention,
        /// but a teardown boundary should also discard every logically pending
        /// transfer instead of waiting for the keys to be collected.
        public void Clear()
        {
            entries = new ConditionalWeakTable<TKey, Entry>();
        }

        public void Set(TKey key, TOwner owner, TValue value)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            entries.Remove(key);
            entries.Add(key, new Entry(owner, value));
        }

        public bool TryGet(TKey key, TOwner owner, out TValue value)
        {
            value = default(TValue);
            if (key == null || owner == null || !entries.TryGetValue(key, out Entry entry))
                return false;
            if (entry.Owner.TryGetTarget(out TOwner storedOwner)
                && ReferenceEquals(storedOwner, owner))
            {
                value = entry.Value;
                return true;
            }

            entries.Remove(key);
            return false;
        }

        public bool TryConsume(TKey key, TOwner owner, out TValue value)
        {
            if (!TryGet(key, owner, out value)) return false;
            entries.Remove(key);
            return true;
        }

        public bool Propagate(TKey source, TKey target, TOwner owner)
        {
            if (target == null || !TryGet(source, owner, out TValue value)) return false;
            Set(target, owner, value);
            return true;
        }
    }
}
