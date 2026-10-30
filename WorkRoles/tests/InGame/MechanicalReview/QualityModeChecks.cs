using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using QualityJobs;
using QualityJobs.Core;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using WorkRoles;

namespace WorkRolesMechanicalReview
{
    public static partial class Probe
    {
        private static Frame observedQualityFrame;
        private static Pawn observedQualityBuilder;

        private static void ObserveQualityCompletion(Frame __instance, Pawn worker)
        {
            if (__instance == observedQualityFrame) observedQualityBuilder = worker;
        }

        internal static void RunQualityModeChecks()
        {
            // The sanctioned player-save fixture omits some source mods. Drop
            // only orphaned jobs whose definitions did not survive that load,
            // before native despawn cleanup dereferences their missing JobDef.
            foreach (Pawn p in map.mapPawns.AllPawns.ToArray())
                if (p.CurJob != null && p.CurJob.def == null)
                {
                    Record("native-fixture-orphaned-job-cleared", p.ThingID);
                    p.jobs.curJob = null; p.jobs.curDriver = null;
                    p.jobs.ClearQueuedJobs(); p.pather.StopDead();
                }
            RoleCommands.SetAutoOptimize(false);
            RoleCommands.SetMechanicalWorkersEnabled(true);
            Pawn human = map.mapPawns.FreeColonistsSpawned.First(p => !p.Downed && !p.InMentalState
                && !p.WorkTypeIsDisabled(WorkTypeDefOf.Construction)
                && p.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation));
            foreach (Pawn p in map.mapPawns.AllPawnsSpawned.ToArray())
            {
                RoleCommands.PasteRoleSet(p, new List<RoleAssignment>());
                p.workSettings?.SetPriority(WorkTypeDefOf.Construction, 0);
                if (p != human && p.RaceProps.IsMechanoid)
                { p.DeSpawn(); Find.WorldPawns.PassToWorld(p, PawnDiscardDecideMode.KeepForever); }
            }
            IntVec3 origin = GenRadial.RadialCellsAround(human.Position, 70f, true).First(c => c.InBounds(map)
                && CellRect.CenteredOn(c, 9, 9).Cells.All(x => x.InBounds(map) && x.Standable(map) && x.GetEdifice(map) == null));
            foreach (Thing old in map.listerThings.AllThings.ToArray()) if (!(old is Pawn)) old.SetForbidden(true, false);
            map.areaManager.Home.Clear();
            if (human.drafter != null) human.drafter.Drafted = false;
            human.jobs.EndCurrentJob(JobCondition.InterruptForced, false);
            human.Position = origin; human.playerSettings.AreaRestrictionInPawnCurrentMap = null;
            human.skills.GetSkill(SkillDefOf.Construction).Level = 5;
            if (!human.health.hediffSet.HasHediff(HediffDefOf.MechlinkImplant))
                human.health.AddHediff(HediffDefOf.MechlinkImplant, human.health.hediffSet.GetBrain());
            Pawn mech = PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.GetNamed("Mech_Constructoid"), Faction.OfPlayer);
            GenSpawn.Spawn(mech, origin + IntVec3.North, map);
            human.relations.AddDirectRelation(PawnRelationDefOf.Overseer, mech);
            human.mechanitor.AssignPawnControlGroup(mech, MechWorkModeDefOf.Work);
            MechanitorControlGroup group = mech.GetMechControlGroup();
            mech.needs.energy.CurLevel = mech.needs.energy.MaxLevel;
            mech.playerSettings.AreaRestrictionInPawnCurrentMap = null;
            int roleId = RoleStore.Current.RoleByTemplate("WS_Builder").id;
            foreach (Pawn p in new[] { human, mech }) RoleCommands.PasteRoleSet(p,
                new List<RoleAssignment> { new RoleAssignment { roleId = roleId } });
            Check(mech.IsColonyMechPlayerControlled && Dispatcher.ConstructionSkillOf(mech) > Dispatcher.ConstructionSkillOf(human),
                "N3 native controlled Constructoid outranks available capable human");
            var condition = new ResumeCondition(1, false, false);
            Check(Dispatcher.SelectConstructionFinisher(map, condition) == mech, "N3 Work mode selects capable native mech");
            VerifyNativeAvailabilityTransitions(human, mech, condition);
            group.SetWorkMode(MechWorkModeDefOf.SelfShutdown);
            Job native = mech.thinker.MainThinkNodeRoot.TryIssueJobPackage(mech, default).Job;
            Check(native?.def == JobDefOf.SelfShutdown, "N3 native shutdown think tree issues SelfShutdown");
            if (native != null) JobMaker.ReturnToPool(native);
            Check(Dispatcher.SelectConstructionFinisher(map, condition) == human,
                "N3 shutdown mode selects available human for manual threshold");
            Check(Dispatcher.SelectAutoFinisher(map, null, condition) == null
                && Dispatcher.DecideAutoConstructionGate(human, condition) == GateOutcome.Pause,
                "N3 temporarily unavailable best mech preserves colony-wide auto-best quality bar");

            var harmony = new Harmony("eprime.workroles.mechanicalreview.qualitycompletion");
            var complete = AccessTools.Method(typeof(Frame), nameof(Frame.CompleteConstruction));
            harmony.Patch(complete, prefix: new HarmonyMethod(typeof(Probe), nameof(ObserveQualityCompletion)));
            try
            {
                group.SetWorkMode(MechWorkModeDefOf.Work);
                MechanitorControlGroup shutdownGroup = human.mechanitor.controlGroups.First(g => g != group);
                shutdownGroup.SetWorkMode(MechWorkModeDefOf.SelfShutdown);
                int groupRevision = (int)Field(QualityJobsStore.Active, "ExternalPawnFactsRevision");
                shutdownGroup.Assign(mech);
                Check((int)Field(QualityJobsStore.Active, "ExternalPawnFactsRevision") != groupRevision
                    && Dispatcher.SelectConstructionFinisher(map, condition) == human,
                    "N3 assignment into native shutdown group updates finisher availability immediately");
                group.Assign(mech);
                Check(Dispatcher.SelectConstructionFinisher(map, condition) == mech,
                    "N3 assignment back to native Work group restores mech eligibility");
                Frame first = CreatePausedQualityFrame(human, origin + IntVec3.East * 3);
                ConstructionPlan plan = QualityJobsStore.Active.FindPlan(first);
                Commands.SetPlanMinSkill(first.thingIDNumber, 1);
                Dispatcher.TryDispatchConstruction(QualityJobsStore.Active, plan);
                Check(plan.finisher == mech && !Dispatcher.ConstructionDispatchInvalid(plan), "N3 native Work mech receives real paused frame dispatch");
                int revision = (int)Field(QualityJobsStore.Active, "ExternalPawnFactsRevision");
                group.SetWorkMode(MechWorkModeDefOf.Work);
                Check((int)Field(QualityJobsStore.Active, "ExternalPawnFactsRevision") == revision, "N3 unchanged native Work mode preserves external facts revision");
                group.SetWorkMode(MechWorkModeDefOf.SelfShutdown);
                Check((int)Field(QualityJobsStore.Active, "ExternalPawnFactsRevision") != revision && Dispatcher.ConstructionDispatchInvalid(plan),
                    "N3 paused Work-to-shutdown switch invalidates existing mech dispatch immediately");
                ConsumeQualityFrame(first, human, mech);
                Check(observedQualityBuilder == human && mech.GetMechWorkMode() == MechWorkModeDefOf.SelfShutdown,
                    "N3 available human actually completes redispatched quality frame while mech stays shutdown");

                group.SetWorkMode(MechWorkModeDefOf.Work);
                Frame second = CreatePausedQualityFrame(human, origin + IntVec3.West * 3);
                Commands.SetPlanMinSkill(second.thingIDNumber, 10);
                plan = QualityJobsStore.Active.FindPlan(second);
                Dispatcher.TryDispatchConstruction(QualityJobsStore.Active, plan);
                Check(plan.finisher == mech, "N3 returning to Work restores native mech dispatch");
                ConsumeQualityFrame(second, human, mech);
                Check(observedQualityBuilder == mech, "N3 native Work mech actually completes quality building");
            }
            finally
            {
                harmony.Unpatch(complete, HarmonyPatchType.Prefix, harmony.Id);
                observedQualityFrame = null; observedQualityBuilder = null;
                Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
            }
        }

        private static void VerifyNativeAvailabilityTransitions(Pawn human, Pawn mech, ResumeCondition condition)
        {
            int revision = (int)Field(QualityJobsStore.Active, "ExternalPawnFactsRevision");
            mech.drafter.Drafted = true;
            Job draftedJob = mech.thinker.MainThinkNodeRoot.TryIssueJobPackage(mech, default).Job;
            Check(draftedJob?.def == JobDefOf.Wait_Combat, "N3 native drafted Work mech issues combat wait instead of work");
            if (draftedJob != null) JobMaker.ReturnToPool(draftedJob);
            Check(Dispatcher.SelectConstructionFinisher(map, condition) == human,
                "N3 drafted Work mech leaves manual finishing to available human");
            Check((int)Field(QualityJobsStore.Active, "ExternalPawnFactsRevision") != revision,
                "N3 native draft transition publishes finisher availability immediately");
            revision = (int)Field(QualityJobsStore.Active, "ExternalPawnFactsRevision");
            mech.drafter.Drafted = true;
            Check((int)Field(QualityJobsStore.Active, "ExternalPawnFactsRevision") == revision,
                "N3 unchanged draft order preserves external facts revision");
            Check(Dispatcher.SelectAutoFinisher(map, null, condition) == null,
                "N3 drafted best mech remains in auto-best quality population");
            Find.TickManager.DoSingleTick();
            mech.drafter.Drafted = false;
            Check(Dispatcher.SelectConstructionFinisher(map, condition) == mech, "N3 undrafting restores Work mech eligibility");
            Find.TickManager.DoSingleTick();

            mech.needs.energy.CurLevel = 0f;
            mech.needs.energy.NeedInterval();
            for (int i = 0; i < 30 && !mech.needs.energy.IsLowEnergySelfShutdown; i++) Find.TickManager.DoSingleTick();
            Check(mech.needs.energy.IsLowEnergySelfShutdown && mech.GetMechWorkMode() == MechWorkModeDefOf.Work,
                "N3 depleted Work mech enters native low-energy self-shutdown");
            Record("native-low-energy-mech-downed", mech.Downed);
            Check(Dispatcher.SelectConstructionFinisher(map, condition) == human,
                "N3 low-energy shutdown leaves manual finishing to available human");
            Find.TickManager.DoSingleTick();
            mech.needs.energy.CurLevel = mech.needs.energy.MaxLevel;
            mech.needs.energy.NeedInterval();
            Check(!mech.needs.energy.IsLowEnergySelfShutdown && Dispatcher.SelectConstructionFinisher(map, condition) == mech,
                "N3 native energy recovery restores Work mech eligibility");
            Find.TickManager.DoSingleTick();

            if (!TileFinder.TryFindNewSiteTile(out var destination)) throw new InvalidOperationException("No caravan destination");
            revision = (int)Field(QualityJobsStore.Active, "ExternalPawnFactsRevision");
            CaravanFormingUtility.StartFormingCaravan(new List<Pawn> { human, mech }, new List<Pawn>(), Faction.OfPlayer,
                new List<TransferableOneWay>(), human.Position, human.Position + IntVec3.North * 4, map.Tile, destination);
            Check(mech.IsFormingCaravan() && mech.GetMechWorkMode() == MechWorkModeDefOf.Work,
                "N3 native forming caravan retains mech Work mode but excludes native work branch");
            Check(Dispatcher.SelectConstructionFinisher(map, condition) == human,
                "N3 caravan mech is excluded while existing human availability behavior is preserved");
            Check((int)Field(QualityJobsStore.Active, "ExternalPawnFactsRevision") != revision,
                "N3 native caravan formation publishes availability immediately");
            revision = (int)Field(QualityJobsStore.Active, "ExternalPawnFactsRevision");
            CaravanFormingUtility.StopFormingCaravan(CaravanFormingUtility.GetFormAndSendCaravanLord(mech));
            Check(!mech.IsFormingCaravan() && Dispatcher.SelectConstructionFinisher(map, condition) == mech,
                "N3 canceling native caravan restores mech finishing eligibility");
            Check((int)Field(QualityJobsStore.Active, "ExternalPawnFactsRevision") != revision,
                "N3 native caravan cancellation publishes availability immediately");
            // Separate independent user commands as actual simulation events;
            // otherwise the fixture trips vanilla's ten-jobs-in-one-tick guard.
            Find.TickManager.DoSingleTick();
        }

        private static Frame CreatePausedQualityFrame(Pawn human, IntVec3 cell)
        {
            Commands.SetManageNewConstructionDefault(true); Commands.SetConstructionAutoBestDefault(false);
            Commands.SetConstructionMinSkillDefault(20); Commands.SetConstructionRequireInspiredDefault(false);
            Commands.SetConstructionRequireSpecialistDefault(false); Commands.SetConstructionTargetQualityDefault(0);
            Commands.SetAllowRobotPreparation(true);
            Blueprint_Build blueprint = GenConstruct.PlaceBlueprintForBuild(ThingDefOf.Bed, cell, map, Rot4.North, Faction.OfPlayer, ThingDefOf.WoodLog);
            Thing blocker;
            while ((blocker = GenConstruct.FirstBlockingThing(blueprint, human)) != null)
            {
                if (blocker.def.category != ThingCategory.Item) throw new InvalidOperationException("Unexpected frame blocker " + blocker);
                blocker.Destroy();
            }
            if (!blueprint.TryReplaceWithSolidThing(human, out Thing created, out _) || !(created is Frame frame))
                throw new InvalidOperationException("Native blueprint did not create construction frame");
            foreach (ThingDefCountClass cost in frame.TotalMaterialCost())
            { Thing material = ThingMaker.MakeThing(cost.thingDef); material.stackCount = cost.count; frame.resourceContainer.TryAdd(material); }
            frame.workDone = frame.WorkToBuild; frame.CompleteConstruction(human);
            Check(QualityJobsStore.Active.FindPlan(frame)?.state == ConstructionPlanState.Paused,
                "N3 actual construction completion gate pauses prepared native frame");
            return frame;
        }

        private static void ConsumeQualityFrame(Frame frame, Pawn human, Pawn mech)
        {
            observedQualityFrame = frame; observedQualityBuilder = null;
            IntVec3 cell = frame.Position;
            for (int i = 0; i < 3000 && !frame.Destroyed; i++)
            {
                if (human.needs.food != null) human.needs.food.CurLevel = human.needs.food.MaxLevel;
                if (human.needs.rest != null) human.needs.rest.CurLevel = 1f;
                mech.needs.energy.CurLevel = mech.needs.energy.MaxLevel;
                Find.TickManager.DoSingleTick();
            }
            Check(frame.Destroyed && map.listerThings.ThingsOfDef(ThingDefOf.Bed)
                .Any(b => b.Position == cell && b.TryGetComp<CompQuality>() != null),
                "N3 native scheduler consumes dispatched frame and creates quality building");
        }
    }
}
