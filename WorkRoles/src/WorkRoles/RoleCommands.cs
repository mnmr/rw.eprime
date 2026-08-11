using System.Collections.Generic;
using System.Linq;
using Multiplayer.API;
using RimWorld;
using UnityEngine;
using Verse;
using WorkRoles.Core;
using WorkRoles.Core.Recs;

namespace WorkRoles
{
    public static partial class RoleCommands
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
            label = label?.Trim();
            if (Store == null || !CatalogNameRules.IsAvailable(
                    label, Store.roles, role => role.label)) return;
            // A blank role has no entries yet, so empty derived skills and a
            // zero age floor are correct.
            Store.roles.Add(new Role { id = Store.NextId(), label = label, tuningSeeded = true, minAge = 0 });
            Store.InvalidateRoleIndex();
            UiVersion.Bump();
        }

        /// Flips a role between regular and composite. The replaced content is
        /// dropped (entries and snapshots one way, members the other); the UI
        /// confirms first when content would be lost. Refused for composite
        /// members: a member cannot itself become a composite (depth 1).
        [SyncMethod]
        public static void SetRoleComposite(int roleId, bool value)
        {
            var role = FindRole(roleId);
            if (role == null || role.composite == value
                || Store.IsCompositeMember(roleId)) return;
            role.composite = value;
            role.entries.Clear();
            role.memberRoleIds.Clear();
            role.workTypeSnapshots.Clear();
            CompiledJobOrders.InvalidateRole(roleId);
            ReconcileHolders(roleId);
        }

        /// Engine-initiated (load-time seeding): runs inside the synced simulation
        /// on every client, so it must NOT be a synced command.
        internal static Role CreateRoleFromDef(RoleDef def)
        {
            if (Store == null || def == null) return null;
            string label = CatalogNameRules.Unique(
                SeededDefIdentity.RoleLabel(def), Store.roles, existing => existing.label);
            if (label == null) return null;
            var (hasColor, color) = def.ResolvedColor();
            var role = new Role
            {
                id = Store.NextId(),
                label = label,
                templateDefName = def.defName,
                templateVersion = WorkRolesMod.Version,
                templateHash = def.StableHash(),
                autoAssign = def.autoAssign,
                blocker = def.blocker,
                hasCustomColor = hasColor,
                color = color,
                iconPath = def.iconPath,
                entries = def.ParsedEntries(),
                category = def.tuning.category,
                time = def.tuning.time,
                championPenalty = def.tuning.championPenalty,
                colonyMin = def.tuning.colonyMin,
                coverage = def.tuning.coverage,
                requiredSkills = new List<string>(def.tuning.skills.required),
                optionalSkills = new List<string>(def.tuning.skills.optional),
                tuningSeeded = true,
            };
            role.minAge = def.tuning.minAge >= 0
                ? def.tuning.minAge : RecsAdapter.MinUnlockAgeOf(role);
            role.maxAge = UnityEngine.Mathf.Clamp(def.tuning.maxAge, 0, 18);
            if (!def.group.NullOrEmpty())
                role.groupId = ResolveOrCreateGroup(SeededDefIdentity.GroupLabel(def)).id;
            if (!def.activeHours.NullOrEmpty() && def.activeHours.Length == 24)
                role.activeHours = RoleFile.BitsToHours(def.activeHours);
            foreach (var location in def.locations)
            {
                if (string.Equals(location, "Settlements", System.StringComparison.OrdinalIgnoreCase))
                    role.locationTokens.Add(LocationRules.Settlements);
                else if (string.Equals(location, "Caravans", System.StringComparison.OrdinalIgnoreCase))
                    role.locationTokens.Add(LocationRules.Caravans);
                else
                    Log.Warning($"[WorkRoles] RoleDef {def.defName}: unknown location '{location}'");
            }
            Store.roles.Add(role);
            Store.InvalidateRoleIndex();
            if (role.templateDefName == "WS_Basics")
                CompiledJobOrders.InvalidateRole(role.id);
            return role;
        }

        /// Seeding path for group creation: runs inside the synced simulation.
        internal static RoleGroup EnsureGroup(string label) => ResolveOrCreateGroup(label);

        /// Applies an import on every client: the raw XML travels with the command
        /// and each client rebuilds the same deterministic plan, so the row-index
        /// selections from the preview stay valid everywhere.
        [SyncMethod]
        public static void ApplyImport(ImportSelection selection)
        {
            if (Store == null || selection == null || selection.xml.NullOrEmpty()) return;
            var doc = RoleIO.Parse(selection.xml);
            if (doc.error != null) return;
            var resolvedLocations = ImportLocationResolver.BuildMap(
                selection.locationFileTokens, selection.locationRuntimeTokens);
            string summary = ApplyImportToStore(Store, doc,
                selection.palette, selection.paletteOverwrite, selection.paletteRows,
                selection.roles, selection.rolesOverwrite, selection.roleRows,
                selection.paths, selection.pathsOverwrite, selection.pathRows,
                selection.order, resolvedLocations);
            UiVersion.Bump();
            UI.WrToast.Show(summary, MessageTypeDefOf.PositiveEvent);
        }

        /// Applies the restore items selected in the Restore Defaults preview:
        /// recreates missing seeded roles and default training paths, regenerates
        /// coverage for uncovered work types, backfills vanilla jobs that mods
        /// moved out of roles, moves and recolors drifted roles back to their def,
        /// restores holder defaults, and resets the recommendation order.
        [SyncMethod]
        public static void RestoreSelected(RestoreSelection selection)
        {
            if (Store == null || selection == null) return;
            var restored = Seeding.RestoreSelected(selection);
            if (restored.Count == 0) return;
            UiVersion.Bump();
            UI.WrToast.Show("WR_RolesRestored".Translate(restored.ToCommaList()),
                MessageTypeDefOf.PositiveEvent);
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

        /// Full replacement of the recommendation order (Options tab reorder).
        [SyncMethod]
        public static void SetRecommendationOrder(List<int> roleIds)
        {
            if (Store == null) return;
            if (TrainingPathMutationPolicy.IntSequenceEqual(
                    Store.recommendationOrder, roleIds)) return;
            Store.recommendationOrder = roleIds ?? new List<int>();
            UiVersion.Bump();
        }

        /// One shared recommendation-formula edit. The option id is synced as
        /// a primitive stable enum value; Core clamps and normalizes the value.
        [SyncMethod]
        public static void SetRecommendationTuningOption(int optionId, int value)
        {
            if (Store == null
                || !System.Enum.IsDefined(
                    typeof(RecommendationTuningOption), optionId))
                return;
            RecommendationsTuningOptions current = Store.recommendationTuning
                ?? RecommendationsTuningOptions.Default;
            RecommendationsTuningOptions changed = current.With(
                (RecommendationTuningOption)optionId, value);
            if (ReferenceEquals(current, changed)) return;
            Store.recommendationTuning = changed;
            Store.RecommendationTuningRevision++;
        }

        /// Atomic reset of one section of the shared formula inputs. The key
        /// is the section's language key: stable and language-independent.
        [SyncMethod]
        public static void ResetRecommendationTuningSection(string sectionKey)
        {
            if (Store == null || sectionKey.NullOrEmpty()) return;
            RecommendationsTuningOptions current = Store.recommendationTuning
                ?? RecommendationsTuningOptions.Default;
            RecommendationsTuningOptions changed = current;
            foreach (RecommendationTuningDescriptor descriptor in
                     RecommendationsTuningOptions.Descriptors)
                if (descriptor.SectionLabelKey == sectionKey)
                    changed = changed.With(descriptor.Option,
                        RecommendationsTuningOptions.Default.Get(descriptor.Option));
            if (ReferenceEquals(current, changed)) return;
            Store.recommendationTuning = changed;
            Store.RecommendationTuningRevision++;
        }

        /// Per-role recommendation tuning. Enums travel as primitive ints for
        /// sync safety; UiVersion is the domain.
        [SyncMethod]
        public static void SetRoleCategory(int roleId, int category)
        {
            var role = FindRole(roleId);
            if (role == null || !System.Enum.IsDefined(typeof(RoleCategory), category)
                || role.category == (RoleCategory)category) return;
            role.category = (RoleCategory)category;
            UiVersion.Bump();
        }

        [SyncMethod]
        public static void SetRoleTime(int roleId, int time)
        {
            var role = FindRole(roleId);
            if (role == null || !System.Enum.IsDefined(typeof(RoleTime), time)
                || role.time == (RoleTime)time) return;
            role.time = (RoleTime)time;
            UiVersion.Bump();
        }

        [SyncMethod]
        public static void SetRoleChampionPenalty(int roleId, bool value)
        {
            var role = FindRole(roleId);
            if (role == null || role.championPenalty == value) return;
            role.championPenalty = value;
            UiVersion.Bump();
        }

        [SyncMethod]
        public static void SetRoleMinimumAge(int roleId, int value)
        {
            var role = FindRole(roleId);
            value = UnityEngine.Mathf.Clamp(value, 0, 18);
            if (role == null || role.minAge == value) return;
            role.minAge = value;
            UiVersion.Bump();
        }

        [SyncMethod]
        public static void SetRoleMaximumAge(int roleId, int value)
        {
            var role = FindRole(roleId);
            value = UnityEngine.Mathf.Clamp(value, 0, 18);
            if (role == null || role.maxAge == value) return;
            role.maxAge = value;
            UiVersion.Bump();
        }

        /// Assignment scaling inputs (future scale replacement).
        [SyncMethod]
        public static void SetRoleColonyMinimum(int roleId, int value)
        {
            var role = FindRole(roleId);
            value = UnityEngine.Mathf.Clamp(value, 0, 30);
            if (role == null || role.colonyMin == value) return;
            role.colonyMin = value;
            UiVersion.Bump();
        }

        [SyncMethod]
        public static void SetRoleCoverage(int roleId, int value)
        {
            var role = FindRole(roleId);
            value = UnityEngine.Mathf.Clamp(value, 0, 100);
            if (role == null || role.coverage == value) return;
            role.coverage = value;
            UiVersion.Bump();
        }

        /// Within one role a skill is either required or optional: adding to
        /// one list removes it from the other in the same command.
        [SyncMethod]
        public static void AddRoleSkill(int roleId, string skillDefName, bool optional)
        {
            var role = FindRole(roleId);
            if (role == null || skillDefName.NullOrEmpty()
                || DefDatabase<SkillDef>.GetNamedSilentFail(skillDefName) == null) return;
            var target = optional ? role.optionalSkills : role.requiredSkills;
            if (target.Contains(skillDefName)) return;
            (optional ? role.requiredSkills : role.optionalSkills).Remove(skillDefName);
            target.Add(skillDefName);
            UiVersion.Bump();
        }

        [SyncMethod]
        public static void RemoveRoleSkill(int roleId, string skillDefName, bool optional)
        {
            var role = FindRole(roleId);
            if (role == null || skillDefName.NullOrEmpty()) return;
            if (!(optional ? role.optionalSkills : role.requiredSkills)
                    .Remove(skillDefName)) return;
            UiVersion.Bump();
        }

        /// Whole-list upsert of a role's own training path. Hard validation: a
        /// synced command from a stale snapshot must never land corrupt
        /// geometry, and the owner role must stay in its own path. The trivial
        /// self-only full-axis path normalizes to empty storage.
        [SyncMethod]
        public static void SetRoleTraining(int roleId,
            List<int> trainingRoleIds, List<int> bandMins, List<int> bandMaxes)
        {
            var role = FindRole(roleId);
            if (role == null) return;
            trainingRoleIds = (trainingRoleIds ?? new List<int>()).ToList();
            bandMins = (bandMins ?? new List<int>()).ToList();
            bandMaxes = (bandMaxes ?? new List<int>()).ToList();
            // Bands ride along when a stale id drops out, so filter as tuples.
            for (int i = trainingRoleIds.Count - 1; i >= 0; i--)
                if (Store.RoleById(trainingRoleIds[i]) == null
                    || trainingRoleIds.IndexOf(trainingRoleIds[i]) != i)
                {
                    trainingRoleIds.RemoveAt(i);
                    if (i < bandMins.Count) bandMins.RemoveAt(i);
                    if (i < bandMaxes.Count) bandMaxes.RemoveAt(i);
                }
            if (trainingRoleIds.Count > 0
                && (!trainingRoleIds.Contains(roleId)
                    || !SkillProgressionMath.Validate(
                        trainingRoleIds.Count, bandMins, bandMaxes)))
                return;
            if (trainingRoleIds.Count == 1
                && bandMins[0] == 0
                && bandMaxes[0] == SkillProgressionMath.MaxLevel)
            {
                trainingRoleIds.Clear();
                bandMins.Clear();
                bandMaxes.Clear();
            }
            IReadOnlyList<int> normalizedMins = trainingRoleIds.Count == 0 ? null : bandMins;
            IReadOnlyList<int> normalizedMaxes = trainingRoleIds.Count == 0 ? null : bandMaxes;
            if (TrainingPathMutationPolicy.BandsEqual(
                    role.trainingRoleIds, role.trainingMins, role.trainingMaxes,
                    trainingRoleIds, normalizedMins, normalizedMaxes)) return;
            role.trainingRoleIds = trainingRoleIds;
            role.trainingMins = trainingRoleIds.Count == 0 ? new List<int>() : bandMins;
            role.trainingMaxes = trainingRoleIds.Count == 0 ? new List<int>() : bandMaxes;
            UiVersion.Bump();
        }

        /// Toggles blocker semantics: the role's jobs become vetoes (or stop being).
        /// Refused for composite members: flipping a member's veto silently
        /// changes every bundle carrying it, so it must leave the bundles first.
        [SyncMethod]
        public static void SetRoleBlocker(int roleId, bool value)
        {
            var role = FindRole(roleId);
            if (role == null || role.blocker == value
                || Store.IsCompositeMember(roleId)) return;
            role.blocker = value;
            CompiledJobOrders.InvalidateRole(roleId);
            ReconcileHolders(roleId);
        }

        [SyncMethod]
        public static void DeleteRole(int roleId)
        {
            var role = FindRole(roleId);
            if (role == null) return;
            using var uiBatch = new UiInvalidationBatch(UiVersion.Bump);
            var losingLastAssignment = Store.pawnSets
                .Where(kv => kv.Value.assignments.Count > 0
                    && kv.Value.assignments.TrueForAll(a => a.roleId == roleId))
                .Select(kv => kv.Key)
                .ToList();
            // Holders that keep other roles lose this role's work; unmanaged
            // pawns keep vanilla authority by design (mirrored fallback).
            // Composite holders lose the member's slice, so they reconcile too.
            var keepers = Store.PawnsWithRole(roleId)
                .Except(losingLastAssignment).ToList();
            foreach (var composited in Store.CompositesContaining(roleId))
                foreach (var pawn in Store.PawnsWithRole(composited.id))
                    if (!keepers.Contains(pawn) && !losingLastAssignment.Contains(pawn))
                        keepers.Add(pawn);
            foreach (var pawn in losingLastAssignment)
                Store.UnmanagePawn(pawn, uiBatch.Request);
            CompiledJobOrders.InvalidateRole(roleId, uiBatch.Request);
            foreach (var set in Store.pawnSets.Values)
                set.assignments.RemoveAll(a => a.roleId == roleId);
            // A pawn's last role going away must unmanage it fully — a lingering
            // empty set would shadow its vanilla priorities (see RoleStore save sync).
            Store.pawnSets.RemoveAll(kv => kv.Value.assignments.Count == 0);
            Store.RemoveBillRolesForRole(roleId);
            Store.roles.Remove(role);
            Store.InvalidateRoleIndex();
            // The deleted role leaves every composite's member list (coverage
            // and compiled orders were invalidated above, while membership was
            // still visible to the reverse scan).
            foreach (var other in Store.roles)
                if (other.composite) other.memberRoleIds.Remove(roleId);
            // The deleted role leaves every other role's training path; a path
            // reduced to the trivial self-only shape clears via sanitize.
            foreach (var other in Store.roles)
            {
                int at = other.trainingRoleIds.IndexOf(roleId);
                if (at < 0) continue;
                // Parallel-list alignment is guaranteed by load sanitize +
                // validated commands; raw RemoveAt is safe here.
                other.trainingRoleIds.RemoveAt(at);
                other.trainingMins.RemoveAt(at);
                other.trainingMaxes.RemoveAt(at);
                Store.SanitizeRoleTraining(other);
            }
            SweepEmptyGroups();
            CompiledJobOrders.EnqueueReconciles(keepers);
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
            if (groupName.NullOrEmpty() || GroupNameRules.IsDefault(groupName))
                return Store.EnsureDefaultGroup();
            var group = Store.GroupByName(groupName);
            if (group != null) return group;
            if (!GroupNameRules.IsAvailable(
                    groupName, Store.groups, existing => existing.label)) return null;
            group = new RoleGroup { id = Store.NextGroupId(), label = groupName };
            Store.groups.Add(group);
            return group;
        }

        /// The role plus (optionally) its same-group tree-children, catalog order.
        /// Overlay members (rules/blocker) never ride along — they don't
        /// display under the parent.
        /// The dragged role plus every same-group role its subtree displays —
        /// the same pair rule the tree uses, so drag moves what the user sees.
        private static List<Role> MovingBlock(Role role, bool withChildren)
        {
            var moving = new List<Role> { role };
            if (withChildren)
                foreach (var other in Store.roles)
                    if (other.groupId == role.groupId && other != role
                        && UI.RolesListState.CanNest(role, other)
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
            if (role == null) return;
            int groupCount = Store.groups.Count;
            var group = ResolveOrCreateGroup(groupName);
            if (group == null) return;
            List<Role> moving = MovingBlock(role, withChildren);
            bool changed = Store.groups.Count != groupCount;
            foreach (var moved in moving)
                if (moved.groupId != group.id)
                {
                    moved.groupId = group.id;
                    changed = true;
                }
            if (!changed) return;
            SweepEmptyGroups();
            UiVersion.Bump();
        }

        /// Drag drop: SetRoleGroup plus a catalog reposition — the moved block
        /// lands just before beforeRoleId (-1 = end), which fixes its place
        /// within the target group's span (catalog order is display order).
        [SyncMethod]
        public static void MoveRoleTo(int roleId, string groupName, int beforeRoleId, bool withChildren)
        {
            var role = FindRole(roleId);
            if (role == null) return;
            int groupCount = Store.groups.Count;
            var group = ResolveOrCreateGroup(groupName);
            if (group == null) return;
            var moving = MovingBlock(role, withChildren);
            var before = beforeRoleId >= 0 ? FindRole(beforeRoleId) : null;
            List<Role> reordered = null;
            if (before == null || !moving.Contains(before))
            {
                reordered = Store.roles.Where(candidate =>
                    !moving.Contains(candidate)).ToList();
                int insertAt = before != null
                    ? reordered.IndexOf(before) : reordered.Count;
                reordered.InsertRange(insertAt, moving);
            }
            bool changed = Store.groups.Count != groupCount;
            for (int i = 0; i < moving.Count && !changed; i++)
                changed = moving[i].groupId != group.id;
            bool orderChanged = false;
            if (reordered != null)
            {
                for (int i = 0; i < reordered.Count; i++)
                    if (!ReferenceEquals(reordered[i], Store.roles[i]))
                    {
                        orderChanged = true;
                        break;
                    }
            }
            if (!changed && !orderChanged) return;
            for (int i = 0; i < moving.Count; i++)
                moving[i].groupId = group.id;
            if (orderChanged)
            {
                Store.roles.Clear();
                Store.roles.AddRange(reordered);
                Store.InvalidateRoleIndex();
            }
            SweepEmptyGroups();
            UiVersion.Bump();
        }

        [SyncMethod]
        public static void RenameGroup(int groupId, string name)
        {
            var group = Store?.GroupById(groupId);
            name = name?.Trim();
            if (group == null || groupId == RoleGroup.DefaultId
                || string.Equals(group.label, name,
                    System.StringComparison.Ordinal)
                || !GroupNameRules.IsAvailable(
                    name, Store.groups, existing => existing.label, group)) return;
            group.label = name;
            UiVersion.Bump();
        }

        /// Reorders the group list (display order). Default stays pinned first.
        [SyncMethod]
        public static void MoveGroupInList(int from, int to)
        {
            var groups = Store?.groups;
            if (groups == null || from < 0 || from >= groups.Count || to < 0 || to >= groups.Count || from == to) return;
            if (groups[from].id == RoleGroup.DefaultId) return;
            var reordered = new List<RoleGroup>(groups);
            var group = reordered[from];
            reordered.RemoveAt(from);
            reordered.Insert(to, group);
            if (reordered.Count > 1
                && reordered[0].id != RoleGroup.DefaultId)
            {
                int defaultIdx = reordered.FindIndex(g =>
                    g.id == RoleGroup.DefaultId);
                if (defaultIdx > 0)
                {
                    var def = reordered[defaultIdx];
                    reordered.RemoveAt(defaultIdx);
                    reordered.Insert(0, def);
                }
            }
            bool changed = false;
            for (int i = 0; i < groups.Count && !changed; i++)
                changed = !ReferenceEquals(groups[i], reordered[i]);
            if (!changed) return;
            groups.Clear();
            groups.AddRange(reordered);
            UiVersion.Bump();
        }

        [SyncMethod]
        public static void RenameRole(int roleId, string label)
        {
            var role = FindRole(roleId);
            label = label?.Trim();
            if (role == null || string.Equals(role.label, label,
                    System.StringComparison.Ordinal)
                || !CatalogNameRules.IsAvailable(
                    label, Store.roles, existing => existing.label, role)) return;
            role.label = label;
            UiVersion.Bump();
        }

        [SyncMethod]
        public static void SetRoleColor(int roleId, UnityEngine.Color color)
        {
            var role = FindRole(roleId);
            if (role == null || TrainingPathMutationPolicy.ColorEqual(
                    role.hasCustomColor,
                    role.color.r, role.color.g, role.color.b, role.color.a,
                    true, color.r, color.g, color.b, color.a)) return;
            role.color = color;
            role.hasCustomColor = true;
            UiVersion.Bump();
        }

        /// Auto-assign roles go to newcomers and lead every plan target.
        [SyncMethod]
        public static void SetRoleAutoAssign(int roleId, bool value)
        {
            var role = FindRole(roleId);
            if (role == null || role.autoAssign == value) return;
            role.autoAssign = value;
            UiVersion.Bump();
        }

        /// Defines one of the shared custom swatch slots in the role editor.
        [SyncMethod]
        public static void SetCustomSwatch(int index, UnityEngine.Color color)
        {
            if (Store == null || index < 0 || index >= RoleStore.MaxCustomSwatches) return;
            if (index < Store.customSwatches.Count)
            {
                UnityEngine.Color current = Store.customSwatches[index];
                if (TrainingPathMutationPolicy.ColorEqual(true,
                        current.r, current.g, current.b, current.a,
                        true, color.r, color.g, color.b, color.a)) return;
            }
            while (Store.customSwatches.Count <= index)
                Store.customSwatches.Add(UnityEngine.Color.clear);
            Store.customSwatches[index] = color;
            UiVersion.Bump();
        }

        /// Empties one shared custom swatch slot; it renders as a "+" picker
        /// again. Callers owning roles painted with the slot's color must
        /// recolor them (SwatchPickPlanner), or their palette highlight dies.
        [SyncMethod]
        public static void ClearCustomSwatch(int index)
        {
            if (Store == null || index < 0
                || index >= Store.customSwatches.Count) return;
            if (Store.customSwatches[index].a < 0.5f) return;
            Store.customSwatches[index] = UnityEngine.Color.clear;
            UiVersion.Bump();
        }

        /// Role-level mutations can revoke or demote work the holders are
        /// already doing; reconcile every holder after the invalidation.
        /// Composites bundle the role's jobs, so their holders reconcile too.
        private static void ReconcileHolders(int roleId)
        {
            CompiledJobOrders.EnqueueReconciles(Store.PawnsWithRole(roleId));
            foreach (var composited in Store.CompositesContaining(roleId))
                CompiledJobOrders.EnqueueReconciles(
                    Store.PawnsWithRole(composited.id));
        }

        [SyncMethod]
        public static void ToggleRoleGlobal(int roleId)
        {
            var role = FindRole(roleId);
            if (role == null) return;
            role.enabled = !role.enabled;
            CompiledJobOrders.InvalidateRole(roleId);
            ReconcileHolders(roleId);
        }

        // Void like CreateRole: MP-deferred execution eats return values.
        [SyncMethod]
        public static void DuplicateRole(int roleId, string label = null)
        {
            var source = FindRole(roleId);
            label = (label ?? source?.label)?.Trim();
            if (source == null || !CatalogNameRules.IsAvailable(
                    label, Store.roles, existing => existing.label)) return;
            var copy = PlayerDuplicate(source, Store.NextId(), label);
            Store.roles.Add(copy);
            Store.InvalidateRoleIndex();
            UiVersion.Bump();
        }

        /// Player duplicates retain role behavior and presentation but deliberately
        /// lose template ownership and auto-assignment. Derived and Scribe caches
        /// remain fresh because materialization creates a new Role instance.
        private static Role PlayerDuplicate(Role source, int id, string label)
        {
            RoleCopyValues<Color> values = new RoleCopyValues<Color>
            {
                Enabled = source.enabled,
                HasCustomColor = source.hasCustomColor,
                Color = source.color,
                IconPath = source.iconPath,
                TemplateDefName = source.templateDefName,
                TemplateVersion = source.templateVersion,
                TemplateHash = source.templateHash,
                AutoAssign = source.autoAssign,
                Blocker = source.blocker,
                HolderScaleName = source.holderScaleName,
                Category = source.category,
                Time = source.time,
                ChampionPenalty = source.championPenalty,
                MinAge = source.minAge,
                MaxAge = source.maxAge,
                ColonyMin = source.colonyMin,
                Coverage = source.coverage,
                RequiredSkills = source.requiredSkills,
                OptionalSkills = source.optionalSkills,
                GroupId = source.groupId,
                ActiveHours = source.activeHours,
                LocationTokens = source.locationTokens,
                Entries = source.entries,
                WorkTypeSnapshots = source.workTypeSnapshots,
            }.ForPlayerDuplicate();

            return new Role
            {
                id = id,
                label = label,
                enabled = values.Enabled,
                hasCustomColor = values.HasCustomColor,
                color = values.Color,
                iconPath = values.IconPath,
                templateDefName = values.TemplateDefName,
                templateVersion = values.TemplateVersion,
                templateHash = values.TemplateHash,
                autoAssign = values.AutoAssign,
                blocker = values.Blocker,
                holderScaleName = values.HolderScaleName,
                category = values.Category,
                time = values.Time,
                championPenalty = values.ChampionPenalty,
                minAge = values.MinAge,
                maxAge = values.MaxAge,
                colonyMin = values.ColonyMin,
                coverage = values.Coverage,
                requiredSkills = values.RequiredSkills,
                optionalSkills = values.OptionalSkills,
                tuningSeeded = true,
                groupId = values.GroupId,
                activeHours = values.ActiveHours,
                locationTokens = values.LocationTokens,
                entries = values.Entries,
                workTypeSnapshots = values.WorkTypeSnapshots,
                composite = source.composite,
                memberRoleIds = new List<int>(source.memberRoleIds),
            };
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
            Store.InvalidateRoleIndex();
            UiVersion.Bump();
        }

        // ----- Role rules -----

        /// Deterministic load/setup migration. Legacy ship tokens used landing
        /// map ids; the game has exactly one Gravship, so every recognized ship
        /// token maps to its sole stable engine identity. This runs at most once
        /// per save and never from UI or render code.
        internal static void MigrateLocationTokensOnce()
        {
            var store = Store;
            if (store == null || store.LocationTokenSchemaVersion
                >= RoleStore.CurrentLocationTokenSchemaVersion)
                return;

            var liveSettlementTokens = new HashSet<string>(
                System.StringComparer.Ordinal);
            string stableShipToken = ColonyScope.CollectLocationMigrationFacts(
                liveSettlementTokens);

            var changedRoles = new List<Role>();
            var replacements = new List<List<string>>();
            for (int i = 0; i < store.roles.Count; i++)
            {
                Role role = store.roles[i];
                if (role == null) continue;
                List<string> normalized = LocationTokenMigration.Normalize(
                    role.locationTokens, stableShipToken,
                    liveSettlementTokens);
                if (SameLocationTokens(role.locationTokens, normalized))
                    continue;
                changedRoles.Add(role);
                replacements.Add(normalized);
            }

            using (var uiBatch = new UiInvalidationBatch(UiVersion.Bump))
                for (int i = 0; i < changedRoles.Count; i++)
                {
                    Role role = changedRoles[i];
                    role.locationTokens = replacements[i];
                    CompiledJobOrders.InvalidateRole(
                        role.id, uiBatch.Request);
                }

            PawnLocationTracker.RefreshManagedLocations();
            store.LocationTokenSchemaVersion =
                RoleStore.CurrentLocationTokenSchemaVersion;
        }

        private static bool SameLocationTokens(
            IReadOnlyList<string> left, IReadOnlyList<string> right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left == null || right == null || left.Count != right.Count)
                return false;
            for (int i = 0; i < left.Count; i++)
                if (left[i] != right[i]) return false;
            return true;
        }

        /// Refused for composite members: rules on a member would silently gate
        /// every bundle carrying it (the composite's own rules do that instead).
        [SyncMethod]
        public static void SetRoleActiveHours(int roleId, int hoursMask)
        {
            var role = FindRole(roleId);
            if (role == null || role.activeHours == hoursMask
                || Store.IsCompositeMember(roleId)) return;
            role.activeHours = hoursMask;
            CompiledJobOrders.InvalidateRole(roleId);
            ReconcileHolders(roleId);
        }

        /// Adds/removes one location token; the role is active wherever any of
        /// its tokens match (none = anywhere). Refused for composite members,
        /// like SetRoleActiveHours.
        [SyncMethod]
        public static void ToggleRoleLocation(int roleId, string token)
        {
            var role = FindRole(roleId);
            if (role == null || token.NullOrEmpty()
                || Store.IsCompositeMember(roleId)) return;
            if (!LocationTokenSelection.Toggle(role.locationTokens, token))
                return;
            CompiledJobOrders.InvalidateRole(roleId);
            ReconcileHolders(roleId);
        }

        [SyncMethod]
        public static void ClearRoleLocations(int roleId)
        {
            var role = FindRole(roleId);
            if (role == null || role.locationTokens.Count == 0) return;
            role.locationTokens.Clear();
            CompiledJobOrders.InvalidateRole(roleId);
            ReconcileHolders(roleId);
        }

        /// Restricts a bill to workers actively holding the role (-1 clears).
        [SyncMethod]
        public static void SetBillRole(Bill bill, int roleId)
        {
            if (Store == null || bill == null) return;
            Store.SetBillRole(bill, roleId);
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
            // Activation-only, but newly active work can demote holders'
            // in-flight jobs below it.
            ReconcileHolders(roleId);
        }

        // ----- Role content -----

        [SyncMethod]
        public static void AddEntry(int roleId, JobEntry entry, int index = -1)
        {
            var role = FindRole(roleId);
            if (role == null || role.composite) return;
            // UI checks run before the synced command lands, so duplicates can
            // still race in (two MP clients adding the same entry).
            if (role.entries.Contains(entry)) return;
            if (index < 0 || index > role.entries.Count) index = role.entries.Count;
            role.entries.Insert(index, entry);
            CompiledJobOrders.InvalidateRole(roleId);
            ReconcileHolders(roleId);
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
            if (role == null || index < 0 || index >= role.entries.Count) return;
            role.entries.RemoveAt(index);
            CompiledJobOrders.InvalidateRole(roleId);
            ReconcileHolders(roleId);
        }

        [SyncMethod]
        public static void MoveEntry(int roleId, int from, int to)
        {
            var role = FindRole(roleId);
            if (role == null || from < 0 || from >= role.entries.Count
                || to < 0 || to >= role.entries.Count || from == to) return;
            var entry = role.entries[from];
            role.entries.RemoveAt(from);
            role.entries.Insert(to, entry);
            CompiledJobOrders.InvalidateRole(roleId);
            ReconcileHolders(roleId);
        }

        // ----- Composite members -----

        /// Membership follows the CompositeRoles policy. UI checks run before
        /// the synced command lands, so stale adds re-validate here.
        [SyncMethod]
        public static void AddCompositeMember(int roleId, int memberId, int index = -1)
        {
            var role = FindRole(roleId);
            if (role == null || !role.composite
                || role.memberRoleIds.Contains(memberId)
                || !CompositeRoles.IsEligibleMember(roleId, memberId,
                    Store.CompositeMemberFactsOf(memberId))) return;
            if (index < 0 || index > role.memberRoleIds.Count)
                index = role.memberRoleIds.Count;
            role.memberRoleIds.Insert(index, memberId);
            CompiledJobOrders.InvalidateRole(roleId);
            ReconcileHolders(roleId);
        }

        [SyncMethod]
        public static void RemoveCompositeMember(int roleId, int index)
        {
            var role = FindRole(roleId);
            if (role == null || !role.composite
                || index < 0 || index >= role.memberRoleIds.Count) return;
            role.memberRoleIds.RemoveAt(index);
            CompiledJobOrders.InvalidateRole(roleId);
            ReconcileHolders(roleId);
        }

        [SyncMethod]
        public static void MoveCompositeMember(int roleId, int from, int to)
        {
            var role = FindRole(roleId);
            if (role == null || !role.composite
                || from < 0 || from >= role.memberRoleIds.Count
                || to < 0 || to >= role.memberRoleIds.Count || from == to) return;
            int memberId = role.memberRoleIds[from];
            role.memberRoleIds.RemoveAt(from);
            role.memberRoleIds.Insert(to, memberId);
            CompiledJobOrders.InvalidateRole(roleId);
            ReconcileHolders(roleId);
        }

        // ----- Pawn assignments -----

        [SyncMethod]
        public static void AssignRole(Pawn pawn, int roleId, int index = -1)
        {
            if (!AssignRoleDirect(pawn, roleId, index)) return;
            // Assigning a blocker role can veto the pawn's current job; the
            // engine path stays interruption-free (load-time seeding).
            CompiledJobOrders.EnqueueReconcile(pawn);
        }

        /// Engine-initiated path (coverage generation): creates a role without going through
        /// sync interception — runs deterministically on every client at load time.
        internal static Role CreateRoleDirect(string label, bool autoAssign = false)
        {
            if (Store == null) return null;
            label = CatalogNameRules.Unique(label, Store.roles, existing => existing.label);
            if (label == null) return null;
            var role = new Role { id = Store.NextId(), label = label, autoAssign = autoAssign };
            Store.roles.Add(role);
            Store.InvalidateRoleIndex();
            return role;
        }

        internal static void AddEntryDirect(int roleId, JobEntry entry, int index = -1)
        {
            var role = FindRole(roleId);
            if (role == null || role.composite || role.entries.Contains(entry)) return;
            if (index < 0 || index > role.entries.Count) index = role.entries.Count;
            role.entries.Insert(index, entry);
            CompiledJobOrders.InvalidateRole(roleId);
        }

        /// Engine-initiated path (seeding, joiner auto-assign): runs inside the synced
        /// simulation on every client, so it must NOT go through sync interception.
        internal static bool AssignRoleDirect(Pawn pawn, int roleId,
            int index = -1)
        {
            if (Store == null || pawn == null || Store.RoleById(roleId) == null)
                return false;
            bool wasManaged = Store.IsManaged(pawn);
            var set = Store.SetFor(pawn);
            if (set.assignments.Any(a => a.roleId == roleId)) return false;
            if (index < 0 || index > set.assignments.Count) index = set.assignments.Count;
            set.assignments.Insert(index, new RoleAssignment { roleId = roleId });
            if (!wasManaged) PawnLocationTracker.NotifyManaged(pawn);
            CompiledJobOrders.Invalidate(pawn);
            return true;
        }

        [SyncMethod]
        public static void RemoveRoleFromPawn(Pawn pawn, int roleId)
        {
            // TryGetValue, not SetFor: a removal against an unmanaged pawn must not
            // create (and scribe) an empty set for it.
            if (Store == null || pawn == null || !Store.pawnSets.TryGetValue(pawn, out var set)) return;
            int assignmentIndex = set.assignments.FindIndex(
                assignment => assignment.roleId == roleId);
            if (assignmentIndex < 0) return;
            if (set.assignments.Count > 0 && set.assignments.TrueForAll(a => a.roleId == roleId))
            {
                Store.UnmanagePawn(pawn);
                return;
            }
            set.assignments.RemoveAll(a => a.roleId == roleId);
            if (set.assignments.Count == 0)
            {
                Store.UnmanagePawn(pawn);
                return;
            }
            CompiledJobOrders.Invalidate(pawn);
            CompiledJobOrders.EnqueueReconcile(pawn);
        }

        [SyncMethod]
        public static void MoveRoleOnPawn(Pawn pawn, int from, int to)
        {
            if (Store == null || pawn == null || !Store.pawnSets.TryGetValue(pawn, out var set)) return;
            if (from < 0 || from >= set.assignments.Count || to < 0
                || to >= set.assignments.Count || from == to) return;
            var assignment = set.assignments[from];
            set.assignments.RemoveAt(from);
            set.assignments.Insert(to, assignment);
            CompiledJobOrders.Invalidate(pawn);
            CompiledJobOrders.EnqueueReconcile(pawn);
        }

        [SyncMethod]
        public static void CycleRoleForPawn(Pawn pawn, int roleId)
        {
            if (Store == null || pawn == null || !Store.pawnSets.TryGetValue(pawn, out var set)) return;
            var assignment = set.assignments.FirstOrDefault(a => a.roleId == roleId);
            if (assignment == null) return;
            // Fully independent of the global toggle: advances only this pawn's
            // state and never touches the role or other holders.
            assignment.state = RoleActivation.Next(assignment.state);
            CompiledJobOrders.Invalidate(pawn);
            CompiledJobOrders.EnqueueReconcile(pawn);
        }

        /// Restores a specific state (int for sync-primitive safety), e.g. after
        /// a drag-move re-creates the assignment on the target pawn.
        [SyncMethod]
        public static void SetAssignmentState(Pawn pawn, int roleId, int state)
        {
            if (state < (int)AssignmentState.Enabled
                || state > (int)AssignmentState.ForceOn) return;
            if (Store == null || pawn == null || !Store.pawnSets.TryGetValue(pawn, out var set)) return;
            var assignment = set.assignments.FirstOrDefault(a => a.roleId == roleId);
            if (assignment == null || assignment.state == (AssignmentState)state) return;
            assignment.state = (AssignmentState)state;
            CompiledJobOrders.Invalidate(pawn);
            CompiledJobOrders.EnqueueReconcile(pawn);
        }

        /// Pinned assignments are the player's placement: fixes never touch them.
        [SyncMethod]
        public static void ToggleAssignmentPin(Pawn pawn, int roleId)
        {
            if (Store == null || pawn == null || !Store.pawnSets.TryGetValue(pawn, out var set)) return;
            var assignment = set.assignments.FirstOrDefault(a => a.roleId == roleId);
            if (assignment == null) return;
            assignment.pinned = !assignment.pinned;
            // Pins shape the plan and the chip's marker width.
            UiVersion.Bump();
        }

        [SyncMethod]
        public static void PasteRoleSet(Pawn pawn, List<RoleAssignment> source)
        {
            if (Store == null || pawn == null || source == null) return;
            var assignments = ClipboardRules.FilterValidDistinct(
                source,
                assignment => assignment?.roleId,
                Store.roles.Select(role => role.id),
                assignment => new RoleAssignment
                {
                    roleId = assignment.roleId,
                    state = assignment.state,
                    pinned = assignment.pinned
                });
            // Pasting an empty set unmanages the pawn — never store an empty set.
            if (assignments.Count == 0)
            {
                if (!Store.IsManaged(pawn)) return;
                Store.UnmanagePawn(pawn);
                return;
            }
            bool wasManaged = Store.IsManaged(pawn);
            if (wasManaged && Store.pawnSets.TryGetValue(pawn,
                    out PawnRoleSet existing)
                && AssignmentSequencesEqual(existing.assignments, assignments))
                return;
            Store.SetFor(pawn).assignments = assignments;
            if (!wasManaged) PawnLocationTracker.NotifyManaged(pawn);
            CompiledJobOrders.Invalidate(pawn);
            CompiledJobOrders.EnqueueReconcile(pawn);
        }

        private static bool AssignmentSequencesEqual(
            IReadOnlyList<RoleAssignment> left,
            IReadOnlyList<RoleAssignment> right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left == null || right == null || left.Count != right.Count)
                return false;
            for (int i = 0; i < left.Count; i++)
            {
                RoleAssignment a = left[i];
                RoleAssignment b = right[i];
                if (a == null || b == null)
                {
                    if (!ReferenceEquals(a, b)) return false;
                    continue;
                }
                if (a.roleId != b.roleId || a.state != b.state
                    || a.pinned != b.pinned) return false;
            }
            return true;
        }
    }
}
