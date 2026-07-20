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
        {
            if (store == null)
            {
                Clear();
                return;
            }

            owner = store;
            copied = set == null
                ? new List<RoleAssignment>()
                : set.assignments
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
                enabled = assignment.enabled,
                pinned = assignment.pinned
            };
    }
}
