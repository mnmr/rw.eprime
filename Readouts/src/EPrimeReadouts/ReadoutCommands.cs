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
            if (!ReadoutsXml.TryImport(xml, out var pools, out var groups, out _)) return;
            store.Model.ApplyImport(pools, groups, store.TakePoolId, store.TakeGroupId);
            store.Bump();
        }

        [SyncMethod]
        public static void CreateGroup(string name)
        {
            var store = ReadoutStore.Current;
            if (store == null) return;
            store.Model.CreateGroup(store.TakeGroupId(), name);
            store.Bump();
        }

        [SyncMethod]
        public static void RenameGroup(int id, string name)
        {
            var store = ReadoutStore.Current;
            if (store == null) return;
            store.Model.RenameGroup(id, name);
            store.Bump();
        }

        [SyncMethod]
        public static void DeleteGroup(int id)
        {
            var store = ReadoutStore.Current;
            if (store == null) return;
            store.Model.DeleteGroup(id);
            store.Bump();
        }

        [SyncMethod]
        public static void ReorderGroup(int id, int delta)
        {
            var store = ReadoutStore.Current;
            if (store == null) return;
            store.Model.ReorderGroup(id, delta);
            store.Bump();
        }

        [SyncMethod]
        public static void SetGroupLayout(int id, string tierBlob)
        {
            var store = ReadoutStore.Current;
            if (store == null) return;
            store.Model.SetTiers(id, TierBlobCodec.Decode(tierBlob));
            store.Bump();
        }

        [SyncMethod]
        public static void SetThreshold(string defName, int low, int critical)
        {
            var store = ReadoutStore.Current;
            if (store == null) return;
            store.Model.SetThreshold(defName, low, critical);
            store.Bump();
        }

        [SyncMethod]
        public static void ClearThreshold(string defName)
        {
            var store = ReadoutStore.Current;
            if (store == null) return;
            store.Model.ClearThreshold(defName);
            store.Bump();
        }

        [SyncMethod]
        public static void MoveGroupTo(int id, int displayIndex)
        {
            var store = ReadoutStore.Current;
            if (store == null) return;
            store.Model.MoveGroupTo(id, displayIndex);
            store.Bump();
        }

        [SyncMethod]
        public static void RestoreDefaults()
        {
            var store = ReadoutStore.Current;
            if (store == null) return;
            store.Model.Groups.Clear();
            store.Model.Pools.Clear();
            store.Model.Thresholds.Clear();
            DefaultGroups.Seed(store);
            store.Bump();
        }

        [SyncMethod]
        public static void CreatePool(string name)
        {
            var store = ReadoutStore.Current;
            if (store == null) return;
            store.Model.CreatePool(store.TakePoolId(), name);
            store.Bump();
        }

        [SyncMethod]
        public static void RenamePool(int id, string name)
        {
            var store = ReadoutStore.Current;
            if (store == null) return;
            store.Model.RenamePool(id, name);
            store.Bump();
        }

        [SyncMethod]
        public static void DeletePool(int id)
        {
            var store = ReadoutStore.Current;
            if (store == null) return;
            store.Model.DeletePool(id);
            store.Bump();
        }

        [SyncMethod]
        public static void SetPoolMembers(int id, string membersBlob)
        {
            var store = ReadoutStore.Current;
            if (store == null) return;
            store.Model.SetPoolMembers(id, PoolMembersCodec.Decode(membersBlob));
            store.Bump();
        }

        [SyncMethod]
        public static void SetPoolIcon(int id, string defName)
        {
            var store = ReadoutStore.Current;
            if (store == null) return;
            store.Model.SetPoolIcon(id, defName);
            store.Bump();
        }
    }
}
