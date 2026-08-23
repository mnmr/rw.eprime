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
            store.Model.CreateGroup(store.TakeGroupId(), name);
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

        [SyncMethod]
        public static void SetGroupLayout(int id, string tierBlob)
        {
            var store = ReadoutStore.Current;
            if (store == null) return;
            if (store.Model.SetTiers(id, TierBlobCodec.Decode(tierBlob)))
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

        /// Per-token count-basis override. The states travel as BasisOverride
        /// ordinals (0 inherit, 1 force on, 2 force off) so the command needs
        /// only primitives; out-of-range values are rejected on every client.
        /// A fully-inherit rule clears the entry.
        [SyncMethod]
        public static void SetCountRule(string token, int storageOnly, int hideForbidden)
        {
            var store = ReadoutStore.Current;
            if (store == null) return;
            if (storageOnly < 0 || storageOnly > 2) return;
            if (hideForbidden < 0 || hideForbidden > 2) return;
            if (store.Model.SetCountRule(token, new CountRule(
                    (BasisOverride)storageOnly, (BasisOverride)hideForbidden)))
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
        public static void SetPoolMembers(int id, string membersBlob)
        {
            var store = ReadoutStore.Current;
            if (store == null) return;
            if (store.Model.SetPoolMembers(id, PoolMembersCodec.Decode(membersBlob)))
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
