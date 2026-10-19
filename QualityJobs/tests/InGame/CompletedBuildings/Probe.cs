using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using HarmonyLib;
using QualityJobs;
using QualityJobs.Core;
using QualityJobs.UI;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace CompletedBuildingsVerification
{
    // Test-only main-thread observer. Real work givers and pawn jobs consume the
    // orders; no quality, resource or completion methods are replaced. UI clicks
    // arrive through Shared automation's process-scoped input pipe.
    [StaticConstructorOnStartup]
    public static partial class Probe
    {
        private static readonly Harmony Patches = new Harmony("eprime.qualityjobs.completedbuildingsprobe");
        private static readonly Dictionary<string, string> Points = new Dictionary<string, string>();
        private static int stage, settle, ticks, originalId, completions;
        private static Building_Bed bed;
        private static Pawn builder, owner;
        private static QualityJobsStore store;
        private static Map map;
        private static IntVec3 position;
        private static Rot4 rotation;
        private static ThingDef material, buildingDef;
        private static ColorDef Paint;
        private static ConstructionPlan active;
        private static string Root => GenFilePaths.SaveDataFolderPath;
        static Probe()
        {
            Patches.Patch(AccessTools.Method(typeof(Root_Play), "Update"), postfix: new HarmonyMethod(typeof(Probe), nameof(Update)));
            Patches.Patch(AccessTools.Method(typeof(Command_QualityJob), "GizmoOnGUI"), postfix: new HarmonyMethod(typeof(Probe), nameof(Gizmo)));
            Patches.Patch(AccessTools.Method(typeof(Widgets), "ButtonText", new[] {
                typeof(Rect), typeof(string), typeof(bool), typeof(bool), typeof(bool), typeof(TextAnchor?) }),
                prefix: new HarmonyMethod(typeof(Probe), nameof(Button)));
            Patches.Patch(AccessTools.Method(typeof(FloatMenuOption), "DoGUI"), prefix: new HarmonyMethod(typeof(Probe), nameof(Menu)));
            Patches.Patch(AccessTools.Method(typeof(Frame), "CompleteConstruction"), postfix: new HarmonyMethod(typeof(Probe), nameof(Completed)));
            Patches.Patch(AccessTools.Method(typeof(Dialog_ConstructionPlanConfig), "DoWindowContents"),
                postfix: new HarmonyMethod(typeof(Probe), nameof(VerifyDialogCaches)));
        }
        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            Log.Message("[CompletedBuildingsProbe] PASS " + message);
        }
        private static void Mark(string text) => File.WriteAllText(Path.Combine(Root, "completed-stage.txt"), text);
        private static object Snapshot(Thing thing)
        {
            object[] args = { thing.thingIDNumber, null };
            AccessTools.Method(typeof(QualityJobsStore), "TryGetCompletedBuilding").Invoke(store, args);
            return args[1];
        }
        private static int Revision => (int)AccessTools.Field(typeof(QualityJobsStore), "PlanStatusRevision").GetValue(store);
        private static void Point(string name, Vector2 local)
        {
            if (Event.current.type != EventType.Repaint) return;
            Vector2 point = GUIUtility.GUIToScreenPoint(local);
            point += GUIUtility.GUIToScreenPoint(Vector2.zero) * (Prefs.UIScale - 1f);
            string value = Math.Round(point.x).ToString(CultureInfo.InvariantCulture) + "," + Math.Round(point.y).ToString(CultureInfo.InvariantCulture);
            if (Points.TryGetValue(name, out string old) && old == value) return;
            Points[name] = value;
            File.WriteAllText(Path.Combine(Root, "completed-" + name + ".txt"), value);
        }
        private static void Gizmo(Vector2 topLeft) => Point("gizmo", topLeft + new Vector2(35f, 35f));
        private static void Button(Rect rect, string label)
        {
            if (Find.WindowStack.WindowOfType<Dialog_ConstructionPlanConfig>() == null) return;
            if (label == "Start rebuilding") Point("start", rect.center);
            if (label == "No retries" || label == "Good") Point("quality", rect.center);
        }
        private static void Menu(FloatMenuOption __instance, Rect rect)
        { if (__instance.Label == "Good") Point("good", rect.center); }
        private static void Completed(Frame __instance, Pawn worker)
        {
            if (worker == builder && __instance.Position == position && __instance.Destroyed)
            {
                completions++;
                Log.Message("[CompletedBuildingsProbe] pawn finished construction " + completions);
            }
        }
        private static void Update()
        {
            if (stage == 99 || Current.ProgramState != ProgramState.Playing
                || Find.CurrentMap == null || LongEventHandler.AnyEventNowOrWaiting) return;
            if (++settle < 10) return;
            try
            {
                if (stage == 0)
                {
                    Setup(); VerifyActivationRules(); VerifySettingsAndCacheLifecycle();
                    SelectBatch(); CameraJumper.TryJump(bed);
                    stage = 1; Mark("ready");
                }
                else if (stage == 1 && Find.WindowStack.WindowOfType<Dialog_ConstructionPlanConfig>() != null)
                {
                    Assert(store.FindPlan(bed) == null && Deconstruction(bed) == null,
                        "opening configuration leaves the completed building alone");
                    stage = 2; Mark("configure");
                }
                else if (stage == 2)
                {
                    var dialog = Find.WindowStack.WindowOfType<Dialog_ConstructionPlanConfig>();
                    if (dialog != null && (int)AccessTools.Field(dialog.GetType(), "minQuality").GetValue(dialog) == 3)
                    {
                        Assert(store.FindPlan(bed) == null && Deconstruction(bed) == null,
                            "target-quality edits remain local until Start rebuilding");
                        stage = 3; Mark("draft");
                    }
                }
                else if (stage == 3 && store.FindPlan(bed) != null)
                {
                    active = store.FindPlan(bed);
                    Assert(active.state == ConstructionPlanState.AwaitingRebuild && Deconstruction(bed) != null,
                        "Start rebuilding creates the replacement order through actual UI input");
                    VerifyBatchActivation();
                    active = RoundTrip(active, "awaiting");
                    stage = 4; Mark("active");
                }
                else if (stage == 4 && File.Exists(Path.Combine(Root, "completed-work.txt")))
                { stage = 5; ticks = 0; }
                else if (stage == 5)
                {
                    DriveWork();
                    Building_Bed replacement = Replacement();
                    if (replacement != null && store.FindPlan(replacement) == null)
                    {
                        Assert(completions > 0 && replacement.TryGetComp<CompQuality>().Quality >= QualityCategory.Good,
                            "pawn consumed the construction job and produced the requested quality");
                        Assert(replacement.Stuff == material && replacement.Rotation == rotation,
                            "replacement retains material, position and rotation");
                        Assert(replacement.PaintColorDef == Paint, "replacement retains original paint");
                        Assert(owner.ownership.OwnedBed == replacement, "replacement restores original bed owner");
                        bed = replacement; SetupCancelledReplacement(); stage = 6; ticks = 0;
                    }
                }
                else if (stage == 6)
                {
                    DriveWork();
                    if (active.target is Blueprint_Build || active.target is Frame)
                    {
                        Commands.RemovePlan(active.target.thingIDNumber);
                        Assert(active.restoreOnly, "cancelling after demolition retains settings without quality management");
                        BeginReload();
                    }
                }
                else if (stage == 7)
                {
                    DriveWork();
                    var replacement = Replacement();
                    if (replacement != null && store.FindPlan(replacement) == null)
                    {
                        Assert(replacement.Medical && replacement.ForPrisoners,
                            "cancelled replacement finishes with saved medical/prisoner settings");
                        Assert(replacement.PaintColorDef == Paint,
                            "paint survives cancellation and save/load during replacement");
                        Assert(Deconstruction(replacement) == null, "cancelled replacement does not retry");
                        bed = replacement; SetupFailedRollCancellation(); stage = 8; ticks = 0;
                    }
                }
                else if (stage == 8)
                {
                    DriveWork();
                    if (completions > completionBeforeCancellation) VerifyFailedRollCancellation();
                }
                else if (stage == 9 && !ReferenceEquals(Find.CurrentMap, map)) ResumeAfterReload();
            }
            catch (Exception error)
            {
                stage = 99; Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                Mark("FAILED " + error); Log.Error("[CompletedBuildingsProbe] FAILED " + error);
            }
        }
        private static Building_Bed Replacement() => position.GetThingList(map).OfType<Building_Bed>()
            .FirstOrDefault(b => b.def == buildingDef && b.thingIDNumber != originalId);
        private static Designation Deconstruction(Thing thing) => map.designationManager.DesignationOn(thing, DesignationDefOf.Deconstruct);
        private static void Setup()
        {
            map = Find.CurrentMap; store = QualityJobsStore.Active;
            Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
            // The player fixture includes jobs from excluded third-party mods.
            // Discard only those unresolved jobs in this disposable copy before
            // advancing simulation; they are unrelated to construction under test.
            foreach (Pawn pawn in map.mapPawns.AllPawns)
            {
                if (pawn.CurJob != null && pawn.CurJob.def == null)
                {
                    pawn.jobs.curJob = null; pawn.jobs.curDriver = null;
                    pawn.jobs.ClearQueuedJobs(); pawn.pather.StopDead();
                }
            }
            Find.WindowStack.TryRemove(typeof(Dialog_MessageBox), false);
            Find.WindowStack.TryRemove(typeof(LudeonTK.EditWindow_Log), false);
            AccessTools.Field(typeof(LudeonTK.EditWindow_Log), "canAutoOpen").SetValue(null, false);
            bed = map.listerBuildings.allBuildingsColonist.OfType<Building_Bed>()
                .First(b => b.def.defName == "Bed" && !b.ForPrisoners && b.TryGetComp<CompQuality>() != null);
            Commands.RemovePlan(bed.thingIDNumber);
            Assert(bed.GetGizmos().OfType<Command_QualityJob>().Any(), "completed player bed exposes Quality Job before it has a plan");
            builder = map.mapPawns.FreeColonists.First(p => !p.Downed && !p.Dead && !p.WorkTypeIsDisabled(WorkTypeDefOf.Construction));
            owner = map.mapPawns.FreeColonists.First(p => p != builder && !p.Dead && !p.Downed);
            builder.skills.GetSkill(SkillDefOf.Construction).Level = 20;
            builder.workSettings.SetPriority(WorkTypeDefOf.Construction, 1);
            builder.jobs.EndCurrentJob(JobCondition.InterruptForced);
            owner.jobs.EndCurrentJob(JobCondition.InterruptForced);
            owner.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.Wait, 999999));
            bed.ForOwnerType = BedOwnerType.Colonist; bed.Medical = false;
            bed.CompAssignableToPawn.TryAssignPawn(owner);
            bed.TryGetComp<CompQuality>().SetQuality(QualityCategory.Normal, null);
            Paint = DefDatabase<ColorDef>.AllDefsListForReading.First(c => c.colorType == ColorType.Structure);
            bed.ChangePaint(Paint);
            originalId = bed.thingIDNumber; position = bed.Position;
            rotation = bed.Rotation; material = bed.Stuff; buildingDef = bed.def;
            Commands.SetConstructionMinSkillDefault(0); Commands.SetConstructionRequireInspiredDefault(false);
            Commands.SetConstructionRequireSpecialistDefault(false); Commands.SetConstructionAutoBestDefault(false);
            Commands.SetConstructionTargetQualityDefault(0);
            foreach (var cost in bed.def.CostListAdjusted(bed.Stuff))
            {
                for (int i = 0; i < 4; i++)
                {
                    Thing resource = ThingMaker.MakeThing(cost.thingDef);
                    resource.stackCount = Math.Min(cost.thingDef.stackLimit, cost.count * 2);
                    GenPlace.TryPlaceThing(resource, position + new IntVec3(2 + i, 0, 2), map, ThingPlaceMode.Near);
                }
            }
        }
        private static void VerifyActivationRules()
        {
            int revision = Revision; object snapshot = Snapshot(bed);
            Commands.StartRebuild(bed.thingIDNumber, 20, false, false, 0, false);
            Commands.StartRebuild(bed.thingIDNumber, 20, false, false, 2, false);
            Assert(store.FindPlan(bed) == null && Revision == revision, "no-target and satisfied requests preserve revisions and create no plan");
            Commands.StartRebuild(bed.thingIDNumber, 0, false, false, 3, false);
            ConstructionPlan plan = store.FindPlan(bed);
            Assert(plan != null && Deconstruction(bed) != null, "below-target activation creates deconstruction order");
            revision = Revision;
            Commands.StartRebuild(bed.thingIDNumber, 0, false, false, 3, false);
            Assert(store.FindPlan(bed) == plan && Revision == revision, "repeated activation preserves plan and revisions");
            Assert(ReferenceEquals(snapshot, Snapshot(bed)), "plan edits preserve unrelated completed-building snapshot");
            Commands.SetPlanMinQuality(bed.thingIDNumber, 2);
            Assert(store.FindPlan(bed) == null && Deconstruction(bed) == null, "lowering target cancels deconstruction immediately while paused");
            var manual = new Designation(bed, DesignationDefOf.Deconstruct);
            map.designationManager.AddDesignation(manual);
            Commands.StartRebuild(bed.thingIDNumber, 0, false, false, 3, false); Commands.RemovePlan(bed.thingIDNumber);
            Assert(store.FindPlan(bed) == null && Deconstruction(bed) == manual, "activation/Clear leave unrelated deconstruction order intact");
            map.designationManager.RemoveDesignation(manual);
            var uninstall = new Designation(bed, DesignationDefOf.Uninstall);
            map.designationManager.AddDesignation(uninstall);
            Commands.StartRebuild(bed.thingIDNumber, 0, false, false, 3, false);
            Assert(store.FindPlan(bed) == null, "uninstall conflicts reject activation");
            map.designationManager.RemoveDesignation(uninstall);
            Commands.StartRebuild(bed.thingIDNumber, 0, false, false, 3, false);
            map.designationManager.RemoveDesignation(Deconstruction(bed));
            Assert(store.FindPlan(bed) == null, "vanilla cancellation removes plan immediately while paused");
            bed.TryGetComp<CompQuality>().SetQuality(QualityCategory.Good, null);
            Assert(!ReferenceEquals(snapshot, Snapshot(bed)), "quality mutation republishes completed-building snapshot");
            snapshot = Snapshot(bed);
            bed.TryGetComp<CompQuality>().SetQuality(QualityCategory.Good, null);
            Assert(ReferenceEquals(snapshot, Snapshot(bed)), "equal quality preserves snapshot identity");
            bed.TryGetComp<CompQuality>().SetQuality(QualityCategory.Normal, null);
        }
        private static ConstructionPlan RoundTrip(ConstructionPlan original, string name)
        {
            string path = Path.Combine(Root, "completed-save-" + name + ".xml");
            var saved = original;
            Scribe.saver.InitSaving(path, "CompletedBuildingRoundTrip"); Scribe_Deep.Look(ref saved, "plan"); Scribe.saver.FinalizeSaving();
            ConstructionPlan loaded = null;
            Scribe.loader.InitLoading(path); Scribe_Deep.Look(ref loaded, "plan");
            var directory = (LoadedObjectDirectory)AccessTools.Field(typeof(CrossRefHandler), "loadedObjectDirectory").GetValue(Scribe.loader.crossRefs);
            directory.RegisterLoaded(original.target);
            foreach (Pawn pawn in map.mapPawns.AllPawns) directory.RegisterLoaded(pawn);
            Scribe.loader.FinalizeLoading();
            Assert(loaded.target == original.target && loaded.state == original.state
                && loaded.minQuality == original.minQuality && loaded.restoreOnly == original.restoreOnly,
                "save/load preserves target, configuration and lifecycle: " + name);
            store.RemovePlan(original); store.AddPlan(loaded); return loaded;
        }
        private static void SetupCancelledReplacement()
        {
            bed.ForOwnerType = BedOwnerType.Prisoner; bed.Medical = true;
            bed.TryGetComp<CompQuality>().SetQuality(QualityCategory.Normal, null);
            Commands.StartRebuild(bed.thingIDNumber, 0, false, false, 3, false);
            active = store.FindPlan(bed);
            Assert(active != null, "completed replacement can be opted in again later");
            originalId = bed.thingIDNumber;
        }
        private static T Giver<T>() where T : WorkGiver_Scanner => (T)DefDatabase<WorkGiverDef>.AllDefsListForReading
            .First(d => d.Worker is T && d.workType == WorkTypeDefOf.Construction).Worker;
        private static void DriveWork()
        {
            if (ticks > 30000) throw new InvalidOperationException("Pawn did not complete replacement: " + builder.CurJob + " target=" + active.target);
            if (builder.needs.food != null) builder.needs.food.CurLevel = builder.needs.food.MaxLevel;
            if (builder.needs.rest != null) builder.needs.rest.CurLevel = builder.needs.rest.MaxLevel;
            if (builder.CurJob == null || builder.CurJob.def != JobDefOf.Deconstruct
                && builder.CurJob.def != JobDefOf.FinishFrame && builder.CurJob.def != JobDefOf.HaulToContainer)
            {
                Thing target = active.target;
                WorkGiver_Scanner giver = target is Blueprint_Build ? (WorkGiver_Scanner)Giver<WorkGiver_ConstructDeliverResourcesToBlueprints>()
                    : target is Frame frame ? frame.IsCompleted() ? (WorkGiver_Scanner)Giver<WorkGiver_ConstructFinishFrames>()
                        : Giver<WorkGiver_ConstructDeliverResourcesToFrames>() : Giver<WorkGiver_Deconstruct>();
                if (target != null && target.Spawned)
                {
                    Job job = giver.JobOnThing(builder, target, true);
                    if (job != null) builder.jobs.TryTakeOrderedJob(job);
                }
            }
            for (int i = 0; i < 90; i++)
            {
                Find.TickManager.DoSingleTick(); ticks++;
                if (stage == 6 && (active.target is Blueprint_Build || active.target is Frame)) break;
                if (stage == 8 && completions > completionBeforeCancellation) break;
                if (!store.plans.Contains(active)) break;
            }
        }
    }
}
