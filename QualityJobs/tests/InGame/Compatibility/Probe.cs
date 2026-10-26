using System;
using System.IO;
using System.Globalization;
using HarmonyLib;
using QualityJobs;
using RimWorld;
using UnityEngine;
using Verse;

namespace QualityJobsCompatibilityProbe
{
    // Runs only in the explicitly activated, isolated test mod. A foreign bill
    // window is a real subclass with observable open/draw/close callbacks: the
    // regression is losing that instance, rather than its implementation type.
    [StaticConstructorOnStartup]
    public static class Probe
    {
        private static readonly Harmony Patches = new Harmony("QJ.CompatibilityProbe");
        private static int settle;
        private static string command;
        private static Bill_ProductionWithUft bill;
        private static Building_WorkTable bench;
        private static Window window;
        private static ForeignBillWindow foreign;
        private static bool provideForeign;
        private static bool customReady;
        private static string Root => GenFilePaths.SaveDataFolderPath;
        static Probe()
        {
            Patches.Patch(AccessTools.Method(typeof(Root_Play), "Update"), postfix: new HarmonyMethod(typeof(Probe), nameof(Update)));
            Patches.Patch(AccessTools.Method(typeof(Bill_Production), "GetBillDialog"),
                prefix: new HarmonyMethod(typeof(Probe), nameof(ForeignFactory)) { priority = Priority.First });
        }
        public static bool ForeignFactory(Bill_Production __instance, ref Window __result)
        {
            if (!provideForeign || __instance != bill) return true;
            foreign = new ForeignBillWindow(bill, bench.Position);
            __result = foreign;
            return false;
        }
        private static void Write(string file, string text) => File.WriteAllText(Path.Combine(Root, "compat-" + file + ".txt"), text);
        private static void Update()
        {
            if (Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null || LongEventHandler.AnyEventNowOrWaiting) return;
            if (++settle < 15) return;
            try
            {
                if (bill == null) Setup();
                string path = Path.Combine(Root, "compat-command.txt");
                string next = File.Exists(path) ? File.ReadAllText(path).Trim() : "normal";
                if (next == command)
                {
                    if (next == "custom" && !customReady && foreign != null && foreign.Draws >= 8)
                    { customReady = true; Write("stage", "custom"); }
                    return;
                }
                command = next;
                if (next == "interacted")
                {
                    bool changed = !QualityJobsStore.Active.ConfigFor(bill).Managed && bill.repeatCount == 2;
                    Write("interaction", changed && ReferenceEquals(window, foreign) ? "PASS" : "FAIL: bill controls did not update their models");
                    Rect before = window.windowRect;
                    window.Notify_ResolutionChanged();
                    window.Notify_ResolutionChanged();
                    Write("resolution", window.windowRect == before ? "PASS" : "FAIL: repeated resolution notification changed window geometry");
                    Write("stage", next);
                    return;
                }
                if (window != null) { window.Close(false); window = null; }
                if (next == "normal" || next == "custom")
                {
                    provideForeign = next == "custom";
                    window = (Window)AccessTools.Method(typeof(Bill_Production), "GetBillDialog").Invoke(bill, null);
                    Find.WindowStack.Add(window);
                    Write("rect", string.Join(",", window.windowRect.x, window.windowRect.y, window.windowRect.width, window.windowRect.height));
                    if (provideForeign)
                    {
                        bool retained = ReferenceEquals(window, foreign) && foreign.Opened;
                        Write("identity", retained ? "PASS" : "FAIL: foreign bill window was replaced");
                        Log.Message("[CompatibilityProbe] " + (retained ? "PASS" : "FAIL") + " foreign bill window retained");
                    }
                    customReady = false;
                    Write("stage", next == "custom" ? "opening-custom" : next);
                }
                else if (next == "closed")
                {
                    Write("lifecycle", foreign != null && foreign.Closed && foreign.Draws > 0 ? "PASS" : "FAIL: foreign window callbacks lost");
                    Write("stage", next);
                }
                else if (next == "warning" || next == "warning-none" || next == "warning-tiled")
                {
                    QualityJobsMod.Settings.compatibilityWarningsSeen.Clear();
                    QualityJobsMod.Settings.welcomeShownSaves.Clear();
                    AccessTools.Method(typeof(QualityJobsStore), "QueueWelcome").Invoke(null, null);
                    window = Find.WindowStack.Windows[Find.WindowStack.Count - 1];
                    if (next == "warning-tiled")
                    {
                        foreach (string name in new[] { "WorkRoles", "EPrimeReadouts", "Implanner" })
                        {
                            Type type = null;
                            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                                if (assembly.GetName().Name == name) type = assembly.GetType("RimShared.UiLib.WelcomeDialog", true);
                            if (type == null) throw new InvalidOperationException("Missing welcome host " + name);
                            Find.WindowStack.Add((Window)Activator.CreateInstance(type, name,
                                "A second welcome uses part of the available screen space.", "Open settings.", "Open now",
                                "", new Rect(), (Action)NoOp));
                        }
                    }
                    string warning = (string)AccessTools.Field(window.GetType(), "conflictText").GetValue(window);
                    bool expected = next == "warning-none"
                        ? string.IsNullOrEmpty(warning) && (float)AccessTools.Field(window.GetType(), "conflictHeight").GetValue(window) == 0f
                        : warning != null && warning.Contains("QualityBuilder")
                            && warning.Contains("No Job Authors") && warning.Contains("Finish it!");
                    Write("warning", expected ? "PASS" : "FAIL: expected active conflict warnings");
                    if (next == "warning" || next == "warning-tiled")
                    {
                        Rect viewport = (Rect)AccessTools.Field(window.GetType(), "viewportRect").GetValue(window);
                        Rect content = (Rect)AccessTools.Field(window.GetType(), "contentRect").GetValue(window);
                        Rect conflict = (Rect)AccessTools.Field(window.GetType(), "conflictRect").GetValue(window);
                        bool needsScroll = content.height > viewport.height;
                        bool constrained = next == "warning-tiled" || UI.screenHeight < 700f;
                        Rect original = window.windowRect;
                        window.Notify_ResolutionChanged();
                        window.Notify_ResolutionChanged();
                        bool fits = original.y >= 0f && original.yMax <= UI.screenHeight - 35f;
                        bool correct = fits && needsScroll == constrained && conflict.y == 0f
                            && window.windowRect == original && (constrained || original.height > 520f)
                            && warning.Contains("Remove the mods listed above") && !warning.Contains("We recommend");
                        Write("welcome-sizing", correct ? "PASS" : "FAIL: welcome sizing or wording");
                        Write("welcome-geometry", $"screen={UI.screenWidth}x{UI.screenHeight}; scale={Prefs.UIScale}; window={original}; viewport={viewport}; content={content}; scrolling={needsScroll}");
                    }
                    Write("stage", next);
                }
                else if (next == "acknowledged")
                {
                    int count = Find.WindowStack.Count;
                    AccessTools.Method(typeof(QualityJobsStore), "QueueWelcome").Invoke(null, null);
                    Write("acknowledged", Find.WindowStack.Count == count ? "PASS" : "FAIL: repeated warning");
                    Write("stage", next);
                }
            }
            catch (Exception ex) { Write("stage", "FAILED: " + ex); Log.Error("[CompatibilityProbe] " + ex); command = "failed"; }
        }
        private static void Setup()
        {
            Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
            Prefs.DevMode = false;
            for (int i = Find.WindowStack.Count - 1; i >= 0; i--) Find.WindowStack[i].Close(false);
            Find.Selector.ClearSelection();
            Map map = Find.CurrentMap;
            // Missing jobs belong to mods removed from this copied save only.
            foreach (Pawn pawn in map.mapPawns.AllPawns)
                if (pawn.CurJob != null && pawn.CurJob.def == null)
                {
                    pawn.jobs.curJob = null; pawn.jobs.curDriver = null;
                    pawn.jobs.ClearQueuedJobs(); pawn.pather.StopDead();
                }
            foreach (Thing thing in map.listerThings.AllThings)
            {
                if (!(thing is Building_WorkTable table)) continue;
                foreach (RecipeDef recipe in table.def.AllRecipes)
                {
                    if (!ManagedRecipes.IsManagedRecipe(recipe)) continue;
                    bench = table;
                    bill = (Bill_ProductionWithUft)recipe.MakeNewBill();
                    table.BillStack.AddBill(bill);
                    Commands.SetBillManaged(bill.GetUniqueLoadID(), true);
                    goto Found;
                }
            }
            Found:
            if (bill == null) throw new InvalidOperationException("No quality workbench in fixture");
            Find.WindowStack.Add(new Backdrop());
            Log.Message("[CompatibilityProbe] fixture " + bench.def.defName + " / " + bill.recipe.defName);
        }
        private sealed class Backdrop : Window
        {
            public Backdrop() { layer = WindowLayer.Dialog; doWindowBackground = false; preventCameraMotion = false; }
            public override Vector2 InitialSize => new Vector2(UI.screenWidth, UI.screenHeight);
            public override void DoWindowContents(Rect rect) => Widgets.DrawBoxSolid(new Rect(-20f, -20f, UI.screenWidth + 40f, UI.screenHeight + 40f), Color.black);
        }
        private static void NoOp() { }
        private sealed class ForeignBillWindow : Dialog_BillConfig
        {
            public bool Opened, Closed;
            public int Draws;
            public ForeignBillWindow(Bill_Production value, IntVec3 position) : base(value, position) { }
            public override void PreOpen() { base.PreOpen(); Opened = true; }
            public override void PostClose() { base.PostClose(); Closed = true; }
            public override void DoWindowContents(Rect rect)
            {
                base.DoWindowContents(rect); Draws++;
                Widgets.Label(new Rect(0f, rect.height - 65f, 220f, 25f), "Foreign window retained");
            }
        }
    }
}
