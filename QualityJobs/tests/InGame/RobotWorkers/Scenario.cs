using System;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using QualityJobs;
using QualityJobs.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace RobotWorkersVerification
{
    public static partial class Probe
    {
        private static int stage, elapsed;
        private static bool running, misc;
        private static Pawn builderRobot;
        private static IntVec3 origin, buildCell;
        private static Building_WorkTable bench;
        private static Bill_ProductionWithUft sourceBill;
        private static UnfinishedThing prepared;
        private static WorkItemEntry entry;
        private static Frame frame;
        private static ConstructionPlan plan;
        private static float xpBefore;
        private static WorkGiver_DoBill billGiver;
        private static WorkGiver_ConstructFinishFrames frameGiver;
        private static MethodInfo settingCommand;
        private static ThinkNode robotWork, mechWork;
        private static ThingDef bedDef;
        private static string scenario;
        private static bool edgeChecksDone;

        private static void Require(bool condition, string message)
        { Check(condition, message); if (!condition) throw new InvalidOperationException(message); }

        private static void SetPreparation(bool value) => settingCommand.Invoke(null, new object[] { value });

        private static void SetupScenario()
        {
            scenario = File.Exists(Path.Combine(Root, "robot-workers-scenario.txt"))
                ? File.ReadAllText(Path.Combine(Root, "robot-workers-scenario.txt")).Trim() : "misc";
            misc = scenario.StartsWith("misc");
            settingCommand = AccessTools.Method(typeof(Commands), "SetAllowRobotPreparation");
            origin = GenRadial.RadialCellsAround(colonist.Position, 70, true).First(c => c.InBounds(map)
                && CellRect.CenteredOn(c, 7, 6).Cells.All(x => x.InBounds(map) && x.Standable(map)
                    && x.GetEdifice(map) == null));
            // The copied live colony has unrelated work. Forbid its preexisting
            // things through vanilla configuration, leaving this test's newly
            // created work available to the unchanged native scheduler.
            foreach (Thing old in map.listerThings.AllThings.ToArray())
                if (!(old is Pawn)) old.SetForbidden(true, false);
            // Repair intentionally ignores forbidden flags. Removing the old
            // home area is vanilla's way to stop those unrelated repair jobs.
            map.areaManager.Home.Clear();
            foreach (Pawn old in map.mapPawns.AllPawnsSpawned.ToArray())
                if (old != robot && old.RaceProps.IsMechanoid)
                {
                    old.jobs.EndCurrentJob(JobCondition.InterruptForced, false);
                    old.DeSpawn();
                    Find.WorldPawns.PassToWorld(old, RimWorld.Planet.PawnDiscardDecideMode.KeepForever);
                }
            buildCell = origin + new IntVec3(4, 0, 0);
            robot.jobs.EndCurrentJob(JobCondition.InterruptForced);
            if (!misc)
            {
                robot.Destroy();
                if (!colonist.health.hediffSet.HasHediff(HediffDefOf.MechlinkImplant))
                    colonist.health.AddHediff(HediffDefOf.MechlinkImplant, colonist.health.hediffSet.GetBrain());
                robot = NewMech("Mech_Fabricor", origin);
                builderRobot = NewMech("Mech_Constructoid", origin + IntVec3.North);
            }
            else builderRobot = robot;
            robot.Position = origin; builderRobot.Position = origin + IntVec3.North;
            robotWork = misc ? (ThinkNode)Activator.CreateInstance(AccessTools.TypeByName("AIRobot.X2_JobGiver_Work")) : new JobGiver_Work();
            mechWork = new JobGiver_Work();
            colonist.jobs.EndCurrentJob(JobCondition.InterruptForced);
            colonist.Position = origin + new IntVec3(0, 0, -2);
            colonist.skills.GetSkill(SkillDefOf.Crafting).Level = 5;
            colonist.skills.GetSkill(SkillDefOf.Construction).Level = 5;
            colonist.skills.GetSkill(SkillDefOf.Crafting).xpSinceLastLevel = 0;
            colonist.skills.GetSkill(SkillDefOf.Construction).xpSinceLastLevel = 0;
            colonist.skills.GetSkill(SkillDefOf.Crafting).passion = Passion.Major;
            colonist.skills.GetSkill(SkillDefOf.Construction).passion = Passion.Major;
            colonist.workSettings.SetPriority(DefDatabase<WorkTypeDef>.GetNamed("Tailoring"), 1);
            colonist.workSettings.SetPriority(WorkTypeDefOf.Construction, 1);
            bench = (Building_WorkTable)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("HandTailoringBench"), ThingDefOf.WoodLog);
            bench.SetFactionDirect(Faction.OfPlayer);
            GenSpawn.Spawn(bench, origin + new IntVec3(0, 0, 2), map, Rot4.South, WipeMode.FullRefund);
            billGiver = (WorkGiver_DoBill)DefDatabase<WorkGiverDef>.AllDefsListForReading
                .First(d => d.Worker is WorkGiver_DoBill && d.fixedBillGiverDefs != null && d.fixedBillGiverDefs.Contains(bench.def)).Worker;
            frameGiver = (WorkGiver_ConstructFinishFrames)DefDatabase<WorkGiverDef>.AllDefsListForReading
                .First(d => d.Worker is WorkGiver_ConstructFinishFrames).Worker;
            bedDef = DefDatabase<ThingDef>.GetNamed("Bed");
            for (int i = 0; i < 3; i++)
            {
                Thing cloth = ThingMaker.MakeThing(ThingDefOf.Cloth); cloth.stackCount = 75;
                GenPlace.TryPlaceThing(cloth, origin + new IntVec3(-2, 0, i), map, ThingPlaceMode.Near);
            }
            DebugSettings.fastCrafting = true;
            Commands.SetShareUnfinishedWork(true); Commands.SetHighPriorityFinish(!scenario.EndsWith("-normal"));
            VerifySelectionContracts();
            AssignWorkRoles(robot, true);
            if (builderRobot != robot) AssignWorkRoles(builderRobot, true);
            AssignWorkRoles(colonist, true);
            NewBill();
            SetPreparation(false);
            Check(!sourceBill.PawnAllowedToStartAnew(robot), scenario + " OFF blocks new managed robotic bill preparation");
            Job blocked = BillJob(robot);
            Check(blocked == null, scenario + " OFF bill scanner produces no preparation job");
            if (blocked != null) JobMaker.ReturnToPool(blocked);
            SetPreparation(true);
            VerifyMechFlag();
            Job job = BillJob(robot);
            Log.Message("[RobotWorkersProbe] FACT preparation=" + job + " reason=" + JobFailReason.Reason
                + " allowed=" + sourceBill.PawnAllowedToStartAnew(robot) + " should=" + sourceBill.ShouldDoNow()
                + " work=" + robot.workSettings.WorkIsActive(Dispatcher.WorkTypeForRecipe(recipe))
                + " ingredient=" + recipe.ingredients[0].GetBaseCount() + " cloth=" + map.listerThings.ThingsOfDef(ThingDefOf.Cloth).Count);
            Require(job != null && job.def == JobDefOf.DoBill, scenario + " ON permits a new robot preparation job");
            robot.jobs.TryTakeOrderedJob(job);
            running = true; stage = 1; Mark("RUNNING " + scenario + " robot preparation");
        }

        private static Pawn NewMech(string name, IntVec3 cell)
        {
            Pawn p = PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.GetNamed(name), Faction.OfPlayer);
            GenSpawn.Spawn(p, cell, map);
            colonist.relations.AddDirectRelation(PawnRelationDefOf.Overseer, p);
            colonist.mechanitor.AssignPawnControlGroup(p, MechWorkModeDefOf.Work);
            Require(p.IsColonyMechPlayerControlled && p.GetMechWorkMode() == MechWorkModeDefOf.Work,
                name + " uses the vanilla mechanitor relationship and Work control group");
            return p;
        }

        private static void NewBill()
        {
            sourceBill = (Bill_ProductionWithUft)BillUtility.MakeNewBill(recipe);
            sourceBill.repeatMode = BillRepeatModeDefOf.RepeatCount; sourceBill.repeatCount = 1;
            sourceBill.SetStoreMode(BillStoreModeDefOf.DropOnFloor);
            sourceBill.ingredientFilter.SetDisallowAll(); sourceBill.ingredientFilter.SetAllow(ThingDefOf.Cloth, true);
            bench.BillStack.AddBill(sourceBill);
            Commands.SetBillManaged(BillIds.IdOf(sourceBill), true);
            Commands.SetBillAutoBest(BillIds.IdOf(sourceBill), false);
            Commands.SetBillRequireInspired(BillIds.IdOf(sourceBill), false);
            Commands.SetBillRequireSpecialist(BillIds.IdOf(sourceBill), false);
            Commands.SetBillMinSkill(BillIds.IdOf(sourceBill), misc ? 20 : 10);
            Commands.SetBillTargetQuality(BillIds.IdOf(sourceBill), 0);
            Commands.SetProductCap(recipe.ProducedThingDef.defName, 0);
        }

        private static Job BillJob(Pawn p)
        { sourceBill.nextTickToSearchForIngredients = 0; return billGiver.JobOnThing(p, bench, true); }

        private static void ContinueScenario()
        {
            if (!running) return;
            try
            {
                if (reloadPhase > 0) { ResumeProvenanceReload(); return; }
                if (elapsed > 16000) throw new InvalidOperationException("Timed out stage=" + stage
                    + " human=" + colonist.CurJob + " robot=" + robot.CurJob + " frame=" + frame
                    + " plan=" + plan?.state + " sourceUft=" + sourceBill.BoundUft);
                KeepReady(colonist); KeepReady(robot); KeepReady(builderRobot);
                for (int i = 0; i < 75 && running; i++)
                {
                    Find.TickManager.DoSingleTick(); elapsed++;
                    if (Advance()) break;
                }
            }
            catch (Exception ex)
            {
                running = false; Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                Log.Error("[RobotWorkersProbe] FAILED " + ex); Mark("FAILED " + ex);
            }
        }

        private static bool Advance()
        {
            if (stage == 1)
            {
                UnfinishedThing uft = sourceBill.BoundUft;
                if (uft == null || uft.workLeft >= sourceBill.GetWorkAmount(uft) - 5) return false;
                prepared = uft;
                float before = prepared.workLeft;
                SetPreparation(false);
                Check(robot.CurJob?.def != JobDefOf.DoBill, scenario + " OFF interrupts robot preparation already in progress");
                Job blocked = BillJob(robot);
                Check(blocked == null && prepared.workLeft == before, scenario + " OFF blocks resuming a prepared managed item");
                if (blocked != null) JobMaker.ReturnToPool(blocked);
                SetPreparation(true);
                Job resume = BillJob(robot);
                Require(resume != null, scenario + " ON permits resuming robot preparation");
                robot.jobs.TryTakeOrderedJob(resume); stage = 2; return true;
            }
            if (stage == 2)
            {
                // Resume is proven by work on the same unfinished item.
                if (prepared.workLeft >= sourceBill.GetWorkAmount(prepared) - 15) return false;
                if (!edgeChecksDone)
                    Check(sourceBill.BoundUft == prepared, scenario + " resumed job consumes the same unfinished item");
                if (!edgeChecksDone && misc)
                {
                    edgeChecksDone = true; VerifyPreparationEdges();
                    if (reloadPhase > 0) return true;
                    if (File.Exists(Path.Combine(Root, "robot-workers-edges-only.txt")))
                    { running = false; Mark(failures == 0 ? "SUCCESS" : "FAILED " + failures + " assertions"); return true; }
                }
                robot.jobs.EndCurrentJob(JobCondition.InterruptForced);
                prepared.Destroy(); bench.BillStack.Delete(sourceBill);
                robot.DeSpawn(); if (builderRobot != robot) builderRobot.DeSpawn();
                NewBill(); SetPreparation(false);
                xpBefore = Xp(colonist, SkillDefOf.Crafting);
                Job prep = BillJob(colonist);
                Require(prep != null, scenario + " OFF still permits colonist preparation");
                colonist.jobs.TryTakeOrderedJob(prep); stage = 3; return true;
            }
            if (stage == 3)
            {
                entry = store.entries.FirstOrDefault(e => e.sourceBill == sourceBill);
                if (entry == null) return false;
                prepared = entry.uft;
                Require(prepared != null && prepared.workLeft <= 0 && !prepared.Destroyed,
                    scenario + " colonist completes preparation and leaves a real unfinished item");
                Check(Xp(colonist, SkillDefOf.Crafting) > xpBefore,
                    scenario + " colonist gained Crafting XP from preparation");
                GenSpawn.Spawn(robot, origin, map);
                Dispatcher.TryDispatch(store, entry);
                Require(entry.finisher == robot && entry.finishBill != null && entry.state == WorkItemState.Dispatched,
                    scenario + " prepared item dispatches to the eligible robot while preparation is OFF");
                Job finish = ScheduledFinish(robot, bench, Dispatcher.WorkTypeForRecipe(recipe), robotWork);
                Log.Message("[RobotWorkersProbe] FACT native bill job=" + finish + " bill=" + finish?.bill + " expected=" + entry.finishBill);
                Require(finish != null && finish.def == JobDefOf.DoBill && finish.bill == entry.finishBill,
                    scenario + " native robot work scheduler issues the dispatched finish bill");
                robot.jobs.TryTakeOrderedJob(finish); stage = 4; return true;
            }
            if (stage == 4)
            {
                if (!prepared.Destroyed) return false;
                Thing product = map.listerThings.ThingsOfDef(recipe.ProducedThingDef)
                    .FirstOrDefault(t => t.Position.DistanceTo(origin) < 10);
                Require(product != null && product.TryGetComp<CompQuality>() != null,
                    scenario + " game consumes the dispatched bill and creates the quality product");
                Check(!store.entries.Contains(entry), scenario + " completed item retires its Quality Jobs entry");
                robot.jobs.EndCurrentJob(JobCondition.InterruptForced); robot.DeSpawn();
                if (builderRobot != robot && builderRobot.Spawned) builderRobot.DeSpawn();
                NewFrame();
                xpBefore = Xp(colonist, SkillDefOf.Construction);
                Job work = frameGiver.JobOnThing(colonist, frame, true);
                Require(work != null, scenario + " colonist starts real construction while robot preparation is OFF");
                colonist.jobs.TryTakeOrderedJob(work); stage = 5; return true;
            }
            if (stage == 5)
            {
                if (frame.Destroyed)
                {
                    Log.Message("[RobotWorkersProbe] FACT consumed preparation frame target=" + plan.target
                        + " state=" + plan.state + " skill=" + plan.minSkill + " built=" + map.thingGrid.ThingAt(buildCell, bedDef));
                    Require(plan.target is Blueprint_Build || plan.target is Frame,
                        scenario + " vanilla construction failure retains retry blueprint or replacement frame");
                    if (plan.target is Blueprint_Build retry) FillFrame(retry);
                    else frame = (Frame)plan.target;
                    colonist.jobs.TryTakeOrderedJob(frameGiver.JobOnThing(colonist, frame, true));
                    return true;
                }
                if (plan.state != ConstructionPlanState.Paused) return false;
                Check(Xp(colonist, SkillDefOf.Construction) > xpBefore,
                    scenario + " colonist gains Construction XP before the finish gate");
                GenSpawn.Spawn(builderRobot, origin, map);
                Dispatcher.TryDispatchConstruction(store, plan);
                Require(plan.finisher == builderRobot && plan.state == ConstructionPlanState.Dispatched,
                    scenario + " completed frame dispatches to the eligible robotic builder");
                Job work = ScheduledFinish(builderRobot, frame, WorkTypeDefOf.Construction, misc ? robotWork : mechWork);
                Log.Message("[RobotWorkersProbe] FACT native construction job=" + work);
                Require(work != null && work.def == JobDefOf.FinishFrame && work.targetA.Thing == frame,
                    scenario + " native work scheduler issues the dispatched FinishFrame job");
                builderRobot.jobs.TryTakeOrderedJob(work); stage = 6; return true;
            }
            if (stage == 6)
            {
                if (!frame.Destroyed) return false;
                Thing built = map.thingGrid.ThingAt(buildCell, bedDef);
                Require(built is Building && built.TryGetComp<CompQuality>() != null,
                    scenario + " game consumes robotic FinishFrame and spawns the quality building");
                Check(store.FindPlan(frame) == null, scenario + " completed building retires its Quality Jobs plan");
                builderRobot.jobs.EndCurrentJob(JobCondition.InterruptForced);
                NewFrame();
                Job blocked = frameGiver.JobOnThing(builderRobot, frame, true);
                Check(blocked == null, scenario + " OFF blocks robot construction preparation on a fresh frame");
                if (blocked != null) JobMaker.ReturnToPool(blocked);
                SetPreparation(true);
                Job work = frameGiver.JobOnThing(builderRobot, frame, true);
                Require(work != null, scenario + " ON permits robot construction preparation");
                builderRobot.jobs.TryTakeOrderedJob(work); stage = 7; return true;
            }
            if (stage == 7)
            {
                if (frame.workDone <= 1) return false;
                float before = frame.workDone; SetPreparation(false);
                Check(builderRobot.CurJob?.def != JobDefOf.FinishFrame,
                    scenario + " OFF interrupts current robot frame preparation");
                Job blocked = frameGiver.JobOnThing(builderRobot, frame, true);
                Check(blocked == null && frame.workDone == before,
                    scenario + " OFF blocks resuming robot frame preparation");
                if (blocked != null) JobMaker.ReturnToPool(blocked);
                SetPreparation(true);
                Job work = frameGiver.JobOnThing(builderRobot, frame, true);
                Require(work != null, scenario + " ON permits resuming robot frame preparation");
                builderRobot.jobs.TryTakeOrderedJob(work); stage = 8; return true;
            }
            if (stage == 8 && frame.workDone > 10)
            {
                Check(true, scenario + " resumed robot construction advances actual frame work");
                running = false; Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                OpenOptions();
                Mark(failures == 0 ? "SUCCESS" : "FAILED " + failures + " assertions");
                Log.Message("[RobotWorkersProbe] " + (failures == 0 ? "SUCCESS" : "FAILED") + " " + scenario);
                return true;
            }
            return false;
        }

        private static void NewFrame()
        {
            Thing old = map.thingGrid.ThingAt(buildCell, bedDef);
            if (old != null) old.Destroy();
            Commands.SetManageNewConstructionDefault(true);
            Commands.SetConstructionMinSkillDefault(misc ? 20 : 10);
            Commands.SetConstructionAutoBestDefault(false);
            Commands.SetConstructionRequireInspiredDefault(false);
            Commands.SetConstructionRequireSpecialistDefault(false);
            Commands.SetConstructionTargetQualityDefault(0);
            var blueprint = GenConstruct.PlaceBlueprintForBuild(bedDef, buildCell, map, Rot4.North, Faction.OfPlayer, ThingDefOf.WoodLog);
            FillFrame(blueprint);
        }

        private static void FillFrame(Blueprint_Build blueprint)
        {
            Thing blocker;
            while ((blocker = GenConstruct.FirstBlockingThing(blueprint, colonist)) != null)
            {
                if (blocker.def.category != ThingCategory.Item) throw new InvalidOperationException("Unexpected blocker " + blocker);
                blocker.Destroy();
            }
            Require(blueprint.TryReplaceWithSolidThing(colonist, out Thing created, out _) && created is Frame,
                scenario + " vanilla blueprint factory creates construction fixture frame");
            frame = (Frame)created;
            foreach (ThingDefCountClass cost in frame.TotalMaterialCost())
            { Thing material = ThingMaker.MakeThing(cost.thingDef); material.stackCount = cost.count; frame.resourceContainer.TryAdd(material); }
            Commands.SetPlanMinSkill(frame.thingIDNumber, misc ? 20 : 10);
            plan = store.FindPlan(frame);
        }

        private static void KeepReady(Pawn p)
        {
            if (p?.needs == null) return;
            if (p.needs.food != null) p.needs.food.CurLevel = p.needs.food.MaxLevel;
            if (p.needs.rest != null) p.needs.rest.CurLevel = p.needs.rest.MaxLevel;
            if (p.needs.energy != null) p.needs.energy.CurLevel = p.needs.energy.MaxLevel;
        }
        private static float Xp(Pawn pawn, SkillDef skill)
        { SkillRecord record = pawn.skills.GetSkill(skill); return record.XpTotalEarned + record.xpSinceLastLevel; }

        private static Job ScheduledFinish(Pawn pawn, Thing target, WorkTypeDef workType, ThinkNode scheduler)
        {
            if (!scenario.EndsWith("-normal")) return scheduler.TryIssueJobPackage(pawn, default).Job;
            // Confirm normal mode uses the robot's actual native work list,
            // then preserve that scheduler's real selection and job metadata.
            var givers = misc
                ? (System.Collections.Generic.List<WorkGiver>)AccessTools.Method(pawn.GetType(), "GetWorkGivers")
                    .Invoke(pawn, new object[] { false })
                : pawn.workSettings.WorkGiversInOrderNormal;
            var giver = (WorkGiver_FinishQualityWork)givers.FirstOrDefault(g => g is WorkGiver_FinishQualityWork
                && g.def.workType == workType);
            Require(giver != null && !giver.ShouldSkip(pawn) && giver.PotentialWorkThingsGlobal(pawn).Contains(target),
                scenario + " native work list exposes dispatched target through its normal-priority scanner");
            return scheduler.TryIssueJobPackage(pawn, default).Job;
        }
    }
}
