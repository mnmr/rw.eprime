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

namespace SauridClawsProbe;

// Player report 2026-11-02 (Vanilla Races Expanded - Saurid): a colonist
// with the saurid claws gene never gets their planned bionic arm, because
// the claws (a gene-granted hediff on each hand, no item) made the arm
// replacement read as destroying something for good.
//  S1 WouldDestroy answers nothing for a bionic arm over a saurid-clawed hand
//     (the claws come from the pawn's gene; the gene stays).
//  S2 the reconcile pass schedules the arm operation (owned bill on the
//     pawn) once the arm item is reserved; capture saurid-overview shows the
//     colonist details.
[StaticConstructorOnStartup]
public static class Probe
{
    const string Package = "eprime.implanner.sauridclaws";
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
        Log.Message("[SauridClaws] " + message);
        File.AppendAllText(Path.Combine(Root, "saurid-claws.txt"), message + Environment.NewLine);
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
                    File.WriteAllText(Path.Combine(Root, "saurid-claws-capture.txt"),
                        captureSeq + " " + pendingCapture);
                    captureHeldUntil = DateTime.UtcNow.AddSeconds(2.5);
                }
                if (DateTime.UtcNow < captureHeldUntil) return;
                pendingCapture = null;
            }
            if (phase > 0 && DateTime.UtcNow > deadline) throw new Exception("Timed out at phase " + phase);
            switch (phase)
            {
                case 0: Setup(); phase = 1; return;
                case 1:
                    if (Find.TickManager.TicksGame < resumeTick + 30) return;
                    Find.TickManager.Pause();
                    CheckScheduled();
                    ShowOverview();
                    Capture("saurid-overview");
                    phase = 2;
                    return;
                case 2: Finish(); return;
            }
        }
        catch (Exception ex)
        {
            Write("ERROR " + ex);
            File.WriteAllText(Path.Combine(Root, "saurid-claws-stage.txt"), "FAILED " + ex.Message);
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
        // A healthy human colonist with natural arms, no artificial parts.
        patient = map.mapPawns.FreeColonistsSpawned.First(p => p.RaceProps.body == BodyDefOf.Human
            && p.genes != null && !p.Downed && !p.health.HasHediffsNeedingTend()
            && !p.health.hediffSet.hediffs.Any(h => h is Hediff_AddedPart || h is Hediff_MissingPart));
        Write("patient " + patient.LabelShort);
        GeneDef claws = DefDatabase<GeneDef>.GetNamed("VRESaurids_SauridClaws");
        patient.genes.AddGene(claws, xenogene: true);
        int clawCount = patient.health.hediffSet.hediffs.Count(h => h.def.defName == "VRESaurids_SauridClaws");
        Write("claw hediffs after gene: " + clawCount);
        if (clawCount == 0)
        {
            // VEF adds them on gene add; fall back to the same hediffs by hand.
            HediffDef def = DefDatabase<HediffDef>.GetNamed("VRESaurids_SauridClaws");
            foreach (BodyPartRecord hand in patient.RaceProps.body.AllParts.Where(p => p.def.defName == "Hand"))
                patient.health.AddHediff(def, hand);
            clawCount = patient.health.hediffSet.hediffs.Count(h => h.def.defName == "VRESaurids_SauridClaws");
            Write("claw hediffs added by hand: " + clawCount);
        }
        if (clawCount != 2) throw new Exception("fixture: expected claws on both hands, found " + clawCount);

        // S1: the gate's own answer for a bionic arm over each arm.
        Type surgery = typeof(ImplannerStore).Assembly.GetType("Implanner.PlannerSurgery");
        Type catalogs = typeof(ImplannerStore).Assembly.GetType("Implanner.Catalogs");
        object entry = AccessTools.Method(catalogs, "ImplantByDefName").Invoke(null, new object[] { "BionicArm" });
        var wouldDestroy = AccessTools.Method(surgery, "WouldDestroy");
        // A bionic arm replaces the shoulder (InstallBionicArm's fixed part);
        // the claws sit on the hand below it.
        foreach (BodyPartRecord shoulder in patient.RaceProps.body.AllParts.Where(p => p.def.defName == "Shoulder"))
        {
            var loss = (Hediff)wouldDestroy.Invoke(null, new object[] { patient, entry, shoulder });
            Check(loss == null, "S1 bionic arm over the " + shoulder.Label + " destroys nothing for good (gate says: "
                + (loss == null ? "nothing" : loss.LabelCap.ToString()) + ")");
        }

        var store = ImplannerStore.Current;
        var model = new PlannerModel();
        store.Model = model;
        model.SetAutomationPaused(false);
        model.SetAutoProduction(false);
        model.SetSurgeryConcurrency(20);
        model.SetCountHospitalized(false);
        model.SetAutoDoctorFloor(true);
        plan = model.CreatePlan("Arms", store.TakePlanId)!;
        model.SetImplantSlot(plan.Id, "BionicArm", new PartAddress("Shoulder", 0), true);
        model.AssignPlan(patient.thingIDNumber, plan.Id);
        Thing item = ThingMaker.MakeThing(ThingDef.Named("BionicArm"));
        GenPlace.TryPlaceThing(item, patient.Position, patient.Map, ThingPlaceMode.Near);
        item.SetForbidden(false, false);
        Write("spawned " + item.ThingID + " at " + item.Position);
        store.Bump(PlannerChange.All);
        deadline = DateTime.UtcNow.AddSeconds(50);
        resumeTick = Find.TickManager.TicksGame;
        Find.TickManager.CurTimeSpeed = TimeSpeed.Normal;
    }

    static void CheckScheduled()
    {
        PlannerModel model = ImplannerStore.Current.Model;
        IReadOnlyDictionary<string, string> owned = model.OwnedBillsFor(patient.thingIDNumber);
        int reservations = model.Reservations.Values.Count(r => r.PawnId == patient.thingIDNumber);
        bool bill = patient.BillStack.Bills.Any(b => b.recipe.defName == "InstallBionicArm");
        Write("ticks advanced " + (Find.TickManager.TicksGame - resumeTick) + ", reservations " + reservations
            + ", owned operations " + (owned?.Count ?? 0) + ", install-arm bill on pawn " + bill);
        Check(reservations == 1, "S2 the bionic arm is reserved for " + patient.LabelShort);
        Check(owned != null && owned.Count == 1 && bill,
            "S2 the arm operation is scheduled for " + patient.LabelShort + " despite the saurid claws");
    }

    static void ShowOverview()
    {
        Type type = typeof(ImplannerStore).Assembly.GetType("Implanner.UI.Dialog_Implanner");
        Type view = typeof(ImplannerStore).Assembly.GetType("Implanner.UI.ImplannerView");
        Type tabs = view.GetNestedType("Tab", AccessTools.all);
        AccessTools.Field(view, "curTab").SetValue(null, Enum.Parse(tabs, "Overview"));
        dialog = (Window)Activator.CreateInstance(type);
        Find.WindowStack.Add(dialog);
        object overview = Traverse.Create(dialog).Field("view").Field("overview").GetValue();
        Traverse.Create(overview).Field("SelectedPawnId").SetValue(patient.thingIDNumber);
    }

    static void Finish()
    {
        phase = 99;
        Write(failures == 0 ? "SUCCESS" : failures + " failure(s)");
        File.WriteAllText(Path.Combine(Root, "saurid-claws-stage.txt"),
            failures == 0 ? "SUCCESS" : "FAILED " + failures + " check(s)");
    }
}
