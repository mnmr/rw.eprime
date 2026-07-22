using RimWorld;
using WorkRoles.Core;

namespace WorkRoles
{
    /// Detached bill copy/MP-serialization state. The bill key and originating
    /// store are both weak so a clipboard cannot retain an old world graph.
    internal static class BillRoleTransfer
    {
        private static readonly OwnerScopedTransferTable<Bill, RoleStore, int> staged =
            new OwnerScopedTransferTable<Bill, RoleStore, int>();

        internal static bool PropagateClone(Bill source, Bill clone, RoleStore store)
        {
            if (source == null || clone == null || store == null) return false;
            if (store.billRoles != null
                && store.billRoles.TryGetValue(source, out int attachedRoleId))
            {
                staged.Set(clone, store, attachedRoleId);
                return true;
            }
            return staged.Propagate(source, clone, store);
        }

        internal static int RoleIdForScribe(Bill bill, RoleStore store) =>
            staged.TryGet(bill, store, out int roleId) ? roleId : -1;

        internal static void RestoreFromScribe(Bill bill, RoleStore store, int roleId)
        {
            if (bill != null && store != null && roleId >= 0)
                staged.Set(bill, store, roleId);
        }

        internal static bool TryConsume(Bill bill, RoleStore store, out int roleId) =>
            staged.TryConsume(bill, store, out roleId);

        internal static void ReleaseForTeardown() => staged.Clear();
    }
}
