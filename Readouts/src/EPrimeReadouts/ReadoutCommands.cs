using EPrimeReadouts.Core;
using Multiplayer.API;

namespace EPrimeReadouts
{
    /// The only writers to ReadoutStore. Every method is a synced command:
    /// in MP it executes on all clients; without MP it runs directly. All
    /// parameters are primitives so no SyncWorkers are needed.
    public static class ReadoutCommands
    {
        /// Overwrite-import of the full configuration. The XML travels through
        /// the sync layer so every MP client parses and applies it
        /// deterministically.
        [SyncMethod]
        public static void ImportAll(string xml)
        {
            var store = ReadoutStore.Current;
            if (store == null) return;
            if (!ReadoutsXml.TryImport(xml, out var pools, out var groups, out _,
                ModRequirements.IsModActive)) return;
            store.Bump(store.Model.ApplyImport(
                pools, groups, store.TakePoolId, store.TakeGroupId));
        }

        [SyncMethod]
        public static void CreateGroup(string name)
        {
            var store = ReadoutStore.Current;
            if (store == null) return;
            // A concurrent create in MP may have taken the name first; the
            // second one is dropped identically on every client.
            string normalized = ReadoutGroupNames.Normalize(name);
            if (!store.Model.CanUseGroupName(normalized)) return;
            store.Model.CreateGroup(store.TakeGroupId(), normalized);
            store.Bump(ReadoutChange.Groups);
        }

        [SyncMethod]
        public static void RenameGroup(int id, string name)
        {
            var store = ReadoutStore.Current;
            if (store == null) return;
            if (store.Model.RenameGroup(id, name))
                store.Bump(ReadoutChange.Groups);
        }

        [SyncMethod]
        public static void DeleteGroup(int id)
        {
            var store = ReadoutStore.Current;
            if (store == null) return;
            if (store.Model.DeleteGroup(id))
                store.Bump(ReadoutChange.Groups);
        }

        [SyncMethod]
        public static void ReorderGroup(int id, int delta)
        {
            var store = ReadoutStore.Current;
            if (store == null) return;
            if (store.Model.ReorderGroup(id, delta))
                store.Bump(ReadoutChange.Groups);
        }

        // Slot and pool-member edits carry their intent, never the resulting
        // layout: in MP a second click is built before the first lands, and a
        // full-state command would overwrite it (see ReadoutModel intent edits).

        /// tier -1 appends to the last tier (the next one when full).
        [SyncMethod]
        public static void AddGroupSlot(int groupId, string token, int tier, int slot)
        {
            var store = ReadoutStore.Current;
            if (store == null) return;
            if (store.Model.AddGroupSlot(groupId, token, tier, slot))
                store.Bump(ReadoutChange.Groups);
        }

        [SyncMethod]
        public static void RemoveGroupSlot(int groupId, string token)
        {
            var store = ReadoutStore.Current;
            if (store == null) return;
            if (store.Model.RemoveGroupSlot(groupId, token))
                store.Bump(ReadoutChange.Groups);
        }

        [SyncMethod]
        public static void MoveGroupSlot(int groupId, string token, int toTier, int toSlot)
        {
            var store = ReadoutStore.Current;
            if (store == null) return;
            if (store.Model.MoveGroupSlot(groupId, token, toTier, toSlot))
                store.Bump(ReadoutChange.Groups);
        }

        [SyncMethod]
        public static void SetGroupSlotShowWhenZero(int groupId, string token, bool show)
        {
            var store = ReadoutStore.Current;
            if (store == null) return;
            if (store.Model.SetGroupSlotShowWhenZero(groupId, token, show))
                store.Bump(ReadoutChange.Groups);
        }

        [SyncMethod]
        public static void SetThreshold(string defName, int low, int critical)
        {
            var store = ReadoutStore.Current;
            if (store == null) return;
            if (store.Model.SetThreshold(defName, low, critical))
                store.Bump(ReadoutChange.Thresholds);
        }

        /// Per-token count-basis override, one option per command so a quick
        /// edit of the other option is never overwritten. The state travels
        /// as a BasisOverride ordinal (0 inherit, 1 force on, 2 force off);
        /// out-of-range values are rejected on every client. A fully-inherit
        /// rule clears the entry.
        [SyncMethod]
        public static void SetCountRuleStorageOnly(string token, int state)
        {
            var store = ReadoutStore.Current;
            if (store == null || state < 0 || state > 2) return;
            if (store.Model.SetCountRuleStorageOnly(token, (BasisOverride)state))
                store.Bump(ReadoutChange.CountRules);
        }

        [SyncMethod]
        public static void SetCountRuleHideForbidden(string token, int state)
        {
            var store = ReadoutStore.Current;
            if (store == null || state < 0 || state > 2) return;
            if (store.Model.SetCountRuleHideForbidden(token, (BasisOverride)state))
                store.Bump(ReadoutChange.CountRules);
        }

        [SyncMethod]
        public static void ClearThreshold(string defName)
        {
            var store = ReadoutStore.Current;
            if (store == null) return;
            if (store.Model.ClearThreshold(defName))
                store.Bump(ReadoutChange.Thresholds);
        }

        [SyncMethod]
        public static void MoveGroupTo(int id, int displayIndex)
        {
            var store = ReadoutStore.Current;
            if (store == null) return;
            if (store.Model.MoveGroupTo(id, displayIndex))
                store.Bump(ReadoutChange.Groups);
        }

        [SyncMethod]
        public static void RestoreDefaults(string xml)
        {
            var store = ReadoutStore.Current;
            if (store == null) return;
            if (!ReadoutsXml.TryImport(xml, out var pools, out var groups, out _,
                ModRequirements.IsModActive)) return;
            store.Bump(store.Model.ApplyImport(
                pools, groups, store.TakePoolId, store.TakeGroupId));
        }

        [SyncMethod]
        public static void CreatePool(string name)
        {
            var store = ReadoutStore.Current;
            if (store == null) return;
            string normalized = PoolNameRules.Normalize(name);
            if (!store.Model.CanUsePoolName(normalized)) return;
            store.Model.CreatePool(store.TakePoolId(), normalized);
            store.Bump(ReadoutChange.Pools);
        }

        [SyncMethod]
        public static void RenamePool(int id, string name)
        {
            var store = ReadoutStore.Current;
            if (store == null) return;
            if (store.Model.RenamePool(id, name))
                store.Bump(ReadoutChange.Pools);
        }

        [SyncMethod]
        public static void DeletePool(int id)
        {
            var store = ReadoutStore.Current;
            if (store == null) return;
            if (store.Model.DeletePool(id, out var change))
                store.Bump(change);
        }

        [SyncMethod]
        public static void SetPoolMemberSelected(int poolId, string defName, bool selected)
        {
            var store = ReadoutStore.Current;
            if (store == null) return;
            if (store.Model.SetPoolMemberSelected(poolId, defName, selected,
                    GameResourceCatalog.Instance))
                store.Bump(ReadoutChange.Pools);
        }

        /// The scoped defs are the picker's filtered rows at click time,
        /// passed explicitly so every client changes the same defs.
        [SyncMethod]
        public static void SetPoolCategoryScopeSelected(int poolId,
            string categoryDefName, string scopedDefsBlob, bool selected)
        {
            var store = ReadoutStore.Current;
            if (store == null) return;
            if (store.Model.SetPoolCategoryScopeSelected(poolId, categoryDefName,
                    PoolMembersCodec.Decode(scopedDefsBlob), selected,
                    GameResourceCatalog.Instance))
                store.Bump(ReadoutChange.Pools);
        }

        [SyncMethod]
        public static void SetPoolIcon(int id, string defName)
        {
            var store = ReadoutStore.Current;
            if (store == null) return;
            if (store.Model.SetPoolIcon(id, defName))
                store.Bump(ReadoutChange.Pools);
        }
    }
}
