using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using QualityJobs;
using QualityJobs.Core;
using RimWorld;
using Verse;

namespace ConstructionTransitionsVerification
{
    // Runtime regression: real Harmony suppression around real game transitions.
    // It catches plans being removed/retargeted when their target survives, and
    // settings captured by a cancelled demolition being reused by a later one.
    [StaticConstructorOnStartup]
    public static class Probe
    {
        private static readonly Harmony Patches = new Harmony("eprime.qualityjobs.constructiontransitionsprobe");
        private static readonly List<string> Failures = new List<string>();
        private static Thing suppressed;
        private static Thing earlyTarget;
        private static bool earlyReplacement;
        private static bool replaceCompletion, ran;
        private static Map map;
        private static QualityJobsStore store;
        private static Pawn worker;
        private static ThingDef buildDef, stuff;
        private static IntVec3 position;
        private static Rot4 rotation;
        private static int Revision => (int)AccessTools.Field(typeof(QualityJobsStore), "PlanStatusRevision").GetValue(store);
        private static string Root => GenFilePaths.SaveDataFolderPath;

        static Probe()
        {
            Patches.Patch(AccessTools.Method(typeof(Root_Play), "Update"), postfix: new HarmonyMethod(typeof(Probe), nameof(Update)));
            // Run after QJ's capture to reproduce the dangerous postfix case.
            var blocker = new HarmonyMethod(typeof(Probe), nameof(Suppress)) { priority = Priority.Last };
            Patches.Patch(AccessTools.Method(typeof(Frame), nameof(Frame.CompleteConstruction)), prefix: blocker);
            Patches.Patch(AccessTools.Method(typeof(Frame), nameof(Frame.FailConstruction)), prefix: blocker);
            Patches.Patch(AccessTools.Method(typeof(Building), nameof(Building.Destroy)), prefix: blocker);
            Patches.Patch(AccessTools.Method(typeof(Frame), nameof(Frame.CompleteConstruction)),
                prefix: new HarmonyMethod(typeof(Probe), nameof(EarlyComplete)) { priority = Priority.First });
            Patches.Patch(AccessTools.Method(typeof(Frame), nameof(Frame.FailConstruction)),
                prefix: new HarmonyMethod(typeof(Probe), nameof(EarlyFail)) { priority = Priority.First });
            Patches.Patch(AccessTools.Method(typeof(Building), nameof(Building.Destroy)),
                prefix: new HarmonyMethod(typeof(Probe), nameof(EarlyDestroy)) { priority = Priority.First });
            Patches.CreateReversePatcher(AccessTools.Method(typeof(Building), nameof(Building.Destroy)),
                new HarmonyMethod(typeof(Probe), nameof(DestroyOriginal))).Patch();
        }

        private static bool EarlyComplete(Frame __instance)
        {
            if (!ReferenceEquals(__instance, earlyTarget)) return true;
            if (earlyReplacement)
            {
                __instance.Destroy();
                var built = ThingMaker.MakeThing(buildDef, stuff);
                built.SetFactionDirect(Faction.OfPlayer);
                built.TryGetComp<CompQuality>().SetQuality(QualityCategory.Normal, ArtGenerationContext.Colony);
                GenSpawn.Spawn(built, position, map, rotation, WipeMode.FullRefund);
            }
            return false;
        }

        private static bool EarlyFail(Frame __instance)
        {
            if (!ReferenceEquals(__instance, earlyTarget)) return true;
            if (earlyReplacement)
            {
                __instance.Destroy(DestroyMode.FailConstruction);
                GenConstruct.PlaceBlueprintForBuild(buildDef, position, map, rotation, Faction.OfPlayer, stuff);
            }
            return false;
        }

        private static bool EarlyDestroy(Building __instance, DestroyMode mode)
        {
            if (!ReferenceEquals(__instance, earlyTarget) || __instance is Frame) return true;
            if (earlyReplacement) DestroyOriginal(__instance, mode);
            return false;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void DestroyOriginal(Building instance, DestroyMode mode)
            => throw new InvalidOperationException("Reverse patch was not installed");

        private static bool Suppress(Thing __instance)
        {
            if (!ReferenceEquals(__instance, suppressed)) return true;
            if (replaceCompletion)
            {
                // A third-party prefix may implement successful construction and
                // return false. Use the game's own factory/spawn construction.
                suppressed = null;
                __instance.Destroy();
                var built = ThingMaker.MakeThing(buildDef, stuff);
                built.SetFactionDirect(Faction.OfPlayer);
                built.TryGetComp<CompQuality>().SetQuality(QualityCategory.Normal, ArtGenerationContext.Colony);
                GenSpawn.Spawn(built, position, map, rotation, WipeMode.FullRefund);
            }
            return false;
        }

        private static void Check(bool condition, string message)
        {
            Log.Message("[ConstructionTransitionsProbe] " + (condition ? "PASS " : "FAIL ") + message);
            if (!condition) Failures.Add(message);
        }

        private static void Update()
        {
            if (ran || Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null
                || LongEventHandler.AnyEventNowOrWaiting) return;
            ran = true;
            try
            {
                map = Find.CurrentMap; store = QualityJobsStore.Active;
                Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                // The reduced-mod copied save can retain unresolved external
                // jobs. Clear only those before inspecting this test fixture.
                foreach (Pawn pawn in map.mapPawns.AllPawns)
                    if (pawn.CurJob != null && pawn.CurJob.def == null)
                    {
                        pawn.jobs.curJob = null; pawn.jobs.curDriver = null;
                        pawn.jobs.ClearQueuedJobs(); pawn.pather.StopDead();
                    }
                worker = map.mapPawns.FreeColonists.First(p => !p.Dead && !p.Downed && !p.WorkTypeIsDisabled(WorkTypeDefOf.Construction));
                worker.skills.GetSkill(SkillDefOf.Construction).Level = 20;
                var fixture = map.listerBuildings.allBuildingsColonist.OfType<Building_Bed>().First(b => b.def.defName == "Bed");
                buildDef = fixture.def; stuff = fixture.Stuff; position = fixture.Position; rotation = fixture.Rotation;
                Commands.RemovePlan(fixture.thingIDNumber); fixture.Destroy();
                Commands.SetConstructionMinSkillDefault(0); Commands.SetConstructionAutoBestDefault(false);
                Commands.SetConstructionRequireInspiredDefault(false); Commands.SetConstructionRequireSpecialistDefault(false);
                Commands.SetConstructionTargetQualityDefault(0);
                VerifyCompletion(); ClearSite();
                VerifyFailure(); ClearSite();
                VerifyDestruction(); ClearSite();
                VerifyReplacementPrefix(); ClearSite();
                VerifyEarlyReplacementPrefixes(); ClearSite();
                File.WriteAllText(Path.Combine(Root, "transitions-stage.txt"), Failures.Count == 0 ? "SUCCESS" : "FAILED " + string.Join("; ", Failures));
            }
            catch (Exception error)
            {
                File.WriteAllText(Path.Combine(Root, "transitions-stage.txt"), "FAILED " + error);
                Log.Error("[ConstructionTransitionsProbe] FAILED " + error);
            }
            finally { suppressed = earlyTarget = null; replaceCompletion = earlyReplacement = false; }
        }

        private static Frame NewFrame()
        {
            var blueprint = GenConstruct.PlaceBlueprintForBuild(buildDef, position, map, rotation, Faction.OfPlayer, stuff);
            Frame frame = ReplaceBlueprint(blueprint);
            Commands.SetPlanMinSkill(frame.thingIDNumber, 1);
            return frame;
        }

        private static Frame ReplaceBlueprint(Blueprint_Build blueprint)
        {
            // Demolition drops salvage in the footprint. Hauling is outside
            // this transition regression; clear only loose item blockers.
            Thing blocker;
            while ((blocker = GenConstruct.FirstBlockingThing(blueprint, worker)) != null)
            {
                if (blocker.def.category != ThingCategory.Item)
                    throw new InvalidOperationException("Unexpected fixture blocker: " + blocker);
                blocker.Destroy();
            }
            if (!blueprint.TryReplaceWithSolidThing(worker, out Thing created, out _) || !(created is Frame frame))
                throw new InvalidOperationException("Fixture blueprint did not become a frame");
            return frame;
        }

        private static void VerifyCompletion()
        {
            Frame frame = NewFrame(); ConstructionPlan plan = store.FindPlan(frame); int revision = Revision;
            suppressed = frame; frame.CompleteConstruction(worker); suppressed = null;
            Check(frame.Spawned && !frame.Destroyed && store.FindPlan(frame) == plan && Revision == revision,
                "suppressed CompleteConstruction retains the original frame plan and revision");
            // Continue even on the red baseline so each independent bug is visible.
            if (store.FindPlan(frame) == null) Commands.SetPlanMinSkill(frame.thingIDNumber, 1);
            frame.CompleteConstruction(worker);
            Check(frame.Destroyed && map.thingGrid.ThingAt(position, buildDef) is Building
                && store.FindPlan(frame) == null, "real completion consumes the frame and retires its satisfied plan");
        }

        private static void VerifyFailure()
        {
            Frame frame = NewFrame(); ConstructionPlan plan = store.FindPlan(frame); int revision = Revision;
            suppressed = frame; frame.FailConstruction(worker); suppressed = null;
            Check(frame.Spawned && !frame.Destroyed && store.FindPlan(frame) == plan && Revision == revision,
                "suppressed FailConstruction retains the original frame plan and revision");
            if (store.FindPlan(frame) == null) { Commands.SetPlanMinSkill(frame.thingIDNumber, 1); plan = store.FindPlan(frame); }
            frame.FailConstruction(worker);
            var blueprint = map.thingGrid.ThingAt(position, buildDef.blueprintDef);
            Check(frame.Destroyed && blueprint is Blueprint_Build && store.FindPlan(blueprint) == plan,
                "real failed construction transfers the existing plan to the replacement blueprint");
        }

        private static void VerifyDestruction()
        {
            Frame frame = NewFrame(); frame.CompleteConstruction(worker);
            var bed = (Building_Bed)map.thingGrid.ThingAt(position, buildDef);
            bed.TryGetComp<CompQuality>().SetQuality(QualityCategory.Normal, null);
            bed.Medical = false;
            Commands.StartRebuild(bed.thingIDNumber, 1, false, false, 6, false);
            ConstructionPlan plan = store.FindPlan(bed); int revision = Revision;
            suppressed = bed; bed.Destroy(DestroyMode.Deconstruct); suppressed = null;
            bool retained = bed.Spawned && !bed.Destroyed && store.FindPlan(bed) == plan;
            Check(retained && Revision == revision && map.thingGrid.ThingAt(position, buildDef.blueprintDef) == null,
                "suppressed deconstruction retains the building, plan and revision without spawning a blueprint");
            if (!retained) return;
            // A suppressed call must not freeze settings before actual demolition.
            bed.Medical = true;
            bed.Destroy(DestroyMode.Deconstruct);
            var blueprint = (Blueprint_Build)map.thingGrid.ThingAt(position, buildDef.blueprintDef);
            Check(bed.Destroyed && store.FindPlan(blueprint) == plan,
                "real deconstruction transfers the existing plan to one blueprint");
            Frame replacement = ReplaceBlueprint(blueprint);
            Commands.SetPlanMinQuality(replacement.thingIDNumber, 0);
            replacement.CompleteConstruction(worker);
            var rebuilt = (Building_Bed)map.thingGrid.ThingAt(position, buildDef);
            Check(rebuilt.Medical && store.FindPlan(rebuilt) == null,
                "rebuild restores settings edited after the cancelled destruction");
            rebuilt.Medical = false; rebuilt.TryGetComp<CompQuality>().SetQuality(QualityCategory.Normal, null);
            Commands.StartRebuild(rebuilt.thingIDNumber, 1, false, false, 6, false);
            plan = store.FindPlan(rebuilt); revision = Revision;
            suppressed = rebuilt; rebuilt.Destroy(DestroyMode.Vanish); suppressed = null;
            Check(rebuilt.Spawned && store.FindPlan(rebuilt) == plan && Revision == revision,
                "suppressed non-deconstruction destruction retains the original plan");
            rebuilt.Destroy();
            Check(rebuilt.Destroyed && store.FindPlan(rebuilt) == null,
                "real non-deconstruction destruction removes the plan");
        }

        private static void VerifyReplacementPrefix()
        {
            Frame frame = NewFrame(); Commands.SetPlanMinQuality(frame.thingIDNumber, 6);
            ConstructionPlan plan = store.FindPlan(frame);
            suppressed = frame; replaceCompletion = true;
            frame.CompleteConstruction(worker);
            suppressed = null; replaceCompletion = false;
            var built = map.thingGrid.ThingAt(position, buildDef);
            Check(frame.Destroyed && built is Building && store.FindPlan(built) == plan
                && plan.state == ConstructionPlanState.AwaitingRebuild,
                "a successful replacement prefix still transfers the plan although vanilla did not run");
        }

        private static void ClearSite()
        {
            suppressed = earlyTarget = null; replaceCompletion = earlyReplacement = false;
            foreach (Thing thing in position.GetThingList(map).ToArray())
                if (thing.def == buildDef || thing.def == buildDef.blueprintDef || thing.def == buildDef.frameDef)
                { Commands.RemovePlan(thing.thingIDNumber); thing.Destroy(); }
        }

        private static void VerifyEarlyReplacementPrefixes()
        {
            Frame frame = NewFrame(); Commands.SetPlanMinQuality(frame.thingIDNumber, 6);
            ConstructionPlan plan = store.FindPlan(frame); int revision = Revision;
            earlyTarget = frame; frame.CompleteConstruction(worker);
            Check(frame.Spawned && store.FindPlan(frame) == plan && Revision == revision,
                "early completion suppression leaves the existing plan unchanged");
            earlyReplacement = true; frame.CompleteConstruction(worker); earlyTarget = null;
            Thing built = map.thingGrid.ThingAt(position, buildDef);
            Check(frame.Destroyed && built is Building && store.FindPlan(built) == plan,
                "early successful completion replacement transfers the existing plan");
            ClearSite();

            frame = NewFrame(); plan = store.FindPlan(frame); revision = Revision;
            earlyTarget = frame; frame.FailConstruction(worker);
            Check(frame.Spawned && store.FindPlan(frame) == plan && Revision == revision,
                "early failure suppression leaves the existing plan unchanged");
            earlyReplacement = true; frame.FailConstruction(worker); earlyTarget = null;
            Thing blueprint = map.thingGrid.ThingAt(position, buildDef.blueprintDef);
            Check(frame.Destroyed && blueprint is Blueprint_Build && store.FindPlan(blueprint) == plan,
                "early successful failure replacement transfers the existing plan");
            ClearSite();

            frame = NewFrame(); frame.CompleteConstruction(worker);
            var bed = (Building_Bed)map.thingGrid.ThingAt(position, buildDef);
            bed.Medical = true; bed.TryGetComp<CompQuality>().SetQuality(QualityCategory.Normal, null);
            Commands.StartRebuild(bed.thingIDNumber, 1, false, false, 6, false);
            plan = store.FindPlan(bed); revision = Revision;
            earlyTarget = bed; bed.Destroy(DestroyMode.Deconstruct);
            Check(bed.Spawned && store.FindPlan(bed) == plan && Revision == revision,
                "early destruction suppression leaves the existing plan unchanged");
            earlyReplacement = true; bed.Destroy(DestroyMode.Deconstruct); earlyTarget = null;
            blueprint = map.thingGrid.ThingAt(position, buildDef.blueprintDef);
            bool transferred = bed.Destroyed && blueprint is Blueprint_Build && store.FindPlan(blueprint) == plan;
            Check(transferred, "early successful deconstruction replacement transfers the existing plan");
            if (transferred)
            {
                Frame replacement = ReplaceBlueprint((Blueprint_Build)blueprint);
                Commands.SetPlanMinQuality(replacement.thingIDNumber, 0);
                replacement.CompleteConstruction(worker);
                Check(((Building_Bed)map.thingGrid.ThingAt(position, buildDef)).Medical,
                    "early deconstruction replacement retains settings captured before demolition");
            }
        }
    }
}
