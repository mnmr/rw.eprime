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

        // Void on purpose: MP defers synced execution, so a return value would
        // be null for the caller. The UI selects the new role by watching for
        // its label (RolesTabView.pendingSelectLabel).
        [SyncMethod]
        public static void CreateRole(string label)
        {
            if (Store == null) return;
            Store.roles.Add(new Role { id = Store.NextId(), label = label });
        }

        /// Engine-initiated (load-time seeding): runs inside the synced simulation
        /// on every client, so it must NOT be a synced command.
        internal static Role CreateRoleFromDef(RoleDef def)
        {
            if (Store == null || def == null) return null;
            var (hasColor, color) = def.ResolvedColor();
            var role = new Role
            {
                id = Store.NextId(),
                label = def.label,
                templateDefName = def.defName,
                templateVersion = WorkRolesMod.Version,
                templateHash = def.StableHash(),
                autoAssign = def.autoAssign,
                blocker = def.blocker,
                hasCustomColor = hasColor,
                color = color,
                iconPath = def.iconPath,
                entries = def.ParsedEntries()
            };
            Store.roles.Add(role);
            return role;
        }

        /// Applies an import on every client: the raw XML travels with the command
        /// and each client rebuilds the same deterministic plan, so the row-index
        /// selections from the preview stay valid everywhere.
        [SyncMethod]
        public static void ApplyImport(string xml,
            bool paletteInclude, bool paletteOverwrite, List<int> paletteRows,
            bool rolesInclude, bool rolesOverwrite, List<int> roleRows)
        {
            if (Store == null || xml.NullOrEmpty()) return;
            var doc = RoleIO.Parse(xml);
            if (doc.error != null) return;
            string summary = RoleIO.Apply(Store, doc,
                paletteInclude, paletteOverwrite, paletteRows,
                rolesInclude, rolesOverwrite, roleRows);
            Messages.Message(summary, MessageTypeDefOf.PositiveEvent, historical: false);
        }

        /// Applies the restore items selected in the Restore Roles preview:
        /// recreates missing seeded roles, regenerates coverage for uncovered work
        /// types, backfills vanilla jobs that mods moved out of roles, and brings
        /// back a player-deleted Odd Jobs.
        [SyncMethod]
        public static void RestoreSelected(List<string> templateDefs, List<string> workTypes,
            List<int> backfillRoleIds, bool oddJobs)
        {
            if (Store == null) return;
            var restored = Seeding.RestoreSelected(templateDefs, workTypes, backfillRoleIds, oddJobs);
            if (restored.Count > 0)
                Messages.Message("WR_RolesRestored".Translate(restored.ToCommaList()),
                    MessageTypeDefOf.PositiveEvent, historical: false);
        }

        /// Vanilla's manual-priorities flag — per-save game state whose only
        /// vanilla UI is the Work tab we replace, so the Options tab hosts it.
        /// Synced: unmanaged pawns' priority reads change under it.
        [SyncMethod]
        public static void SetUseWorkPriorities(bool value)
        {
            var playSettings = Current.Game?.playSettings;
            if (playSettings == null || playSettings.useWorkPriorities == value) return;
            playSettings.useWorkPriorities = value;
            foreach (var pawn in PawnsFinder.AllMapsWorldAndTemporary_Alive)
                if (pawn.Faction == Faction.OfPlayer && pawn.workSettings != null)
                    pawn.workSettings.Notify_UseWorkPrioritiesChanged();
        }

        /// GetPriority range for readers like Numbers: raw ranks or vanilla 0-4.
        [SyncMethod]
        public static void SetReportVanillaPriorities(bool value)
        {
            if (Store == null || Store.reportVanillaPriorities == value) return;
            Store.reportVanillaPriorities = value;
        }

        /// Toggles blocker semantics: the role's jobs become vetoes (or stop being).
        [SyncMethod]
        public static void SetRoleBlocker(int roleId, bool value)
        {
            var role = FindRole(roleId);
            if (role == null || role.blocker == value) return;
            role.blocker = value;
            CompiledJobOrders.InvalidateRole(roleId);
        }

        [SyncMethod]
        public static void DeleteRole(int roleId)
        {
            var role = FindRole(roleId);
            if (role == null) return;
            // Deleting Odd Jobs is a player opt-out: coverage stops recreating
            // it (and stops collecting invisible work types) until restored.
            if (role.managed) Store.oddJobsDeleted = true;
            CompiledJobOrders.InvalidateRole(roleId);
            foreach (var set in Store.pawnSets.Values)
                set.assignments.RemoveAll(a => a.roleId == roleId);
            Store.billRoles.RemoveAll(kv => kv.Value == roleId);
            Store.roles.Remove(role);
            SweepEmptyGroups();
        }

        // ----- Role groups (purely organizational: no priority impact) -----

        /// User groups with no stored member disappear; Default (id 0) included —
        /// it re-materializes on demand with the same id and label.
        internal static void SweepEmptyGroups()
        {
            Store?.groups.RemoveAll(g => Store.roles.All(r => r.groupId != g.id));
        }

        /// Empty/null = the Default group. The sentinel is language-independent
        /// on purpose: command args travel between MP clients, and comparing
        /// against a locally-translated name inside the command body would
        /// resolve differently per language — a guaranteed desync.
        private static RoleGroup ResolveOrCreateGroup(string groupName)
        {
            groupName = groupName?.Trim();
            if (groupName.NullOrEmpty()) return Store.EnsureDefaultGroup();
            var group = Store.GroupByName(groupName);
            if (group != null) return group;
            group = new RoleGroup { id = Store.NextGroupId(), label = groupName };
            Store.groups.Add(group);
            return group;
        }

        /// The role plus (optionally) its same-group tree-children, catalog order.
        /// Overlay members (rules/blocker/managed) never ride along — they don't
        /// display under the parent.
        private static List<Role> MovingBlock(Role role, bool withChildren)
        {
            var moving = new List<Role> { role };
            if (withChildren)
                foreach (var other in Store.roles)
                    if (other.groupId == role.groupId && other != role
                        && !other.blocker && !other.managed && !other.HasRules
                        && role.Covers(other))
                        moving.Add(other);
            return moving;
        }

        /// Moves a role (and, when asked, its current same-group tree-children)
        /// into the named group, creating it if needed. Children are resolved
        /// inside the command from synced state, so every client agrees.
        [SyncMethod]
        public static void SetRoleGroup(int roleId, string groupName, bool withChildren)
        {
            var role = FindRole(roleId);
            if (role == null || role.managed) return;
            var group = ResolveOrCreateGroup(groupName);
            if (group == null) return;
            foreach (var moved in MovingBlock(role, withChildren))
                moved.groupId = group.id;
            SweepEmptyGroups();
        }

        /// Drag drop: SetRoleGroup plus a catalog reposition — the moved block
        /// lands just before beforeRoleId (-1 = end), which fixes its place
        /// within the target group's span (catalog order is display order).
        [SyncMethod]
        public static void MoveRoleTo(int roleId, string groupName, int beforeRoleId, bool withChildren)
        {
            var role = FindRole(roleId);
            if (role == null || role.managed) return;
            var group = ResolveOrCreateGroup(groupName);
            if (group == null) return;
            var moving = MovingBlock(role, withChildren);
            foreach (var moved in moving)
                moved.groupId = group.id;
            var before = beforeRoleId >= 0 ? FindRole(beforeRoleId) : null;
            if (before == null || !moving.Contains(before))
            {
                Store.roles.RemoveAll(moving.Contains);
                int insertAt = before != null ? Store.roles.IndexOf(before) : Store.roles.Count;
                Store.roles.InsertRange(insertAt, moving);
            }
            SweepEmptyGroups();
        }

        [SyncMethod]
        public static void RenameGroup(int groupId, string name)
        {
            var group = Store?.GroupById(groupId);
            if (group == null || groupId == RoleGroup.DefaultId || name.NullOrEmpty()) return;
            group.label = name.Trim();
        }

        /// Reorders the group list (display order). Default stays pinned first.
        [SyncMethod]
        public static void MoveGroupInList(int from, int to)
        {
            var groups = Store?.groups;
            if (groups == null || from < 0 || from >= groups.Count || to < 0 || to >= groups.Count || from == to) return;
            if (groups[from].id == RoleGroup.DefaultId) return;
            var group = groups[from];
            groups.RemoveAt(from);
            groups.Insert(to, group);
            if (groups.Count > 1 && groups[0].id != RoleGroup.DefaultId)
            {
                int defaultIdx = groups.FindIndex(g => g.id == RoleGroup.DefaultId);
                if (defaultIdx > 0)
                {
                    var def = groups[defaultIdx];
                    groups.RemoveAt(defaultIdx);
                    groups.Insert(0, def);
                }
            }
        }

        /// Composition: the parent gains the child's jobs (appended; job order is
        /// edited in the Selected Jobs pane) and the child moves into the
        /// parent's group so coverage nesting shows it there.
        [SyncMethod]
        public static void IncludeRole(int parentId, int childId)
        {
            var parent = FindRole(parentId);
            var child = FindRole(childId);
            if (parent == null || child == null || parent == child
                || parent.managed || child.managed || child.blocker) return;
            foreach (var entry in child.entries)
                if (!parent.entries.Contains(entry))
                    parent.entries.Add(entry);
            child.groupId = parent.groupId;
            SweepEmptyGroups();
            CompiledJobOrders.InvalidateRole(parentId);
        }

        /// Un-composition: the parent loses the child's jobs (the child role
        /// itself is untouched and un-nests by no longer being covered).
        [SyncMethod]
        public static void ExcludeChild(int parentId, int childId)
        {
            var parent = FindRole(parentId);
            var child = FindRole(childId);
            if (parent == null || child == null || parent == child || parent.managed) return;
            var childSet = new HashSet<JobEntry>(child.entries);
            if (parent.entries.RemoveAll(e => childSet.Contains(e)) > 0)
                CompiledJobOrders.InvalidateRole(parentId);
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
            if (Store == null || index < 0 || index >= RoleStore.MaxCustomSwatches) return;
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

        // Void like CreateRole: MP-deferred execution eats return values.
        [SyncMethod]
        public static void DuplicateRole(int roleId, string label = null)
        {
            var source = FindRole(roleId);
            if (source == null) return;
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
                locationTokens = new List<string>(source.locationTokens),
                groupId = source.groupId,
                entries = new List<JobEntry>(source.entries)
            };
            Store.roles.Add(copy);
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

        /// Adds/removes one location token; the role is active wherever any of
        /// its tokens match (none = anywhere).
        [SyncMethod]
        public static void ToggleRoleLocation(int roleId, string token)
        {
            var role = FindRole(roleId);
            if (role == null || token.NullOrEmpty()) return;
            if (!role.locationTokens.Remove(token))
                role.locationTokens.Add(token);
            CompiledJobOrders.InvalidateRole(roleId);
        }

        [SyncMethod]
        public static void ClearRoleLocations(int roleId)
        {
            var role = FindRole(roleId);
            if (role == null || role.locationTokens.Count == 0) return;
            role.locationTokens.Clear();
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
            role.locationTokens.Clear();
            CompiledJobOrders.InvalidateRole(roleId);
        }

        // ----- Role content -----

        [SyncMethod]
        public static void AddEntry(int roleId, JobEntry entry, int index = -1)
        {
            var role = FindRole(roleId);
            if (role == null || role.managed) return;
            // UI checks run before the synced command lands, so duplicates can
            // still race in (two MP clients adding the same entry).
            if (role.entries.Contains(entry)) return;
            if (index < 0 || index > role.entries.Count) index = role.entries.Count;
            role.entries.Insert(index, entry);
            CompiledJobOrders.InvalidateRole(roleId);
        }

        /// Removes entries with no effect (claimed above, duplicates). Behavior-
        /// neutral: dead entries change neither the compiled order nor coverage.
        /// The editor shows them dimmed while editing and commits the scrub when
        /// the player leaves the role.
        [SyncMethod]
        public static void ScrubDeadEntries(int roleId)
        {
            var role = FindRole(roleId);
            if (role != null && ScrubDeadEntriesDirect(role))
                CompiledJobOrders.InvalidateRole(roleId);
        }

        /// Engine path (load sweep): runs in the synced simulation on every client.
        internal static bool ScrubDeadEntriesDirect(Role role)
        {
            var dead = JobOrderCompiler.DeadEntryIndexes(role.entries, GameJobCatalog.Instance);
            if (dead.Count == 0) return false;
            foreach (int index in dead.OrderByDescending(i => i))
                role.entries.RemoveAt(index);
            return true;
        }

        [SyncMethod]
        public static void RemoveEntry(int roleId, int index)
        {
            var role = FindRole(roleId);
            if (role == null || role.managed || index < 0 || index >= role.entries.Count) return;
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
            if (role == null || role.entries.Contains(entry)) return;
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
