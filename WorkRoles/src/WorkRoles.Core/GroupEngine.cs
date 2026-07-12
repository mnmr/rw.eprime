using System;
using System.Collections.Generic;
using System.Linq;

namespace WorkRoles.Core
{
    /// One rendered section of a grouped list.
    public sealed class GroupSection<T>
    {
        public string Key;    // stable id ("faction:Zorble", "slave:1") for collapse state
        public string Title;  // display title, without member count
        public List<T> Members = new List<T>();
    }

    /// One named member set for membership-based partitioning (groups whose
    /// membership is looked up, not derived — e.g. player-made groups).
    public sealed class MembershipGroup<T>
    {
        public string Key;
        public string Title;
        public HashSet<T> Members = new HashSet<T>();
    }

    public static class GroupEngine
    {
        /// Partitions items into titled sections: classify gives each item its
        /// section key and title. Sections are ordered by title (A-Z, ordinal,
        /// case-insensitive); members keep their input order.
        public static List<GroupSection<T>> Partition<T>(
            IEnumerable<T> items, Func<T, (string key, string title)> classify)
        {
            var byKey = new Dictionary<string, GroupSection<T>>();
            var sections = new List<GroupSection<T>>();
            foreach (var item in items)
            {
                var (key, title) = classify(item);
                key = key ?? "";
                if (!byKey.TryGetValue(key, out var section))
                {
                    section = new GroupSection<T> { Key = key, Title = title ?? "" };
                    byKey.Add(key, section);
                    sections.Add(section);
                }
                section.Members.Add(item);
            }
            return sections
                .OrderBy(s => s.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// Partitions by membership lookup: sections follow the GIVEN group
        /// order (it is meaningful, unlike classify's A-Z), each holds the
        /// items in that group in input order. An item in several groups
        /// appears in each; empty groups are skipped; items in no group form
        /// a trailing section keyed "ungrouped".
        public static List<GroupSection<T>> PartitionByMembership<T>(
            IEnumerable<T> items, IReadOnlyList<MembershipGroup<T>> groups, string ungroupedTitle)
        {
            var itemList = items as IList<T> ?? items.ToList();
            var result = new List<GroupSection<T>>();
            var grouped = new HashSet<T>();
            foreach (var group in groups)
            {
                var section = new GroupSection<T> { Key = group.Key ?? "", Title = group.Title ?? "" };
                foreach (var item in itemList)
                    if (group.Members.Contains(item))
                    {
                        section.Members.Add(item);
                        grouped.Add(item);
                    }
                if (section.Members.Count > 0) result.Add(section);
            }
            var ungrouped = new GroupSection<T> { Key = "ungrouped", Title = ungroupedTitle ?? "" };
            foreach (var item in itemList)
                if (!grouped.Contains(item))
                    ungrouped.Members.Add(item);
            if (ungrouped.Members.Count > 0) result.Add(ungrouped);
            return result;
        }
    }
}
