using System.Collections.Generic;

namespace EPrimeReadouts.Core
{
    public readonly struct PoolSnapshotEntry
    {
        public int Id { get; }
        public string Name { get; }
        public IReadOnlyList<string> Members { get; }
        public string? IconDefName { get; }

        internal PoolSnapshotEntry(int id, string name,
            IReadOnlyList<string> members, string? iconDefName)
        {
            Id = id;
            Name = name;
            Members = members;
            IconDefName = iconDefName;
        }
    }

    /// Immutable per-rebuild resolution of every pool: expanded canonical
    /// member defNames (deduped, member order, category refs expanded via the
    /// catalog), the canonical effective icon defName, and deterministic input
    /// order. Built once per rebuild; rendering only reads it.
    public sealed class PoolSnapshot
    {
        private readonly Dictionary<int, PoolSnapshotEntry> entries;
        private readonly PoolSnapshotEntry[] orderedEntries;

        private PoolSnapshot(Dictionary<int, PoolSnapshotEntry> entries,
            PoolSnapshotEntry[] orderedEntries)
        {
            this.entries = entries;
            this.orderedEntries = orderedEntries;
        }

        public int Count => orderedEntries.Length;

        public PoolSnapshotEntry EntryAt(int index) => orderedEntries[index];

        public static PoolSnapshot Build(IReadOnlyList<ResourcePool> pools, IResourceCatalog catalog)
        {
            var entries = new Dictionary<int, PoolSnapshotEntry>(pools.Count);
            var orderedEntries = new PoolSnapshotEntry[pools.Count];
            for (int poolIndex = 0; poolIndex < pools.Count; poolIndex++)
            {
                ResourcePool pool = pools[poolIndex];
                var expanded = new List<string>();
                var seen = new HashSet<string>();

                foreach (var member in pool.Members)
                {
                    if (string.IsNullOrEmpty(member)) continue;
                    if (member.StartsWith("@"))
                    {
                        // Category ref: expand via catalog
                        string catName = member.Substring(1);
                        var defs = catalog.CountedDefsIn(catName);
                        foreach (var def in defs)
                        {
                            string canonical = catalog.CanonicalDefNameOf(def);
                            if (!string.IsNullOrEmpty(canonical) && seen.Add(canonical))
                                expanded.Add(canonical);
                        }
                    }
                    else
                    {
                        // Plain defName
                        string canonical = catalog.CanonicalDefNameOf(member);
                        if (!string.IsNullOrEmpty(canonical) && seen.Add(canonical))
                            expanded.Add(canonical);
                    }
                }

                // Resolve icon: explicit first, else first expanded member, else null
                string? icon = null;
                if (!string.IsNullOrEmpty(pool.IconDefName))
                {
                    string canonical = catalog.CanonicalDefNameOf(pool.IconDefName!);
                    if (!string.IsNullOrEmpty(canonical)) icon = canonical;
                }
                if (icon == null && expanded.Count > 0)
                    icon = expanded[0];

                var entry = new PoolSnapshotEntry(
                    pool.Id,
                    pool.Name ?? "",
                    expanded.AsReadOnly(),
                    icon);
                entries[pool.Id] = entry;
                orderedEntries[poolIndex] = entry;
            }
            return new PoolSnapshot(entries, orderedEntries);
        }

        /// Returns true and populates out-params when the pool id is found.
        public bool TryGet(int poolId, out IReadOnlyList<string>? members,
            out string? iconDefName, out string? name)
        {
            if (entries.TryGetValue(poolId, out var entry))
            {
                members = entry.Members;
                iconDefName = entry.IconDefName;
                name = entry.Name;
                return true;
            }
            members = null;
            iconDefName = null;
            name = null;
            return false;
        }
    }
}
