using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using Implanner;
using Implanner.Core;
using RimWorld;
using Verse;
using Plan = Implanner.Core.Plan;

namespace HelpShotsProbe;

// Help screenshots for body-typed plans and the body filter, taken at the
// run's preferences (1920x1080, UI scale 1.0 set by run.ps1). Frames:
//  help-picker-body-filter: an unpinned plan with the body filter on Android.
//  help-plan-pinned: the "Android only" plan selected (card captions, the
//      fixed "for Android" label in the picker row).
//  help-newplan-body: the New plan dialog with Body set to Android.
//  help-overview-notforbody: a colonist on a plan made for another body.
[StaticConstructorOnStartup]
public static class Probe
{
    const string Package = "eprime.implanner.helpshots";
    static int phase, captureSeq, waitFrames;
    static string pendingCapture;
    static DateTime captureHeldUntil, deadline;
    static Window dialog, newPlan;
    static Pawn human;
    static Plan everyone, androidOnly;
    static int androidBody = -1;
    static string Root => GenFilePaths.SaveDataFolderPath;

    static Probe() => new Harmony(Package).Patch(
        AccessTools.Method(typeof(Root_Play), "Update"),
        postfix: new HarmonyMethod(typeof(Probe), nameof(Update)));

    static void Write(string message)
    {
        Log.Message("[HelpShots] " + message);
        File.AppendAllText(Path.Combine(Root, "help-shots.txt"), message + Environment.NewLine);
    }

    static void Update()
    {
        if (phase == 99 || Current.ProgramState != ProgramState.Playing
            || Find.CurrentMap == null || LongEventHandler.AnyEventNowOrWaiting) return;
        try
        {
            AccessTools.Field(typeof(LudeonTK.EditWindow_Log), "canAutoOpen").SetValue(null, false);
            foreach (Window window in Find.WindowStack.Windows.ToArray())
                if (!(window is MainTabWindow) && window != dialog && window != newPlan) window.Close(false);
            if (pendingCapture != null)
            {
                if (waitFrames-- > 0) return;
                if (waitFrames == -1)
                {
                    File.WriteAllText(Path.Combine(Root, "help-shots-capture.txt"),
                        captureSeq + " " + pendingCapture);
                    captureHeldUntil = DateTime.UtcNow.AddSeconds(2.5);
                }
                if (DateTime.UtcNow < captureHeldUntil) return;
                pendingCapture = null;
            }
            if (phase > 0 && DateTime.UtcNow > deadline) throw new Exception("Timed out at phase " + phase);
            switch (phase)
            {
                case 0: Setup(); ShowPicker(everyone.Id, androidBody); Capture("help-picker-body-filter"); phase = 1; return;
                case 1: ShowPicker(androidOnly.Id, -1); Capture("help-plan-pinned"); phase = 2; return;
                case 2: ShowNewPlan(); Capture("help-newplan-body"); phase = 3; return;
                case 3: CloseNewPlan(); ShowOverview(); Capture("help-overview-notforbody"); phase = 4; return;
                case 4: Finish(); return;
            }
        }
        catch (Exception ex)
        {
            Write("ERROR " + ex);
            File.WriteAllText(Path.Combine(Root, "help-shots-stage.txt"), "FAILED " + ex.Message);
            phase = 99;
        }
    }

    static void Capture(string name, int frames = 3)
    {
        Find.TickManager.CurTimeSpeed = TimeSpeed.Normal;
        captureSeq++;
        pendingCapture = name;
        waitFrames = frames;
    }

    static void Setup()
    {
        Find.TickManager.Pause();
        Map map = Find.CurrentMap;
        Write("screen " + UI.screenWidth + "x" + UI.screenHeight + " uiScale " + Prefs.UIScale);
        human = map.mapPawns.FreeColonistsSpawned.First(p => p.RaceProps.body == BodyDefOf.Human);
        var store = ImplannerStore.Current;
        var model = new PlannerModel();
        store.Model = model;
        model.SetAutomationPaused(false);
        model.SetAutoProduction(false);
        model.SetSurgeryConcurrency(20);
        model.SetCountHospitalized(false);
        model.SetAutoDoctorFloor(true);
        everyone = model.CreatePlan("Everyone", store.TakePlanId)!;
        model.SetImplantSlot(everyone.Id, "BionicEye", new PartAddress("Eye", 0), true);
        model.SetImplantSlot(everyone.Id, "BionicEye", new PartAddress("Eye", 1), true);
        model.SetImplantSlot(everyone.Id, "BionicLeg", new PartAddress("Leg", 0), true);
        model.SetImplantSlot(everyone.Id, "BionicLeg", new PartAddress("Leg", 1), true);
        androidOnly = model.CreatePlan("Androids", store.TakePlanId, 0, "Android4Tier")!;
        model.SetImplantSlot(androidOnly.Id, "ALReceptor", new PartAddress("LeftSightSensor", 0), true);
        model.SetImplantSlot(androidOnly.Id, "ALReceptor", new PartAddress("RightSightSensor", 0), true);
        Plan humans = model.CreatePlan("Humans", store.TakePlanId, everyone.Id, "Human")!;
        model.SetImplantSlot(humans.Id, "ElbowBlade", new PartAddress("Arm", 0), true);
        // Several humans on the plans so the cards show counts and progress.
        List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned
            .Where(p => p.RaceProps.body == BodyDefOf.Human).Take(6).ToList();
        for (int i = 0; i < colonists.Count; i++)
            model.AssignPlan(colonists[i].thingIDNumber, i < 4 ? everyone.Id : humans.Id);
        // One human forced onto the android plan (what an import can leave).
        model.AssignPlan(human.thingIDNumber, androidOnly.Id);
        store.Bump(PlannerChange.All);
        var bodies = (List<BodyDef>)AccessTools.Method(AccessTools.TypeByName("Implanner.Catalogs"), "PlannableBodies").Invoke(null, null);
        androidBody = bodies.FindIndex(b => b.defName == "Android4Tier");
        deadline = DateTime.UtcNow.AddSeconds(50);
    }

    static void ShowPicker(int planId, int body)
    {
        OpenDialog("Plans");
        object plans = Traverse.Create(dialog).Field("view").Field("plans").GetValue();
        Traverse.Create(plans).Field("SelectedPlanId").SetValue(planId);
        Traverse.Create(plans).Field("Body").SetValue(body);
        Traverse.Create(plans).Field("Region").SetValue(0);
    }

    static void ShowNewPlan()
    {
        Type type = typeof(ImplannerStore).Assembly.GetType("Implanner.UI.Dialog_NewPlan");
        newPlan = (Window)Activator.CreateInstance(type);
        Find.WindowStack.Add(newPlan);
        AccessTools.Method(type, "SetBody").Invoke(newPlan, new object[] { "Android4Tier" });
    }

    static void CloseNewPlan()
    {
        newPlan?.Close(false);
        newPlan = null;
    }

    static void ShowOverview()
    {
        OpenDialog("Overview");
        object overview = Traverse.Create(dialog).Field("view").Field("overview").GetValue();
        Traverse.Create(overview).Field("SelectedPawnId").SetValue(human.thingIDNumber);
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
        File.WriteAllText(Path.Combine(Root, "help-shots-stage.txt"), "SUCCESS");
    }
}
