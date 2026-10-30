using System.Collections.Generic;
using System;
using System.Linq.Expressions;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using WorkRoles.Core;
using WorkRoles.Core.Recs;

namespace WorkRoles
{
    /// Native management retains ownership of charging, control groups and areas.
    /// Owns opt-in role capabilities, roster membership, lifecycle transitions
    /// and detached service facts consumed by the recommendation producer.
    internal static class MechanicalWorkers
    {
        // Owner/key: process/optional QJ assembly; value: immutable accessor.
        // Dependencies: fixed loaded API. Refresh once; equality one binding;
        // teardown process exit, no retained game/world/model instances.
        private static readonly Func<bool> qualityPreparationAllowed = BindQualityPreparation();
        private static readonly AccessTools.FieldRef<Pawn_PlayerSettings, Dictionary<Map, Area>> allowedAreas =
            AccessTools.FieldRefAccess<Pawn_PlayerSettings, Dictionary<Map, Area>>("allowedAreas");
        private static readonly Comparison<Pawn> WorkerOrder = (a, b) => a.thingIDNumber.CompareTo(b.thingIDNumber);
        internal static bool QualityPreparationAllowed => qualityPreparationAllowed();

        private static Func<bool> BindQualityPreparation()
        {
            Type? type = AccessTools.TypeByName("QualityJobs.QualityJobsStore");
            var active = type == null ? null : AccessTools.Property(type, "Active");
            var field = type == null ? null : AccessTools.Field(type, "allowRobotPreparation");
            if (active == null || field == null) return () => true;
            var store = Expression.Variable(type!, "store");
            return Expression.Lambda<Func<bool>>(Expression.Block(new[] { store },
                Expression.Assign(store, Expression.Property(null, active)),
                Expression.OrElse(Expression.Equal(store, Expression.Constant(null, type!)),
                    Expression.Field(store, field)))).Compile();
        }
        internal static void NotifyChanged(bool force = false)
        {
            RoleStore? store = RoleStore.Current;
            if (store != null && (force || store.mechanicalWorkersEnabled))
                unchecked { store.MechanicalFactsRevision++; }
        }

        internal static void RegisterStation(Thing station)
        {
            List<Pawn>? affected = MiscRobotSupport.RegisterStation(station);
            if (affected == null || RoleStore.Current?.mechanicalWorkersEnabled != true) return;
            for (int i = 0; i < affected.Count; i++)
            {
                PawnLocationTracker.NotifyManaged(affected[i]);
                ExternalPawnFacts.Invalidate(affected[i]);
                CompiledJobOrders.Invalidate(affected[i]);
            }
            NotifyChanged();
            UiVersion.RosterChanged();
        }

        internal static void NotifyStationMoved(Thing station)
        {
            if (MiscRobotSupport.IsRegisteredStation(station)) RegisterStation(station);
        }

        internal static void RemoveStation(Thing station) => ReleaseContainedWorkers(MiscRobotSupport.RemoveStation(station));
        internal static void ReleaseMap(Map map) => ReleaseContainedWorkers(MiscRobotSupport.ReleaseMap(map));

        private static void ReleaseContainedWorkers(List<Pawn>? removed)
        {
            if (removed == null) return;
            for (int i = 0; i < removed.Count; i++)
            {
                Pawn pawn = removed[i];
                // Native plain-list containers escape holder-based map cleanup.
                // Release role ownership only when no surviving holder owns it.
                if (pawn.MapHeld != null || pawn.ParentHolder != null || pawn.IsWorldPawn()) continue;
                RoleStore.Current?.UnmanagePawn(pawn);
                CompiledJobOrders.RemoveCached(pawn);
                PawnLocationTracker.NotifyDestroyed(pawn);
                ExternalPawnFacts.Release(pawn);
                PriorityGridFacts.Release(pawn);
                JobRankBaseline.NotifyDestroyed(pawn);
                Patches.Patch_MultiFloorsWorkScan.NotifyDestroyed(pawn);
                RoleStore.Current?.Revisions.ForgetPawn(pawn.thingIDNumber);
            }
            if (RoleStore.Current?.mechanicalWorkersEnabled == true) UiVersion.RosterChanged();
            NotifyChanged();
        }

        internal static List<Pawn> OnMap(Map map)
        {
            var result = new List<Pawn>();
            IReadOnlyList<Pawn> spawned = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < spawned.Count; i++)
                if (IsColonyWorker(spawned[i])) result.Add(spawned[i]);
            int firstDocked = result.Count;
            MiscRobotSupport.AppendDockedRobots(result, map);
            for (int i = result.Count - 1; i >= firstDocked; i--)
                if (!IsColonyWorker(result[i])) result.RemoveAt(i);
            result.Sort(WorkerOrder);
            return result;
        }

        private static bool HasUnrestrictedServiceArea(Pawn pawn)
        {
            Map? map = ColonyScope.MapOf(pawn);
            if (map == null) return false;
            return ServiceArea(pawn) == null;
        }

        internal static Area? ServiceArea(Pawn pawn)
        {
            Map? map = ColonyScope.MapOf(pawn);
            return map != null && pawn.playerSettings != null
                && allowedAreas(pawn.playerSettings).TryGetValue(map, out Area? area) ? area : null;
        }

        internal static MechanicalWorkerView Capture(Pawn pawn, RoleStore store)
        {
            PawnView facts = RecsAdapter.CapturePawnSnapshot(pawn,
                Core.Signals.PawnSignalSnapshot.Empty).RecommendationFacts;
            var worker = new MechanicalWorkerView { Id = pawn.thingIDNumber, Facts = facts };
            if (store.pawnSets.TryGetValue(pawn, out PawnRoleSet? set))
                foreach (RoleAssignment assignment in set.assignments)
                    facts.Existing.Add(new AssignmentView { RoleId = assignment.roleId, Pinned = assignment.pinned });
            bool preparationAllowed = QualityPreparationAllowed;
            bool misc = MiscRobotSupport.IsRobot(pawn);
            worker.ProvidesService = IsColonyWorker(pawn) && !pawn.Downed
                && !pawn.InMentalState && !pawn.Drafted
                && (misc ? MiscRobotSupport.AutomaticWorkEnabled(pawn)
                    : pawn.IsColonyMechPlayerControlled && pawn.GetMechWorkMode() == MechWorkModeDefOf.Work)
                // Restricted-area work is partial coverage; do not promise
                // colony-wide replacement without spatial demand evidence.
                && HasUnrestrictedServiceArea(pawn);
            foreach (WorkGiverDef giver in DefDatabase<WorkGiverDef>.AllDefsListForReading)
                if (giver.workType != null && CanDo(pawn, giver) && !WorkTypeDisabled(pawn, giver.workType)
                    && !WorkTagsDisabled(pawn, giver.workTags) && !facts.WorkEvidence.Blocks(giver.defName))
                {
                    worker.ExecutableGivers.Add(giver.defName);
                    if (!preparationAllowed && (giver.workType == WorkTypeDefOf.Construction
                        || typeof(WorkGiver_DoBill).IsAssignableFrom(giver.giverClass)))
                        worker.PartialServiceGivers.Add(giver.defName);
                }
            if (store.IsManaged(pawn))
            {
                foreach (int id in CompiledJobOrders.ActiveRolesFor(pawn)) worker.ActiveRoleIds.Add(id);
                foreach (WorkGiver giver in CompiledJobOrders.NormalFor(pawn)) worker.CommittedGivers.Add(giver.def.defName);
                foreach (WorkGiver giver in CompiledJobOrders.EmergencyFor(pawn)) worker.CommittedGivers.Add(giver.def.defName);
            }
            return worker;
        }

        internal static void AddSupply(RoleStore store, ColonyView colony, IReadOnlyList<Pawn> humans)
        {
            if (!store.mechanicalWorkersEnabled || !store.mechanicalRecommendationsEnabled) return;
            Map? map = SharedMap(humans);
            if (map == null) return;
            colony.IntegrateMechanicalWorkers = true;
            foreach (Pawn pawn in OnMap(map)) colony.MechanicalWorkers.Add(Capture(pawn, store));
        }

        internal static Map? SharedMap(IReadOnlyList<Pawn> pawns)
        {
            Map? map = pawns.Count == 0 ? null : ColonyScope.MapOf(pawns[0]);
            if (map == null) return null;
            for (int i = 1; i < pawns.Count; i++) if (ColonyScope.MapOf(pawns[i]) != map) return null;
            return map;
        }

        internal static bool IsMechanical(Pawn pawn) => pawn != null
            && !pawn.RaceProps.Humanlike
            && (pawn.RaceProps.IsMechanoid || MiscRobotSupport.IsRobot(pawn));

        internal static bool IsColonyWorker(Pawn pawn) => IsMechanical(pawn)
            && pawn.Faction == Faction.OfPlayer && !pawn.Dead && !pawn.Destroyed;

        // Misc. Robots selects work from its def's robotWorkTypes and the
        // giver's required capacities only (X2_AIRobot.GetWorkGivers,
        // X2_JobGiver_Work.PawnCanUseWorkGiver); vanilla disabled work types
        // and work tags never gate it, and mechEnabledWorkTypes is maintained
        // by hand in those defs. Vanilla mechs keep the JobGiver_Work rules.
        internal static bool WorkTypeDisabled(Pawn pawn, WorkTypeDef workType)
            => !MiscRobotSupport.IsRobot(pawn) && pawn.WorkTypeIsDisabled(workType);

        internal static bool WorkTagsDisabled(Pawn pawn, WorkTags tags)
            => !MiscRobotSupport.IsRobot(pawn) && pawn.WorkTagIsDisabled(tags);

        internal static WorkTags DisabledWorkTags(Pawn pawn)
            => MiscRobotSupport.IsRobot(pawn) ? WorkTags.None : pawn.CombinedDisabledWorkTags;

        internal static bool CanDo(Pawn pawn, WorkGiverDef giver)
        {
            if (!IsMechanical(pawn)) return true;
            if (MiscRobotSupport.IsRobot(pawn))
                return MiscRobotSupport.DeclaresWorkGiver(pawn.def, giver);
            return giver.canBeDoneByMechs
                && pawn.RaceProps.mechEnabledWorkTypes.Contains(giver.workType);
        }

        // Called only by the external-facts producer. Native work definitions
        // decide which skills are useful; a generic robot skill tracker alone
        // does not. A native giver exercises its work type's relevant skills
        // and the skills its recipes use or gate (the drug lab is Crafting work
        // run on Intellectual). Temporary job availability and role assignments
        // are not skill dependencies. The producer publishes these levels for
        // all UI and recommendation consumers, under its factual/definition gates.
        internal static void CaptureWorkSkills(Pawn pawn, IDictionary<string, int> levels)
        {
            IReadOnlyDictionary<string, JobProfileGiverFacts> facts = JobSkillProfiles.GiverFacts();
            var names = new HashSet<string>();
            foreach (WorkGiverDef giver in DefDatabase<WorkGiverDef>.AllDefsListForReading)
            {
                if (!CanDo(pawn, giver) || WorkTagsDisabled(pawn, giver.workTags)) continue;
                if (giver.workType?.relevantSkills != null)
                    foreach (SkillDef relevant in giver.workType.relevantSkills) names.Add(relevant.defName);
                if (facts.TryGetValue(giver.defName, out JobProfileGiverFacts? giverFacts))
                    giverFacts.CollectExercisedSkills(names);
            }
            // SkillRecord.TotallyDisabled is derived from the work types that
            // list the skill as relevant, which a Crafting-only robot fails for
            // Intellectual even though its drug lab recipes run on that skill.
            // Executable work decides here; the record only supplies the level.
            foreach (string name in names)
            {
                SkillDef? def = DefDatabase<SkillDef>.GetNamedSilentFail(name);
                if (def == null) continue;
                SkillRecord? skill = pawn.skills?.GetSkill(def);
                levels[def.defName] = skill?.Level ?? pawn.RaceProps.mechFixedSkillLevel;
            }
        }

        internal static List<Pawn> ListedPawns(ScopeOption scope)
        {
            List<Pawn> result = ColonyScope.PawnsIn(scope);
            if (RoleStore.Current?.mechanicalWorkersEnabled != true) return result;
            string current = ColonyScope.CurrentLocationId();
            foreach (Map map in Find.Maps)
            {
                if (!ScopeEngine.Matches(scope, ColonyScope.LocationId(map), current)) continue;
                IReadOnlyList<Pawn> spawned = map.mapPawns.AllPawnsSpawned;
                for (int i = 0; i < spawned.Count; i++)
                    if (IsColonyWorker(spawned[i])) result.Add(spawned[i]);
            }
            int firstDocked = result.Count;
            MiscRobotSupport.AppendDockedRobots(result);
            for (int i = result.Count - 1; i >= firstDocked; i--)
            {
                Pawn pawn = result[i];
                Map? map = ColonyScope.MapOf(pawn);
                bool inScope = map == null
                    ? scope.Kind == ScopeKind.All && MiscRobotSupport.HasPersistenceAnchor(pawn)
                    : Find.Maps.Contains(map) && ScopeEngine.Matches(scope, ColonyScope.LocationId(map), current);
                if (!IsColonyWorker(pawn) || !inScope) result.RemoveAt(i);
            }
            foreach (var caravan in Find.WorldObjects.Caravans)
                if (caravan.Faction == Faction.OfPlayer)
                    foreach (Pawn pawn in caravan.PawnsListForReading)
                        if (IsColonyWorker(pawn) && (scope.Kind == ScopeKind.All
                            || ScopeEngine.Matches(scope, PawnLocationTracker.EffectiveLocationId(pawn), current)))
                            result.Add(pawn);
            return result;
        }
    }
}
