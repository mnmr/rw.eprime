using System.Collections.Generic;

namespace EPrimeReadouts.Core
{
    /// Immutable per-rebuild resolution of every pool: expanded member defNames
    /// (deduped, member order, category refs expanded via the catalog) and the
    /// effective icon defName. Built once per rebuild; rendering only reads it.
    public sealed class PoolSnapshot
    {
        private readonly Dictionary<int, Entry> entries;

        private sealed class Entry
        {
            public IReadOnlyList<string> Members;
            public string IconDefName;
            public string Name;
        }

        private PoolSnapshot(Dictionary<int, Entry> entries)
        {
            this.entries = entries;
        }

        public static PoolSnapshot Build(IReadOnlyList<ResourcePool> pools, IResourceCatalog catalog)
        {
            var entries = new Dictionary<int, Entry>(pools.Count);
            foreach (var pool in pools)
            {
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
                string icon = null;
                if (!string.IsNullOrEmpty(pool.IconDefName) && catalog.Exists(pool.IconDefName))
                    icon = pool.IconDefName;
                else if (expanded.Count > 0)
                    icon = expanded[0];

                entries[pool.Id] = new Entry
                {
                    Members = expanded,
                    IconDefName = icon,
                    Name = pool.Name ?? "",
                };
            }
            return new PoolSnapshot(entries);
        }

        /// Returns true and populates out-params when the pool id is found.
        public bool TryGet(int poolId, out IReadOnlyList<string> members,
            out string iconDefName, out string name)
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
