using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace EPrimeReadouts.Core
{
    /// <summary>Detached immutable pool/group data for previews and export.</summary>
    public sealed class ReadoutSnapshot
    {
        public sealed class Pool
        {
            internal Pool(int id, string name, string? iconDefName, string[] members)
            {
                Id = id;
                Name = name;
                IconDefName = iconDefName;
                Members = Array.AsReadOnly(members);
            }

            public int Id { get; }
            public string Name { get; }
            public string? IconDefName { get; }
            public IReadOnlyList<string> Members { get; }
        }

        public sealed class Group
        {
            internal Group(int id, string name, int orderIndex, bool defaultEnabled,
                IReadOnlyList<IReadOnlyList<string>> tiers)
            {
                Id = id;
                Name = name;
                OrderIndex = orderIndex;
                DefaultEnabled = defaultEnabled;
                Tiers = new ReadOnlyCollection<IReadOnlyList<string>>(
                    new List<IReadOnlyList<string>>(tiers));
            }

            public int Id { get; }
            public string Name { get; }
            public int OrderIndex { get; }
            public bool DefaultEnabled { get; }
            public IReadOnlyList<IReadOnlyList<string>> Tiers { get; }
        }

        private ReadoutSnapshot(Pool[] pools, Group[] groups)
        {
            Pools = Array.AsReadOnly(pools);
            Groups = Array.AsReadOnly(groups);
        }

        public IReadOnlyList<Pool> Pools { get; }
        public IReadOnlyList<Group> Groups { get; }

        public static ReadoutSnapshot Capture(
            IReadOnlyList<ResourcePool>? pools,
            IReadOnlyList<ReadoutGroup>? groups)
        {
            var poolCopy = new Pool[pools?.Count ?? 0];
            for (int i = 0; i < poolCopy.Length; i++)
            {
                ResourcePool source = pools![i]; // Non-empty copy => pools exists.
                string[] members = source.Members?.ToArray() ?? Array.Empty<string>();
                poolCopy[i] = new Pool(source.Id, source.Name ?? "", source.IconDefName, members);
            }

            var groupCopy = new Group[groups?.Count ?? 0];
            for (int i = 0; i < groupCopy.Length; i++)
            {
                ReadoutGroup source = groups![i]; // Non-empty copy => groups exists.
                int tierCount = source.Tiers?.Count ?? 0;
                var tiers = new IReadOnlyList<string>[tierCount];
                for (int tier = 0; tier < tierCount; tier++)
                    tiers[tier] = Array.AsReadOnly(
                        source.Tiers![tier]?.ToArray() ?? Array.Empty<string>());
                groupCopy[i] = new Group(source.Id, source.Name ?? "", source.OrderIndex,
                    source.DefaultEnabled, tiers);
            }
            return new ReadoutSnapshot(poolCopy, groupCopy);
        }

        public string ToXml(Func<string, string?>? packageIdOf = null)
        {
            var pools = new List<ResourcePool>(Pools.Count);
            for (int i = 0; i < Pools.Count; i++)
            {
                Pool source = Pools[i];
                pools.Add(new ResourcePool
                {
                    Id = source.Id,
                    Name = source.Name,
                    IconDefName = source.IconDefName,
                    Members = new List<string>(source.Members),
                });
            }

            var groups = new List<ReadoutGroup>(Groups.Count);
            for (int i = 0; i < Groups.Count; i++)
            {
                Group source = Groups[i];
                var tiers = new List<List<string>>(source.Tiers.Count);
                for (int tier = 0; tier < source.Tiers.Count; tier++)
                    tiers.Add(new List<string>(source.Tiers[tier]));
                groups.Add(new ReadoutGroup
                {
                    Id = source.Id,
                    Name = source.Name,
                    OrderIndex = source.OrderIndex,
                    DefaultEnabled = source.DefaultEnabled,
                    Tiers = tiers,
                });
            }
            return ReadoutsXml.Export(pools, groups, packageIdOf);
        }
    }
}
