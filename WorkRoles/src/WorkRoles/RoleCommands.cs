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

        /// Colony-wide lossless collapse (see CombineCore).
        [SyncMethod]
        public static void CombineAssignedRoles()
        {
            if (Store == null) return;
            foreach (var kv in Store.pawnSets)
                if (CombineCore(kv.Value.assignments, null, dryRun: false))
                    CompiledJobOrders.Invalidate(kv.Key);
        }

        /// Lossless collapse for one pawn.
        [SyncMethod]
        public static void CombineAssignedRolesFor(Pawn pawn)
        {
            if (Store == null || pawn == null || !Store.pawnSets.TryGetValue(pawn, out var set)) return;
            if (CombineCore(set.assignments, null, dryRun: false))
                CompiledJobOrders.Invalidate(pawn);
        }

        /// Whether a combine would change this pawn's role set (no mutation).
        public static bool CanCombineFor(Pawn pawn)
            => Store != null && pawn != null && Store.pawnSets.TryGetValue(pawn, out var set)
               && CombineCore(set.assignments, null, dryRun: true);

        /// Full combine simulation for one pawn, without touching its role set:
        /// runs the exact apply algorithm on a clone and reports each collapse.
        public static List<(Role combo, List<Role> members)> CombinePlanFor(Pawn pawn)
        {
            var steps = new List<(Role combo, List<Role> members)>();
            if (Store == null || pawn == null || !Store.pawnSets.TryGetValue(pawn, out var set)) return steps;
            var clone = set.assignments
                .Select(a => new RoleAssignment { roleId = a.roleId, enabled = a.enabled })
                .ToList();
            CombineCore(clone, steps, dryRun: false);
            return steps;
        }

        /// Lossless collapse: each CONSECUTIVE block of assigned roles whose entry sets
        /// union to EXACTLY a covering catalog role's entry set (and whose per-pawn
        /// toggles agree) is replaced by one assignment of that role. Consecutiveness
        /// guarantees no priority reshuffle relative to the pawn's other roles;
        /// rule-carrying or disabled roles never participate. Later combos see earlier
        /// replacements, so collapses cascade within one call.
        /// dryRun: no mutation, returns true at the first possible collapse.
        private static bool CombineCore(List<RoleAssignment> assignments,
            List<(Role combo, List<Role> members)> steps, bool dryRun)
        {
            bool changed = false;
            foreach (var combo in Store.roles)
            {
                if (!combo.enabled || combo.HasRules) continue;
                if (assignments.Any(a => a.roleId == combo.id)) continue;

                // Candidates: assigned roles the combo covers, globally enabled, rule-free.
                var members = new List<int>();
                for (int i = 0; i < assignments.Count; i++)
                {
                    var role = Store.RoleById(assignments[i].roleId);
                    if (role != null && role.enabled && !role.HasRules && combo.Covers(role))
                        members.Add(i);
                }
                if (members.Count < 2) continue;

                // Members must form one consecutive block in the assignment list.
                if (members[members.Count - 1] - members[0] != members.Count - 1) continue;

                // All per-pawn toggles must agree, and the union of the members'
                // entry sets must equal the combo's entry set exactly.
                bool sharedEnabled = assignments[members[0]].enabled;
                var union = new HashSet<JobEntry>();
                bool flagsAgree = true;
                foreach (int i in members)
                {
                    if (assignments[i].enabled != sharedEnabled) { flagsAgree = false; break; }
                    union.UnionWith(Store.RoleById(assignments[i].roleId).entries);
                }
                if (!flagsAgree || !union.SetEquals(combo.entries)) continue;

                if (dryRun) return true;

                steps?.Add((combo, members.Select(i => Store.RoleById(assignments[i].roleId)).ToList()));

                int blockStart = members[0];
                assignments.RemoveRange(blockStart, members.Count);
                assignments.Insert(blockStart, new RoleAssignment { roleId = combo.id, enabled = sharedEnabled });
                changed = true;
            }
            return changed;
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
