using System.Collections.Generic;
using System.Linq;
using Multiplayer.API;
using Verse;
using WorkRoles.Core;

namespace WorkRoles
{
    public static class RoleCommands
    {
        private static RoleStore Store => RoleStore.Current;

        private static Role FindRole(int roleId) => Store?.RoleById(roleId);

        // ----- Role lifecycle -----

        [SyncMethod]
        public static Role CreateRole(string label)
        {
            if (Store == null) return null;
            var role = new Role { id = Store.NextId(), label = label };
            Store.roles.Add(role);
            return role;
        }

        /// Engine-initiated (load-time seeding): runs inside the synced simulation
        /// on every client, so it must NOT be a synced command.
        internal static Role CreateRoleFromDef(RoleDef def)
        {
            if (Store == null || def == null) return null;
            var role = new Role
            {
                id = Store.NextId(),
                label = def.label,
                templateDefName = def.defName,
                autoAssign = def.autoAssign,
                hasCustomColor = def.hasCustomColor,
                color = def.color,
                iconPath = def.iconPath,
                entries = def.ParsedEntries()
            };
            Store.roles.Add(role);
            return role;
        }

        [SyncMethod]
        public static void DeleteRole(int roleId)
        {
            if (Store != null && roleId == Store.basicsRoleId) return;
            var role = FindRole(roleId);
            if (role == null) return;
            CompiledJobOrders.InvalidateRole(roleId);
            foreach (var set in Store.pawnSets.Values)
                set.assignments.RemoveAll(a => a.roleId == roleId);
            Store.roles.Remove(role);
        }

        [SyncMethod]
        public static void RenameRole(int roleId, string label)
        {
            var role = FindRole(roleId);
            if (role != null) role.label = label;
        }

        [SyncMethod]
        public static void SetRoleColor(int roleId, UnityEngine.Color color)
        {
            var role = FindRole(roleId);
            if (role == null) return;
            role.color = color;
            role.hasCustomColor = true;
        }

        [SyncMethod]
        public static void ToggleRoleGlobal(int roleId)
        {
            var role = FindRole(roleId);
            if (role == null) return;
            role.enabled = !role.enabled;
            CompiledJobOrders.InvalidateRole(roleId);
        }

        [SyncMethod]
        public static Role DuplicateRole(int roleId, string label = null)
        {
            var source = FindRole(roleId);
            if (source == null) return null;
            var copy = new Role
            {
                id = Store.NextId(),
                // templateDefName deliberately NOT copied: the copy is player-owned
                // (keeps RoleByTemplate unambiguous and autoAssign un-duplicated).
                label = label ?? source.label + " copy",
                enabled = source.enabled,
                hasCustomColor = source.hasCustomColor,
                color = source.color,
                iconPath = source.iconPath,
                entries = new List<JobEntry>(source.entries)
            };
            Store.roles.Add(copy);
            return copy;
        }

        /// Reorders the role catalog (palette / list order). UI-only ordering:
        /// no cache invalidation needed.
        [SyncMethod]
        public static void MoveRoleInCatalog(int from, int to)
        {
            var roles = Store?.roles;
            if (roles == null || from < 0 || from >= roles.Count || to < 0 || to >= roles.Count || from == to) return;
            var role = roles[from];
            roles.RemoveAt(from);
            roles.Insert(to, role);
        }

        // ----- Role content -----

        [SyncMethod]
        public static void AddEntry(int roleId, JobEntry entry, int index = -1)
        {
            var role = FindRole(roleId);
            if (role == null) return;
            if (index < 0 || index > role.entries.Count) index = role.entries.Count;
            role.entries.Insert(index, entry);
            CompiledJobOrders.InvalidateRole(roleId);
        }

        [SyncMethod]
        public static void RemoveEntry(int roleId, int index)
        {
            var role = FindRole(roleId);
            if (role == null || index < 0 || index >= role.entries.Count) return;
            role.entries.RemoveAt(index);
            CompiledJobOrders.InvalidateRole(roleId);
        }

        [SyncMethod]
        public static void MoveEntry(int roleId, int from, int to)
        {
            var role = FindRole(roleId);
            if (role == null || from < 0 || from >= role.entries.Count || to < 0 || to >= role.entries.Count) return;
            var entry = role.entries[from];
            role.entries.RemoveAt(from);
            role.entries.Insert(to, entry);
            CompiledJobOrders.InvalidateRole(roleId);
        }

        // ----- Pawn assignments -----

        [SyncMethod]
        public static void AssignRole(Pawn pawn, int roleId, int index = -1)
            => AssignRoleDirect(pawn, roleId, index);

        /// Engine-initiated path (coverage generation): creates a role without going through
        /// sync interception — runs deterministically on every client at load time.
        internal static Role CreateRoleDirect(string label, bool autoAssign = false)
        {
            if (Store == null) return null;
            var role = new Role { id = Store.NextId(), label = label, autoAssign = autoAssign };
            Store.roles.Add(role);
            return role;
        }

        internal static void AddEntryDirect(int roleId, JobEntry entry)
        {
            var role = FindRole(roleId);
            if (role == null) return;
            role.entries.Add(entry);
            CompiledJobOrders.InvalidateRole(roleId);
        }

        /// Engine-initiated path (seeding, joiner auto-assign): runs inside the synced
        /// simulation on every client, so it must NOT go through sync interception.
        internal static void AssignRoleDirect(Pawn pawn, int roleId, int index = -1)
        {
            if (Store == null || pawn == null || Store.RoleById(roleId) == null) return;
            var set = Store.SetFor(pawn);
            if (set.assignments.Any(a => a.roleId == roleId)) return;
            if (index < 0 || index > set.assignments.Count) index = set.assignments.Count;
            set.assignments.Insert(index, new RoleAssignment { roleId = roleId });
            CompiledJobOrders.Invalidate(pawn);
        }

        [SyncMethod]
        public static void RemoveRoleFromPawn(Pawn pawn, int roleId)
        {
            // TryGetValue, not SetFor: a removal against an unmanaged pawn must not
            // create (and scribe) an empty set for it.
            if (Store == null || pawn == null || !Store.pawnSets.TryGetValue(pawn, out var set)) return;
            set.assignments.RemoveAll(a => a.roleId == roleId);
            CompiledJobOrders.Invalidate(pawn);
        }

        [SyncMethod]
        public static void MoveRoleOnPawn(Pawn pawn, int from, int to)
        {
            if (Store == null || pawn == null || !Store.pawnSets.TryGetValue(pawn, out var set)) return;
            if (from < 0 || from >= set.assignments.Count || to < 0 || to >= set.assignments.Count) return;
            var assignment = set.assignments[from];
            set.assignments.RemoveAt(from);
            set.assignments.Insert(to, assignment);
            CompiledJobOrders.Invalidate(pawn);
        }

        [SyncMethod]
        public static void ToggleRoleForPawn(Pawn pawn, int roleId)
        {
            if (Store == null || pawn == null || !Store.pawnSets.TryGetValue(pawn, out var set)) return;
            var assignment = set.assignments.FirstOrDefault(a => a.roleId == roleId);
            if (assignment == null) return;
            var role = Store.RoleById(roleId);
            if (role != null && !role.enabled)
            {
                // Enabling a globally-disabled role on one pawn means "run it here only":
                // the role comes back on globally, restricted to this pawn.
                role.enabled = true;
                foreach (var otherSet in Store.pawnSets.Values)
                    foreach (var other in otherSet.assignments)
                        if (other.roleId == roleId)
                            other.enabled = false;
                assignment.enabled = true;
                CompiledJobOrders.InvalidateRole(roleId);
                return;
            }
            assignment.enabled = !assignment.enabled;
            CompiledJobOrders.Invalidate(pawn);
        }

        [SyncMethod]
        public static void PasteRoleSet(Pawn pawn, List<RoleAssignment> source)
        {
            if (Store == null || pawn == null || source == null) return;
            var set = Store.SetFor(pawn);
            var seen = new HashSet<int>();
            set.assignments = source
                .Where(a => seen.Add(a.roleId))
                .Select(a => new RoleAssignment { roleId = a.roleId, enabled = a.enabled })
                .ToList();
            CompiledJobOrders.Invalidate(pawn);
        }
    }
}
