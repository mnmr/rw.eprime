using System;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using WorkRoles;
using WorkRoles.Core;
using WorkRoles.UI;

namespace WorkRolesBugAudit;

// Native command/input-boundary reproductions in an independent copied save.
// No production seams and no writes to the player's save or preferences.
public sealed class Probe : GameComponent
{
    private int frames, phase;
    private RoleStore store;
    private Pawn source, target;
    private int roleId;
    private static string Root => GenFilePaths.SaveDataFolderPath;
    private static void Record(string value)
    {
        File.AppendAllText(Path.Combine(Root, "bug-audit-results.txt"), value + Environment.NewLine);
        Log.Message("[WorkRolesBugAudit] " + value);
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        Record("PASS " + message);
    }
    public Probe(Game game) { }
    public override void GameComponentUpdate()
    {
        if (phase == 99 || Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null
            || LongEventHandler.AnyEventNowOrWaiting || ++frames < 30) return;
        try
        {
            if (phase == 0)
            {
                Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                AccessTools.Field(typeof(LudeonTK.EditWindow_Log), "canAutoOpen").SetValue(null, false);
                foreach (var popup in Find.WindowStack.Windows.ToArray()) popup.Close(false);
                RoleCommands.SetAutoOptimize(false);
                store = RoleStore.Current;
                source = Find.CurrentMap.mapPawns.FreeColonists.First(p => p.LabelShort == "Takeo");
                target = Find.CurrentMap.mapPawns.FreeColonists.First(p => p.LabelShort == "Hysott");
                RoleCommands.CreateRole("Audit protected cleaning");
                var role = store.roles.Single(r => r.label == "Audit protected cleaning");
                roleId = role.id;
                RoleCommands.AddEntry(roleId, new JobEntry(JobEntryKind.WorkType, "Cleaning"));
                RoleCommands.SetRoleRecommendationExcluded(roleId, true);
                RoleCommands.AssignRole(source, roleId);
                RoleCommands.ToggleAssignmentPin(source, roleId);
                RoleCommands.SetAssignmentState(source, roleId, (int)AssignmentState.Disabled);
                Require(store.pawnSets[source].assignments.Single(a => a.roleId == roleId).pinned,
                    "source assignment is pinned and excluded before the move");
                new Harmony("tests.workroles.bugaudit").Patch(AccessTools.Method(typeof(MainTabWindow_WorkRoles), "DoWindowContents"),
                    postfix: new HarmonyMethod(typeof(Probe), nameof(Draw)));
                active = this;
                phase = 1;
            }
            if (phase == 1)
            {
                var button = DefDatabase<MainButtonDef>.AllDefsListForReading.First(d => d.TabWindow is MainTabWindow_WorkRoles);
                if (!Find.WindowStack.Windows.Contains(button.TabWindow))
                {
                    Find.MainTabsRoot.SetCurrentTab(button);
                    if (!Find.WindowStack.Windows.Contains(button.TabWindow))
                        AccessTools.Method(typeof(RoleCommands).Assembly.GetType("WorkRoles.SetupDefaults"), "Decide").Invoke(null, new object[] { false, false });
                }
            }
        }
        catch (Exception ex) { Finish("FAILED " + ex); }
    }
    private static Probe active;
    private static void Draw()
    {
        if (active == null || active.phase != 1 || Event.current.type != EventType.Repaint) return;
        active.phase = 2;
        try
        {
            Drop(active.source, active.target, active.roleId);
            Require(!active.store.pawnSets[active.source].assignments.Any(a => a.roleId == active.roleId), "assignment moved off source");
            var moved = active.store.pawnSets[active.target].assignments.Single(a => a.roleId == active.roleId);
            Require(moved.state == AssignmentState.Disabled, "assignment disabled state survives move");
            Record("DRAG_RESULT source=" + active.source.LabelShort + " target=" + active.target.LabelShort
                + " role=" + active.roleId + " pinned=" + moved.pinned + " state=" + moved.state);
            Require(moved.pinned, "WR22 cross-colonist drag preserves pin");
            var window = Find.WindowStack.Windows.OfType<MainTabWindow_WorkRoles>().Single();
            var view = AccessTools.Field(typeof(MainTabWindow_WorkRoles), "colonistsTab").GetValue(window);
            AccessTools.Method(view.GetType(), "ApplyFix").Invoke(view, new object[] { active.target, null, false });
            Require(active.store.pawnSets[active.target].assignments.Any(a => a.roleId == active.roleId && a.pinned),
                "WR22 actual Make It So preserves the moved excluded pinned assignment");

            // A duplicate destination must preserve both owners and publish no changes.
            RoleCommands.AssignRole(active.source, active.roleId);
            RoleCommands.SetAssignmentState(active.source, active.roleId, (int)AssignmentState.ForceOn);
            int revision = UiVersion.Current;
            Drop(active.source, active.target, active.roleId);
            Require(active.store.pawnSets[active.source].assignments.Single(a => a.roleId == active.roleId).state == AssignmentState.ForceOn
                && ReferenceEquals(moved, active.store.pawnSets[active.target].assignments.Single(a => a.roleId == active.roleId))
                && UiVersion.Current == revision, "WR22 duplicate destination is an unchanged no-op");

            RoleCommands.CreateRole("WR22 missing source control");
            int absentId = active.store.roles.Single(r => r.label == "WR22 missing source control").id;
            revision = UiVersion.Current;
            Drop(active.source, active.target, absentId);
            Require(!active.store.pawnSets[active.target].assignments.Any(a => a.roleId == absentId)
                && UiVersion.Current == revision, "WR22 stale source cannot create a destination assignment");

            RoleCommands.RemoveRoleFromPawn(active.target, active.roleId);
            RoleCommands.PasteRoleSet(active.source, new System.Collections.Generic.List<RoleAssignment>
            {
                new RoleAssignment { roleId = active.roleId, state = AssignmentState.ForceOn, pinned = true }
            });
            Drop(active.source, active.target, active.roleId);
            var lastMoved = active.store.pawnSets[active.target].assignments.Single(a => a.roleId == active.roleId);
            Require(!active.store.IsManaged(active.source) && lastMoved.pinned && lastMoved.state == AssignmentState.ForceOn,
                "WR22 last assignment transfer unmanages source and preserves forced state and pin");
            active.Finish("SUCCESS WR22 native assignment move regression");
        }
        catch (Exception ex) { active.Finish("FAILED " + ex); }
    }
    private static void Drop(Pawn source, Pawn target, int roleId)
    {
        // Validated drop state isolates the production mouse-up command boundary;
        // pointer hit testing is outside this regression's scope.
        var drag = typeof(RoleDrag);
        AccessTools.Property(drag, "Active").SetValue(null, true);
        AccessTools.Property(drag, "RoleId").SetValue(null, roleId);
        AccessTools.Property(drag, "SourcePawn").SetValue(null, source);
        RoleDrag.HoverPawn = target;
        RoleDrag.HoverInsertIndex = 0;
        var previousEvent = Event.current;
        try
        {
            Event.current = new Event { type = EventType.MouseUp, rawType = EventType.MouseUp, button = 0 };
            RoleDrag.ResolveMouseUp();
        }
        finally { Event.current = previousEvent; RoleDrag.Cancel(); }
    }
    private void Finish(string result)
    {
        phase = 99;
        Record(result);
        File.WriteAllText(Path.Combine(Root, "bug-audit-stage.txt"), result);
    }
}
