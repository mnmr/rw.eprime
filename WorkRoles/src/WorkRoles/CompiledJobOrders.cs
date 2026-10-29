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
            // Misc. Robots has a single work node. Built with the same entry
            // and invalidation contract; emergency work precedes normal work.
            public List<WorkGiver> Combined = null!;
            /// Role ids whose assignment, global toggle and runtime rules all
            /// passed when this pawn snapshot was compiled.
            public int[] ActiveRoleIds = null!;
            /// giver defName -> role id whose claim ranked it (first-claim-wins).
            public Dictionary<string, int> GiverRoleIds = null!;
            /// Flat def-index priorities: the GetPriority prefix
            /// runs thousands of times per second — array reads, no hashing.
            public int[] PriorityByIndex = null!;
            public int[] VanillaByIndex = null!;
            // Cache metadata, not published order data: the last local hour
            // whose rule outcomes this entry represents. Equal rule results
            // advance the observation without replacing the order artifact.
            public int RuleHour;
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
        private static readonly Func<Pawn, Map?, bool> PawnBelongsToMap =
            (pawn, map) => ColonyScope.MapOf(pawn) == map;
        private static readonly Func<Pawn, Pawn, bool> SamePawn = (left, right) => ReferenceEquals(left, right);
        private static readonly Action<Pawn> PublishRuntime = UiVersion.RuntimeChanged;
        private static readonly Action<Pawn> InvalidateTimedOrders = pawn =>
        {
            cache.Remove(pawn);
            pendingReconciles.Enqueue(pawn);
        };
        private static ProjectionDefinitionCache? projectionDefinitions;
        private static VanillaProjectionMetadata? projectionMetadata;
        private static int basicsRevision;
        private static int projectionMetadataBasicsRevision = -1;
        private static int projectionBasicsRoleId = -1;

        public static void Invalidate(Pawn pawn)
        {
            if (cache.Remove(pawn) || pawn.IsColonist || pawn.IsSlaveOfColony)
                UiVersion.RuntimeChanged(pawn);
        }

        internal static void InvalidateAssignments(Pawn pawn, bool planChanged)
        {
            cache.Remove(pawn);
            using var batch = RoleStore.Current?.Revisions.Batch();
            UiVersion.PawnChanged(pawn, planChanged);
            UiVersion.RuntimeChanged(pawn);
        }

        internal static void InvalidateBatch(IEnumerable<Pawn> pawns)
        {
            if (pawns == null) return;
            using var batch = RoleStore.Current?.Revisions.Batch();
            foreach (var pawn in pawns)
                if (pawn != null) Invalidate(pawn);
        }

        /// A map changed settlement/gravship classification without its pawns
        /// moving. Only pawns holding location-ruled roles need recompilation,
        /// but the open window snapshot also needs to see the new map category.
        internal static void InvalidateLocationRules(Map map)
        {
            if (map == null) return;

            var store = RoleStore.Current;
            using var batch = store?.Revisions.Batch();
            if (store?.pawnSets != null)
                foreach (var pair in store.pawnSets)
                {
                    var pawn = pair.Key;
                    var assignments = pair.Value?.assignments;
                    if (pawn == null || ColonyScope.MapOf(pawn) != map || assignments == null) continue;

                    for (int i = 0; i < assignments.Count; i++)
                    {
                        var assignment = assignments[i];
                        var role = assignment == null
                            ? null : store.RoleById(assignment.roleId);
                        if (role?.locationTokens == null
                            || role.locationTokens.Count == 0) continue;
                        cache.Remove(pawn);
                        UiVersion.RuntimeChanged(pawn);
                        pendingReconciles.Enqueue(pawn);
                        break;
                    }
                }

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
            => DrainContextPendingReconciles(map);

        internal static void DrainWorldPendingReconciles()
            => DrainContextPendingReconciles(null);

        private static void DrainContextPendingReconciles(Map? map)
        {
            if (pendingReconciles.Count == 0) return;
            var pawns = pendingReconciles.Drain(map, PawnBelongsToMap);
            ReconcileAll(pawns);
        }

        public static void InvalidateRole(int roleId) =>
            InvalidateRole(roleId, ModelDomain.RoleWork | ModelDomain.RoleCapability);

        internal static void InvalidateRole(int roleId, ModelDomain changes,
            bool memberBlockerChanged = false, bool memberBlockerOnlyCompilationChange = false)
        {
            if (changes == ModelDomain.RoleActivation)
            {
                InvalidateRoleActivation(roleId);
                return;
            }
            var store = RoleStore.Current;
            if (store == null) { cache.Clear(); return; }
            using var batch = store.Revisions.Batch();
            var role = store.RoleById(roleId);
            bool workChanged = (changes & ModelDomain.RoleWork) != 0;
            if (workChanged)
            {
                role?.InvalidateCoverage();
                changes |= ModelDomain.RoleCapability;
                if (IsBasicsRole(roleId, role))
                {
                    InvalidateProjectionMetadata();
                    cache.Clear();
                    foreach (var pawn in store.pawnSets.Keys) UiVersion.RuntimeChanged(pawn);
                }
            }
            UiVersion.RoleChanged(roleId, changes);
            foreach (var pawn in store.PawnsWithRole(roleId)) Invalidate(pawn);
            // Member activation affects compiled slices, but never coverage or
            // work specifications; only work/capability changes propagate those.
            foreach (var composite in store.CompositesContaining(roleId))
            {
                ModelDomain dependent = RoleRevisionDependencies.ForComposite(changes,
                    composite.blocker, role?.blocker == true, memberBlockerChanged);
                if ((dependent & ModelDomain.RoleWork) != 0) composite.InvalidateCoverage();
                UiVersion.RoleChanged(composite.id, dependent);
                // Every member of a blocker composite already contributes a
                // veto; changing that member's own veto bit cannot change its
                // holders' compiled orders.
                if (!composite.blocker || !memberBlockerOnlyCompilationChange)
                    foreach (var pawn in store.PawnsWithRole(composite.id)) Invalidate(pawn);
            }
        }

        private static void InvalidateRoleActivation(int roleId)
        {
            var store = RoleStore.Current;
            var role = store?.RoleById(roleId);
            if (store == null || role == null) return;
            using var batch = store.Revisions.Batch();
            UiVersion.RoleChanged(roleId, ModelDomain.RoleActivation);
            foreach (var pawn in store.PawnsWithRole(roleId))
            {
                var assignments = store.pawnSets[pawn].assignments;
                for (int i = 0; i < assignments.Count; i++)
                {
                    var assignment = assignments[i];
                    if (assignment.roleId != roleId || !RoleActivation.ChangesExecution(
                            !role.enabled, assignment.state, role.enabled, assignment.state)) continue;
                    Invalidate(pawn);
                    EnqueueReconcile(pawn);
                    break;
                }
            }
            // A member's enabled bit controls its slice even when the outer
            // composite assignment is ForceOn. Only the outer assignment's
            // effective activation can make that member change irrelevant.
            foreach (var composite in store.CompositesContaining(roleId))
            {
                UiVersion.RoleChanged(composite.id, ModelDomain.RoleActivation);
                foreach (var pawn in store.PawnsWithRole(composite.id))
                {
                    var assignments = store.pawnSets[pawn].assignments;
                    for (int i = 0; i < assignments.Count; i++)
                        if (assignments[i].roleId == composite.id
                            && RoleActivation.IsActive(composite.enabled, assignments[i].state))
                        {
                            Invalidate(pawn);
                            EnqueueReconcile(pawn);
                            break;
                        }
                }
            }
        }

        internal static void InvalidateRoleWorkFacts(int roleId)
        {
            var store = RoleStore.Current;
            if (store == null) return;
            using var batch = store.Revisions.Batch();
            UiVersion.RoleChanged(roleId, ModelDomain.RoleWork | ModelDomain.RoleCapability);
            foreach (var composite in store.CompositesContaining(roleId))
                UiVersion.RoleChanged(composite.id, RoleRevisionDependencies.ForComposite(
                    ModelDomain.RoleWork | ModelDomain.RoleCapability, composite.blocker,
                    store.RoleById(roleId)?.blocker == true, memberBlockerChanged: false));
        }

        private static bool IsBasicsRole(int roleId, Role? role) =>
            role?.templateDefName == BasicsTemplate || roleId == projectionBasicsRoleId;

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

        internal static void InvalidateRuntimeAll()
        {
            cache.Clear();
            var store = RoleStore.Current;
            using var batch = store?.Revisions.Batch();
            if (store != null)
                foreach (var pawn in store.pawnSets.Keys) UiVersion.RuntimeChanged(pawn);
            UiVersion.Changed(ModelDomain.Preferences);
        }

        public static void InvalidateAll()
        {
            InvalidateProjectionMetadata();
            cache.Clear();
            var store = RoleStore.Current;
            if (store == null) return;
            using var batch = store.Revisions.Batch();
            UiVersion.Bump();
            foreach (var role in store.roles)
            {
                if (role == null) continue;
                role.InvalidateCoverage();
                UiVersion.RoleChanged(role.id, ModelDomain.RoleWork | ModelDomain.RoleCapability);
            }
            foreach (var pawn in store.pawnSets.Keys) UiVersion.RuntimeChanged(pawn);
        }

        /// World teardown must release pawn-keyed compiled snapshots. Definition
        /// metadata is process-wide and contains no world or pawn references.
        internal static void ReleaseForTeardown()
        {
            cache.Clear();
            pendingReconciles.Clear();
            InvalidateProjectionMetadata();
        }

        /// At the explicit hour boundary, compare schedule outcomes against
        /// each warm order's observation. This never polls between boundaries.
        internal static void InvalidateTimeRuledForMap(Map map)
        {
            if (map != null) ApplyTimeRuledPlan(PlanTimeRuled(map, null));
        }

        internal static void InvalidateWorldTimeRuled() =>
            ApplyTimeRuledPlan(PlanTimeRuled(null, null));

        internal static void InvalidateTimeRuledForMovedMap(Map map) =>
            InvalidateTimeRuledForMap(map);

        internal static void InvalidateTimeRuledForTravelers(IReadOnlyList<Pawn> pawns) =>
            ApplyTimeRuledPlan(PlanTimeRuled(null, pawns));

        private static void ApplyTimeRuledPlan(TimedRoleInvalidationPlan<Pawn>? plan)
        {
            if (plan == null) return;
            using var batch = RoleStore.Current?.Revisions.Batch();
            plan.ApplyRuntime(PublishRuntime, InvalidateTimedOrders);
        }

        private static TimedRoleInvalidationPlan<Pawn>? PlanTimeRuled(Map? map,
            IReadOnlyList<Pawn>? travelers)
        {
            var store = RoleStore.Current;
            if (store?.roles == null) return null;
            var roleSources = new List<TimedRoleInvalidationSource>();
            for (int i = 0; i < store.roles.Count; i++)
            {
                var role = store.roles[i];
                if (role != null && role.activeHours != Role.AllHours)
                    roleSources.Add(new TimedRoleInvalidationSource(role.id, role.activeHours, role.enabled));
            }
            if (roleSources.Count == 0) return null;
            var assignments = new List<TimedRoleHolderAssignment<Pawn>>();
            if (travelers != null)
            {
                for (int i = 0; i < travelers.Count; i++)
                    if (store.pawnSets.TryGetValue(travelers[i], out var set))
                        AddTimedAssignments(store, travelers[i], set, assignments);
            }
            else
                foreach (var pair in store.pawnSets)
                    if (pair.Key != null && ColonyScope.MapOf(pair.Key) == map)
                        AddTimedAssignments(store, pair.Key, pair.Value, assignments);
            return TimedRoleInvalidationPlanner.Plan(roleSources, assignments);
        }

        private static void AddTimedAssignments(RoleStore store, Pawn pawn, PawnRoleSet set,
            List<TimedRoleHolderAssignment<Pawn>> result)
        {
            if (set?.assignments == null) return;
            int hour = RoleRules.LocalHour(pawn);
            bool cached = cache.TryGetCached(pawn, out Entry? entry);
            int previousHour = cached ? entry!.RuleHour : int.MinValue;
            for (int i = 0; i < set.assignments.Count; i++)
            {
                var assignment = set.assignments[i];
                var role = assignment == null ? null : store.RoleById(assignment.roleId);
                if (role == null || role.activeHours == Role.AllHours) continue;
                bool locationMatches = role.locationTokens.Count == 0
                    || LocationRules.Matches(role.locationTokens, ColonyScope.PlaceOf(pawn));
                result.Add(new TimedRoleHolderAssignment<Pawn>(pawn, pawn.thingIDNumber, role.id,
                    assignment!.state, previousHour, hour, locationMatches));
            }
            // Only the freshness metadata moves. If the plan detects a changed
            // active contribution it evicts this entry before any subsequent read.
            if (cached) entry!.RuleHour = hour;
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
        public static List<WorkGiver> CombinedFor(Pawn pawn) => For(pawn).Combined;
        internal static IReadOnlyList<int> ActiveRolesFor(Pawn pawn) => For(pawn).ActiveRoleIds;
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
        internal static void RemoveCached(Pawn pawn)
        {
            cache.Remove(pawn);
            if (pendingReconciles.Count != 0) pendingReconciles.Drain(pawn, SamePawn);
        }

        private static AccessTools.FieldRef<Pawn_WorkSettings, DefMap<WorkTypeDef, int>>?
            vanillaPriorities;
        private static bool vanillaPrioritiesResolved;

        /// Mirrors the compiled order into the dormant vanilla priorities map as 0-4
        /// values, so removing the mod leaves the vanilla Work tab in a sane state.
        /// Writes the private DefMap directly: SetPriority is swallowed for managed
        /// pawns and its side effects (job interruption on 0) must not fire here.
        private static void SyncVanillaFallback(Pawn pawn, Entry entry)
        {
            // Mechanical support is optional. Keep the native scheduler's
            // authored settings intact so disabling support restores them.
            if (MechanicalWorkers.IsMechanical(pawn)) return;
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
                && MechanicalWorkers.CanDo(pawn, def)
                && !pawn.WorkTypeIsDisabled(def.workType)
                && !pawn.WorkTagIsDisabled(def.workTags);
        }

        private static readonly List<JobEntry> EmptyEntries = new List<JobEntry>();

        private static Entry Build(Pawn pawn)
        {
            var store = RoleStore.Current;
            int ruleHour = RoleRules.LocalHour(pawn);
            var roleEntries = new List<(IReadOnlyList<JobEntry> entries, bool blocker)>();
            List<int>? activeRoleIds = null;
            // Slices contributed by the first EmergencyRoleLimit active roles
            // in row order (a composite counts once, whatever its member
            // count), for the per-save role-priority emergency rule.
            int emergencySliceCount = 0;
            int activeAssignments = 0;
            if (store != null && store.pawnSets.TryGetValue(pawn, out var set))
            {
                foreach (var assignment in set.assignments)
                {
                    var role = store.RoleById(assignment.roleId);
                    if (role == null
                        || !RoleActivation.IsActive(role.enabled, assignment.state)
                        || RoleRules.FailReason(role, pawn, ruleHour) != RuleFailReason.None) continue;
                    activeRoleIds ??= new List<int>();
                    bool leadingRole = ++activeAssignments <= JobOrderCompiler.EmergencyRoleLimit;
                    if (!role.composite)
                    {
                        activeRoleIds.Add(role.id);
                        roleEntries.Add((JobOrderCompiler.WithMovedSnapshotGivers(
                            role.entries, role.workTypeSnapshots, GameJobCatalog.Instance), role.blocker));
                        if (leadingRole) emergencySliceCount = roleEntries.Count;
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
                    if (leadingRole) emergencySliceCount = roleEntries.Count;
                }
            }

            var compiled = JobOrderCompiler.Compile(
                roleEntries, GameJobCatalog.Instance, pawn, PawnCanDoJob);
            var buckets = JobOrderCompiler.ToVanillaPriorities(compiled.WorkTypePriorities,
                ProjectionMetadata());
            if (store?.rolePriorityEmergencyRule == true)
                JobOrderCompiler.ApplyRolePriorityEmergencyRule(
                    compiled, GameJobCatalog.Instance, emergencySliceCount);
            int defCount = DefDatabase<WorkTypeDef>.DefCount;

            var entry = new Entry
            {
                RuleHour = ruleHour,
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
            if (MiscRobotSupport.IsRobot(pawn))
            {
                entry.Combined = new List<WorkGiver>(entry.Emergency.Count + entry.Normal.Count);
                entry.Combined.AddRange(entry.Emergency);
                entry.Combined.AddRange(entry.Normal);
            }
            else entry.Combined = entry.Normal;
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
