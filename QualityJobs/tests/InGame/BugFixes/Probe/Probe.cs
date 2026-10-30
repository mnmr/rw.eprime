using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using QualityJobs;
using QualityJobs.UI;
using RimWorld;
using UnityEngine;
using Verse;

namespace QualityJobsBugReview
{
    [StaticConstructorOnStartup]
    public static class Probe
    {
        private static bool ran;
        private static bool crafting;
        private static bool uiPending;
        private static Window billWindow;
        private static bool optionsShown;
        private static object originalBillPanel;
        private static Window optionsWindow;
        private static bool visualActive, waitingForCapture, needsResolutionNotice;
        private static string visualStage;
        private static Window visualWindow;
        private static float visualScale, visualDeadline;
        private static int visualRepaints, lastVisualRepaintFrame, resolutionNotices, initialResolutionNotices;
        public static Map Map;
        public static QualityJobsStore Store;
        public static readonly List<string> Results = new List<string>();
        static Probe()
        {
            var harmony = new Harmony("eprime.qualityjobs.bugreviewprobe");
            harmony.Patch(
                AccessTools.Method(typeof(Root_Play), "Update"),
                postfix: new HarmonyMethod(typeof(Probe), nameof(Update)));
            harmony.Patch(AccessTools.Method(typeof(Window), "InnerWindowOnGUI"),
                postfix: new HarmonyMethod(typeof(Probe), nameof(ObserveWindowRepaint)));
            harmony.Patch(AccessTools.Method(typeof(Window), nameof(Window.Notify_ResolutionChanged)),
                postfix: new HarmonyMethod(typeof(Probe), nameof(ObserveResolutionNotice)));
        }

        public static void Record(string id, bool observed, string detail)
        {
            string line = id + " passed=" + observed + " " + detail;
            Results.Add(line);
            Log.Message("[QualityJobsBugReview] " + line);
            File.WriteAllLines(Path.Combine(GenFilePaths.SaveDataFolderPath, "review-results.txt"), Results);
        }

        private static void Update()
        {
            if (visualActive)
            {
                UpdateVisualChecks();
                return;
            }
            if (ran && !crafting && !uiPending && !optionsShown
                && File.Exists(Path.Combine(GenFilePaths.SaveDataFolderPath, "review-open-options.txt")))
            {
                optionsShown = true;
                CloseWindows();
                var options = new Dialog_QualityJobs();
                Find.WindowStack.Add(options);
                Record("QJ011-OPTIONS", OnScreen(options),
                    "Options window=" + options.windowRect + "; screenLogical=" + Verse.UI.screenWidth
                    + "x" + Verse.UI.screenHeight);
                File.WriteAllText(Path.Combine(GenFilePaths.SaveDataFolderPath, "review-stage.txt"), "OPTIONS");
                return;
            }
            if (crafting)
            {
                try
                {
                    if (!HandoffChecks.Update()) return;
                }
                catch (Exception ex) { Record("HANDOFF-STAGED-ERROR", false, ex.ToString()); HandoffChecks.Cleanup(); }
                crafting = false;
                Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                Run("UI", UiChecks);
                uiPending = true;
                return;
            }
            if (uiPending)
            {
                uiPending = false;
                Run("UI-LABEL", CheckStatusLabel);
                if (File.Exists(Path.Combine(GenFilePaths.SaveDataFolderPath, "review-ui-scale-transitions.txt")))
                {
                    originalBillPanel = BillPanelFor(billWindow);
                    BeginVisualStage("BILL-125", billWindow, 1.25f);
                }
                else WriteStage("READY");
                return;
            }
            if (ran || Current.ProgramState != ProgramState.Playing
                || Find.CurrentMap == null || LongEventHandler.AnyEventNowOrWaiting) return;
            ran = true;
            Map = Find.CurrentMap;
            Store = QualityJobsStore.Active;
            Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
            AccessTools.Field(typeof(LudeonTK.EditWindow_Log), "canAutoOpen").SetValue(null, false);
            CloseWindows();
            Run("COPY", CopyChecks);
            Run("CLIPBOARD", () => ClipboardChecks.Run(Map, Store));
            Run("PERSISTENCE", () => PersistenceChecks.Run(Map, Store));
            Run("FINISHERS", () => FinisherChecks.Run(Map, Store));
            if (File.Exists(Path.Combine(GenFilePaths.SaveDataFolderPath, "review-ui-only.txt")))
            {
                Run("UI", UiChecks);
                uiPending = true;
                return;
            }
            Run("HANDOFF", () => HandoffChecks.Run(Map, Store));
            CloseWindows();
            crafting = true;
            Find.TickManager.CurTimeSpeed = TimeSpeed.Ultrafast;
            File.WriteAllText(Path.Combine(GenFilePaths.SaveDataFolderPath, "review-stage.txt"), "CRAFTING");
        }

        private static void CloseWindows()
        {
            for (int i = Find.WindowStack.Windows.Count - 1; i >= 0; i--)
                if (Find.WindowStack.Windows[i] is not MainTabWindow)
                    Find.WindowStack.TryRemove(Find.WindowStack.Windows[i], false);
        }

        private static void Run(string name, Action action)
        {
            try { action(); }
            catch (Exception ex) { Record(name + "-ERROR", false, ex.ToString()); }
        }

        private static void CopyChecks()
        {
            Commands.SetManageNewConstructionDefault(false);
            ThingDef bed = ThingDefOf.Bed;
            IntVec3 position = EmptySpot(bed, 18);
            Blueprint_Build blueprint = GenConstruct.PlaceBlueprintForBuild(bed, position,
                Map, Rot4.North, Faction.OfPlayer, ThingDefOf.WoodLog);
            Commands.ApplyPlanSettings(blueprint.thingIDNumber, 17, true, false, 5, true);
            Find.Selector.ClearSelection();
            Find.Selector.Select(blueprint);
            Command copy = BuildCopyCommandUtility.BuildCopyCommand(bed, ThingDefOf.WoodLog,
                null, null, false);
            if (copy == null) throw new Exception("No native build-copy command for bed.");
            copy.ProcessInput(new Event { type = EventType.MouseDown, button = 0 });
            // LR2: an unrelated native auto-rebuild can spawn while the player
            // is still deciding where to put the copied bed. Only the intended
            // designator placement may inherit this source's requirements.
            IntVec3 rebuildPosition = EmptySpot(bed, 22);
            var rebuildBed = (Building)ThingMaker.MakeThing(bed, ThingDefOf.WoodLog);
            rebuildBed.SetFaction(Faction.OfPlayer);
            GenSpawn.Spawn(rebuildBed, rebuildPosition, Map, Rot4.North);
            bool oldAutoRebuild = Find.PlaySettings.autoRebuild;
            bool oldHome = Map.areaManager.Home[rebuildPosition];
            try
            {
                Find.PlaySettings.autoRebuild = true;
                Map.areaManager.Home[rebuildPosition] = true;
                rebuildBed.Destroy(DestroyMode.KillFinalize);
                Thing rebuilt = Map.thingGrid.ThingAt(rebuildPosition, bed.blueprintDef);
                Record("LR2-AUTOREBUILD", rebuilt != null && Store.FindPlan(rebuilt) == null,
                    "Native auto-rebuild while copied bed remains selected; blueprint="
                    + (rebuilt != null) + "; inherited copy plan="
                    + (rebuilt != null && Store.FindPlan(rebuilt) != null));
            }
            finally
            {
                Find.PlaySettings.autoRebuild = oldAutoRebuild;
                Map.areaManager.Home[rebuildPosition] = oldHome;
            }
            var unrelatedDesignator = new Designator_Build(bed);
            unrelatedDesignator.SetStuffDef(ThingDefOf.WoodLog);
            IntVec3 unrelatedPosition = EmptySpot(bed, 23);
            unrelatedDesignator.DesignateSingleCell(unrelatedPosition);
            Thing independent = Map.thingGrid.ThingAt(unrelatedPosition, bed.blueprintDef);
            Record("LR2-INDEPENDENT", independent != null && Store.FindPlan(independent) == null,
                "Separate native build designator while copy remains selected; inherited copy plan="
                + (independent != null && Store.FindPlan(independent) != null));
            IntVec3 next = EmptySpot(bed, 24);
            Designator selected = Find.DesignatorManager.SelectedDesignator;
            if (selected == null || !selected.CanDesignateCell(next).Accepted)
                throw new Exception("Copy designator unavailable or destination invalid.");
            selected.DesignateSingleCell(next);
            Thing copied = Map.thingGrid.ThingAt(next, bed.blueprintDef);
            ConstructionPlan plan = copied == null ? null : Store.FindPlan(copied);
            Record("QJ001", copied != null && plan != null && plan.minSkill == 17
                && plan.requireInspired && !plan.requireSpecialist && plan.minQuality == 5 && plan.autoBest,
                "Native copy clicked; source skill=17 inspired=true target=Masterwork auto=true; copied="
                + (copied != null) + "; copiedHasPlan=" + (plan != null));

            AccessTools.Field(typeof(Designator_Place), "placingRot").SetValue(selected, Rot4.East);
            IntVec3 bulkFirst = EmptySpot(bed, 26);
            IntVec3 bulkSecond = IntVec3.Invalid;
            foreach (IntVec3 candidate in GenRadial.RadialCellsAround(bulkFirst, 30f, true))
            {
                if ((candidate - bulkFirst).LengthHorizontalSquared < 25) continue;
                bool empty = true;
                foreach (IntVec3 footprint in GenAdj.OccupiedRect(candidate, Rot4.East, bed.Size).ExpandedBy(2))
                    if (!footprint.InBounds(Map) || !footprint.Standable(Map)
                        || footprint.GetThingList(Map).Count != 0) { empty = false; break; }
                if (empty) { bulkSecond = candidate; break; }
            }
            if (!selected.CanDesignateCell(bulkFirst).Accepted
                || !bulkSecond.IsValid || !selected.CanDesignateCell(bulkSecond).Accepted)
                throw new Exception("Rotated multi-placement destinations invalid.");
            selected.DesignateMultiCell(new[] { bulkFirst, bulkSecond });
            bool bulkMatches = true;
            var bulkDetails = new List<string>();
            foreach (IntVec3 cell in new[] { bulkFirst, bulkSecond })
            {
                var bulk = Map.thingGrid.ThingAt(cell, bed.blueprintDef) as Blueprint_Build;
                ConstructionPlan bulkPlan = bulk == null ? null : Store.FindPlan(bulk);
                // Blueprints are not made from stuff themselves. Native
                // PlaceBlueprintForBuild records the future material here.
                bulkMatches &= bulk != null && bulk.Rotation == Rot4.East
                    && bulk.stuffToUse == ThingDefOf.WoodLog && bulkPlan != null
                    && bulkPlan.minSkill == 17 && bulkPlan.requireInspired
                    && !bulkPlan.requireSpecialist && bulkPlan.minQuality == 5 && bulkPlan.autoBest;
                bulkDetails.Add("cell=" + cell + ", blueprint=" + (bulk?.ToString() ?? "null")
                    + ", rotation=" + (bulk == null ? "null" : bulk.Rotation.AsInt.ToString())
                    + ", futureStuff=" + (bulk?.stuffToUse?.defName ?? "null")
                    + ", blueprintStuff=" + (bulk?.Stuff?.defName ?? "null")
                    + ", plan=" + (bulkPlan == null ? "null" : "skill:" + bulkPlan.minSkill
                        + ",inspired:" + bulkPlan.requireInspired + ",specialist:" + bulkPlan.requireSpecialist
                        + ",quality:" + bulkPlan.minQuality + ",auto:" + bulkPlan.autoBest));
            }
            Record("LR2-MULTI", bulkMatches,
                "Native two-cell placement expects skill17/inspired/quality5/auto, wood and East(1). "
                + string.Join(" | ", bulkDetails));
            Find.DesignatorManager.Deselect();
            // Vanilla caches build designators: selecting the same object anew
            // must not revive the previous copy input.
            Find.DesignatorManager.Select(selected);
            IntVec3 ordinaryPosition = EmptySpot(bed, 28);
            selected.DesignateSingleCell(ordinaryPosition);
            Thing unrelated = Map.thingGrid.ThingAt(ordinaryPosition, bed.blueprintDef);
            Record("QJ001-CLEAR", unrelated != null && Store.FindPlan(unrelated) == null,
                "Deselect and reuse the native designator; next ordinary blueprint remains unmanaged.");
            Find.DesignatorManager.Deselect();
        }

        public static IntVec3 EmptySpot(ThingDef def, int start)
        {
            for (int z = start; z < Map.Size.z - 10; z++)
                for (int x = start; x < Map.Size.x - 10; x++)
                {
                    var pos = new IntVec3(x, 0, z);
                    bool empty = true;
                    foreach (IntVec3 cell in GenAdj.OccupiedRect(pos, Rot4.North, def.Size).ExpandedBy(2))
                        if (!cell.InBounds(Map) || !cell.Standable(Map) || cell.GetThingList(Map).Count != 0)
                        { empty = false; break; }
                    if (empty) return pos;
                }
            throw new Exception("No empty footprint for " + def.defName);
        }

        private static void UiChecks()
        {
            Building_WorkTable bench = null;
            RecipeDef recipe = null;
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (!typeof(Building_WorkTable).IsAssignableFrom(def.thingClass)) continue;
                foreach (RecipeDef r in def.AllRecipes)
                    if (ManagedRecipes.IsManagedRecipe(r)) { recipe = r; break; }
                if (recipe == null) continue;
                bench = (Building_WorkTable)ThingMaker.MakeThing(def,
                    def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null);
                bench.SetFaction(Faction.OfPlayer);
                GenSpawn.Spawn(bench, EmptySpot(def, 34), Map, Rot4.North);
                break;
            }
            if (bench == null) throw new Exception("No quality bill worktable.");
            var bill = (Bill_ProductionWithUft)recipe.MakeNewBill();
            bench.BillStack.AddBill(bill);
            string id = bill.GetUniqueLoadID();
            Commands.SetBillMinSkill(id, Store.minSkillDefault == 20 ? 19 : 20);
            Commands.SetBillTargetQuality(id, 6);
            Commands.SetBillRequireInspired(id, true);
            BillUtility.Clipboard = bill.Clone();
            var pasted = (Bill_ProductionWithUft)BillUtility.Clipboard.Clone();
            pasted.InitializeAfterClone();
            bench.BillStack.AddBill(pasted);
            Record("QJ004", Store.ConfigFor(pasted).Equals(Store.ConfigFor(bill))
                && Store.TargetQualityFor(pasted) == Store.TargetQualityFor(bill),
                "Native clipboard Clone/InitializeAfterClone; original skill=" + Store.ConfigFor(bill).Condition.MinSkill
                + " target=" + Store.TargetQualityFor(bill) + "; pasted skill="
                + Store.ConfigFor(pasted).Condition.MinSkill + " target=" + Store.TargetQualityFor(pasted));
            if (File.Exists(Path.Combine(GenFilePaths.SaveDataFolderPath, "review-ui-scale-transitions.txt")))
            {
                bool targetCountAvailable = recipe.WorkerCounter.CanCountProducts(bill);
                if (targetCountAvailable)
                {
                    bill.repeatMode = BillRepeatModeDefOf.TargetCount;
                    bill.targetCount = 5;
                    bill.pauseWhenSatisfied = true;
                    bill.unpauseWhenYouHave = 2;
                    bill.includeEquipped = true;
                    bill.includeTainted = true;
                    bill.limitToAllowedStuff = true;
                }
                Record("QJ011-TARGET-COUNT", targetCountAvailable,
                    "Compact native bill uses target count 5, pause/unpause 2 and counting controls; recipe="
                    + recipe.defName);
            }
            CloseWindows();
            Find.WindowStack.Add(new Dialog_BillConfig(bill, bench.Position));
            Window window = Find.WindowStack.Windows[Find.WindowStack.Windows.Count - 1];
            billWindow = window;
            Record("QJ011-BILL", OnScreen(window),
                "Bill window=" + window.windowRect + "; screenLogical=" + Verse.UI.screenWidth
                + "x" + Verse.UI.screenHeight + "; UIScale=" + Prefs.UIScale);
        }

        private static void CheckStatusLabel()
        {
            object panel = BillPanelFor(billWindow);
            Type type = panel.GetType();
            int eligible = (int)AccessTools.Field(type, "statusEligibleCount").GetValue(panel);
            string label = (string)AccessTools.Field(type, "_statusFinishersLabel").GetValue(panel);
            Record("QJ012", eligible == 0 && label == "QJ_StatusFinishersNone".Translate().ToString(),
                "Real bill panel after rendering: eligible=" + eligible + "; label=" + (label ?? "<null>"));
        }

        private static object BillPanelFor(Window window)
        {
            Type integration = typeof(QualityJobsStore).Assembly.GetType("QualityJobs.UI.BillWindowIntegration");
            object table = AccessTools.Field(integration, "panels").GetValue(null);
            object[] args = { window, null };
            bool attached = (bool)table.GetType().GetMethod("TryGetValue").Invoke(table, args);
            if (!attached) throw new Exception("Bill panel missing");
            return args[1];
        }

        // The external script acknowledges each capture before the probe moves
        // on, so every image belongs to the exact asserted window/scale state.
        private static void BeginVisualStage(string stage, Window window, float scale)
        {
            visualActive = true;
            waitingForCapture = false;
            visualStage = stage;
            visualWindow = window;
            visualScale = scale;
            visualRepaints = 0;
            lastVisualRepaintFrame = -1;
            initialResolutionNotices = resolutionNotices;
            needsResolutionNotice = Prefs.UIScale != scale;
            // Fail before the shared run guard's ten-second paused-input limit.
            // The driving script supplies one hover-leave input for each stage.
            visualDeadline = Time.realtimeSinceStartup + 5f;
            WriteStage("WAIT-" + stage);
            if (needsResolutionNotice)
            {
                // 1.7's safe setter accepts the preference before system DPI;
                // our assertions and the window geometry use effective scale.
                ResolutionUtility.SafeSetUIScale(scale / ResolutionUtility.DpiScale);
                // Dialog_ResolutionConfirm's native Keep button only closes it.
                Find.WindowStack.WindowOfType<Dialog_ResolutionConfirm>()?.Close(false);
            }
        }

        private static void ObserveResolutionNotice(Window __instance)
        {
            if (visualActive && ReferenceEquals(__instance, visualWindow)) resolutionNotices++;
        }

        private static void ObserveWindowRepaint(Window __instance)
        {
            if (!visualActive || waitingForCapture || !ReferenceEquals(__instance, visualWindow)
                || Event.current == null || Event.current.type != EventType.Repaint) return;
            if (lastVisualRepaintFrame == Time.frameCount) return;
            lastVisualRepaintFrame = Time.frameCount;
            if (VisualGeometryReady()) visualRepaints++;
            else visualRepaints = 0;
        }

        private static bool VisualGeometryReady()
        {
            if (!visualWindow.IsOpen || Prefs.UIScale != visualScale
                || Verse.UI.screenWidth != Mathf.RoundToInt(Screen.width / visualScale)
                || Verse.UI.screenHeight != Mathf.RoundToInt(Screen.height / visualScale)
                || needsResolutionNotice && resolutionNotices == initialResolutionNotices) return false;
            float width = ReferenceEquals(visualWindow, billWindow)
                ? Mathf.Min(1080f, Verse.UI.screenWidth)
                : Mathf.Min(1180f, Verse.UI.screenWidth - 16f);
            float height = ReferenceEquals(visualWindow, billWindow)
                ? 664f : Mathf.Min(760f, Verse.UI.screenHeight - 16f);
            Rect expected = new Rect((Verse.UI.screenWidth - width) / 2f,
                (Verse.UI.screenHeight - height) / 2f, width, height).Rounded();
            return visualWindow.windowRect == expected && OnScreen(visualWindow);
        }

        private static void UpdateVisualChecks()
        {
            try
            {
                if (Time.realtimeSinceStartup > visualDeadline)
                    throw new TimeoutException("UI-scale stage " + visualStage
                        + (waitingForCapture ? " awaiting capture acknowledgement" : " awaiting native resize/repaint")
                        + "; open=" + visualWindow.IsOpen + "; scale=" + Prefs.UIScale
                        + "; requested scale=" + visualScale + "; physical=" + Screen.width + "x" + Screen.height
                        + "; logical=" + Verse.UI.screenWidth + "x"
                        + Verse.UI.screenHeight + "; bounds=" + visualWindow.windowRect
                        + "; initial size=" + visualWindow.InitialSize
                        + "; native notices=" + (resolutionNotices - initialResolutionNotices)
                        + "; ready repaints=" + visualRepaints);
                if (waitingForCapture)
                {
                    string ackPath = Path.Combine(GenFilePaths.SaveDataFolderPath,
                        "review-captured-" + visualStage + ".txt");
                    if (!File.Exists(ackPath)) return;
                    AdvanceVisualStage();
                    return;
                }
                if (!VisualGeometryReady() || visualRepaints < 2) return;
                bool valid = true;
                string detail = "same window open=" + visualWindow.IsOpen
                    + "; bounds=" + visualWindow.windowRect + "; logical=" + Verse.UI.screenWidth
                    + "x" + Verse.UI.screenHeight + "; scale=" + Prefs.UIScale
                    + "; native notices=" + (resolutionNotices - initialResolutionNotices)
                    + "; ready repaints=" + visualRepaints;
                if (ReferenceEquals(visualWindow, billWindow))
                {
                    object panel = BillPanelFor(billWindow);
                    Type type = panel.GetType();
                    int eligible = (int)AccessTools.Field(type, "statusEligibleCount").GetValue(panel);
                    string label = (string)AccessTools.Field(type, "_statusFinishersLabel").GetValue(panel);
                    valid = ReferenceEquals(panel, originalBillPanel) && eligible == 0
                        && label == "QJ_StatusFinishersNone".Translate().ToString();
                    detail += "; same panel=" + ReferenceEquals(panel, originalBillPanel)
                        + "; eligible=" + eligible + "; label=" + (label ?? "<null>");
                }
                else valid = ReferenceEquals(visualWindow, optionsWindow);
                Record("QJ011-" + visualStage, valid, detail);
                if (visualStage == "OPTIONS-125")
                    Record("QJ011-OPTIONS", valid, detail);
                waitingForCapture = true;
                visualDeadline = Time.realtimeSinceStartup + 8f;
                WriteStage(visualStage);
            }
            catch (Exception ex)
            {
                Record("QJ011-SCALE-ERROR", false, ex.ToString());
                visualActive = false;
                WriteStage("UI-SCALE-FAILED");
            }
        }

        private static void AdvanceVisualStage()
        {
            switch (visualStage)
            {
                case "BILL-125": BeginVisualStage("BILL-100", billWindow, 1f); break;
                case "BILL-100": BeginVisualStage("BILL-125-RESTORED", billWindow, 1.25f); break;
                case "BILL-125-RESTORED":
                    CloseWindows();
                    optionsShown = true;
                    optionsWindow = new Dialog_QualityJobs();
                    Find.WindowStack.Add(optionsWindow);
                    BeginVisualStage("OPTIONS-125", optionsWindow, 1.25f);
                    break;
                case "OPTIONS-125": BeginVisualStage("OPTIONS-100", optionsWindow, 1f); break;
                case "OPTIONS-100": BeginVisualStage("OPTIONS-125-RESTORED", optionsWindow, 1.25f); break;
                case "OPTIONS-125-RESTORED":
                    visualActive = false;
                    WriteStage("UI-SCALE-COMPLETE");
                    break;
                default: throw new InvalidOperationException("Unknown visual stage " + visualStage);
            }
        }

        private static void WriteStage(string stage)
        {
            File.WriteAllText(Path.Combine(GenFilePaths.SaveDataFolderPath, "review-stage.txt"), stage);
        }

        private static bool OnScreen(Window window)
            => window.windowRect.x >= 0 && window.windowRect.y >= 0
                && window.windowRect.xMax <= Verse.UI.screenWidth
                && window.windowRect.yMax <= Verse.UI.screenHeight;
    }
}
