using System;
using RimShared.Common;

namespace EPrimeReadouts.Core
{
    public readonly struct PoolAssignmentTreeRow
    {
        public bool IsRoot { get; }
        public int PoolId { get; }
        public string Label { get; }
        public string Token { get; }
        public string? IconDefName { get; }
        public bool Expanded { get; }

        internal PoolAssignmentTreeRow(bool isRoot, int poolId,
            string label, string token, string? iconDefName, bool expanded)
        {
            IsRoot = isRoot;
            PoolId = poolId;
            Label = label;
            Token = token;
            IconDefName = iconDefName;
            Expanded = expanded;
        }
    }

    public static class PoolAssignmentTree
    {
        public static PoolAssignmentTreeRow[] Build(PoolSnapshot? pools,
            bool expanded, ItemTreeFilter filter, string rootLabel)
        {
            if (pools == null || pools.Count == 0)
                return Array.Empty<PoolAssignmentTreeRow>();

            bool filtering = SearchMatcher.IsActive(filter.Query);
            int matchCount = 0;
            for (int i = 0; i < pools.Count; i++)
            {
                PoolSnapshotEntry entry = pools.EntryAt(i);
                if (!filtering || SearchMatcher.Matches(entry.Name, filter.Query))
                    matchCount++;
            }
            if (matchCount == 0)
                return Array.Empty<PoolAssignmentTreeRow>();

            bool open = filtering || expanded;
            var rows = new PoolAssignmentTreeRow[open ? matchCount + 1 : 1];
            rows[0] = new PoolAssignmentTreeRow(
                true, -1, rootLabel ?? "", "", null, open);
            if (!open) return rows;

            int rowIndex = 1;
            for (int i = 0; i < pools.Count; i++)
            {
                PoolSnapshotEntry entry = pools.EntryAt(i);
                if (filtering && !SearchMatcher.Matches(entry.Name, filter.Query))
                    continue;
                rows[rowIndex++] = new PoolAssignmentTreeRow(
                    false,
                    entry.Id,
                    entry.Name,
                    SlotToken.PoolToken(entry.Id),
                    entry.IconDefName,
                    false);
            }
            return rows;
        }
    }
}
