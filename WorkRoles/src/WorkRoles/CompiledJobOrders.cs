using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using WorkRoles.Core;

namespace WorkRoles
{
    public static class CompiledJobOrders
    {
        private const string BasicsTemplate = "WS_Basics";

        private sealed class Entry
        {
            public List<WorkGiver> Normal;
            public List<WorkGiver> Emergency;
            /// Flat def-index priorities: the GetPriority prefix
            /// runs thousands of times per second — array reads, no hashing.
            public int[] PriorityByIndex;
            public int[] VanillaByIndex;
        }

        private sealed class ProjectionDefinitionCache
        {
            public WorkTypeDef[] AllWorkTypes;
            public VanillaProjectionDefinitionMetadata Metadata;
        }

        private static readonly Dictionary<Pawn, Entry> cache = new Dictionary<Pawn, Entry>();
        private static readonly Func<Pawn, string, bool> PawnCanDoJob = CanPawnDoJob;
        private static ProjectionDefinitionCache projectionDefinitions;
        private static VanillaProjectionMetadata projectionMetadata;
        private static int basicsRevision;
        private static int projectionMetadataBasicsRevision = -1;
        private static int projectionBasicsRoleId = -1;

        public static void Invalidate(Pawn pawn)
        {
            // Bump only for pawns the UI can show: this runs on EVERY pawn's
            // spawn/despawn/death (animals, raiders included), and each bump
            // tears down the open window's snapshots.
            if (cache.Remove(pawn) || pawn.IsColonist || pawn.IsSlaveOfColony)
                UiVersion.Bump();
        }

        internal static void InvalidateBatch(IEnumerable<Pawn> pawns)
        {
            if (pawns == null) return;

            var unique = new HashSet<Pawn>(ReferenceIdentityComparer<Pawn>.Instance);
            bool invalidateUi = false;
            foreach (var pawn in pawns)
            {
                if (pawn == null || !unique.Add(pawn)) continue;
                bool removed = cache.Remove(pawn);
                if (removed || pawn.IsColonist || pawn.IsSlaveOfColony)
                    invalidateUi = true;
            }

            if (invalidateUi)
                UiVersion.Bump();
        }

        public static void InvalidateRole(int roleId) => InvalidateRole(roleId, UiVersion.Bump);

        internal static void InvalidateRole(int roleId, Action invalidateUi)
        {
            var store = RoleStore.Current;
            var role = store?.RoleById(roleId);
            if (IsBasicsRole(roleId, role))
            {
                InvalidateBasics(role, invalidateUi);
                return;
            }

            invalidateUi();
            if (store == null) { cache.Clear(); return; }
            role?.InvalidateCoverage();
            foreach (var pawn in store.PawnsWithRole(roleId).ToList())
                cache.Remove(pawn);
        }

        private static bool IsBasicsRole(int roleId, Role role) =>
            role?.templateDefName == BasicsTemplate || roleId == projectionBasicsRoleId;

        private static void InvalidateBasics(Role role, Action invalidateUi)
        {
            role?.InvalidateCoverage();
            InvalidateProjectionMetadata();
            cache.Clear();
            invalidateUi();
        }

        private static void InvalidateProjectionMetadata()
        {
            unchecked { basicsRevision++; }
            projectionMetadata = null;
            projectionMetadataBasicsRevision = -1;
            projectionBasicsRoleId = -1;
        }

        internal static void InvalidateDefinitions()
        {
            projectionDefinitions = null;
            InvalidateAll();
        }

        public static void InvalidateAll()
        {
            InvalidateProjectionMetadata();
            UiVersion.Bump();
            cache.Clear();
            var store = RoleStore.Current;
            if (store?.roles != null)
                foreach (var role in store.roles)
                    role?.InvalidateCoverage();
        }

        /// Recompile every pawn holding a role with a time rule (hour boundary crossed).
        public static void InvalidateAllTimeRuled()
        {
            var store = RoleStore.Current;
            if (store?.roles == null) return;

            List<TimedRoleInvalidationSource> roleSources = null;
            Dictionary<int, Role> rolesById = null;
            for (int i = 0; i < store.roles.Count; i++)
            {
                var role = store.roles[i];
                if (role == null) continue;
                if (role.activeHours != Role.AllHours)
                {
                    if (roleSources == null)
                    {
                        roleSources = new List<TimedRoleInvalidationSource>();
                        rolesById = new Dictionary<int, Role>();
                    }
                    roleSources.Add(new TimedRoleInvalidationSource(role.id,
                        hasTimeRule: true, role.enabled, role.blocker, role.autoAssign));
                }
                // RoleById is last-wins for corrupt duplicate ids. Once a timed
                // id has appeared, every possible later winner must be retained.
                if (rolesById != null)
                    rolesById[role.id] = role;
            }
            if (roleSources == null) return;

            var pawnSets = store.pawnSets;
            IEnumerable<TimedRoleHolderAssignment<Pawn>> AssignmentSources()
            {
                if (pawnSets == null) yield break;
                foreach (var pair in pawnSets)
                {
                    var pawn = pair.Key;
                    var set = pair.Value;
                    if (pawn == null || set?.assignments == null) continue;

                    foreach (var assignment in set.assignments)
                    {
                        if (assignment == null) continue;
                        yield return new TimedRoleHolderAssignment<Pawn>(pawn,
                            pawn.thingIDNumber, assignment.roleId,
                            assignment.enabled, assignment.pinned);
                    }
                }
            }

            var plan = TimedRoleInvalidationPlanner.Plan(
                roleSources, AssignmentSources());
            for (int i = 0; i < plan.RoleIds.Count; i++)
                if (rolesById.TryGetValue(plan.RoleIds[i], out var role))
                    role.InvalidateCoverage();
            for (int i = 0; i < plan.Pawns.Count; i++)
            {
                var pawn = plan.Pawns[i];
                cache.Remove(pawn);
            }
            UiVersion.Bump();
        }

        /// Returned lists are owned by the cache — callers must never mutate them.
        public static List<WorkGiver> NormalFor(Pawn pawn) => For(pawn).Normal;
        public static List<WorkGiver> EmergencyFor(Pawn pawn) => For(pawn).Emergency;

        public static int PriorityFor(Pawn pawn, WorkTypeDef workType)
        {
            if (workType == null) return 0;
            var byIndex = For(pawn).PriorityByIndex;
            int index = workType.index;
            return (uint)index < (uint)byIndex.Length ? byIndex[index] : 0;
        }

        /// The rank projected onto vanilla's 0-4 scale, such that vanilla's
        /// replay of the numbers reproduces the internal order where four
        /// numbers suffice (same values as the dormant fallback map).
        public static int VanillaPriorityFor(Pawn pawn, WorkTypeDef workType)
        {
            if (workType == null) return 0;
            var byIndex = For(pawn).VanillaByIndex;
            int index = workType.index;
            return (uint)index < (uint)byIndex.Length ? byIndex[index] : 0;
        }

        private static Entry For(Pawn pawn)
        {
            if (!cache.TryGetValue(pawn, out var entry))
            {
                entry = Build(pawn);
                cache[pawn] = entry;
                SyncVanillaFallback(pawn, entry);
            }
            return entry;
        }

        /// Ensures the pawn's compiled order (and its vanilla fallback map) is current.
        public static void EnsureFresh(Pawn pawn) => For(pawn);

        /// Rebuilds even when an entry is cached, guaranteeing the fallback map
        /// reflects the role set that is about to relinquish authority.
        internal static void MirrorFreshVanillaFallback(Pawn pawn)
        {
            cache.Remove(pawn);
            EnsureFresh(pawn);
        }

        /// Cache-only eviction for lifecycle code that owns its own UI bump.
        internal static void RemoveCached(Pawn pawn) => cache.Remove(pawn);

        private static readonly AccessTools.FieldRef<Pawn_WorkSettings, DefMap<WorkTypeDef, int>> VanillaPriorities =
            AccessTools.FieldRefAccess<Pawn_WorkSettings, DefMap<WorkTypeDef, int>>("priorities");

        /// Mirrors the compiled order into the dormant vanilla priorities map as 0-4
        /// values, so removing the mod leaves the vanilla Work tab in a sane state.
        /// Writes the private DefMap directly: SetPriority is swallowed for managed
        /// pawns and its side effects (job interruption on 0) must not fire here.
        private static void SyncVanillaFallback(Pawn pawn, Entry entry)
        {
            // An unmanaged pawn's real vanilla priorities ARE its work settings —
            // mirroring the (empty) projection over them would zero them.
            if (RoleStore.Current?.IsManaged(pawn) != true) return;
            var workSettings = pawn.workSettings;
            if (workSettings == null) return;
            var map = VanillaPriorities(workSettings);
            if (map == null) return;
            foreach (var workType in ProjectionDefinitions().AllWorkTypes)
            {
                int index = workType.index;
                map[workType] = (uint)index < (uint)entry.VanillaByIndex.Length
                    ? entry.VanillaByIndex[index] : 0;
            }
        }

        private static ProjectionDefinitionCache ProjectionDefinitions()
        {
            if (projectionDefinitions != null) return projectionDefinitions;

            var allDefs = DefDatabase<WorkTypeDef>.AllDefsListForReading;
            var allWorkTypes = new List<WorkTypeDef>(allDefs.Count);
            var sources = new List<VanillaProjectionWorkTypeSource>(allDefs.Count);
            foreach (var workType in allDefs)
            {
                if (workType == null || workType.defName.NullOrEmpty()) continue;
                allWorkTypes.Add(workType);
                bool skilled = !workType.relevantSkills.NullOrEmpty();
                bool research = skilled
                    && workType.relevantSkills.Contains(SkillDefOf.Intellectual);
                sources.Add(new VanillaProjectionWorkTypeSource(
                    workType.defName, skilled, research));
            }

            var priorityOrder = new List<string>();
            foreach (var workType in WorkTypeDefsUtility.WorkTypeDefsInPriorityOrder)
                if (workType != null && !workType.defName.NullOrEmpty())
                    priorityOrder.Add(workType.defName);

            projectionDefinitions = new ProjectionDefinitionCache
            {
                AllWorkTypes = allWorkTypes.ToArray(),
                Metadata = new VanillaProjectionDefinitionMetadata(sources, priorityOrder),
            };
            return projectionDefinitions;
        }

        private static List<string> BasicsWorkTypes(Role basics)
        {
            var result = new List<string>();
            if (basics?.entries == null) return result;
            foreach (var entry in basics.entries)
            {
                string type = entry.Kind == JobEntryKind.WorkType
                    ? entry.DefName
                    : GameJobCatalog.Instance.WorkTypeOf(entry.DefName);
                if (type != null) result.Add(type);
            }
            return result;
        }

        private static VanillaProjectionMetadata ProjectionMetadata()
        {
            if (projectionMetadata != null
                && projectionMetadataBasicsRevision == basicsRevision)
                return projectionMetadata;

            Role basics = null;
            var roles = RoleStore.Current?.roles;
            if (roles != null)
                for (int i = 0; i < roles.Count; i++)
                {
                    var candidate = roles[i];
                    if (candidate?.templateDefName != BasicsTemplate) continue;
                    basics = candidate;
                    break;
                }

            var definitions = ProjectionDefinitions();
            projectionMetadata = definitions.Metadata.WithBasics(
                BasicsWorkTypes(basics));
            projectionMetadataBasicsRevision = basicsRevision;
            projectionBasicsRoleId = basics?.id ?? -1;
            return projectionMetadata;
        }

        internal static void WarmProjectionMetadata() => ProjectionMetadata();

        private static bool CanPawnDoJob(Pawn pawn, string giverDefName)
        {
            var def = GameJobCatalog.Instance.GiverDef(giverDefName);
            return def != null
                && !pawn.WorkTypeIsDisabled(def.workType)
                && !pawn.WorkTagIsDisabled(def.workTags);
        }

        private static Entry Build(Pawn pawn)
        {
            var store = RoleStore.Current;
            var roleEntries = new List<(IReadOnlyList<JobEntry> entries, bool blocker)>();
            if (store != null && store.pawnSets.TryGetValue(pawn, out var set))
            {
                foreach (var assignment in set.assignments)
                {
                    if (!assignment.enabled) continue;
                    var role = store.RoleById(assignment.roleId);
                    if (role != null && role.enabled && RoleRules.Pass(role, pawn))
                        roleEntries.Add((JobOrderCompiler.WithMovedSnapshotGivers(
                            role.entries, role.workTypeSnapshots, GameJobCatalog.Instance), role.blocker));
                }
            }

            var compiled = JobOrderCompiler.Compile(
                roleEntries, GameJobCatalog.Instance, pawn, PawnCanDoJob);
            var buckets = JobOrderCompiler.ToVanillaPriorities(compiled.WorkTypePriorities,
                ProjectionMetadata());
            int defCount = DefDatabase<WorkTypeDef>.DefCount;

            var entry = new Entry
            {
                Normal = new List<WorkGiver>(compiled.Normal.Count),
                Emergency = new List<WorkGiver>(compiled.Emergency.Count),
                PriorityByIndex = new int[defCount],
                VanillaByIndex = new int[defCount],
            };
            foreach (string giver in compiled.Normal)
                entry.Normal.Add(GameJobCatalog.Instance.GiverDef(giver).Worker);
            foreach (string giver in compiled.Emergency)
                entry.Emergency.Add(GameJobCatalog.Instance.GiverDef(giver).Worker);
            foreach (var pair in compiled.WorkTypePriorities)
            {
                int index = DefDatabase<WorkTypeDef>.GetNamed(pair.Key).index;
                if ((uint)index < (uint)defCount)
                    entry.PriorityByIndex[index] = pair.Value;
            }
            foreach (var pair in buckets)
            {
                int index = DefDatabase<WorkTypeDef>.GetNamed(pair.Key).index;
                if ((uint)index < (uint)defCount)
                    entry.VanillaByIndex[index] = pair.Value;
            }
            return entry;
        }
    }
}
