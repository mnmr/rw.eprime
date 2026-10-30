using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using WorkRoles;
using WorkRoles.Core;
using WorkRoles.Core.Recs;
using WorkRoles.UI;

namespace WorkRolesMechanicalReview
{
    [StaticConstructorOnStartup]
    public static partial class Probe
    {
        internal static Map map;
        internal static Pawn[] workers = Array.Empty<Pawn>();
        internal static Pawn[] humans;
        internal static object view;
        internal static string Root => GenFilePaths.SaveDataFolderPath;
        private static bool started, done;
        private static int frames, scene, failures;
        internal static bool ReferenceDrawing;
        internal static string Scene => scene < sceneNames.Length ? sceneNames[scene] : "completed";
        private static readonly string[] sceneNames = { "human-short", "human-long", "slave-short", "slave-long", "after-toggle", "reference-control", "human-hover", "robot-long", "robot-grouped", "priority-grid" };
        private static bool referenceCaptured;
        private static bool nativeStarted;
        private static bool NativePhase => File.Exists(Path.Combine(Root, "review-phase.txt")) && File.ReadAllText(Path.Combine(Root, "review-phase.txt")).Trim() == "native";
        private static Dictionary<int, string> originalHumanPriorities;
        private static Dictionary<int, string> originalHumanRecommendations;

        internal static object Field(object target, string name) => AccessTools.Field(target.GetType(), name).GetValue(target);
        internal static object Property(object target, string name) => AccessTools.Property(target.GetType(), name).GetValue(target);
        internal static object Invoke(object target, string name, params object[] args) =>
            AccessTools.GetDeclaredMethods(target.GetType()).Single(m => m.Name == name && m.GetParameters().Length == args.Length).Invoke(target, args);
        internal static Type Wr(string name) => typeof(RoleStore).Assembly.GetType("WorkRoles." + name, true);
        internal static void Mark(string value) => File.WriteAllText(Path.Combine(Root, "review-stage.txt"), value);
        internal static void Record(string key, object value) => File.AppendAllText(Path.Combine(Root, "review-observations.txt"), key + "=" + value + Environment.NewLine);
        internal static void Check(bool value, string message)
        {
            if (!value) failures++;
            string line = (value ? "PASS " : "FAIL ") + message;
            File.AppendAllText(Path.Combine(Root, "review-checks.txt"), line + Environment.NewLine);
            Log.Message("[MechanicalReview] " + line);
        }

        static Probe()
        {
            var harmony = new Harmony("eprime.workroles.mechanicalreview");
            harmony.Patch(AccessTools.Method(typeof(Root_Play), "Update"), postfix: new HarmonyMethod(typeof(Probe), nameof(Update)));
            InstallVisualReference(harmony);
        }

        private static void Update()
        {
            if (done || Current.ProgramState != ProgramState.Playing || LongEventHandler.AnyEventNowOrWaiting) return;
            try
            {
                if (NativeReloadPending)
                {
                    if (FinishNativeReloadCheck()) { done = true; Mark(failures == 0 ? "SUCCESS" : "COMPLETE " + failures + " failures"); }
                    return;
                }
                if (Find.CurrentMap == null) return;
                if (!started) { started = true; Setup(); return; }
                if (NativePhase)
                {
                    if (++frames < 4 || nativeStarted) return;
                    nativeStarted = true;
                    RunNativeChecks();
                    if (!NativeReloadPending) { done = true; Mark(failures == 0 ? "SUCCESS" : "COMPLETE " + failures + " failures"); }
                    return;
                }
                if (++frames == 4)
                {
                    if (!ReferenceDrawing) VerifyScene();
                    if (Scene == "human-hover") File.WriteAllText(Path.Combine(Root, "review-hover.txt"),
                        LastTagCenter.x.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," + LastTagCenter.y.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    Mark("CAPTURE " + Scene + (ReferenceDrawing ? "-reference" : "-current"));
                }
                string capture = Scene + (ReferenceDrawing ? "-reference" : "-current");
                if (frames < 4 || !File.Exists(Path.Combine(Root, "captured-" + capture))) return;
                FlushGeometry();
                if (Scene == "human-hover")
                {
                    Type presenter = typeof(RoleStore).Assembly.GetType("RimShared.UiLib.StructuredTipPresenter");
                    object frozen = AccessTools.Field(presenter, "frozen").GetValue(null);
                    Check(frozen == null || !((string)Property(frozen, "PlainText")).Contains(humans[0].LabelShort),
                        "both-OFF selected human name retains baseline absence of new pawn tooltip");
                }
                if (scene <= 5 && !referenceCaptured)
                {
                    ReferenceDrawing = true;
                    referenceCaptured = true;
                    frames = 0;
                    return;
                }
                ReferenceDrawing = false;
                referenceCaptured = false;
                if (++scene == sceneNames.Length)
                {
                    done = true;
                    Mark(failures == 0 ? "SUCCESS" : "COMPLETE " + failures + " failures");
                    return;
                }
                PrepareScene();
                frames = 0;
            }
            catch (Exception ex)
            {
                done = true;
                Record("EXCEPTION", ex);
                Mark("FAILED " + ex.GetType().Name);
            }
        }

        private static void Setup()
        {
            map = Find.CurrentMap;
            Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
            if (!ModsConfig.BiotechActive)
            {
                // The copied player save used Biotech. Native no-DLC humans have
                // an empty tracker (PawnComponentsUtility), not unresolved genes.
                // Normalize only this disposable fixture's missing-DLC trackers.
                int repaired = 0;
                foreach (Pawn pawn in map.mapPawns.AllPawns)
                    if (pawn.genes != null && pawn.genes.GenesListForReading.Any(g => g == null || g.def == null))
                    {
                        pawn.genes = new Pawn_GeneTracker(pawn);
                        pawn.Notify_DisabledWorkTypesChanged();
                        repaired++;
                    }
                Record("missing-dlc-gene-trackers-normalized", repaired);
            }
            Type log = AccessTools.TypeByName("LudeonTK.EditWindow_Log");
            AccessTools.Field(log, "canAutoOpen").SetValue(null, false);
            AccessTools.Field(log, "wantsToOpen").SetValue(null, false);
            foreach (Window popup in Find.WindowStack.Windows.ToArray()) if (!(popup is MainTabWindow)) popup.Close(false);
            Check(!RoleStore.Current.mechanicalWorkersEnabled && !RoleStore.Current.mechanicalRecommendationsEnabled, "old source save loads with both mechanical options off");
            RoleCommands.SetMechanicalWorkersEnabled(false);
            RoleCommands.SetMechanicalRecommendationsEnabled(false);
            humans = map.mapPawns.FreeColonistsSpawned.Where(p => p.RaceProps.Humanlike && !p.Downed && !p.DevelopmentalStage.Baby()).Take(4).ToArray();
            if (humans.Length != 4) throw new InvalidOperationException("Four native adult human fixtures required");
            humans[0].Name = new NameSingle("WR Bo");
            humans[1].Name = new NameSingle("WR Alexander Maximilian Sonnenblumenstrasse");
            humans[2].Name = new NameSingle("WR Li");
            humans[3].Name = new NameSingle("WR Elisabeth Charlottenburg Sondergaard");
            humans[2].guest.SetGuestStatus(Faction.OfPlayer, GuestStatus.Slave);
            humans[3].guest.SetGuestStatus(Faction.OfPlayer, GuestStatus.Slave);
            Check(humans[2].IsSlaveOfColony && humans[3].IsSlaveOfColony, "short and long native slave fixtures are in player faction");
            // A real backstory incapability leaves skillless hauling Neutral in d47bdc4c.
            humans[0].story.Childhood = DefDatabase<BackstoryDef>.AllDefsListForReading.First(b => b.slot == BackstorySlot.Childhood && (b.workDisables & WorkTags.ManualDumb) != 0);
            humans[0].Notify_DisabledWorkTypesChanged();
            Check(humans[0].WorkTypeIsDisabled(WorkTypeDefOf.Hauling), "human fixture is incapable of hauling through native backstory");
            RoleCommands.CreateRole("WR review hauling");
            Role haul = RoleStore.Current.roles.Single(r => r.label == "WR review hauling");
            RoleCommands.AddEntry(haul.id, new JobEntry(JobEntryKind.WorkGiver, "HaulGeneral"));
            RoleCommands.AssignRole(humans[0], haul.id);
            originalHumanPriorities = HumanPriorities();
            originalHumanRecommendations = HumanRecommendations();
            Record("ui-scale", Prefs.UIScale);
            Record("baseline-drawing-commit", "d47bdc4c");
            WorkRolesMod.Settings.groupBy = "none";
            WorkRolesMod.Settings.verdictsOnColonistChips = true;
            WorkRolesMod.Settings.verdictsInPalette = true;
            WorkRolesMod.Settings.verdictsOnRecommendationChips = true;
            Find.Selector.ClearSelection(); Find.Selector.Select(humans[0]);
            var button = DefDatabase<MainButtonDef>.AllDefsListForReading.First(d => d.TabWindow is MainTabWindow_WorkRoles);
            Find.MainTabsRoot.SetCurrentTab(button);
            view = Field(button.TabWindow, "colonistsTab");
            PropertySetter(Field(view, "rosterState"), "Search", "WR ");
            if (NativePhase) scene = 7;
            PrepareScene();
        }

        internal static void PropertySetter(object target, string name, object value) => AccessTools.Property(target.GetType(), name).SetValue(target, value);
        private static void Select(Pawn pawn)
        {
            object selection = Field(view, "selection");
            var click = AccessTools.Method(selection.GetType(), "Click");
            click.Invoke(selection, click.GetParameters().Select((p, i) => i == 0 ? (object)pawn : p.DefaultValue).ToArray());
        }
        private static void PrepareScene()
        {
            if (scene < 4) Select(humans[scene]);
            else if (scene == 4)
            {
                RoleCommands.SetMechanicalWorkersEnabled(true);
                RoleCommands.SetMechanicalRecommendationsEnabled(true);
                RoleCommands.SetMechanicalRecommendationsEnabled(false);
                RoleCommands.SetMechanicalWorkersEnabled(false);
                Select(humans[1]);
                Check(HumanPriorities().All(p => originalHumanPriorities[p.Key] == p.Value), "human priorities survive ON to both-OFF transition");
                Check(HumanRecommendations().All(p => originalHumanRecommendations[p.Key] == p.Value), "human ordered recommendations survive ON to both-OFF transition");
            }
            else if (scene == 5 || scene == 6) Select(humans[0]);
            else if (scene == 7)
            {
                RoleCommands.SetMechanicalWorkersEnabled(true);
                IntVec3 cell = map.AllCells.First(c => c.x > 8 && c.z > 8 && CellRect.CenteredOn(c, 5, 5).Cells.All(x => x.Standable(map) && x.GetEdifice(map) == null));
                var factory = AccessTools.Method(AccessTools.TypeByName("AIRobot.X2_Building_AIRobotCreator"), "CreateRobot", new[] { typeof(string), typeof(IntVec3), typeof(Map) });
                workers = new[] { (Pawn)factory.Invoke(null, new object[] { "AIRobot_Cleaner", cell, map }), (Pawn)factory.Invoke(null, new object[] { "RPP_Bot_Omni_V", cell + IntVec3.East * 3, map }) };
                workers[0].Name = new NameSingle("WR Cleaner");
                workers[1].Name = new NameSingle("WR Mehrzweckroboter mit aussergewoehnlich langem Namen 12345");
                RoleCommands.PasteRoleSet(workers[1], new List<RoleAssignment>());
                Select(workers[1]);
            }
            else if (scene == 8) { WorkRolesMod.Settings.groupBy = "race"; Select(workers[1]); }
            else if (scene == 9) Find.WindowStack.Add(new Dialog_PriorityGrid(humans.Concat(workers).ToList()));
        }

        private static void VerifyScene()
        {
            if (scene == 0)
            {
                var verdicts = (IDictionary)Invoke(view, "VerdictsFor", humans[0]);
                Role haul = RoleStore.Current.roles.Single(r => r.label == "WR review hauling");
                object verdict = verdicts[haul.id];
                object bucket = AccessTools.Field(verdict.GetType(), "Item2").GetValue(verdict);
                Record("human-disabled-hauling-bucket", bucket);
                Check(bucket.ToString() == "Neutral", "human incapable hauling retains pre-feature Neutral suitability while both options off");
                var roster = ((IEnumerable)Invoke(view, "ListedPawns")).Cast<Pawn>().ToArray();
                Check(humans.All(roster.Contains), "actual both-OFF roster contains ordinary colonists and slaves");
            }
            if (scene == 7) VerifyMechanicalPreview();
            if (scene == 9) VerifyNativeGrid();
        }

        private static Dictionary<int, string> HumanPriorities() => humans.ToDictionary(p => p.thingIDNumber, p => string.Join(",", DefDatabase<WorkTypeDef>.AllDefsListForReading.Select(w => w.defName + ":" + p.workSettings.GetPriority(w))));
        private static Dictionary<int, string> HumanRecommendations()
        {
            RecommendationPlan plan = RecommendationPlan.Build(RecsAdapter.BuildColonyView(RoleStore.Current, humans.ToList()), RoleStore.Current.recommendationTuning);
            return Enumerable.Range(0, humans.Length).ToDictionary(i => humans[i].thingIDNumber, i => string.Join(",", Enumerable.Range(0, plan.RoleCountAt(i)).Select(r => plan.RoleAt(i, r))));
        }
        private static void VerifyNativeGrid()
        {
            var build = AccessTools.Method(typeof(Dialog_PriorityGrid), "BuildCell");
            foreach (Pawn worker in workers)
                foreach (string name in new[] { "Cooking", "Research" })
                {
                    WorkTypeDef work = DefDatabase<WorkTypeDef>.GetNamed(name);
                    object cell = build.Invoke(null, new object[] { worker, work, RoleStore.Current });
                    bool native = ((IEnumerable)AccessTools.Method(worker.GetType(), "GetWorkGivers").Invoke(worker, new object[] { false })).Cast<WorkGiver>().Any(g => g.def.workType == work);
                    bool available = (bool)Property(cell, "Available");
                    int priority = (int)Property(cell, "RawPriority");
                    bool passion = Property(cell, "PassionTexture") != null;
                    Record(worker.def.defName + "." + name, "native=" + native + ",available=" + available + ",priority=" + priority + ",passion=" + passion);
                    Check(native == available, worker.def.defName + " priority grid availability matches native " + name + " capability");
                    // Misc Robots++ 1.6/Defs/ThingDefs/RPP_Races.xml, Omni V:
                    // Cooking priority 2; Research priority 0. This fixture has
                    // no WR assignments, so the native declared priority wins.
                    if (worker.def.defName == "RPP_Bot_Omni_V" && name == "Cooking")
                        Check(priority == 2, "unmanaged Omni Cooking grid retains native declared priority 2");
                    Check(!passion, worker.def.defName + " priority grid never infers human passion for " + name);
                }
        }
        private static void VerifyMechanicalPreview()
        {
            // The real view has already produced the selected robot's preview.
            object state = Field(view, "recommendationState");
            var plans = ((IEnumerable)Field(state, "plans")).Cast<object>();
            object plan = plans.First(p => (Pawn)Property(p, "Pawn") == workers[1]);
            var reasons = (IDictionary)Property(plan, "Explanations");
            var target = ((IEnumerable)Property(plan, "Target")).Cast<RoleAssignment>().ToArray();
            Record("robot-preview-target-count", target.Length);
            Record("robot-preview-explanation-count", reasons.Count);
            Check(target.Length > 0 && target.All(a => reasons.Contains(a.roleId)), "robot preview explains each recommended duty");
        }
    }
}
