using System.Collections.Generic;
using System.Linq;
using WorkRoles.Core;

namespace WorkRoles.UI
{
    public static class RoleClipboard
    {
        private static RoleStore owner;
        private static List<RoleAssignment> copied;

        public static bool HasContent =>
            owner != null && ReferenceEquals(owner, RoleStore.Current)
                && copied != null && copied.Count > 0;

        public static List<RoleAssignment> Content => ClipboardRules.SnapshotForOwner(
            owner, RoleStore.Current, copied, Snapshot);

        public static void CopyFrom(RoleStore store, PawnRoleSet set)
            => CopyFromSnapshot(store, set?.assignments);

        internal static void CopyFromSnapshot(RoleStore store,
            IReadOnlyList<RoleAssignment> assignments)
        {
            if (store == null)
            {
                Clear();
                return;
            }

            owner = store;
            copied = assignments == null
                ? new List<RoleAssignment>()
                : assignments
                    .Where(assignment => assignment != null)
                    .Select(Snapshot)
                    .ToList();
        }

        public static void Clear()
        {
            owner = null;
            copied = null;
        }

        private static RoleAssignment Snapshot(RoleAssignment assignment) =>
            new RoleAssignment
            {
                roleId = assignment.roleId,
                state = assignment.state,
                pinned = assignment.pinned
            };
    }
}
