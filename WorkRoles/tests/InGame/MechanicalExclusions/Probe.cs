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
using WorkRoles.UI;

namespace MechanicalExclusions;

[StaticConstructorOnStartup]
public static class Probe
{
    private static int frames;
    private static bool done;
    private static object Get(object target, string name) => AccessTools.Property(target.GetType(), name)?.GetValue(target) ?? AccessTools.Field(target.GetType(), name)?.GetValue(target);
    private static object Call(object target, string name, params object[] args) => AccessTools.GetDeclaredMethods(target.GetType()).Single(m => m.Name == name && m.GetParameters().Length == args.Length).Invoke(target, args);
    private static void Record(string value) => File.AppendAllText(Path.Combine(GenFilePaths.SaveDataFolderPath, "mechanical-exclusion-results.txt"), value + Environment.NewLine);
    private static void Require(bool value, string text) { if (!value) throw new Exception(text); Record("PASS " + text); }
    static Probe() => new Harmony("eprime.workroles.mechanicalexclusions").Patch(AccessTools.Method(typeof(Root_Play), "Update"), postfix: new HarmonyMethod(typeof(Probe), nameof(Update)));
    private static Role NewRole(string label, string giverName)
    {
        RoleCommands.CreateRole(label);
        Role role = RoleStore.Current.roles.Single(r => r.label == label);
        RoleCommands.AddEntry(role.id, new JobEntry(JobEntryKind.WorkGiver, giverName));
        return role;
    }
    private static void Update()
    {
        if (done || Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null || LongEventHandler.AnyEventNowOrWaiting) return;
        if (++frames < 45) return;
        done = true;
        try
        {
            Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
            AccessTools.Field(typeof(LudeonTK.EditWindow_Log), "canAutoOpen").SetValue(null, false);
            foreach (Window window in Find.WindowStack.Windows.ToArray()) window.Close(false);
            RoleCommands.SetAutoOptimize(false);
            RoleCommands.SetMechanicalWorkersEnabled(true);
            var map = Find.CurrentMap;
            var worker = map.mapPawns.AllPawnsSpawned.FirstOrDefault(p => p.Faction == Faction.OfPlayer && p.RaceProps.IsMechanoid && p.RaceProps.mechEnabledWorkTypes.Contains(WorkTypeDefOf.Construction));
            if (worker == null)
            {
                worker = PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.GetNamed("Mech_Constructoid"), Faction.OfPlayer);
                var cell = map.AllCells.First(c => !c.Fogged(map) && c.Standable(map) && c.GetEdifice(map) == null);
                GenSpawn.Spawn(worker, cell, map);
            }
            Require(worker.RaceProps.mechEnabledWorkTypes.Contains(WorkTypeDefOf.Construction), "real native worker supports Construction; " + worker.def.defName);
            var gives = DefDatabase<WorkGiverDef>.AllDefsListForReading.Where(g => g.workType == WorkTypeDefOf.Construction && g.canBeDoneByMechs).ToArray();
            Require(gives.Length >= 3, "native mech construction offers three real giver choices");
            Role primary = NewRole("Review Mechanical Builder", gives[0].defName);
            RoleCommands.AddEntry(primary.id, new JobEntry(JobEntryKind.WorkGiver, gives[1].defName));
            RoleCommands.AddEntry(primary.id, new JobEntry(JobEntryKind.WorkGiver, gives[2].defName));
            Role conditional = NewRole("Review Excluded Shift", gives[0].defName);
            RoleCommands.SetRoleActiveHours(conditional.id, 0);
            Role blocker = NewRole("Review Excluded Blocker", gives[1].defName);
            RoleCommands.SetRoleBlocker(blocker.id, true);
            Role pinned = NewRole("Review Excluded Pinned", gives[2].defName);
            RoleCommands.SetRoleActiveHours(pinned.id, 0);
            RoleCommands.PasteRoleSet(worker, new List<RoleAssignment> {
                new RoleAssignment { roleId = blocker.id },
                new RoleAssignment { roleId = primary.id },
                new RoleAssignment { roleId = conditional.id },
                new RoleAssignment { roleId = pinned.id, pinned = true }
            });
            foreach (Role role in new[] { conditional, blocker, pinned }) RoleCommands.SetRoleRecommendationExcluded(role.id, true);
            Require(RoleRules.FailReason(conditional, worker) == RuleFailReason.OutsideHours, "excluded conditional is inactive and not worker primary duty");
            Require(!CompiledJobOrders.NormalFor(worker).Any(g => g.def == gives[1]), "excluded blocker currently vetoes native " + gives[1].defName);
            Find.Selector.ClearSelection(); Find.Selector.Select(worker);
            var button = DefDatabase<MainButtonDef>.AllDefsListForReading.First(d => d.TabWindow is MainTabWindow_WorkRoles);
            Find.MainTabsRoot.SetCurrentTab(button);
            object view = Get(button.TabWindow, "colonistsTab");
            ((ListSelection<Pawn>)Get(view, "selection")).Click(worker);
            Call(view, "RefreshExternalSnapshotIfNeeded");
            // This is the actual preview producer and the actual Apply path.
            var previewEntries = ((IEnumerable)Call(view, "BuildFixEntries", worker, false)).Cast<object>().ToArray();
            object state = Get(view, "recommendationState");
            object plan = ((IEnumerable)Get(state, "plans")).Cast<object>().Single(p => ReferenceEquals(Get(p, "Pawn"), worker));
            var target = ((IEnumerable)Get(plan, "Target")).Cast<RoleAssignment>().ToArray();
            Record("Actual UI plan target=" + string.Join(",", target.Select(a => RoleStore.Current.RoleById(a.roleId).label)));
            Record("Actual Fix preview rows for worker=" + previewEntries.Length);
            Call(view, "ApplyFix", worker, null, false);
            RoleAssignment[] after = RoleStore.Current.pawnSets[worker].assignments.ToArray();
            Record("After actual UI ApplyFix=" + string.Join(",", after.Select(a => RoleStore.Current.RoleById(a.roleId).label)));
            Require(!after.Any(a => a.roleId == conditional.id || a.roleId == blocker.id),
                "WR11 actual UI ApplyFix removes excluded unpinned nonprimary protections");
            Require(after.Any(a => a.roleId == primary.id), "accepted primary duty survives");
            Require(CompiledJobOrders.NormalFor(worker).Any(g => g.def == gives[1]), "removed blocker releases native repair/build giver");
            Record("After ApplyFix blocked native giver present=" + CompiledJobOrders.NormalFor(worker).Any(g => g.def == gives[1]));
            Require(after.Any(a => a.roleId == pinned.id && a.pinned), "pinned excluded control retained as documented");
            Record("COMPLETE WR11 actual preview/apply and native compiled work verified");
            File.WriteAllText(Path.Combine(GenFilePaths.SaveDataFolderPath, "mechanical-exclusion-stage.txt"), "READY");
        }
        catch (Exception ex)
        {
            Record("ERROR " + ex);
            File.WriteAllText(Path.Combine(GenFilePaths.SaveDataFolderPath, "mechanical-exclusion-stage.txt"), "FAILED");
        }
    }
}
