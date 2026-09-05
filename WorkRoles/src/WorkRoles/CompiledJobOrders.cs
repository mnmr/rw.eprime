using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimShared.Common;
using RimWorld;
using Verse;
using Verse.AI;
using WorkRoles.Core;

namespace WorkRoles
{
    public static class CompiledJobOrders
    {
        private const string BasicsTemplate = "WS_Basics";

        private sealed class Entry
        {
            public List<WorkGiver> Normal = null!;    // set by Build
            public List<WorkGiver> Emergency = null!; // set by Build
            /// Role ids whose assignment, global toggle and runtime rules all
            /// passed when this pawn snapshot was compiled.
            public int[] ActiveRoleIds = null!;
            /// giver defName -> role id whose claim ranked it (first-claim-wins).
            public Dictionary<string, int> GiverRoleIds = null!;
            /// Flat def-index priorities: the GetPriority prefix
            /// runs thousands of times per second — array reads, no hashing.
            public int[] PriorityByIndex = null!;
            public int[] VanillaByIndex = null!;
        }

        private sealed class ProjectionDefinitionCache
        {
            public WorkTypeDef[] AllWorkTypes = null!;                 // set by ProjectionDefinitions
            public VanillaProjectionDefinitionMetadata Metadata = null!;
        }

        private static readonly ExplicitProjectionCache<Pawn, Entry> cache =
            new ExplicitProjectionCache<Pawn, Entry>(
                Build, SyncVanillaFallback,
                ReferenceIdentityComparer<Pawn>.Instance);
        /// Pawns evicted by patches that run mid-operation (location-rule
        /// transitions): job interruption is unsafe there, so it is deferred
        /// to the next game-component tick.
        private static readonly ContextualDrainQueue<Pawn> pendingReconciles =
            new ContextualDrainQueue<Pawn>(ReferenceIdentityComparer<Pawn>.Instance);
        private static readonly Func<Pawn, string, bool> PawnCanDoJob = CanPawnDoJob;
        private static readonly Func<Pawn, bool> PawnIsManaged =
            pawn => RoleStore.Current?.IsManaged(pawn) == true;
        private static ProjectionDefinitionCache? projectionDefinitions;
        private static VanillaProjectionMetadata? projectionMetadata;
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

        /// A map changed settlement/gravship classification without its pawns
        /// moving. Only pawns holding location-ruled roles need recompilation,
        /// but the open window snapshot also needs to see the new map category.
        internal static void InvalidateLocationRules(Map map)
        {
            if (map == null) return;

            var store = RoleStore.Current;
            if (store?.pawnSets != null)
                foreach (var pair in store.pawnSets)
                {
                    var pawn = pair.Key;
                    var assignments = pair.Value?.assignments;
                    if (pawn?.MapHeld != map || assignments == null) continue;

                    for (int i = 0; i < assignments.Count; i++)
                    {
                        var assignment = assignments[i];
                        var role = assignment == null
                            ? null : store.RoleById(assignment.roleId);
                        if (role?.locationTokens == null
                            || role.locationTokens.Count == 0) continue;
                        cache.Remove(pawn);
                        pendingReconciles.Enqueue(pawn);
                        break;
                    }
                }

            UiVersion.Bump();
        }

        /// Defers a reconcile to the next game-component tick (deterministic,
        /// fully initialized, and only while the game is running).
        internal static void EnqueueReconcile(Pawn pawn)
        {
            pendingReconciles.Enqueue(pawn);
        }

        internal static void EnqueueReconciles(IEnumerable<Pawn> pawns)
        {
            if (pawns == null) return;
            foreach (var pawn in pawns) pendingReconciles.Enqueue(pawn);
        }

        internal static void DrainPendingReconciles(Map map)
            => DrainPendingReconciles(pawn => pawn.MapHeld == map);

        internal static void DrainWorldPendingReconciles()
            => DrainPendingReconciles(pawn => pawn.MapHeld == null);

        private static void DrainPendingReconciles(Func<Pawn, bool> belongsToContext)
        {
            if (pendingReconciles.Count == 0) return;
            var pawns = pendingReconciles.Drain(belongsToContext);
            ReconcileAll(pawns);
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
            // A member edit changes every composite bundling the role: their
            // coverage union and their holders' compiled orders (depth 1 by
            // policy, so one scan is complete).
            foreach (var composited in store.CompositesContaining(roleId))
            {
                composited.InvalidateCoverage();
                foreach (var pawn in store.PawnsWithRole(composited.id).ToList())
                    cache.Remove(pawn);
            }
        }

        private static bool IsBasicsRole(int roleId, Role? role) =>
            role?.templateDefName == BasicsTemplate || roleId == projectionBasicsRoleId;

        private static void InvalidateBasics(Role? role, Action invalidateUi)
        {
            role?.InvalidateCoverage();
            // The full cache clear below covers compiled orders; composites
            // bundling Basics still need their coverage union refreshed.
            var store = RoleStore.Current;
            if (role != null && store != null)
                foreach (var composited in store.CompositesContaining(role.id))
                    composited.InvalidateCoverage();
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

        /// World teardown must release pawn-keyed compiled snapshots. Definition
        /// metadata is process-wide and contains no world or pawn references.
        internal static void ReleaseForTeardown()
        {
            cache.Clear();
            pendingReconciles.Clear();
            InvalidateProjectionMetadata();
        }

        /// Recompile every pawn holding a role with a time rule (hour boundary crossed).
        internal static void InvalidateTimeRuledForMap(Map map)
        {
            if (map != null)
                InvalidateTimeRuled(pawn => pawn.MapHeld == map);
        }

        internal static void InvalidateWorldTimeRuled()
            => InvalidateTimeRuled(pawn => pawn.MapHeld == null);

        private static void InvalidateTimeRuled(Func<Pawn, bool> belongsToContext)
        {
            var plan = PlanTimeRuled(belongsToContext);
            if (plan == null) return;
            plan.ApplyRuntime(pawn => cache.Remove(pawn), UiVersion.Bump);
            // Plan order is deterministic (thingIDNumber), so the interruption
            // cascade (new job searches, reservations) matches across MP clients.
            for (int i = 0; i < plan.Pawns.Count; i++)
                ReconcileInFlightWork(plan.Pawns[i]);
        }

        /// A live map crossed a timezone meridian (dev map moves, modded moving
        /// map parents): aboard holders' local hours jumped mid-interval. Runs
        /// inside the tile setter, so reconciles defer to the map component tick.
        internal static void InvalidateTimeRuledForMovedMap(Map map)
        {
            if (map == null) return;
            var plan = PlanTimeRuled(pawn => pawn.MapHeld == map);
            if (plan == null) return;
            plan.ApplyRuntime(pawn =>
            {
                cache.Remove(pawn);
                pendingReconciles.Enqueue(pawn);
            }, UiVersion.Bump);
        }

        private static TimedRoleInvalidationPlan<Pawn>? PlanTimeRuled(
            Func<Pawn, bool> belongsToContext)
        {
            var store = RoleStore.Current;
            if (store?.roles == null) return null;

            List<TimedRoleInvalidationSource>? roleSources = null;
            for (int i = 0; i < store.roles.Count; i++)
            {
                var role = store.roles[i];
                if (role == null) continue;
                if (role.activeHours != Role.AllHours)
                {
                    if (roleSources == null)
                        roleSources = new List<TimedRoleInvalidationSource>();
                    roleSources.Add(new TimedRoleInvalidationSource(role.id,
                        hasTimeRule: true, role.enabled, role.blocker, role.autoAssign));
                }
            }
            if (roleSources == null) return null;

            var pawnSets = store.pawnSets;
            IEnumerable<TimedRoleHolderAssignment<Pawn>> AssignmentSources()
            {
                if (pawnSets == null) yield break;
                foreach (var pair in pawnSets)
                {
                    var pawn = pair.Key;
                    var set = pair.Value;
                    if (pawn == null || !belongsToContext(pawn)
                        || set?.assignments == null) continue;

                    foreach (var assignment in set.assignments)
                    {
                        if (assignment == null) continue;
                        yield return new TimedRoleHolderAssignment<Pawn>(pawn,
                            pawn.thingIDNumber, assignment.roleId,
                            assignment.state != AssignmentState.Disabled,
                            assignment.pinned);
                    }
                }
            }

            return TimedRoleInvalidationPlanner.Plan(
                roleSources, AssignmentSources());
        }

        /// Deterministic wrapper for callers holding an unordered pawn set:
        /// interruption cascades into job searches and reservations, so MP
        /// clients must process pawns identically.
        internal static void ReconcileAll(IEnumerable<Pawn> pawns)
        {
            if (pawns == null) return;
            var ordered = pawns
                .Where(pawn => pawn != null)
                .Distinct(ReferenceIdentityComparer<Pawn>.Instance)
                .OrderBy(pawn => pawn.thingIDNumber)
                .ToList();
            for (int i = 0; i < ordered.Count; i++)
                ReconcileInFlightWork(ordered[i]);
        }

        /// Ends in-flight jobs whose work type lost authority (rank 0) or
        /// standing (worse rank than when the job was issued) after a role-state
        /// change. Synced contexts only (commands, ticks): the rebuild this
        /// forces, and the interruptions, must not originate from one client's UI.
        internal static void ReconcileInFlightWork(Pawn pawn)
        {
            if (pawn == null || RoleStore.Current?.IsManaged(pawn) != true) return;
            var jobs = pawn.jobs;
            if (jobs == null) return;

            var current = jobs.curJob;
            var currentType = current?.workGiverDef?.workType;

            // Revoked types across the current and queued jobs: vanilla's
            // notify scrubs the queue and ends the current job unless
            // player-forced. PriorityFor forces the rebuild first, so the job
            // search triggered by an interruption sees the new giver lists.
            List<WorkTypeDef>? revoked = null;
            void CollectRevoked(WorkTypeDef? workType)
            {
                if (workType == null || PriorityFor(pawn, workType) != 0) return;
                revoked = revoked ?? new List<WorkTypeDef>();
                if (!revoked.Contains(workType)) revoked.Add(workType);
            }
            CollectRevoked(currentType);
            if (jobs.jobQueue != null)
                foreach (var queued in jobs.jobQueue)
                    CollectRevoked(queued?.job?.workGiverDef?.workType);
            if (revoked != null)
                for (int i = 0; i < revoked.Count; i++)
                    jobs.Notify_WorkTypeDisabled(revoked[i]);

            // Demoted current job: the type stayed active but ranks worse than
            // at issue (its claimant deactivated, or new work entered above).
            if (currentType == null || current!.playerForced) return; // currentType != null implies current != null
            if (jobs.curJob != current) return;
            int rank = PriorityFor(pawn, currentType);
            if (rank == 0) return;
            if (JobRankBaseline.TryGetRank(pawn, current, out int issueRank)
                && rank > issueRank)
                jobs.EndCurrentJob(JobCondition.InterruptForced);
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

        /// One cache probe on the steady managed path. A miss proves ownership
        /// before publishing, so unmanaged pawns keep vanilla authority and do
        /// not acquire empty compiled entries.
        internal static bool TryPriorityForManaged(Pawn pawn, WorkTypeDef workType,
            bool vanillaProjection, out int priority)
        {
            priority = 0;
            if (workType == null
                || !cache.TryGetManaged(pawn, PawnIsManaged, out Entry? entry))
                return false;
            int[] byIndex = vanillaProjection
                ? entry!.VanillaByIndex
                : entry!.PriorityByIndex;
            int index = workType.index;
            priority = (uint)index < (uint)byIndex.Length ? byIndex[index] : 0;
            return true;
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

        /// The role whose claim ranked this giver for the pawn (first-claim-wins).
        internal static bool TryGetClaimingRole(Pawn pawn, string giverDefName, out int roleId)
        {
            roleId = -1;
            return giverDefName != null
                && For(pawn).GiverRoleIds.TryGetValue(giverDefName, out roleId);
        }

        /// Work-type-level attribution for jobs that carry no giver (non-scan
        /// givers): the claimant of the type's highest-priority claimed giver.
        internal static bool TryGetClaimingRoleForWorkType(
            Pawn pawn, string workTypeDefName, out int roleId)
        {
            roleId = -1;
            if (workTypeDefName == null) return false;
            var map = For(pawn).GiverRoleIds;
            var givers = GameJobCatalog.Instance.WorkGiversOf(workTypeDefName);
            for (int i = 0; i < givers.Count; i++)
                if (map.TryGetValue(givers[i], out roleId))
                    return true;
            return false;
        }

        internal static bool IsRoleActive(Pawn pawn, int roleId)
        {
            var activeRoleIds = For(pawn).ActiveRoleIds;
            for (int i = 0; i < activeRoleIds.Length; i++)
                if (activeRoleIds[i] == roleId)
                    return true;
            return false;
        }

        private static Entry For(Pawn pawn) => cache.GetOrBuild(pawn);

        /// Ensures the pawn's compiled order (and its vanilla fallback map) is current.
        public static void EnsureFresh(Pawn pawn) => For(pawn);

        /// Rebuilds even when an entry is cached, guaranteeing the fallback map
        /// reflects the role set that is about to relinquish authority.
        internal static void MirrorFreshVanillaFallback(Pawn pawn)
        {
            cache.PublishFresh(pawn);
        }

        /// Cache-only eviction for lifecycle code that owns its own UI bump.
        internal static void RemoveCached(Pawn pawn) => cache.Remove(pawn);

        private static AccessTools.FieldRef<Pawn_WorkSettings, DefMap<WorkTypeDef, int>>?
            vanillaPriorities;
        private static bool vanillaPrioritiesResolved;

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
            if (!TryGetVanillaPriorities(workSettings, out var map)) return;
            if (map == null) return;
            foreach (var workType in ProjectionDefinitions().AllWorkTypes)
            {
                int index = workType.index;
                map[workType] = (uint)index < (uint)entry.VanillaByIndex.Length
                    ? entry.VanillaByIndex[index] : 0;
            }
        }

        private static bool TryGetVanillaPriorities(
            Pawn_WorkSettings workSettings,
            out DefMap<WorkTypeDef, int>? priorities)
        {
            priorities = null;
            if (!vanillaPrioritiesResolved)
            {
                vanillaPrioritiesResolved = true;
                try
                {
                    var field = AccessTools.Field(
                        typeof(Pawn_WorkSettings), "priorities");
                    if (field == null || field.IsStatic
                        || field.FieldType != typeof(DefMap<WorkTypeDef, int>))
                        throw new MissingFieldException(
                            typeof(Pawn_WorkSettings).FullName,
                            "priorities: DefMap<WorkTypeDef, int>");
                    vanillaPriorities = AccessTools.FieldRefAccess<
                        Pawn_WorkSettings, DefMap<WorkTypeDef, int>>("priorities");
                }
                catch (Exception exception)
                {
                    Log.Warning("[WorkRoles] Vanilla priority mirroring disabled; "
                        + "role scheduling remains active: " + exception.Message);
                }
            }

            if (vanillaPriorities == null) return false;
            try
            {
                priorities = vanillaPriorities(workSettings);
                return true;
            }
            catch (Exception exception)
            {
                vanillaPriorities = null;
                Log.Warning("[WorkRoles] Vanilla priority mirroring disabled after "
                    + "the private priorities field became inaccessible; role "
                    + "scheduling remains active: " + exception.Message);
                return false;
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

        private static List<string> BasicsWorkTypes(Role? basics)
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

            Role? basics = null;
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

        private static readonly List<JobEntry> EmptyEntries = new List<JobEntry>();

        private static Entry Build(Pawn pawn)
        {
            var store = RoleStore.Current;
            var roleEntries = new List<(IReadOnlyList<JobEntry> entries, bool blocker)>();
            List<int>? activeRoleIds = null;
            if (store != null && store.pawnSets.TryGetValue(pawn, out var set))
            {
                foreach (var assignment in set.assignments)
                {
                    var role = store.RoleById(assignment.roleId);
                    if (role == null
                        || !RoleActivation.IsActive(role.enabled, assignment.state)
                        || !RoleRules.Pass(role, pawn)) continue;
                    activeRoleIds ??= new List<int>();
                    if (!role.composite)
                    {
                        activeRoleIds.Add(role.id);
                        roleEntries.Add((JobOrderCompiler.WithMovedSnapshotGivers(
                            role.entries, role.workTypeSnapshots, GameJobCatalog.Instance), role.blocker));
                        continue;
                    }
                    // Composite: one slice per live member, member order, all
                    // attributed to the composite (activeRoleIds stays in
                    // lockstep with the slices for GiverRoleIds). Member
                    // blocker and enabled flags keep their own semantics, so
                    // holding the composite equals holding the members.
                    int slicesBefore = roleEntries.Count;
                    for (int m = 0; m < role.memberRoleIds.Count; m++)
                    {
                        var member = store.RoleById(role.memberRoleIds[m]);
                        if (member == null || member.composite) continue;
                        if (!CompositeRoles.TryGetMemberSlice(role.blocker,
                                member.enabled, member.blocker, out bool sliceBlocker))
                            continue;
                        activeRoleIds.Add(role.id);
                        roleEntries.Add((JobOrderCompiler.WithMovedSnapshotGivers(
                            member.entries, member.workTypeSnapshots, GameJobCatalog.Instance), sliceBlocker));
                    }
                    // No live member: an empty slice keeps the composite in
                    // ActiveRoleIds, matching an empty normal role's behavior.
                    if (roleEntries.Count == slicesBefore)
                    {
                        activeRoleIds.Add(role.id);
                        roleEntries.Add((EmptyEntries, role.blocker));
                    }
                }
            }

            var compiled = JobOrderCompiler.Compile(
                roleEntries, GameJobCatalog.Instance, pawn, PawnCanDoJob);
            var buckets = JobOrderCompiler.ToVanillaPriorities(compiled.WorkTypePriorities,
                ProjectionMetadata());
            if (store?.vanillaEmergencyRule == true)
                JobOrderCompiler.ApplyVanillaEmergencyRule(
                    compiled, GameJobCatalog.Instance, buckets);
            int defCount = DefDatabase<WorkTypeDef>.DefCount;

            var entry = new Entry
            {
                Normal = new List<WorkGiver>(compiled.Normal.Count),
                Emergency = new List<WorkGiver>(compiled.Emergency.Count),
                ActiveRoleIds = activeRoleIds?.ToArray() ?? Array.Empty<int>(),
                GiverRoleIds = new Dictionary<string, int>(compiled.ClaimedBySlice.Count),
                PriorityByIndex = new int[defCount],
                VanillaByIndex = new int[defCount],
            };
            // Slice indexes are positional: activeRoleIds grew in lockstep with
            // the role slices handed to the compiler.
            if (activeRoleIds != null)
                foreach (var claim in compiled.ClaimedBySlice)
                    entry.GiverRoleIds[claim.Key] = activeRoleIds[claim.Value];
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
