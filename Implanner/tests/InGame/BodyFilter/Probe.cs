using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using Implanner;
using Implanner.Core;
using RimWorld;
using Verse;
using Plan = Implanner.Core.Plan;

namespace BodyFilterProbe;

// Per-pawn slot applicability and the picker body filter (2026-11-01),
// against real catalog data with Android Tiers Continued loaded:
//  B1 a human colonist carrying a bionic arm, assigned a plan with an elbow
//     blade (under the arm) and a bionic eye: the blade is outside their
//     target (no item held, the eye remains), and the colonist details
//     list it as "Blocked by Bionic arm".
//  B2 the plan editor's body filter: "All bodies" lists human and android
//     slots, the Android body hides the elbow blade and shows the android
//     light sensor, the Human body the reverse; no empty group nodes.
[StaticConstructorOnStartup]
public static class Probe
{
    const string Package = "eprime.implanner.bodyfilter";
    static int phase, failures, captureSeq, waitFrames;
    static string pendingCapture;
    static DateTime captureHeldUntil, deadline;
    static Window dialog;
    static Pawn patient;
    static Plan plan;
    static List<BodyDef> bodies;
    static int androidBody = -1, humanBody = -1;
    static string Root => GenFilePaths.SaveDataFolderPath;

    static Probe() => new Harmony(Package).Patch(
        AccessTools.Method(typeof(Root_Play), "Update"),
        postfix: new HarmonyMethod(typeof(Probe), nameof(Update)));

    static void Write(string message)
    {
        Log.Message("[BodyFilter] " + message);
        File.AppendAllText(Path.Combine(Root, "body-filter.txt"), message + Environment.NewLine);
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
                // The run script polls the request and captures the frame;
                // the frame is held for a bounded 2.5 s.
                if (waitFrames-- > 0) return;
                if (waitFrames == -1)
                {
                    File.WriteAllText(Path.Combine(Root, "body-filter-capture.txt"),
                        captureSeq + " " + pendingCapture);
                    captureHeldUntil = DateTime.UtcNow.AddSeconds(2.5);
                }
                if (DateTime.UtcNow < captureHeldUntil) return;
                pendingCapture = null;
            }
            if (phase > 0 && DateTime.UtcNow > deadline) throw new Exception("Timed out at phase " + phase);
            switch (phase)
            {
                case 0: Setup(); ShowOverview(); Capture("overview-blocked"); phase = 1; return;
                case 1: CheckDetails(); ShowPicker(androidBody); Capture("picker-android"); phase = 2; return;
                case 2: CheckTree("Android", expectElbow: false, expectSensor: true); ShowPicker(humanBody); Capture("picker-human"); phase = 3; return;
                case 3: CheckTree("Human", expectElbow: true, expectSensor: false); ShowPicker(-1); Capture("picker-all"); phase = 4; return;
                case 4: CheckTree("All bodies", expectElbow: true, expectSensor: true); PinnedSetup(); ShowOverview(); Capture("overview-notforbody"); phase = 5; return;
                case 5: CheckNotForBody(); CheckPlanMenu(); RestoreAssignment(); ShowPinnedPicker(); Capture("picker-pinned-android"); phase = 6; return;
                case 6: CheckPinnedTree(); Finish(); return;
            }
        }
        catch (Exception ex)
        {
            Write("ERROR " + ex);
            failures++;
            Finish();
        }
    }

    static void Capture(string name, int frames = 3)
    {
        Find.TickManager.CurTimeSpeed = TimeSpeed.Normal;
        captureSeq++;
        pendingCapture = name;
        waitFrames = frames;
    }

    static Plan androidOnly, humanOnly;

    // ---------------------------------------------------- Body-typed plans
    //  B3 creation: pinned plans, a pinned child of an unpinned base, and a
    //     refused unpinned child of a pinned base (the synced command).
    //  B4 enlisting: the synced assignment refuses a human for an Android
    //     plan, accepts the Human plan; a forced mismatch reads "Not for
    //     this body" and the plan menu greys the Android plan.
    //  B5 picker: a pinned plan fixes the body label and lists that body's
    //     slots only.
    static void PinnedSetup()
    {
        var store = ImplannerStore.Current;
        PlannerModel model = store.Model;
        int before = model.Plans.Count;
        PlannerCommands.CreatePlan("Android only", 0, "Android4Tier", "");
        PlannerCommands.CreatePlan("Human only", 0, "Human", "");
        PlannerCommands.CreatePlan("Human child of any", plan.Id, "Human", "");
        androidOnly = model.Plans.First(p => p.Name == "Android only");
        humanOnly = model.Plans.First(p => p.Name == "Human only");
        Plan humanChild = model.Plans.First(p => p.Name == "Human child of any");
        Check(androidOnly.Body == "Android4Tier" && humanOnly.Body == "Human"
            && humanChild.Body == "Human" && humanChild.BasePlanId == plan.Id,
            "B3: pinned plans and a pinned child of an unpinned base are created with their bodies");
        PlannerCommands.CreatePlan("Any of human", humanOnly.Id, "", "");
        PlannerCommands.CreatePlan("Android of human", humanOnly.Id, "Android4Tier", "");
        Check(model.Plans.Count == before + 3,
            "B3: an unpinned or differently pinned child of a pinned base is refused (" + (model.Plans.Count - before) + " created)");

        PlannerCommands.AssignPlan(patient.thingIDNumber, androidOnly.Id);
        Check(model.AssignedPlan(patient.thingIDNumber) == plan,
            "B4: the synced assignment refuses a human colonist for the Android plan");
        PlannerCommands.AssignPlan(patient.thingIDNumber, humanOnly.Id);
        Check(model.AssignedPlan(patient.thingIDNumber) == humanOnly,
            "B4: the synced assignment accepts the Human plan");
        // Forced through the model (what an import or a removed race mod
        // could leave behind): the overview must show it, not "Done".
        model.AssignPlan(patient.thingIDNumber, androidOnly.Id);
        store.Bump(PlannerChange.All);
    }

    static object OverviewRowOf(Pawn pawn)
    {
        object overview = Traverse.Create(dialog).Field("view").Field("overview").GetValue();
        object snapshot = Traverse.Create(overview).Method("Current", ImplannerStore.Current).GetValue();
        var rows = (IList)Traverse.Create(snapshot).Field("Rows").GetValue();
        foreach (object row in rows)
            if (Traverse.Create(row).Field<int>("PawnId").Value == pawn.thingIDNumber) return row;
        throw new Exception("overview row for " + pawn.LabelShort + " not found");
    }

    static void CheckNotForBody()
    {
        object row = OverviewRowOf(patient);
        string state = Traverse.Create(row).Field<string>("StateText").Value;
        string progress = Traverse.Create(row).Field<string>("ProgressText").Value;
        float bar = Traverse.Create(row).Field<float>("Progress").Value;
        Check(state == "IMP_StateNotForBody".Translate() && progress == "0 / 0" && bar == 0f,
            "B4: a human forced onto the Android plan reads '" + state + "' with " + progress + " and an empty bar");
        bool held = ImplannerStore.Current.Model.Reservations.Values.Any(r => r.PawnId == patient.thingIDNumber);
        Check(!held && patient.BillStack.Count == 0, "B4: nothing is reserved or scheduled for the mismatched colonist");
    }

    static void CheckPlanMenu()
    {
        object row = OverviewRowOf(patient);
        Type type = typeof(ImplannerStore).Assembly.GetType("Implanner.UI.ImplannerView");
        var menu = (IEnumerable)AccessTools.Method(type, "GeneratePlanMenu").Invoke(null, new[] { row });
        bool androidGreyed = false, humanOffered = false;
        foreach (object element in menu)
        {
            var option = (FloatMenuOption)Traverse.Create(element).Field("option").GetValue();
            if (option.Label.StartsWith("Android only")) androidGreyed = option.Disabled && option.Label.Contains("Android");
            if (option.Label == "Human only") humanOffered = !option.Disabled;
        }
        Check(androidGreyed && humanOffered, "B4: the plan menu greys the Android plan with its body and offers the Human plan");
    }

    static void RestoreAssignment() => PlannerCommands.AssignPlan(patient.thingIDNumber, humanOnly.Id);

    static void ShowPinnedPicker()
    {
        OpenDialog("Plans");
        object plans = Traverse.Create(dialog).Field("view").Field("plans").GetValue();
        Traverse.Create(plans).Field("SelectedPlanId").SetValue(androidOnly.Id);
        Traverse.Create(plans).Field("Body").SetValue(-1);
        Traverse.Create(plans).Field("Region").SetValue(0);
    }

    static void CheckPinnedTree()
    {
        object plans = Traverse.Create(dialog).Field("view").Field("plans").GetValue();
        object snapshot = Traverse.Create(plans).Method("Current", ImplannerStore.Current).GetValue();
        bool pinned = Traverse.Create(snapshot).Field<bool>("PinnedBody").Value;
        string text = Traverse.Create(snapshot).Field<string>("BodyFilterText").Value;
        var labels = Traverse.Create(snapshot).Field<string[]>("BodyLabels").Value;
        var tree = (IList)Traverse.Create(snapshot).Field("Tree").GetValue();
        bool elbow = false;
        for (int i = 0; i < tree.Count; i++)
            if (Traverse.Create(tree[i]).Field<string>("DefName").Value == "ElbowBlade") elbow = true;
        Traverse.Create(plans).Field("Region").SetValue(2);
        var head = (IList)Traverse.Create(Traverse.Create(plans).Method("Current", ImplannerStore.Current).GetValue()).Field("Tree").GetValue();
        bool sensor = false;
        for (int i = 0; i < head.Count; i++)
            if (Traverse.Create(head[i]).Field<string>("DefName").Value == "ALReceptor") sensor = true;
        Check(pinned && labels.Length == 0 && text == "IMP_PlanForBody".Translate("Android").ToString(),
            "B5: the pinned plan shows a fixed body label '" + text + "' and no filter menu");
        Check(!elbow && sensor, "B5: the pinned plan lists android slots only (elbow blade " + (elbow ? "shown" : "hidden") + ", light sensor " + (sensor ? "shown" : "hidden") + ")");
        // The plan card carries the body caption.
        var cards = (IList)Traverse.Create(snapshot).Field("Plans").GetValue();
        bool caption = false;
        foreach (object card in cards)
            if (Traverse.Create(card).Field<int>("PlanId").Value == androidOnly.Id)
                caption = Traverse.Create(card).Field<string>("ExtendsText").Value == "IMP_PlanForBody".Translate("Android").ToString();
        Check(caption, "B5: the plan card's name row reads 'for Android'");
    }

    static void Setup()
    {
        Find.TickManager.Pause();
        Map map = Find.CurrentMap;
        // The untouched save must load with every plan unpinned (the
        // field is new; absent reads as any body).
        IReadOnlyList<Plan> loadedPlans = ImplannerStore.Current.Model.Plans;
        int pinnedLoaded = loadedPlans.Count(p => p.IsPinned);
        Check(pinnedLoaded == 0, "B3: the existing save's " + loadedPlans.Count + " plans load unpinned");
        patient = map.mapPawns.FreeColonistsSpawned.First(p => p.RaceProps.body == BodyDefOf.Human);
        BodyPartRecord shoulder = patient.RaceProps.body.GetPartsWithDef(BodyPartDefOf.Shoulder)[0];
        patient.health.RestorePart(shoulder);
        patient.health.AddHediff(HediffDef.Named("BionicArm"), shoulder);

        var store = ImplannerStore.Current;
        var model = new PlannerModel();
        store.Model = model;
        model.SetAutomationPaused(false);
        model.SetAutoProduction(false);
        model.SetSurgeryConcurrency(20);
        model.SetCountHospitalized(false);
        model.SetAutoDoctorFloor(true);
        plan = model.CreatePlan("Blade and eye", store.TakePlanId);
        model.SetImplantSlot(plan.Id, "ElbowBlade", new PartAddress("Arm", 0), true);
        model.SetImplantSlot(plan.Id, "BionicEye", new PartAddress("Eye", 0), true);
        model.AssignPlan(patient.thingIDNumber, plan.Id);
        store.Bump(PlannerChange.All);
        Thing blade = ThingMaker.MakeThing(ThingDef.Named("ElbowBlade"));
        GenPlace.TryPlaceThing(blade, patient.Position, map, ThingPlaceMode.Near);
        blade.SetForbidden(false, false);
        AccessTools.Method(typeof(ImplannerStore).Assembly.GetType("Implanner.PlannerReconciler"), "Reconcile")
            .Invoke(null, new object[] { store, true });

        var projection = typeof(ImplannerStore).Assembly.GetType("Implanner.PawnProjection");
        var evaluation = (PlanEvaluation)AccessTools.Method(projection, "Evaluate")
            .Invoke(null, new object[] { model, patient, model.EffectiveImplants(plan), false, 0, plan.Body });
        Check(evaluation.Contexts[0].Block(0) == SlotBlock.UnderArtificialPart
            && evaluation.Contexts[0].Blocker(0) == "BionicArm",
            "B1: the elbow blade slot is blocked by the installed bionic arm");
        Check(evaluation.Implants[0].Requested == 0 && evaluation.TotalUnits == 1,
            "B1: the blade is outside the target, the eye remains (" + evaluation.SatisfiedUnits + "/" + evaluation.TotalUnits + ")");
        bool bladeHeld = model.Reservations.Values.Any(r => r.PawnId == patient.thingIDNumber && r.GoalKey.Contains("ElbowBlade"));
        Check(!bladeHeld, "B1: no elbow blade is held for the colonist (" + model.Reservations.Count + " reservations)");

        bodies = (List<BodyDef>)AccessTools.Method(AccessTools.TypeByName("Implanner.Catalogs"), "PlannableBodies").Invoke(null, null);
        humanBody = bodies.IndexOf(BodyDefOf.Human);
        androidBody = bodies.FindIndex(b => b.defName == "Android4Tier");
        Check(humanBody == 0 && androidBody > 0,
            "B2: plannable bodies are human first with Android4Tier present (" + string.Join(", ", bodies.Select(b => b.defName)) + ")");
        deadline = DateTime.UtcNow.AddSeconds(50);
    }

    static void ShowOverview()
    {
        OpenDialog("Overview");
        object overview = Traverse.Create(dialog).Field("view").Field("overview").GetValue();
        Traverse.Create(overview).Field("SelectedPawnId").SetValue(patient.thingIDNumber);
    }

    static void CheckDetails()
    {
        object overview = Traverse.Create(dialog).Field("view").Field("overview").GetValue();
        var rows = Traverse.Create(overview).Field("detailRows").GetValue() as IList;
        string found = "";
        bool blocked = false;
        if (rows != null)
            foreach (object row in rows)
            {
                string label = Traverse.Create(row).Field<string>("Label").Value;
                string status = Traverse.Create(row).Field<string>("StatusText").Value;
                found += label + "=" + status + "; ";
                if (label.IndexOf("blade", StringComparison.OrdinalIgnoreCase) >= 0
                    && (status == "IMP_GoalBlockedBy".Translate("Bionic arm")
                        || status == "IMP_GoalBlocked".Translate())
                    && Traverse.Create(row).Field<bool>("Blocked").Value)
                    blocked = true;
            }
        Check(blocked, "B1: colonist details list the elbow blade as blocked by the bionic arm (" + found + ")");
    }

    static void ShowPicker(int body)
    {
        OpenDialog("Plans");
        object plans = Traverse.Create(dialog).Field("view").Field("plans").GetValue();
        Traverse.Create(plans).Field("SelectedPlanId").SetValue(plan.Id);
        Traverse.Create(plans).Field("Body").SetValue(body);
    }

    static void CheckTree(string expectedButton, bool expectElbow, bool expectSensor)
    {
        object plans = Traverse.Create(dialog).Field("view").Field("plans").GetValue();
        object snapshot = Traverse.Create(plans).Method("Current", ImplannerStore.Current).GetValue();
        var tree = (IList)Traverse.Create(snapshot).Field("Tree").GetValue();
        var labels = Traverse.Create(snapshot).Field<string[]>("BodyLabels").Value;
        string button = Traverse.Create(snapshot).Field<string>("BodyFilterText").Value;
        bool elbow = false, sensor = false, emptyNode = false;
        for (int i = 0; i < tree.Count; i++)
        {
            string def = Traverse.Create(tree[i]).Field<string>("DefName").Value;
            bool node = Traverse.Create(tree[i]).Field<bool>("Node").Value;
            if (def == "ElbowBlade") elbow = true;
            if (node && (i + 1 >= tree.Count || Traverse.Create(tree[i + 1]).Field<bool>("Node").Value))
                emptyNode = true;
        }
        // The light sensor sits in the Head region; the captured view is
        // Limbs, so it is read from the Head view and the region restored.
        Traverse.Create(plans).Field("Region").SetValue(2);
        var head = (IList)Traverse.Create(Traverse.Create(plans).Method("Current", ImplannerStore.Current).GetValue()).Field("Tree").GetValue();
        for (int i = 0; i < head.Count; i++)
            if (Traverse.Create(head[i]).Field<string>("DefName").Value == "ALReceptor") sensor = true;
        Traverse.Create(plans).Field("Region").SetValue(0);
        Check(labels.Length == bodies.Count + 1 && button == expectedButton,
            "B2: body filter offers " + labels.Length + " entries and reads '" + button + "' (expected '" + expectedButton + "')");
        Check(elbow == expectElbow && sensor == expectSensor,
            "B2 " + expectedButton + ": elbow blade " + (elbow ? "shown" : "hidden") + ", android light sensor " + (sensor ? "shown" : "hidden") + " (" + tree.Count + " rows)");
        Check(!emptyNode, "B2 " + expectedButton + ": no empty group node");
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
        File.WriteAllText(Path.Combine(Root, "body-filter-stage.txt"),
            failures == 0 ? "SUCCESS" : "FAILED " + failures);
    }
}
