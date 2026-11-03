using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using WorkRoles;

namespace MechModMixProbe;

// WorkRoles and Quality Jobs beside the three mech mods a player may combine:
// WVC - Work Modes (fourteen MechWorkModeDefs with their own think-tree
// branches), Mech Work Tab (a mech priority tab writing work priorities)
// and Smart Work Mode. Checks:
//  X1 the shared working-mode rule classifies every loaded mode by whether
//     its branch runs work (WVC work modes yes, combat/escort/recharge no).
//  X2 Mech Work Tab style priority writes: with mech support off a mech's
//     priority write lands; with support on and a role assigned, WorkRoles
//     blocks it and queues its one-time notice (the documented behaviour).
//  X3 both mech tabs open without exceptions beside WorkRoles.
[StaticConstructorOnStartup]
public static class Probe
{
    static bool started, done;
    static int failures;
    static string Root => GenFilePaths.SaveDataFolderPath;

    static Probe() => new Harmony("eprime.workroles.mechmodmix").Patch(
        AccessTools.Method(typeof(Root_Play), "Update"),
        postfix: new HarmonyMethod(typeof(Probe), nameof(Update)));

    static void Record(string s) { File.AppendAllText(Path.Combine(Root, "mech-mix.txt"), s + Environment.NewLine); Log.Message("[MechModMix] " + s); }
    static void Check(bool ok, string s) { Record((ok ? "PASS " : "FAIL ") + s); if (!ok) failures++; }
    static void Stage(string s) => File.WriteAllText(Path.Combine(Root, "mech-mix-stage.txt"), s);

    static void Update()
    {
        if (done || Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null || LongEventHandler.AnyEventNowOrWaiting) return;
        try
        {
            if (started) return;
            started = true;
            Find.TickManager.Pause();
            AccessTools.Field(typeof(LudeonTK.EditWindow_Log), "canAutoOpen").SetValue(null, false);
            Modes();
            Priorities();
            Tabs();
            done = true;
            Stage(failures == 0 ? "SUCCESS" : "FAILED " + failures + " check(s)");
        }
        catch (Exception ex) { done = true; Record("ERROR " + ex); Stage("FAILED " + ex.Message); }
    }

    static void Modes()
    {
        Type rule = typeof(RoleCommands).Assembly.GetType("RimShared.GameLib.MechWorkModes", true);
        var isWorking = AccessTools.Method(rule, "IsWorking", new[] { typeof(MechWorkModeDef) });
        var expected = new Dictionary<string, bool>
        {
            ["Work"] = true, ["SmartWork"] = true,
            ["WVC_WorkAndRecharge"] = true, ["WVC_SafeWorkAndRecharge"] = true, ["WVC_WorkAndWaitEnemy"] = true,
            ["WVC_WorkRechargeEscort"] = true, ["WVC_EscortIfEnemyWorkAndRecharge"] = true,
            ["Escort"] = false, ["Recharge"] = false, ["SelfShutdown"] = false,
            ["WVC_FindAndDestroy"] = false, ["WVC_WaitEnemy"] = false, ["WVC_Ambush"] = false,
            ["WVC_DefendYourself"] = false, ["WVC_RechargeAndShutdown"] = false, ["WVC_EscaortAndRecharge"] = false,
            ["WVC_EscortIfEnemyOnMap"] = false, ["WVC_EscortIfDraftedOrDowned"] = false,
            ["WVC_HiveMindResearching"] = false, ["WVC_Scavenging"] = false,
        };
        var seen = new List<string>();
        foreach (MechWorkModeDef mode in DefDatabase<MechWorkModeDef>.AllDefsListForReading)
        {
            bool working = (bool)isWorking.Invoke(null, new object[] { mode });
            seen.Add(mode.defName + "=" + working);
            if (expected.TryGetValue(mode.defName, out bool want))
                Check(working == want, "X1 " + mode.defName + " " + (want ? "works" : "does not work"));
            else Record("X1 unlisted mode " + mode.defName + " working=" + working);
        }
        Record("modes: " + string.Join(", ", seen));
    }

    static void Priorities()
    {
        Map map = Find.CurrentMap;
        Pawn mech = map.mapPawns.AllPawnsSpawned.FirstOrDefault(p => p.RaceProps.IsMechanoid && p.Faction == Faction.OfPlayer
            && p.workSettings != null && p.workSettings.EverWork && p.def.defName == "Mech_Constructoid" && !p.Downed);
        if (mech == null) throw new Exception("fixture: no constructoid with work settings");
        WorkTypeDef work = WorkTypeDefOf.Construction;
        RoleStore store = RoleStore.Current;
        Type watcher = typeof(RoleCommands).Assembly.GetType("WorkRoles.PrioritySetWatcher", true);
        bool Pending() => (bool)AccessTools.Property(watcher, "HasPendingWarning").GetValue(null);

        RoleCommands.SetMechanicalWorkersEnabled(false);
        int before = mech.workSettings.GetPriority(work);
        int target = before == 1 ? 2 : 1;
        mech.workSettings.SetPriority(work, target);
        Check(mech.workSettings.GetPriority(work) == target && !Pending(),
            "X2 support off: a Mech Work Tab style priority write on " + mech.LabelShort + " lands (" + before + " -> " + target + ")");
        mech.workSettings.SetPriority(work, before);

        RoleCommands.SetMechanicalWorkersEnabled(true);
        int role = store.RoleByTemplate("WS_Builder").id;
        RoleCommands.AssignRole(mech, role);
        Check(store.IsManaged(mech), "X2 fixture: " + mech.LabelShort + " is managed with the Builder role");
        // Disabling work the role enables is a conflicting write: WorkRoles
        // swallows it and queues its one-time notice (a write roles already
        // satisfy is swallowed silently by design).
        int managed = mech.workSettings.GetPriority(work);
        Check(managed > 0, "X2 fixture: the Builder role enables Construction on " + mech.LabelShort + " (" + managed + ")");
        mech.workSettings.SetPriority(work, 0);
        Check(mech.workSettings.GetPriority(work) == managed, "X2 support on + role: the write is blocked");
        Check(Pending(), "X2 support on + role: the one-time notice is queued for the writing mod");
        RoleCommands.PasteRoleSet(mech, new List<RoleAssignment>());
        RoleCommands.SetMechanicalWorkersEnabled(false);
    }

    static void Tabs()
    {
        foreach (string name in new[] { "SM_MechWork", "WVC_MechsWork" })
        {
            MainButtonDef button = DefDatabase<MainButtonDef>.GetNamedSilentFail(name);
            if (button == null) { Record("X3 tab " + name + " not loaded"); continue; }
            try
            {
                Find.MainTabsRoot.SetCurrentTab(button);
                Check(Find.WindowStack.Windows.Contains(button.TabWindow), "X3 " + name + " opens beside WorkRoles");
                Find.MainTabsRoot.SetCurrentTab(button); // toggle closed
            }
            catch (Exception ex) { Check(false, "X3 " + name + " threw " + ex.Message); }
        }
    }
}
