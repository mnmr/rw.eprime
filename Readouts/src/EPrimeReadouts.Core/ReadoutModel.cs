using System;
using System.Collections.Generic;

namespace EPrimeReadouts.Core
{
    public sealed class ReadoutGroup
    {
        public int Id;
        public string Name = "";
        public int OrderIndex;
        public List<List<string>> Tiers = new List<List<string>>();
        public int TierCount => Tiers.Count;
        public bool DefaultEnabled = true;
    }

    public readonly struct ThresholdSpec
    {
        public readonly int Low;
        public readonly int Critical;
        public ThresholdSpec(int low, int critical) { Low = low; Critical = critical; }
    }

    /// <summary>
    /// Shared readout definition: groups plus global thresholds. Pure state
    /// and operations; persistence and MP sync live in the game assembly,
    /// which routes every mutation through these methods.
    /// </summary>
    public sealed class ReadoutModel
    {
        public List<ReadoutGroup> Groups = new List<ReadoutGroup>();
        public Dictionary<string, ThresholdSpec> Thresholds = new Dictionary<string, ThresholdSpec>();

        public ReadoutGroup GroupById(int id)
        {
            foreach (var group in Groups)
                if (group.Id == id) return group;
            return null;
        }

        public List<ReadoutGroup> InDisplayOrder()
        {
            var sorted = new List<ReadoutGroup>(Groups);
            sorted.Sort((a, b) => a.OrderIndex.CompareTo(b.OrderIndex));
            return sorted;
        }

        public ReadoutGroup CreateGroup(int id, string name)
        {
            int maxOrder = -1;
            foreach (var group in Groups)
                if (group.OrderIndex > maxOrder) maxOrder = group.OrderIndex;
            var created = new ReadoutGroup { Id = id, Name = name ?? "", OrderIndex = maxOrder + 1 };
            Groups.Add(created);
            return created;
        }

        public bool RenameGroup(int id, string name)
        {
            var group = GroupById(id);
            if (group == null) return false;
            group.Name = name ?? "";
            return true;
        }

        public bool DeleteGroup(int id)
        {
            var group = GroupById(id);
            if (group == null) return false;
            Groups.Remove(group);
            return true;
        }

        /// <summary>
        /// Swaps display position with the neighbor delta steps away
        /// (-1 = toward the top of the readout, +1 = toward the bottom).
        /// </summary>
        public bool ReorderGroup(int id, int delta)
        {
            var order = InDisplayOrder();
            int index = order.FindIndex(g => g.Id == id);
            int target = index + delta;
            if (index < 0 || target < 0 || target >= order.Count) return false;
            (order[index].OrderIndex, order[target].OrderIndex) =
                (order[target].OrderIndex, order[index].OrderIndex);
            return true;
        }

        /// <summary>
        /// Repositions the group identified by <paramref name="id"/> so that
        /// it appears at <paramref name="targetDisplayIndex"/> in display order
        /// (clamped to 0..count-1). Afterwards all groups' OrderIndex values
        /// are normalized to 0..n-1 matching the new display order.
        /// Returns false when the id is unknown.
        /// </summary>
        public bool MoveGroupTo(int id, int targetDisplayIndex)
        {
            var group = GroupById(id);
            if (group == null) return false;
            var order = InDisplayOrder();
            int count = order.Count;
            if (count <= 1) return true;
            int clampedTarget = Math.Max(0, Math.Min(count - 1, targetDisplayIndex));
            // Remove from current position, insert at target
            order.Remove(group);
            order.Insert(clampedTarget, group);
            // Normalize all OrderIndexes to 0..n-1
            for (int i = 0; i < order.Count; i++)
                order[i].OrderIndex = i;
            return true;
        }

        public bool SetTiers(int id, List<List<string>> tiers)
        {
            var group = GroupById(id);
            if (group == null || tiers == null || tiers.Count > TierOps.MaxTiers) return false;
            group.Tiers = TierOps.Clone(tiers);
            TierOps.Compact(group.Tiers);
            return true;
        }

        public void SetThreshold(string defName, int low, int critical) =>
            Thresholds[defName] = new ThresholdSpec(low, critical);

        public bool ClearThreshold(string defName) => Thresholds.Remove(defName);

        /// <summary>
        /// Load-time cleanup: purge defNames that no longer resolve, compact
        /// tiers, drop stale thresholds. Deterministic for a given save plus
        /// def set, so MP clients converge without any syncing.
        /// </summary>
        public void CleanupMissing(Func<string, bool> exists)
        {
            foreach (var group in Groups) TierOps.Cleanup(group.Tiers, exists);
            var stale = new List<string>();
            foreach (var defName in Thresholds.Keys)
                if (!exists(defName)) stale.Add(defName);
            foreach (var defName in stale) Thresholds.Remove(defName);
        }
    }
}
