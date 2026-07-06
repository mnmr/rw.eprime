using System.Collections.Generic;
using System.Linq;

namespace WorkRoles.UI
{
    public static class RoleClipboard
    {
        private static List<RoleAssignment> copied;

        public static bool HasContent => copied != null && copied.Count > 0;
        public static List<RoleAssignment> Content => copied;

        public static void CopyFrom(PawnRoleSet set) =>
            copied = set == null
                ? new List<RoleAssignment>()
                : set.assignments
                    .Select(a => new RoleAssignment { roleId = a.roleId, enabled = a.enabled })
                    .ToList();
    }
}
