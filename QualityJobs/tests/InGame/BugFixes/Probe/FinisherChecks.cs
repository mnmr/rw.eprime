using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using QualityJobs;
using QualityJobs.Core;
using QualityJobs.UI;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace QualityJobsBugReview
{
    // Disposable managed-run fixtures only. Real game factories, admission
    // checks, Quality Jobs commands, completion gate and lock are exercised.
    public static class FinisherChecks
    {
        public static void Run(Map map, QualityJobsStore store)
        {
            var oldPriorities = new Dictionary<Pawn, int>();
            Pawn low = null, capable = null;
            Frame frame = null;
            Blueprint_Build blueprint = null;
            Area_Allowed area = null;
            try
            {
                IntVec3 origin = FindClear(map);
                foreach (Pawn p in map.mapPawns.AllPawnsSpawned.ToArray())
                    if (p.workSettings != null && !p.WorkTypeIsDisabled(WorkTypeDefOf.Construction))
                    {
                        oldPriorities.Add(p, p.workSettings.GetPriority(WorkTypeDefOf.Construction));
                        p.workSettings.SetPriority(WorkTypeDefOf.Construction, 0);
                    }

                low = MakeWorker(map, origin + new IntVec3(-3, 0, 0), 5);
                capable = MakeWorker(map, origin + new IntVec3(-3, 0, 2), 12);
                Require(low.mindState.inspirationHandler.TryStartInspiration(
                    InspirationDefOf.Inspired_Creativity, "Quality Jobs review fixture", false),
                    "Could not start native Inspired Creativity on fixture worker");

                ThingDef buildDef = DefDatabase<ThingDef>.GetNamed("RoyalBed");
                blueprint = GenConstruct.PlaceBlueprintForBuild(buildDef, origin, map,
                    Rot4.North, Faction.OfPlayer, ThingDefOf.WoodLog);
                Commands.ApplyPlanSettings(blueprint.thingIDNumber, 0, false, false, 5, true);
                CheckConstructionDisplay("LR1-BLUEPRINT", store, blueprint, capable,
                    new[] { capable }, autoBest: true, expectedSkill: 12);
                Require(blueprint.TryReplaceWithSolidThing(capable, out Thing created, out _)
                    && created is Frame, "Native blueprint did not produce a frame");
                frame = (Frame)created;
                foreach (ThingDefCountClass cost in frame.TotalMaterialCost())
                {
                    Thing material = ThingMaker.MakeThing(cost.thingDef);
                    material.stackCount = cost.count;
                    Require(frame.resourceContainer.TryAdd(material), "Could not supply native frame material");
                }
                frame.workDone = frame.WorkToBuild;
                Require(frame.IsCompleted(), "Frame is not material-complete");

                var vanilla = (WorkGiver_ConstructFinishFrames)DefDatabase<WorkGiverDef>
                    .GetNamed("ConstructFinishFrames").Worker;
                var qj = (WorkGiver_FinishQualityWork)FinishWorkGivers.All
                    .First(d => d.defName == "QJ_FinishQualityWork_Construction").Worker;

                ConstructionPlan plan = PauseWithRealGate(store, frame, capable);
                Commands.ApplyPlanSettings(frame.thingIDNumber, 5, false, false, 0, false);
                bool nativeLow = GenConstruct.CanConstruct(frame, low, checkSkills: true);
                bool nativeCapable = GenConstruct.CanConstruct(frame, capable, checkSkills: true);
                Dispatcher.TryDispatchConstruction(store, plan);
                bool selectedLow = plan.finisher == low;
                bool selectedCapable = plan.finisher == capable;
                bool invalid = Dispatcher.ConstructionDispatchInvalid(plan);
                Job lowJob = qj.JobOnThing(low, frame);
                Job otherJob = vanilla.JobOnThing(capable, frame);
                bool chosenRejected = lowJob == null;
                bool otherLocked = otherJob == null;
                Return(lowJob); Return(otherJob);
                Commands.RemovePlan(frame.thingIDNumber);
                Job unlockedJob = vanilla.JobOnThing(capable, frame);
                bool unlockedWorks = unlockedJob != null && unlockedJob.def == JobDefOf.FinishFrame;
                Return(unlockedJob);
                Probe.Record("QJ002", selectedCapable && !nativeLow && nativeCapable && !invalid
                    && chosenRejected && !otherLocked && unlockedWorks,
                    "Manual royal-bed gate=5; required building skill=" + frame.def.constructionSkillPrerequisite
                    + "; inspired skill5 rank=" + FinisherRank.RankMilliOf(Dispatcher.ConstructionFactsFor(low))
                    + "; ordinary skill12 rank=" + FinisherRank.RankMilliOf(Dispatcher.ConstructionFactsFor(capable))
                    + "; selected low=" + selectedLow + "; native low/capable=" + nativeLow + "/" + nativeCapable
                    + "; dispatch invalid=" + invalid + "; selected job rejected=" + chosenRejected
                    + "; capable job locked=" + otherLocked + "; capable job after removing plan=" + unlockedWorks);

                plan = PauseWithRealGate(store, frame, capable);
                Commands.ApplyPlanSettings(frame.thingIDNumber, 0, false, false, 0, true);
                Dispatcher.TryDispatchConstruction(store, plan);
                otherJob = qj.JobOnThing(capable, frame);
                Probe.Record("QJ002-AUTO", plan.finisher == capable && otherJob?.def == JobDefOf.FinishFrame,
                    "Auto-best ignores inspired workers below the building's native minimum skill.");
                Return(otherJob);
                Dialog_ConstructionPlanConfig retainedDialog = CheckConstructionDisplay("LR1-FRAME", store, frame, capable,
                    new[] { capable }, autoBest: true, expectedSkill: 12);
                CheckRetainedConstructionDisplay(store, frame, retainedDialog, low, capable);
                Commands.RemovePlan(frame.thingIDNumber);

                // Same reachable native frame, now both workers have enough
                // building skill; the best worker's allowed area excludes it.
                low.mindState.inspirationHandler.EndInspiration(InspirationDefOf.Inspired_Creativity);
                low.skills.GetSkill(SkillDefOf.Construction).Level = 20;
                Require(map.areaManager.TryMakeNewAllowed(out area), "No spare allowed-area slot");
                area.SetLabel("QJ review worker area");
                area[low.Position] = true;
                low.playerSettings.AreaRestrictionInPawnCurrentMap = area;
                plan = PauseWithRealGate(store, frame, capable);
                Commands.ApplyPlanSettings(frame.thingIDNumber, 5, false, false, 0, false);
                Dispatcher.TryDispatchConstruction(store, plan);
                bool forbiddenLow = frame.IsForbidden(low);
                bool forbiddenCapable = frame.IsForbidden(capable);
                nativeCapable = GenConstruct.CanConstruct(frame, capable, checkSkills: true);
                selectedLow = plan.finisher == low;
                selectedCapable = plan.finisher == capable;
                invalid = Dispatcher.ConstructionDispatchInvalid(plan);
                lowJob = qj.JobOnThing(low, frame);
                otherJob = vanilla.JobOnThing(capable, frame);
                chosenRejected = lowJob == null;
                otherLocked = otherJob == null;
                Return(lowJob); Return(otherJob);
                CheckConstructionDisplay("LR1-AREA", store, frame, null,
                    new[] { capable }, autoBest: false, expectedSkill: 5);
                Commands.RemovePlan(frame.thingIDNumber);
                unlockedJob = vanilla.JobOnThing(capable, frame);
                unlockedWorks = unlockedJob != null && unlockedJob.def == JobDefOf.FinishFrame;
                Return(unlockedJob);
                Probe.Record("QJ003", selectedCapable && forbiddenLow && !forbiddenCapable && nativeCapable
                    && !invalid && chosenRejected && !otherLocked && unlockedWorks,
                    "Manual gate=5; best skill20 worker excludes royal bed from allowed area; selected best="
                    + selectedLow + "; forbidden best/other=" + forbiddenLow + "/" + forbiddenCapable
                    + "; capable native admissible=" + nativeCapable + "; dispatch invalid=" + invalid
                    + "; selected job rejected=" + chosenRejected + "; capable job locked=" + otherLocked
                    + "; capable job after removing plan=" + unlockedWorks);

                plan = PauseWithRealGate(store, frame, capable);
                Commands.ApplyPlanSettings(frame.thingIDNumber, 0, false, false, 5, true);
                Dispatcher.TryDispatchConstruction(store, plan);
                Probe.Record("LR1-AUTO-WAIT", plan.finisher == null
                    && plan.state == ConstructionPlanState.Paused,
                    "Auto-best keeps waiting for the level-20 builder outside the allowed area.");
                CheckConstructionDisplay("LR1-AUTO-WAIT-DISPLAY", store, frame, low,
                    Array.Empty<Pawn>(), autoBest: true, expectedSkill: 20);
                Commands.RemovePlan(frame.thingIDNumber);

                // Drafting is a real native scheduling restriction. This records
                // the selection fact, without claiming a separate defect from
                // the two proven target-specific stalls above.
                low.playerSettings.AreaRestrictionInPawnCurrentMap = null;
                low.drafter.Drafted = true;
                bool draftedSelected = Dispatcher.SelectConstructionFinisher(map,
                    new ResumeCondition(5, false, false)) == low;
                Log.Message("[QualityJobsBugReview] FIN-DRAFT FACT manual selection chooses drafted human="
                    + draftedSelected + "; drafted=" + low.Drafted);
            }
            finally
            {
                if (frame != null && !frame.Destroyed)
                {
                    Commands.RemovePlan(frame.thingIDNumber);
                    frame.Destroy();
                }
                if (blueprint != null && !blueprint.Destroyed) blueprint.Destroy();
                if (low != null)
                {
                    if (low.playerSettings != null) low.playerSettings.AreaRestrictionInPawnCurrentMap = null;
                    if (!low.Destroyed) low.Destroy();
                }
                if (capable != null && !capable.Destroyed) capable.Destroy();
                if (area != null) area.Delete();
                foreach (KeyValuePair<Pawn, int> item in oldPriorities)
                    item.Key.workSettings.SetPriority(WorkTypeDefOf.Construction, item.Value);
            }
        }

        // Exercises the dialog's real gated display builders and the public API.
        // The expected pawns/skill come from the native Royal-bed fixture, not
        // from Dispatcher. Reflection keeps the probe runnable against baseline
        // assemblies so this assertion can establish the red result first.
        private static Dialog_ConstructionPlanConfig CheckConstructionDisplay(string id, QualityJobsStore store,
            Thing target, Pawn expectedBest, Pawn[] expectedEligible, bool autoBest,
            int expectedSkill, Dialog_ConstructionPlanConfig dialog = null)
        {
            dialog ??= new Dialog_ConstructionPlanConfig(
                new List<int> { target.thingIDNumber }, target.Map, Rect.zero);
            object[] presentationArgs = { target.thingIDNumber, null };
            bool found = (bool)AccessTools.Method(typeof(QualityJobsStore),
                "TryGetPlanPresentation").Invoke(store, presentationArgs);
            Require(found, "Missing construction presentation fixture");
            Type type = typeof(Dialog_ConstructionPlanConfig);
            if (autoBest) AccessTools.Method(type, "EnsureAutoBest").Invoke(dialog, null);
            AccessTools.Method(type, "EnsureStatus").Invoke(dialog,
                new[] { presentationArgs[1] });
            string bestLabel = (string)AccessTools.Field(type, "autoBestCurrentLabel").GetValue(dialog);
            string names = (string)AccessTools.Field(type, "statusNamesLabel").GetValue(dialog);
            int eligibleCount = (int)AccessTools.Field(type, "statusEligibleCount").GetValue(dialog);
            bool correctDisplay = eligibleCount == expectedEligible.Length;
            foreach (Pawn pawn in expectedEligible)
                correctDisplay &= names != null && names.Contains(pawn.LabelShort);
            if (autoBest)
                correctDisplay &= bestLabel == "QJ_AutoBestCurrent".Translate(expectedBest.LabelShort).ToString()
                    && (int)AccessTools.Field(type, "cachedAutoSkill").GetValue(dialog) == expectedSkill
                    && (bool)AccessTools.Field(type, "cachedAutoInspired").GetValue(dialog)
                        == (expectedBest.InspirationDef == InspirationDefOf.Inspired_Creativity);

            store.GameComponentUpdate();
            ManagedConstructionJob job = QualityJobsApi.GetManagedJobs().Jobs
                .OfType<ManagedConstructionJob>().Single(j => j.Targets.Contains(target));
            Probe.Record(id, correctDisplay && job.Settings.SkillGate == expectedSkill,
                "Construction dialog/API for " + target.def.defName
                + "; current best=" + (bestLabel ?? "<manual>")
                + "; eligible=" + eligibleCount + "; names=" + (names ?? "<none>")
                + "; API skill=" + job.Settings.SkillGate + "; expected skill=" + expectedSkill);
            return dialog;
        }

        private static void CheckRetainedConstructionDisplay(QualityJobsStore store,
            Frame frame, Dialog_ConstructionPlanConfig dialog, Pawn inspired, Pawn ordinary)
        {
            SkillRecord inspiredSkill = inspired.skills.GetSkill(SkillDefOf.Construction);
            SkillRecord ordinarySkill = ordinary.skills.GetSkill(SkillDefOf.Construction);
            int originalInspiredSkill = inspiredSkill.Level, originalOrdinarySkill = ordinarySkill.Level;
            Type dialogType = typeof(Dialog_ConstructionPlanConfig);
            var bestField = AccessTools.Field(dialogType, "autoBestCurrentLabel");
            var namesField = AccessTools.Field(dialogType, "statusNamesLabel");
            var factsRevision = AccessTools.Field(typeof(QualityJobsStore), "ExternalPawnFactsRevision");
            var statusRevision = AccessTools.Field(typeof(QualityJobsStore), "PlanStatusRevision");
            try
            {
                // The native skill setter is already patched for immediate
                // invalidation, even while paused. No synthetic revision bump
                // or simulated 250-tick access fallback is needed here.
                inspiredSkill.Level = 8;
                CheckConstructionDisplay("LR1-RETAINED-SKILL", store, frame, inspired,
                    new[] { inspired }, true, 8, dialog);
                object bestLabel = bestField.GetValue(dialog), namesLabel = namesField.GetValue(dialog);

                // A genuine dependency change still leaves the same inspired
                // winner. Reference assertions protect the cache equality
                // contract, which visible string equality alone cannot prove.
                ordinarySkill.Level = 13;
                CheckConstructionDisplay("LR1-RETAINED-EQUAL", store, frame, inspired,
                    new[] { inspired }, true, 8, dialog);
                bool equalRebuildReused = ReferenceEquals(bestLabel, bestField.GetValue(dialog))
                    && ReferenceEquals(namesLabel, namesField.GetValue(dialog));

                int beforeFacts = (int)factsRevision.GetValue(store);
                int beforeStatus = (int)statusRevision.GetValue(store);
                ManagedQualityJobsSnapshot publication = QualityJobsApi.GetManagedJobs();
                inspiredSkill.Level = 8;
                CheckConstructionDisplay("LR1-RETAINED-NOOP", store, frame, inspired,
                    new[] { inspired }, true, 8, dialog);
                bool unchangedReused = beforeFacts == (int)factsRevision.GetValue(store)
                    && beforeStatus == (int)statusRevision.GetValue(store)
                    && ReferenceEquals(bestLabel, bestField.GetValue(dialog))
                    && ReferenceEquals(namesLabel, namesField.GetValue(dialog))
                    && ReferenceEquals(publication, QualityJobsApi.GetManagedJobs());
                Probe.Record("LR1-RETAINED-CACHE", equalRebuildReused && unchangedReused,
                    "Same dialog: equal dependency refresh preserves label identity="
                    + equalRebuildReused + "; no-op skill assignment preserves revisions and identity="
                    + unchangedReused);
            }
            finally
            {
                inspiredSkill.Level = originalInspiredSkill;
                ordinarySkill.Level = originalOrdinarySkill;
            }
            CheckConstructionDisplay("LR1-RETAINED-RESTORED", store, frame, ordinary,
                new[] { ordinary }, true, 12, dialog);
        }

        private static ConstructionPlan PauseWithRealGate(QualityJobsStore store, Frame frame, Pawn worker)
        {
            Commands.ApplyPlanSettings(frame.thingIDNumber, 20, false, false, 0, false);
            ConstructionPlan plan = store.FindPlan(frame);
            Require(plan != null && worker.skills.GetSkill(SkillDefOf.Construction).Level < 20,
                "Cannot establish manual threshold fixture");
            frame.CompleteConstruction(worker);
            Require(!frame.Destroyed && plan.state == ConstructionPlanState.Paused,
                "Real completion gate did not pause the constructed frame");
            return plan;
        }

        private static Pawn MakeWorker(Map map, IntVec3 cell, int construction)
        {
            Pawn pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,
                Faction.OfPlayer, forceGenerateNewPawn: true, canGeneratePawnRelations: false,
                fixedBiologicalAge: 30f, forceNoIdeo: true, forceNoBackstory: true, forbidAnyTitle: true));
            GenSpawn.Spawn(pawn, cell, map);
            Require(!pawn.WorkTypeIsDisabled(WorkTypeDefOf.Construction), "Fixture cannot construct");
            pawn.workSettings.SetPriority(WorkTypeDefOf.Construction, 1);
            pawn.skills.GetSkill(SkillDefOf.Construction).Level = construction;
            pawn.skills.GetSkill(SkillDefOf.Crafting).Level = 20;
            pawn.skills.GetSkill(SkillDefOf.Artistic).Level = 20;
            if (pawn.needs.food != null) pawn.needs.food.CurLevel = pawn.needs.food.MaxLevel;
            if (pawn.needs.rest != null) pawn.needs.rest.CurLevel = pawn.needs.rest.MaxLevel;
            return pawn;
        }

        private static IntVec3 FindClear(Map map)
        {
            foreach (IntVec3 candidate in GenRadial.RadialCellsAround(map.Center, 80f, true))
            {
                bool clear = true;
                foreach (IntVec3 cell in CellRect.CenteredOn(candidate, 5))
                {
                    if (!cell.InBounds(map) || !cell.Standable(map) || cell.Fogged(map)
                        || cell.GetThingList(map).Any(t => t.def.category != ThingCategory.Plant
                            && t.def.category != ThingCategory.Filth)
                        || CellRect.CenteredOn(candidate, 2).Contains(cell)
                            && cell.GetThingList(map).Any(t => t.def.category == ThingCategory.Plant
                                && t.def.plant.harvestWork > ThingDefOf.Plant_Dandelion.plant.harvestWork))
                    { clear = false; break; }
                }
                if (clear) return candidate;
            }
            throw new InvalidOperationException("No clear finisher-review fixture area");
        }

        private static void Return(Job job) { if (job != null) JobMaker.ReturnToPool(job); }
        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); }
    }
}
