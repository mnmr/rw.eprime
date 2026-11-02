using System;
using System.IO;
using System.Linq;
using HarmonyLib;
using Implanner;
using Implanner.Core;
using RimWorld;
using Verse;
using Plan = Implanner.Core.Plan;

namespace LevelModWarningProbe;

// Level mods warn instead of standing automation down (2026-11-02), with
// Strata loaded on the local 1.7 copy:
//  L1 the Automation tab shows the Strata warning beside the master switch
//     (capture level-mod-automation) and the Overview strip chip reads
//     "Automation on" (capture level-mod-overview).
//  L2 automation actually runs: a bionic eye spawned at the colony is
//     reserved for the assigned colonist by the next reconcile pass.
[StaticConstructorOnStartup]
public static class Probe
{
    const string Package = "eprime.implanner.levelmodwarning";
    static int phase, failures, captureSeq, waitFrames, resumeTick;
    static string pendingCapture;
    static DateTime captureHeldUntil, deadline;
    static Window dialog;
    static Pawn patient;
    static Plan plan;
    static string Root => GenFilePaths.SaveDataFolderPath;

    static Probe() => new Harmony(Package).Patch(
        AccessTools.Method(typeof(Root_Play), "Update"),
        postfix: new HarmonyMethod(typeof(Probe), nameof(Update)));

    static void Write(string message)
    {
        Log.Message("[LevelModWarning] " + message);
        File.AppendAllText(Path.Combine(Root, "level-mod-warning.txt"), message + Environment.NewLine);
    }

    static void Check(bool condition, string message)
    {
        Write((condition ? "PASS " : "FAIL ") + message);
        if (!condition) failures++;
    }

    static void Update()
    {
        if (phase == 99 || Current.ProgramState != ProgramState.Playing
            || Find.CurrentMap == null || LongEventHandler.AnyEventNowOrWaiting) return;
        try
        {
            AccessTools.Field(typeof(LudeonTK.EditWindow_Log), "canAutoOpen").SetValue(null, false);
            foreach (Window window in Find.WindowStack.Windows.ToArray())
                if (!(window is MainTabWindow) && window != dialog) window.Close(false);
            if (pendingCapture != null)
            {
                if (waitFrames-- > 0) return;
                if (waitFrames == -1)
                {
                    File.WriteAllText(Path.Combine(Root, "level-mod-warning-capture.txt"),
                        captureSeq + " " + pendingCapture);
                    captureHeldUntil = DateTime.UtcNow.AddSeconds(2.5);
                }
                if (DateTime.UtcNow < captureHeldUntil) return;
                pendingCapture = null;
            }
            if (phase > 0 && DateTime.UtcNow > deadline) throw new Exception("Timed out at phase " + phase);
            switch (phase)
            {
                case 0: Setup(); OpenDialog("Automation"); Capture("level-mod-automation"); phase = 1; return;
                case 1: OpenDialog("Overview"); Capture("level-mod-overview"); phase = 2; return;
                case 2: SpawnEyeAndResume(); phase = 3; return;
                case 3:
                    if (Find.TickManager.TicksGame < resumeTick + 30) return;
                    Find.TickManager.Pause();
                    CheckReservation();
                    Finish();
                    return;
            }
        }
        catch (Exception ex)
        {
            Write("ERROR " + ex);
            File.WriteAllText(Path.Combine(Root, "level-mod-warning-stage.txt"), "FAILED " + ex.Message);
            phase = 99;
        }
    }

    static void Capture(string name, int frames = 3)
    {
        captureSeq++;
        pendingCapture = name;
        waitFrames = frames;
    }

    static void Setup()
    {
        Find.TickManager.Pause();
        Map map = Find.CurrentMap;
        Write("screen " + UI.screenWidth + "x" + UI.screenHeight + " uiScale " + Prefs.UIScale);
        Type automation = typeof(ImplannerStore).Assembly.GetType("Implanner.PlannerAutomation");
        string levelMod = (string)AccessTools.Property(automation, "LevelMod").GetValue(null);
        Write("level mod: '" + levelMod + "'");
        Check(levelMod == "Strata", "Strata is recognized as the active level mod");
        // A colonist whose eyes are still natural, so the slot is missing.
        patient = map.mapPawns.FreeColonistsSpawned.First(p => p.RaceProps.body == BodyDefOf.Human
            && !p.health.hediffSet.hediffs.Any(h => h is Hediff_AddedPart && h.Part?.def.defName == "Eye"));
        Write("patient " + patient.LabelShort);
        var store = ImplannerStore.Current;
        var model = new PlannerModel();
        store.Model = model;
        model.SetAutomationPaused(false);
        model.SetAutoProduction(false);
        model.SetSurgeryConcurrency(20);
        model.SetCountHospitalized(false);
        model.SetAutoDoctorFloor(true);
        plan = model.CreatePlan("Eyes", store.TakePlanId)!;
        model.SetImplantSlot(plan.Id, "BionicEye", new PartAddress("Eye", 0), true);
        model.AssignPlan(patient.thingIDNumber, plan.Id);
        store.Bump(PlannerChange.All);
        deadline = DateTime.UtcNow.AddSeconds(50);
    }

    static void SpawnEyeAndResume()
    {
        dialog?.Close(false);
        dialog = null;
        Thing eye = ThingMaker.MakeThing(ThingDef.Named("BionicEye"));
        GenPlace.TryPlaceThing(eye, patient.Position, patient.Map, ThingPlaceMode.Near);
        eye.SetForbidden(false, false);
        Write("spawned " + eye.ThingID + " at " + eye.Position);
        ImplannerStore.Current.Bump(PlannerChange.All);
        resumeTick = Find.TickManager.TicksGame;
        Find.TickManager.CurTimeSpeed = TimeSpeed.Normal;
    }

    static void CheckReservation()
    {
        PlannerModel model = ImplannerStore.Current.Model;
        Write("ticks advanced " + (Find.TickManager.TicksGame - resumeTick)
            + ", reservations " + model.Reservations.Count);
        bool reserved = model.Reservations.Values.Any(r => r.PawnId == patient.thingIDNumber);
        Check(reserved, "the reconcile pass reserved the bionic eye for " + patient.LabelShort + " with Strata active");
    }

    static void OpenDialog(string tab)
    {
        Type type = typeof(ImplannerStore).Assembly.GetType("Implanner.UI.Dialog_Implanner");
        Type view = typeof(ImplannerStore).Assembly.GetType("Implanner.UI.ImplannerView");
        Type tabs = view.GetNestedType("Tab", AccessTools.all);
        AccessTools.Field(view, "curTab").SetValue(null, Enum.Parse(tabs, tab));
        if (dialog == null)
        {
            dialog = (Window)Activator.CreateInstance(type);
            Find.WindowStack.Add(dialog);
        }
    }

    static void Finish()
    {
        phase = 99;
        Write(failures == 0 ? "SUCCESS" : failures + " failure(s)");
        File.WriteAllText(Path.Combine(Root, "level-mod-warning-stage.txt"),
            failures == 0 ? "SUCCESS" : "FAILED " + failures + " check(s)");
    }
}
