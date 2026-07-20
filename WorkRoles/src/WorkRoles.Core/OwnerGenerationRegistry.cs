using System;
using System.Collections.Generic;

namespace WorkRoles.Core
{
    /// <summary>
    /// Tracks values by an owner-local stable key while exposing a separate
    /// lookup key. Values not touched in an owner's latest generation are
    /// retired, but remain resolvable until <see cref="FlushRetired"/> so a
    /// consumer later in the same frame can finish using them.
    /// </summary>
    public sealed class OwnerGenerationRegistry<TOwner, TLocalKey, TLookupKey, TValue>
    {
        private readonly struct OwnedKey : IEquatable<OwnedKey>
        {
            internal readonly TOwner Owner;
            internal readonly TLocalKey LocalKey;

            internal OwnedKey(TOwner owner, TLocalKey localKey)
            {
                Owner = owner;
                LocalKey = localKey;
            }

            public bool Equals(OwnedKey other) =>
                EqualityComparer<TOwner>.Default.Equals(Owner, other.Owner)
                && EqualityComparer<TLocalKey>.Default.Equals(LocalKey, other.LocalKey);

            public override bool Equals(object obj) =>
                obj is OwnedKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    return (EqualityComparer<TOwner>.Default.GetHashCode(Owner) * 397)
                           ^ EqualityComparer<TLocalKey>.Default.GetHashCode(LocalKey);
                }
            }
        }

        private sealed class Entry
        {
            internal readonly TOwner Owner;
            internal readonly TLocalKey LocalKey;
            internal TLookupKey LookupKey;
            internal TValue Value;
            internal long Generation;
            internal bool Retired;
            internal Entry CollisionNewer;
            internal Entry CollisionOlder;

            internal Entry(TOwner owner, TLocalKey localKey)
            {
                Owner = owner;
                LocalKey = localKey;
            }
        }

        private readonly Dictionary<OwnedKey, Entry> activeEntries =
            new Dictionary<OwnedKey, Entry>();
        private readonly Dictionary<TOwner, HashSet<Entry>> entriesByOwner =
            new Dictionary<TOwner, HashSet<Entry>>();
        private readonly HashSet<Entry> retiredEntries = new HashSet<Entry>();
        private readonly List<Entry> removalBuffer = new List<Entry>();
        private readonly Dictionary<TLookupKey, Entry> lookup =
            new Dictionary<TLookupKey, Entry>();
        private bool generationActive;
        private TOwner activeOwner;
        private long generation;
        private int entryCount;

        public int Count => entryCount;
        public int RetiredCount => retiredEntries.Count;

        public void Begin(TOwner owner)
        {
            RequireNotNull(owner, nameof(owner));
            if (generationActive)
                throw new InvalidOperationException("A generation is already active.");
            generationActive = true;
            activeOwner = owner;
            generation++;
        }

        public void Touch(TLocalKey localKey, TLookupKey lookupKey, TValue value)
        {
            if (!generationActive)
                throw new InvalidOperationException("Touch requires an active generation.");
            RequireNotNull(localKey, nameof(localKey));
            RequireNotNull(lookupKey, nameof(lookupKey));

            var ownedKey = new OwnedKey(activeOwner, localKey);
            if (!activeEntries.TryGetValue(ownedKey, out Entry entry))
            {
                entry = AddEntry(activeOwner, localKey, lookupKey);
                activeEntries.Add(ownedKey, entry);
            }
            else if (!EqualityComparer<TLookupKey>.Default.Equals(
                         entry.LookupKey, lookupKey))
            {
                Retire(entry);
                entry = AddEntry(activeOwner, localKey, lookupKey);
                activeEntries[ownedKey] = entry;
            }
            else
                retiredEntries.Remove(entry);

            entry.LookupKey = lookupKey;
            entry.Value = value;
            entry.Generation = generation;
            entry.Retired = false;
            MoveToLookupHead(entry);
        }

        public void End(TOwner owner)
        {
            RequireNotNull(owner, nameof(owner));
            if (!generationActive)
                throw new InvalidOperationException("No generation is active.");
            if (!EqualityComparer<TOwner>.Default.Equals(activeOwner, owner))
                throw new InvalidOperationException("The active generation belongs to another owner.");

            if (entriesByOwner.TryGetValue(owner, out HashSet<Entry> owned))
            {
                foreach (Entry entry in owned)
                    if (entry.Generation != generation)
                        Retire(entry);
            }

            generationActive = false;
            activeOwner = default(TOwner);
        }

        public bool TryGet(TLookupKey lookupKey, out TValue value)
        {
            value = default(TValue);
            if (ReferenceEquals(lookupKey, null)
                || !lookup.TryGetValue(lookupKey, out Entry entry))
                return false;
            value = entry.Value;
            return true;
        }

        public int FlushRetired()
        {
            if (retiredEntries.Count == 0) return 0;
            removalBuffer.Clear();
            try
            {
                foreach (Entry entry in retiredEntries)
                    removalBuffer.Add(entry);
                int removed = removalBuffer.Count;
                Remove(removalBuffer);
                return removed;
            }
            finally
            {
                removalBuffer.Clear();
            }
        }

        public int Release(TOwner owner)
        {
            RequireNotNull(owner, nameof(owner));
            if (!entriesByOwner.TryGetValue(owner, out HashSet<Entry> owned))
                return 0;
            var doomed = new List<Entry>(owned);
            Remove(doomed);
            return doomed.Count;
        }

        public void Clear()
        {
            activeEntries.Clear();
            entriesByOwner.Clear();
            retiredEntries.Clear();
            removalBuffer.Clear();
            lookup.Clear();
            generationActive = false;
            activeOwner = default(TOwner);
            generation = 0;
            entryCount = 0;
        }

        private Entry AddEntry(TOwner owner, TLocalKey localKey, TLookupKey lookupKey)
        {
            var entry = new Entry(owner, localKey) { LookupKey = lookupKey };
            entryCount++;
            if (!entriesByOwner.TryGetValue(owner, out HashSet<Entry> owned))
            {
                owned = new HashSet<Entry>();
                entriesByOwner.Add(owner, owned);
            }
            owned.Add(entry);
            AttachToLookupHead(entry);
            return entry;
        }

        private void Retire(Entry entry)
        {
            if (entry.Retired) return;
            entry.Retired = true;
            retiredEntries.Add(entry);
        }

        private void Remove(List<Entry> doomed)
        {
            if (doomed.Count == 0) return;
            foreach (Entry entry in doomed)
            {
                var ownedKey = new OwnedKey(entry.Owner, entry.LocalKey);
                if (activeEntries.TryGetValue(ownedKey, out Entry active)
                    && ReferenceEquals(active, entry))
                    activeEntries.Remove(ownedKey);
                entryCount--;
                retiredEntries.Remove(entry);
                UnlinkLookup(entry);
                if (entriesByOwner.TryGetValue(entry.Owner, out HashSet<Entry> owned))
                {
                    owned.Remove(entry);
                    if (owned.Count == 0)
                        entriesByOwner.Remove(entry.Owner);
                }
            }
        }

        private void MoveToLookupHead(Entry entry)
        {
            if (lookup.TryGetValue(entry.LookupKey, out Entry head)
                && ReferenceEquals(head, entry)) return;
            UnlinkLookup(entry);
            AttachToLookupHead(entry);
        }

        private void AttachToLookupHead(Entry entry)
        {
            entry.CollisionNewer = null;
            if (lookup.TryGetValue(entry.LookupKey, out Entry head))
            {
                entry.CollisionOlder = head;
                head.CollisionNewer = entry;
            }
            else
                entry.CollisionOlder = null;
            lookup[entry.LookupKey] = entry;
        }

        private void UnlinkLookup(Entry entry)
        {
            Entry newer = entry.CollisionNewer;
            Entry older = entry.CollisionOlder;
            if (newer != null)
                newer.CollisionOlder = older;
            else if (older != null)
                lookup[entry.LookupKey] = older;
            else
                lookup.Remove(entry.LookupKey);
            if (older != null)
                older.CollisionNewer = newer;
            entry.CollisionNewer = null;
            entry.CollisionOlder = null;
        }

        private static void RequireNotNull<T>(T value, string name)
        {
            if (ReferenceEquals(value, null))
                throw new ArgumentNullException(name);
        }
    }
}
