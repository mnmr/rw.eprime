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

    /// Immutable per-rebuild resolution of every pool: expanded member defNames
    /// (deduped, member order, category refs expanded via the catalog), the
    /// effective icon defName, and deterministic input order. Built once per
    /// rebuild; rendering only reads it.
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
                            if (!string.IsNullOrEmpty(def) && seen.Add(def))
                                expanded.Add(def);
                    }
                    else
                    {
                        // Plain defName
                        if (catalog.Exists(member) && seen.Add(member))
                            expanded.Add(member);
                    }
                }

                // Resolve icon: explicit first, else first expanded member, else null
                string? icon = null;
                if (!string.IsNullOrEmpty(pool.IconDefName) && catalog.Exists(pool.IconDefName!))
                    icon = pool.IconDefName;
                else if (expanded.Count > 0)
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
