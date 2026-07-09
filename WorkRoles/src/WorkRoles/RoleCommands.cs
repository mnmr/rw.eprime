using System.Collections.Generic;
using System.Linq;
using Multiplayer.API;
using RimWorld;
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

        /// Recreates seeded roles missing from the catalog (deleted, or never seeded
        /// due to a load-time failure) and coverage roles for uncovered work types.
        [SyncMethod]
        public static void RestoreMissingRoles()
        {
            if (Store == null) return;
            var restored = Seeding.RestoreMissingRoles();
            if (restored.Count > 0)
                Messages.Message("WR_RolesRestored".Translate(restored.ToCommaList()),
                    MessageTypeDefOf.PositiveEvent, historical: false);
        }

        [SyncMethod]
        public static void DeleteRole(int roleId)
        {
            var role = FindRole(roleId);
            if (role == null) return;
            CompiledJobOrders.InvalidateRole(roleId);
            foreach (var set in Store.pawnSets.Values)
                set.assignments.RemoveAll(a => a.roleId == roleId);
            Store.billRoles.RemoveAll(kv => kv.Value == roleId);
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

        /// Auto-assign roles go to newcomers and lead every plan target.
        [SyncMethod]
        public static void SetRoleAutoAssign(int roleId, bool value)
        {
            var role = FindRole(roleId);
            if (role != null) role.autoAssign = value;
        }

        /// Defines one of the shared custom swatch slots in the role editor.
        [SyncMethod]
        public static void SetCustomSwatch(int index, UnityEngine.Color color)
        {
            if (Store == null || index < 0 || index >= 32) return;
            while (Store.customSwatches.Count <= index)
                Store.customSwatches.Add(UnityEngine.Color.clear);
            Store.customSwatches[index] = color;
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
                label = label ?? source.label,
                enabled = source.enabled,
                hasCustomColor = source.hasCustomColor,
                color = source.color,
                iconPath = source.iconPath,
                activeHours = source.activeHours,
                location = source.location,
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

        // ----- Role rules -----

        [SyncMethod]
        public static void SetRoleActiveHours(int roleId, int hoursMask)
        {
            var role = FindRole(roleId);
            if (role == null || role.activeHours == hoursMask) return;
            role.activeHours = hoursMask;
            CompiledJobOrders.InvalidateRole(roleId);
        }

        [SyncMethod]
        public static void SetRoleLocation(int roleId, RoleLocation location)
        {
            var role = FindRole(roleId);
            if (role == null || role.location == location) return;
            role.location = location;
            CompiledJobOrders.InvalidateRole(roleId);
        }

        /// Restricts a bill to workers actively holding the role (-1 clears).
        [SyncMethod]
        public static void SetBillRole(Bill bill, int roleId)
        {
            if (Store == null || bill == null) return;
            if (roleId < 0 || FindRole(roleId) == null) Store.billRoles.Remove(bill);
            else Store.billRoles[bill] = roleId;
        }

        /// Turns an auto role back into a manual one (a role is auto iff any rule is set).
        [SyncMethod]
        public static void ClearRoleRules(int roleId)
        {
            var role = FindRole(roleId);
            if (role == null || !role.HasRules) return;
            role.activeHours = Role.AllHours;
            role.location = RoleLocation.Any;
            CompiledJobOrders.InvalidateRole(roleId);
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

        /// Min index in entries of any of child's entries (int.MaxValue when none present).
        internal static int BlockStart(List<JobEntry> entries, Role child)
        {
            int min = int.MaxValue;
            foreach (var entry in child.entries)
            {
                int idx = entries.IndexOf(entry);
                if (idx >= 0 && idx < min) min = idx;
            }
            return min;
        }

        /// Adds child's entries (those parent lacks) to parent as one contiguous run in
        /// the child's own entry order, inserted before beforeChildId's entry block (-1
        /// or not found = append at end). Coverage then renders child under parent in
        /// the role tree. When parent already covers child, a given position moves the
        /// child's block instead (MoveChildBefore semantics); with no position (-1)
        /// nothing changes.
        [SyncMethod]
        public static void NestRoleInto(int parentId, int childId, int beforeChildId)
        {
            var parent = FindRole(parentId);
            var child = FindRole(childId);
            if (parent == null || child == null || parentId == childId) return;

            if (parent.Covers(child))
            {
                if (beforeChildId == -1) return;
                if (MoveChildCore(parent, child, beforeChildId))
                    CompiledJobOrders.InvalidateRole(parentId);
                return;
            }

            var missing = child.entries.Where(e => !parent.entries.Contains(e)).ToList();
            if (missing.Count == 0) return;

            int insertAt = parent.entries.Count;
            var before = FindRole(beforeChildId);
            if (before != null)
            {
                int blockStart = BlockStart(parent.entries, before);
                if (blockStart != int.MaxValue) insertAt = blockStart;
            }
            parent.entries.InsertRange(insertAt, missing);
            CompiledJobOrders.InvalidateRole(parentId);
        }

        /// Reorders parent's entries by moving childId's entry block: removes every
        /// entry of child from parent, reinserts them contiguously (child entry order)
        /// before beforeChildId's block, computed AFTER removal (-1 = append at end).
        [SyncMethod]
        public static void MoveChildBefore(int parentId, int childId, int beforeChildId)
        {
            var parent = FindRole(parentId);
            var child = FindRole(childId);
            if (parent == null || child == null || parentId == childId || !parent.Covers(child)) return;
            if (MoveChildCore(parent, child, beforeChildId))
                CompiledJobOrders.InvalidateRole(parentId);
        }

        /// Removes from parent every entry that child carries EXACTLY, pulling an
        /// exact-entry child out of its parent (it re-roots unless another role
        /// still covers it). Purely semantic children — covered only through a
        /// parent work-type entry — have nothing to remove: no-op.
        [SyncMethod]
        public static void UnnestRole(int parentId, int childId)
        {
            var parent = FindRole(parentId);
            var child = FindRole(childId);
            if (parent == null || child == null || parentId == childId || !parent.Covers(child)) return;
            var childSet = new HashSet<JobEntry>(child.entries);
            int removed = parent.entries.RemoveAll(e => childSet.Contains(e));
            if (removed > 0)
                CompiledJobOrders.InvalidateRole(parentId);
        }

        /// Shared block move; returns false when the reinsertion reproduces the
        /// current order (no-op: caller skips invalidation). A purely semantic
        /// child (no exact entries in parent) has no block to move: no-op, so a
        /// reorder never injects its entries into the parent.
        private static bool MoveChildCore(Role parent, Role child, int beforeChildId)
        {
            var before = FindRole(beforeChildId);
            if (before == child) return false;

            var childSet = new HashSet<JobEntry>(child.entries);
            var remaining = parent.entries.Where(e => !childSet.Contains(e)).ToList();
            if (remaining.Count == parent.entries.Count) return false;

            int insertAt = remaining.Count;
            if (before != null)
            {
                int blockStart = BlockStart(remaining, before);
                if (blockStart != int.MaxValue) insertAt = blockStart;
            }

            var result = new List<JobEntry>(remaining);
            result.InsertRange(insertAt, child.entries);
            if (result.SequenceEqual(parent.entries)) return false;
            parent.entries = result;
            return true;
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

        /// Pinned assignments are the player's placement: fixes never touch them.
        [SyncMethod]
        public static void ToggleAssignmentPin(Pawn pawn, int roleId)
        {
            if (Store == null || pawn == null || !Store.pawnSets.TryGetValue(pawn, out var set)) return;
            var assignment = set.assignments.FirstOrDefault(a => a.roleId == roleId);
            if (assignment != null) assignment.pinned = !assignment.pinned;
        }

        [SyncMethod]
        public static void PasteRoleSet(Pawn pawn, List<RoleAssignment> source)
        {
            if (Store == null || pawn == null || source == null) return;
            var set = Store.SetFor(pawn);
            var seen = new HashSet<int>();
            set.assignments = source
                .Where(a => seen.Add(a.roleId))
                .Select(a => new RoleAssignment { roleId = a.roleId, enabled = a.enabled, pinned = a.pinned })
                .ToList();
            CompiledJobOrders.Invalidate(pawn);
        }
    }
}
