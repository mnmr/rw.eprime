using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using WorkRoles;

namespace SmartWorkModeProbe;

// Smart Work Mode (smartworkmode.1trickPwnyta) adds the "SmartWork" mech
// work mode, whose think-tree branch works like Work plus idle self-shutdown.
// WorkRoles and Quality Jobs must treat mechs in that mode as workers:
//  M1 a control group switched to SmartWork: WorkRoles' mechanical capture
//     reports ProvidesService and Quality Jobs' WorkerCapabilities.Available
//     is true for its spawned, healthy mechs (as in Work mode).
//  M2 the same group switched to SelfShutdown: both report false (control).
[StaticConstructorOnStartup]
public static class Probe
{
    static bool started, done;
    static string Root => GenFilePaths.SaveDataFolderPath;
    static int failures;

    static Probe() => new Harmony("eprime.workroles.smartworkmode").Patch(
        AccessTools.Method(typeof(Root_Play), "Update"),
        postfix: new HarmonyMethod(typeof(Probe), nameof(Update)));

    static void Record(string s) { File.AppendAllText(Path.Combine(Root, "smart-work.txt"), s + Environment.NewLine); Log.Message("[SmartWorkProbe] " + s); }
    static void Check(bool ok, string s) { Record((ok ? "PASS " : "FAIL ") + s); if (!ok) failures++; }
    static void Stage(string s) => File.WriteAllText(Path.Combine(Root, "smart-work-stage.txt"), s);

    static void Update()
    {
        if (done || Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null || LongEventHandler.AnyEventNowOrWaiting) return;
        try
        {
            if (started) return;
            started = true;
            Find.TickManager.Pause();
            AccessTools.Field(typeof(LudeonTK.EditWindow_Log), "canAutoOpen").SetValue(null, false);
            Run();
            done = true;
            Stage(failures == 0 ? "SUCCESS" : "FAILED " + failures + " check(s)");
        }
        catch (Exception ex) { done = true; Record("ERROR " + ex); Stage("FAILED " + ex.Message); }
    }

    static void Run()
    {
        MechWorkModeDef smart = DefDatabase<MechWorkModeDef>.GetNamed("SmartWork");
        Map map = Find.CurrentMap;
        MechanitorControlGroup group = null;
        List<Pawn> mechs = null;
        foreach (Pawn colonist in map.mapPawns.FreeColonistsSpawned)
        {
            if (colonist.mechanitor == null) continue;
            foreach (MechanitorControlGroup candidate in colonist.mechanitor.controlGroups)
            {
                var assigned = candidate.AssignedMechs.Select(m => m.pawn)
                    .Where(p => p.Spawned && p.Map == map && !p.Dead && !p.Downed && !p.Drafted).ToList();
                if (assigned.Count > 0) { group = candidate; mechs = assigned; break; }
            }
            if (group != null) break;
        }
        if (group == null) throw new Exception("fixture: no control group with a spawned mech");
        Record("group " + group.Index + " of " + group.Tracker.Pawn.LabelShort + ": " + string.Join(", ", mechs.Select(m => m.LabelShort + " (" + m.def.defName + ")")));

        Type workers = typeof(RoleCommands).Assembly.GetType("WorkRoles.MechanicalWorkers", true);
        var capture = AccessTools.Method(workers, "Capture");
        Type qjCapabilities = AccessTools.TypeByName("QualityJobs.WorkerCapabilities");
        var qjAvailable = qjCapabilities == null ? null : AccessTools.Method(qjCapabilities, "Available");
        RoleStore store = RoleStore.Current;

        bool WorkRolesService(Pawn mech)
        {
            object view = capture.Invoke(null, new object[] { mech, store });
            return (bool)AccessTools.Field(view.GetType(), "ProvidesService").GetValue(view);
        }
        bool QualityJobsAvailable(Pawn mech) => qjAvailable != null && (bool)qjAvailable.Invoke(null, new object[] { mech });

        // SetWorkMode re-evaluates every assigned mech's job, which throws for
        // the copied save's broken lifters; the mode field alone is what both
        // mods read, so set it directly (the mods' own SetWorkMode patches
        // only decide invalidation, not the answer under test).
        var modeField = AccessTools.Field(typeof(MechanitorControlGroup), "workMode");
        void SetMode(MechWorkModeDef mode) => modeField.SetValue(group, mode);

        SetMode(MechWorkModeDefOf.Work);
        Record("Work mode: " + string.Join(", ", mechs.Select(m => m.LabelShort + " WR=" + WorkRolesService(m) + " QJ=" + QualityJobsAvailable(m))));
        var reference = mechs.Where(m => WorkRolesService(m) && QualityJobsAvailable(m)).ToList();
        if (reference.Count == 0) throw new Exception("fixture: no mech counts as a worker even in Work mode");

        SetMode(smart);
        Check(group.WorkMode == smart, "fixture: group switched to Smart Work");
        foreach (Pawn mech in reference)
        {
            Check(WorkRolesService(mech), "M1 WorkRoles counts " + mech.LabelShort + " as a working mech in Smart Work mode");
            Check(QualityJobsAvailable(mech), "M1 Quality Jobs counts " + mech.LabelShort + " as available in Smart Work mode");
        }

        SetMode(MechWorkModeDefOf.SelfShutdown);
        foreach (Pawn mech in reference)
        {
            Check(!WorkRolesService(mech), "M2 WorkRoles drops " + mech.LabelShort + " in Self-shutdown mode");
            Check(!QualityJobsAvailable(mech), "M2 Quality Jobs drops " + mech.LabelShort + " in Self-shutdown mode");
        }
        SetMode(MechWorkModeDefOf.Work);
    }
}
