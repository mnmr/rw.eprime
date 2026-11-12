using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using WorkRoles;
using WorkRoles.Core;
using WorkRoles.Core.Recs;
using WorkRoles.UI;

namespace MechanicalCache;

// Capture counts protect the independent cache contract: identical published
// results alone cannot reveal expensive equal rebuilds.
public sealed class Probe : GameComponent
{
    private int frame, phase, failures;
    private static bool counting;
    private static readonly Dictionary<Pawn, int> captures = new();
    private static readonly Dictionary<Pawn, int> externalCaptures = new();
    private Pawn human, first, second;
    private object view, recs, producer;
    private MainTabWindow_WorkRoles window;
    private static string Root => GenFilePaths.SaveDataFolderPath;
    private static Type Wr(string name) => typeof(RoleStore).Assembly.GetType("WorkRoles." + name, true);
    private static object Field(object target, string name) => AccessTools.Field(target.GetType(), name).GetValue(target);
    private static object Call(object target, string name, params object[] args) =>
        AccessTools.GetDeclaredMethods(target.GetType()).Single(m => m.Name == name && m.GetParameters().Length == args.Length).Invoke(target, args);
    private static void Record(string text) { File.AppendAllText(Path.Combine(Root, "mechanical-cache-results.txt"), text + Environment.NewLine); Log.Message("[MechanicalCache] " + text); }
    private void Check(bool value, string text) { if (!value) failures++; Record((value ? "PASS " : "FAIL PF1 ") + text); }
    public Probe(Game game) { }
    private static void Captured(Pawn pawn) { if (counting) { captures.TryGetValue(pawn, out int count); captures[pawn] = count + 1; } }
    private static void ExternalCaptured(Pawn pawn) { if (counting) { externalCaptures.TryGetValue(pawn, out int count); externalCaptures[pawn] = count + 1; } }
    private static int Count(Pawn pawn) => captures.TryGetValue(pawn, out int count) ? count : 0;
    private static int ExternalCount(Pawn pawn) => externalCaptures.TryGetValue(pawn, out int count) ? count : 0;
    private void Refresh() { Call(view, "RefreshExternalSnapshotIfNeeded"); Call(view, "BuildFixEntries", null, false); }
    private object Snapshot() => Field(producer, "mechanical");
    private MechanicalWorkerView Worker(Pawn pawn)
    {
        object snapshot = Snapshot();
        var pawns = (IReadOnlyList<Pawn>)AccessTools.Property(snapshot.GetType(), "Pawns").GetValue(snapshot);
        var workers = (IReadOnlyList<MechanicalWorkerView>)AccessTools.Property(snapshot.GetType(), "Workers").GetValue(snapshot);
        for (int i = 0; i < pawns.Count; i++) if (pawns[i] == pawn) return workers[i];
        throw new InvalidOperationException("Missing native worker " + pawn);
    }
    public override void GameComponentUpdate()
    {
        if (phase == 99 || Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null || LongEventHandler.AnyEventNowOrWaiting || ++frame < 20) return;
        try
        {
            if (phase == 0)
            {
                Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                AccessTools.Field(typeof(LudeonTK.EditWindow_Log), "canAutoOpen").SetValue(null, false);
                foreach (Window popup in Find.WindowStack.Windows.ToArray()) popup.Close(false);
                RoleCommands.SetAutoOptimize(false);
                RoleCommands.SetMechanicalWorkersEnabled(true);
                RoleCommands.SetMechanicalRecommendationsEnabled(true);
                Map map = Find.CurrentMap;
                human = map.mapPawns.FreeColonistsSpawned.First(p => p.skills != null && p.skills.skills.Any(s => !s.TotallyDisabled && s.Level < 19));
                PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamed("Mech_Lifter");
                IntVec3 cell = map.AllCells.First(c => c.Standable(map));
                first = PawnGenerator.GeneratePawn(kind, Faction.OfPlayer);
                second = PawnGenerator.GeneratePawn(kind, Faction.OfPlayer);
                GenSpawn.Spawn(first, cell, map); GenSpawn.Spawn(second, cell, map);
                Role role = RoleStore.Current.roles.First(r => !r.composite && !r.blocker && r.entries.Count > 0);
                RoleCommands.AssignRole(first, role.id); RoleCommands.AssignRole(second, role.id);
                var harmony = new Harmony("tests.workroles.mechanicalcache");
                harmony.Patch(AccessTools.Method(typeof(RecsAdapter), "CapturePawnSnapshot"),
                    prefix: new HarmonyMethod(typeof(Probe), nameof(ExternalCaptured)));
                harmony.Patch(
                    AccessTools.Method(Wr("MechanicalWorkers"), "CaptureFromSnapshot")
                        ?? AccessTools.Method(Wr("MechanicalWorkers"), "Capture", new[] { typeof(Pawn), typeof(RoleStore) }),
                    prefix: new HarmonyMethod(typeof(Probe), nameof(Captured)));
                var button = DefDatabase<MainButtonDef>.AllDefsListForReading.First(d => d.TabWindow is MainTabWindow_WorkRoles);
                Find.MainTabsRoot.SetCurrentTab(button); window = (MainTabWindow_WorkRoles)button.TabWindow;
                view = Field(window, "colonistsTab"); recs = Field(view, "recommendationState"); producer = Field(recs, "producer");
                phase = 1; frame = 0;
                return;
            }
            Refresh();
            object before = Snapshot();
            captures.Clear(); counting = true;
            SkillRecord skill = human.skills.skills.First(s => !s.TotallyDisabled && s.Level < 19);
            skill.Level++;
            Refresh();
            Check(Count(first) == 0 && Count(second) == 0, "human whole-level change retains both worker captures (actual " + Count(first) + "," + Count(second) + ")");
            Check(ReferenceEquals(before, Snapshot()), "human skill edit preserves mechanical snapshot identity");
            captures.Clear();
            var humanRole = RoleStore.Current.pawnSets[human].assignments.First();
            RoleCommands.SetAssignmentState(human, humanRole.roleId, (int)(humanRole.state == AssignmentState.Disabled ? AssignmentState.Enabled : AssignmentState.Disabled));
            Refresh();
            Check(Count(first) == 0 && Count(second) == 0, "human assignment edit retains both worker captures");
            Check(ExternalCount(first) == 0 && ExternalCount(second) == 0, "human edits never recapture unrelated worker external facts");
            captures.Clear();
            var assignment = RoleStore.Current.pawnSets[first].assignments.First();
            RoleCommands.SetAssignmentState(first, assignment.roleId, (int)AssignmentState.Disabled);
            Refresh();
            Check(Count(first) == 1 && Count(second) == 0, "worker assignment edit recaptures only affected worker (actual " + Count(first) + "," + Count(second) + ")");
            Check(!Worker(first).Facts.Existing.First(a => a.RoleId == assignment.roleId).Enabled, "published worker assignment is disabled");
            Check(ExternalCount(first) == 0 && ExternalCount(second) == 0, "worker assignment overlay reuses shared external facts");
            captures.Clear();
            RoleCommands.SetAssignmentState(first, assignment.roleId, (int)AssignmentState.Disabled);
            Refresh();
            Check(Count(first) == 0 && Count(second) == 0, "no-op worker assignment preserves captures");
            object retained = Snapshot();
            RoleCommands.SetMechanicalWorkersEnabled(false);
            Refresh();
            Check(((IReadOnlyList<Pawn>)AccessTools.Property(Snapshot().GetType(), "Pawns").GetValue(Snapshot())).Count == 0,
                "support off releases the mechanical cohort");
            captures.Clear();
            externalCaptures.Clear();
            RoleCommands.SetMechanicalWorkersEnabled(true);
            Refresh();
            Check(Count(first) == 1 && Count(second) == 1 && !ReferenceEquals(retained, Snapshot()),
                "support on builds a fresh cohort after teardown");
            Check(Worker(first).Facts.CapableWorkTypes.Contains("Hauling")
                && Worker(second).Facts.CapableWorkTypes.Contains("Hauling")
                && Worker(first).ExecutableGivers.Contains("HaulGeneral")
                && Worker(second).ExecutableGivers.Contains("HaulGeneral"),
                "support on shares native hauling capabilities for newly admitted lifters");
            Check(ExternalCount(first) == 1 && ExternalCount(second) == 1, "support on captures worker external facts once per worker across consumers");
            var mechanicalFor = AccessTools.Method(producer.GetType(), "MechanicalFor");
            if (mechanicalFor.GetParameters().Length == 3)
            {
                var accessor = AccessTools.Method(view.GetType(), "ExternalSnapshotFor")
                    .CreateDelegate(mechanicalFor.GetParameters()[2].ParameterType, view);
                mechanicalFor.Invoke(producer, new object[] { RoleStore.Current, null, accessor });
                Check(((IReadOnlyList<Pawn>)AccessTools.Property(Snapshot().GetType(), "Pawns").GetValue(Snapshot())).Count == 0,
                    "missing map releases the prior map's mechanical cohort");
                captures.Clear();
                Call(recs, "InvalidatePlan"); Refresh();
                Check(Count(first) == 1 && Count(second) == 1, "returning to map builds a fresh mechanical cohort");
            }
            counting = false;
            window.Close(false);
            Check(((IReadOnlyList<Pawn>)AccessTools.Property(Snapshot().GetType(), "Pawns").GetValue(Snapshot())).Count == 0, "window close releases mechanical cohort");
            Record("COMPLETE failures=" + failures);
            File.WriteAllText(Path.Combine(Root, "mechanical-cache-stage.txt"), failures == 0 ? "SUCCESS" : "RED failures=" + failures);
            phase = 99;
        }
        catch (Exception error) { counting = false; Record("ERROR " + error); File.WriteAllText(Path.Combine(Root, "mechanical-cache-stage.txt"), "ERROR " + error); phase = 99; }
    }
}
