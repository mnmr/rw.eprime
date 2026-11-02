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
using Verse.AI;
using Plan = Implanner.Core.Plan;

namespace AndroidTiersProbe;

// Keep the patient at the native bed-rest toil while drafted surgeons
// receive explicit jobs (the ImplantSafety pattern).
public sealed class ScenarioRestDriver : JobDriver_LayDown
{
    public override bool LookForOtherJobs => false;
}

// Android Tiers Continued against real catalog data, real bills, the
// ordinary reconciler and native surgeon jobs:
//  AT1 sides: an android's left and right sight sensors are separate,
//      labelled slots, and planning the RIGHT one installs on the right.
//  AT2 workers: android implants (Recipe_InstallImplantAndroid) are
//      ordinary implants, not modules needing an artificial host.
//  AT3 floors: android operations check Crafting; the bill's floor comes
//      from the surgeon pool that actually performs them (doctors, then
//      crafters with "repair androids" under the Android Tiers setting).
[StaticConstructorOnStartup]
public static class Probe
{
    const string Package = "eprime.implanner.androidtiers";
    static Map map;
    static Pawn patient, doctor, crafter;
    static Building_Bed bed;
    static BodyPartRecord left, right;
    static Thing rightItem, leftItem;
    static Bill_Medical active;
    static Plan plan;
    static Window dialog;
    static int phase, failures, startedTick, waitFrames, captureSeq, followFrame;
    static string pendingCapture;
    static DateTime deadline, captureHeldUntil;
    static string Root => GenFilePaths.SaveDataFolderPath;

    static Probe() => new Harmony(Package).Patch(
        AccessTools.Method(typeof(Root_Play), "Update"),
        postfix: new HarmonyMethod(typeof(Probe), nameof(Update)));

    static void Write(string message)
    {
        Log.Message("[AndroidTiers] " + message);
        File.AppendAllText(Path.Combine(Root, "android-tiers.txt"), message + Environment.NewLine);
    }

    static void Check(bool condition, string message)
    {
        Write((condition ? "PASS " : "FAIL ") + message);
        if (!condition) failures++;
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception("Fixture: " + message);
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
                // The run script polls the request every 150 ms and captures
                // the frame. Files reach the host one way only under WSL, so
                // the frame is held for a bounded 2.5 s instead of an ack.
                if (waitFrames-- > 0) return;
                if (waitFrames == -1)
                {
                    File.WriteAllText(Path.Combine(Root, "android-tiers-capture.txt"),
                        captureSeq + " " + pendingCapture);
                    captureHeldUntil = DateTime.UtcNow.AddSeconds(2.5);
                }
                if (DateTime.UtcNow < captureHeldUntil) return;
                pendingCapture = null;
            }
            if (phase == 0) { Setup(); return; }
            if (DateTime.UtcNow > deadline) throw new Exception("Timed out: phase=" + phase
                + "; surgeon job=" + (phase < 5 ? doctor : crafter)?.CurJob
                + "; patient=" + patient.CurJob + "; bed=" + patient.CurrentBed()
                + "; bill=" + active?.DeletedOrDereferenced);
            if (phase == 1) { ShowOptions("options-doctors"); phase = 2; return; }
            if (phase == 2) { CloseDialog(); BeginDoctorSurgery(); return; }
            if (phase == 3) { DoctorSurgery(); return; }
            if (phase == 4) { CrafterSetting(); return; }
            if (phase == 6) { CloseDialog(); BeginCrafterSurgery(); return; }
            if (phase == 7) { CrafterSurgery(); return; }
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
        // Time keeps running at normal speed: the run guard counts ticks as
        // progress, and software-rendered frames under WSL are slow.
        Find.TickManager.CurTimeSpeed = TimeSpeed.Normal;
        captureSeq++;
        pendingCapture = name;
        waitFrames = frames;
    }

    // ------------------------------------------------------------ Setup

    static void Setup()
    {
        Find.TickManager.Pause();
        map = Find.CurrentMap;
        IntVec3 near = map.mapPawns.FreeColonistsSpawned.First().Position;
        foreach (Pawn occupant in map.mapPawns.AllPawnsSpawned.ToArray())
        {
            if (occupant.jobs != null) occupant.jobs.curJob = null;
            occupant.DeSpawn();
        }
        doctor = Colonist(p => !p.skills.GetSkill(SkillDefOf.Medicine).TotallyDisabled
            && !p.skills.GetSkill(SkillDefOf.Crafting).TotallyDisabled
            && !p.WorkTypeIsDisabled(WorkTypeDefOf.Doctor));
        patient = PawnGenerator.GeneratePawn(PawnKindDef.Named("AndroidT3ColonistGeneral"), Faction.OfPlayer);
        foreach (Pawn pawn in new[] { patient, doctor })
        {
            foreach (Hediff hediff in pawn.health.hediffSet.hediffs.ToArray())
                if (hediff is Hediff_Injury || hediff is Hediff_MissingPart || hediff.def.isBad && hediff.def.tendable)
                    pawn.health.RemoveHediff(hediff);
            GenSpawn.Spawn(pawn, CellFinder.RandomClosewalkCellNear(near, map, 6), map);
            foreach (Need need in pawn.needs.AllNeeds) need.CurLevelPercentage = 1;
        }
        // Medicine 20 but Crafting 8: a Medical floor (the old rule) would
        // keep this doctor off every android bill.
        doctor.skills.GetSkill(SkillDefOf.Medicine).Level = 20;
        doctor.skills.GetSkill(SkillDefOf.Crafting).Level = 8;
        doctor.workSettings.SetPriority(WorkTypeDefOf.Doctor, 1);
        doctor.drafter.Drafted = true;
        if (!patient.skills.GetSkill(SkillDefOf.Crafting).TotallyDisabled)
            patient.skills.GetSkill(SkillDefOf.Crafting).Level = 3;
        Require(patient.RaceProps.FleshType.defName == "AndroidTier", "patient is an Android Tiers android");
        var body = patient.RaceProps.body;
        left = body.GetPartsWithDef(DefDatabase<BodyPartDef>.GetNamed("LeftSightSensor")).Single();
        right = body.GetPartsWithDef(DefDatabase<BodyPartDef>.GetNamed("RightSightSensor")).Single();
        bed = SpawnBed();
        doctor.Position = CellFinder.RandomClosewalkCellNear(bed.Position, map, 2);
        doctor.Notify_Teleported(true, false);
        SetAndroidTiersCraftersOnly(false);

        CatalogChecks();

        PlannerModel model = ResetModel();
        var store = ImplannerStore.Current;
        plan = model.CreatePlan("Android sensors", store.TakePlanId);
        model.SetImplantSlot(plan.Id, "ALReceptor", new PartAddress("RightSightSensor", 0), true);
        model.AssignPlan(patient.thingIDNumber, plan.Id);
        store.Bump(PlannerChange.All);
        rightItem = Spawn("MultiSpectrumEye");
        Spawn("RepairKitAT").stackCount = 10;
        Reconcile();
        string key = GoalKeys.ImplantSlot(plan.Implants[0], new PartAddress("RightSightSensor", 0));
        Check(model.Reservations.TryGetValue(rightItem.thingIDNumber, out ItemReservation reservation)
            && reservation.GoalKey == key, "AT1: the right sensor goal reserves the advanced light sensor (" + key + ")");
        active = patient.BillStack.Bills.OfType<Bill_Medical>().SingleOrDefault();
        Check(active != null && active.Part == right && active.recipe.defName == "InstallMultiSpectrumEye",
            "AT1: the operation targets the RIGHT sight sensor (part " + active?.Part?.Label + ")");
        Require(active != null, "an operation was scheduled");
        Check(active.recipe.workSkill == SkillDefOf.Crafting, "AT3: android operations check Crafting");
        int poolFloor = FloorEndingWith("|Crafting/Doctors");
        Check(poolFloor == 8, "AT3: the doctors' Crafting floor is published (" + poolFloor + ")");
        Check(active.allowedSkillRange.min == 8,
            "AT3: the bill asks for Crafting 8, not the best doctor's Medicine 20 (" + active.allowedSkillRange.min + ")");

        OpenDialog("Plans");
        object plansState = Traverse.Create(dialog).Field("view").Field("plans").GetValue();
        Traverse.Create(plansState).Field("SelectedPlanId").SetValue(plan.Id);
        ((QuickSearchWidget)Traverse.Create(plansState).Field("Search").GetValue()).filter.Text = "light sensor";
        Capture("picker");
        phase = 1;
        startedTick = Find.TickManager.TicksGame;
        deadline = DateTime.UtcNow.AddSeconds(50);
    }

    static void CatalogChecks()
    {
        Type catalogs = AccessTools.TypeByName("Implanner.Catalogs");
        object Entry(string def) => AccessTools.Method(catalogs, "ImplantByDefName").Invoke(null, new object[] { def });
        List<string> Slots(object entry)
        {
            var result = new List<string>();
            foreach (object slot in (IList)Traverse.Create(entry).Property("Slots").GetValue())
                result.Add(Traverse.Create(slot).Property<PartAddress>("Address").Value.Key
                    + "=" + Traverse.Create(slot).Property<string>("Label").Value);
            return result;
        }
        object sensor = Entry("ALReceptor");
        Require(sensor != null, "advanced light sensor is in the catalog");
        string sensorSlots = string.Join(", ", Slots(sensor));
        Check(sensorSlots == "LeftSightSensor#0=left sight sensor, RightSightSensor#0=right sight sensor",
            "AT1: the advanced light sensor has a labelled left and right slot (" + sensorSlots + ")");
        Write("advanced light sensor group: " + Traverse.Create(sensor).Property<string>("GroupLabel").Value);

        object mind = Entry("SoldierMind");
        Require(mind != null, "a mind implant is in the catalog");
        Check(!Traverse.Create(mind).Property<bool>("MountsOnArtificialParts").Value
            && !Traverse.Create(mind).Property<bool>("RequiresArtificialPart").Value,
            "AT2: mind implants are ordinary implants, not modules needing a host");
        object chip = Entry("ATPP_HediffVX0Chip");
        string chipSlots = chip != null ? string.Join(", ", Slots(chip)) : "absent";
        Check(chipSlots.Contains("Brain#0=brain") && chipSlots.Contains("ArtificialBrain#0="),
            "AT1: the VX0 chip lists the brain and the artificial brain (" + chipSlots + ")");

        Type conflicts = AccessTools.TypeByName("Implanner.ImplantConflicts");
        bool Conflict(string a, string slotA, string b, string slotB) => (bool)AccessTools.Method(conflicts, "Conflicts",
            new[] { typeof(string), typeof(PartAddress), typeof(string), typeof(PartAddress) })
            .Invoke(null, new object[] { a, ParseSlot(slotA), b, ParseSlot(slotB) });
        // Weaker-than-natural parts (crude sensors, Pit Fighter hands) are
        // not plannable, so the pairs use the advanced and archotech kinds.
        Check(Conflict("ALReceptor", "RightSightSensor#0", "AL2Receptor", "RightSightSensor#0")
            && !Conflict("ALReceptor", "LeftSightSensor#0", "AL2Receptor", "RightSightSensor#0"),
            "AT1: two sensors on the same side conflict, opposite sides do not");
        Check(Conflict("ARArm", "LeftMechanicalShoulder#0", "AR2Arm", "LeftMechanicalShoulder#0")
            && !Conflict("ARArm", "RightMechanicalShoulder#0", "AR2Arm", "LeftMechanicalShoulder#0"),
            "AT1: two android arms conflict on the same shoulder only");

        string Legacy(string def, int ordinal) => string.Join(",", ((IReadOnlyList<PartAddress>)AccessTools
            .Method(catalogs, "LegacySlots").Invoke(null, new object[] { def, ordinal })).Select(a => a.Key));
        // Each pawn enumerated LeftSightSensor then RightSightSensor, so the
        // old placeholder ordinal 0 acted on the left side.
        Check(Legacy("ALReceptor", 0) == "LeftSightSensor#0" && Legacy("ALReceptor", 1) == "RightSightSensor#0"
            && Legacy("ALReceptor", 2) == ""
            && Legacy("ATPP_HediffVX0Chip", 0).Split(',').OrderBy(s => s).SequenceEqual(new[] { "ArtificialBrain#0", "Brain#0" }),
            "Migration: old slot ordinals keep acting where they acted (" + Legacy("ATPP_HediffVX0Chip", 0) + ")");
    }

    static PartAddress ParseSlot(string text)
    {
        Require(PartAddress.TryParse(text, out PartAddress address), "slot " + text);
        return address;
    }

    // ------------------------------------------------- Doctor surgery

    static void BeginDoctorSurgery()
    {
        active.recipe.workAmount = 60f;
        active.recipe.surgerySuccessChanceFactor = 100f;
        phase = 3;
        Find.TickManager.CurTimeSpeed = TimeSpeed.Ultrafast;
    }

    static void DoctorSurgery()
    {
        if (!patient.health.hediffSet.HasHediff(HediffDef.Named("ALReceptor"), right))
        {
            StartSurgeon(doctor, active);
            Find.TickManager.CurTimeSpeed = TimeSpeed.Ultrafast;
            return;
        }
        Check(rightItem.Destroyed, "AT1: the doctor consumed the reserved advanced light sensor");
        Check(!patient.health.hediffSet.HasHediff(HediffDef.Named("ALReceptor"), left),
            "AT1: the left sight sensor is untouched");
        Write("Doctor surgery done at tick +" + (Find.TickManager.TicksGame - startedTick));
        phase = 4;
    }

    // ------------------------------------------------ Crafter surgery

    static Type AndroidTiersBridge => typeof(ImplannerStore).Assembly.GetType("Implanner.AndroidTiers");

    static void CrafterSetting()
    {
        PlannerModel model = ImplannerStore.Current.Model;
        if (followFrame == 0)
        {
            // Load path: a save whose option disagrees with Android Tiers is
            // corrected on the first update after loading.
            model.SetAndroidSurgeryByCrafters(true);
            AccessTools.Field(AndroidTiersBridge, "FollowPending").SetValue(null, true);
            followFrame = 1;
            return;
        }
        if (followFrame == 1)
        {
            Check(!model.AndroidSurgeryByCrafters && !(bool)AccessTools.Field(AndroidTiersBridge, "FollowPending").GetValue(null),
                "AT3: after a load the option follows Android Tiers (off)");
            followFrame = 2;
        }
        // Settings window path: Android Tiers switched to crafters, then the
        // game writes its settings as the window closes.
        SetAndroidTiersCraftersOnly(true);
        LoadedModManager.ModHandles.Single(m => m.GetType().FullName == "MOARANDROIDS.AndroidTiersPP").WriteSettings();
        Check(model.AndroidSurgeryByCrafters, "AT3: writing the Android Tiers settings makes the option follow (on)");
        OpenDialog("Options");
        Capture("options-crafters");
        phase = 6;
    }

    static void ShowOptions(string name)
    {
        OpenDialog("Options");
        Capture(name);
    }

    static void BeginCrafterSurgery()
    {
        crafter = Colonist(p => !p.skills.GetSkill(SkillDefOf.Crafting).TotallyDisabled
            && !p.WorkTypeIsDisabled(DefDatabase<WorkTypeDef>.GetNamed("Smithing")));
        GenSpawn.Spawn(crafter, CellFinder.RandomClosewalkCellNear(bed.Position, map, 2), map);
        foreach (Need need in crafter.needs.AllNeeds) need.CurLevelPercentage = 1;
        crafter.skills.GetSkill(SkillDefOf.Crafting).Level = 12;
        crafter.workSettings.SetPriority(DefDatabase<WorkTypeDef>.GetNamed("Smithing"), 1);
        ThingComp owner = crafter.AllComps.SingleOrDefault(c => c.GetType().FullName == "MOARANDROIDS.CompSurrogateOwner");
        Require(owner != null, "Android Tiers gives colonists the surrogate owner comp");
        AccessTools.Field(owner.GetType(), "repairAndroids").SetValue(owner, true);
        crafter.drafter.Drafted = true;

        PlannerModel model = ImplannerStore.Current.Model;
        model.SetImplantSlot(plan.Id, "ALReceptor", new PartAddress("LeftSightSensor", 0), true);
        ImplannerStore.Current.Bump(PlannerChange.All);
        leftItem = Spawn("MultiSpectrumEye");
        Reconcile();
        active = patient.BillStack.Bills.OfType<Bill_Medical>().SingleOrDefault(b => b.Part == left);
        Require(active != null, "the left operation was scheduled");
        int poolFloor = FloorEndingWith("|Crafting/Crafters");
        Check(poolFloor == 12, "AT3: the crafters' Crafting floor is published (" + poolFloor + ")");
        Check(active.allowedSkillRange.min == 12,
            "AT3: crafter mode asks for the best crafter's Crafting 12 (" + active.allowedSkillRange.min + ")");
        PutPatientInBed();
        Check(FindJob(doctor, active, exact: true) == null,
            "AT3: the doctor gets no android operation while crafters operate");
        active.recipe.workAmount = 60f;
        active.recipe.surgerySuccessChanceFactor = 100f;
        phase = 7;
        Find.TickManager.CurTimeSpeed = TimeSpeed.Ultrafast;
    }

    static void CrafterSurgery()
    {
        if (!patient.health.hediffSet.HasHediff(HediffDef.Named("ALReceptor"), left))
        {
            StartSurgeon(crafter, active);
            Find.TickManager.CurTimeSpeed = TimeSpeed.Ultrafast;
            return;
        }
        Check(leftItem.Destroyed, "AT3: the crafter consumed the second sensor");
        Check(patient.health.hediffSet.HasHediff(HediffDef.Named("ALReceptor"), right),
            "AT1: both sides now carry an advanced light sensor");
        SetAndroidTiersCraftersOnly(false);
        Finish();
    }

    // ---------------------------------------------------------- Helpers

    static Pawn Colonist(Func<Pawn, bool> valid)
    {
        for (int attempt = 0; attempt < 50; attempt++)
        {
            Pawn pawn = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
            if (valid(pawn)) return pawn;
        }
        throw new Exception("Fixture: no suitable colonist generated");
    }

    static int FloorEndingWith(string suffix)
    {
        foreach (KeyValuePair<string, int> pair in ImplannerStore.Current.Model.DoctorFloors)
            if (pair.Key.EndsWith(suffix, StringComparison.Ordinal)) return pair.Value;
        return -1;
    }

    static void SetAndroidTiersCraftersOnly(bool value) =>
        AccessTools.Field(AccessTools.TypeByName("MOARANDROIDS.Settings"), "androidsCanOnlyBeHealedByCrafter")
            .SetValue(null, value);

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

    static void CloseDialog()
    {
        dialog?.Close(false);
        dialog = null;
    }

    static PlannerModel ResetModel()
    {
        var model = new PlannerModel();
        ImplannerStore.Current.Model = model;
        model.SetAutomationPaused(false);
        model.SetAutoProduction(false);
        model.SetSurgeryConcurrency(20);
        model.SetCountHospitalized(false);
        model.SetAutoDoctorFloor(true);
        model.SetIteration(IterationStrategy.Asap);
        return model;
    }

    static void Reconcile() => AccessTools.Method(typeof(ImplannerStore).Assembly.GetType("Implanner.PlannerReconciler"),
        "Reconcile").Invoke(null, new object[] { ImplannerStore.Current, true });

    static Thing Spawn(string defName)
    {
        Thing item = ThingMaker.MakeThing(ThingDef.Named(defName));
        IntVec3 cell = GenRadial.RadialCellsAround(bed.Position, 8, false).First(c =>
            c.InBounds(map) && c.Standable(map) && c.GetEdifice(map) == null
            && c.GetFirstItem(map) == null && c.GetFirstPawn(map) == null
            && c.DistanceToSquared(bed.Position) >= 4
            && doctor.CanReach(c, PathEndMode.OnCell, Danger.Deadly));
        GenSpawn.Spawn(item, cell, map);
        item.SetForbidden(false, false);
        return item;
    }

    static Building_Bed SpawnBed()
    {
        foreach (IntVec3 cell in GenRadial.RadialCellsAround(patient.Position, 25, true))
        {
            bool free = true;
            foreach (IntVec3 occupied in GenAdj.OccupiedRect(cell, Rot4.North, ThingDefOf.Bed.size))
                if (!occupied.InBounds(map) || !occupied.Standable(map) || occupied.GetEdifice(map) != null
                    || occupied.GetFirstItem(map) != null || occupied.GetFirstPawn(map) != null
                    || !doctor.CanReach(occupied, PathEndMode.OnCell, Danger.Deadly)) { free = false; break; }
            if (!free) continue;
            var result = (Building_Bed)ThingMaker.MakeThing(ThingDefOf.Bed, ThingDefOf.WoodLog);
            GenSpawn.Spawn(result, cell, map);
            result.SetFaction(Faction.OfPlayer);
            result.Medical = true;
            return result;
        }
        throw new Exception("No bed location");
    }

    static void PutPatientInBed()
    {
        if (patient.CurrentBed() == bed) return;
        if (patient.CurJob?.def.defName == "AndroidTiers_Rest" && patient.CurJob.targetA.Thing == bed) return;
        patient.jobs.StopAll();
        patient.Position = bed.GetSleepingSlotPos(0);
        patient.Notify_Teleported(true, false);
        Job rest = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("AndroidTiers_Rest"), bed);
        rest.playerForced = true;
        rest.forceSleep = true;
        rest.restUntilHealed = true;
        rest.expiryInterval = 60000;
        patient.jobs.StartJob(rest, JobCondition.InterruptForced);
    }

    /// The surgeon's job on the patient from any bill-giving work giver
    /// (vanilla doctors and Android Tiers' crafter copies). Vanilla may
    /// first need to haul an obstruction off the bed, so only an exact
    /// query insists on the bill itself.
    static Job FindJob(Pawn surgeon, Bill_Medical bill, bool exact = false)
    {
        bill.nextTickToSearchForIngredients = 0;
        foreach (WorkGiverDef def in DefDatabase<WorkGiverDef>.AllDefsListForReading)
        {
            if (!def.billGiversAllHumanlikes || !(def.Worker is WorkGiver_DoBill giver)) continue;
            Job job = giver.JobOnThing(surgeon, patient, true);
            if (job != null && (!exact || job.bill == bill)) return job;
        }
        return null;
    }

    static void StartSurgeon(Pawn surgeon, Bill_Medical bill)
    {
        PutPatientInBed();
        if (patient.CurrentBed() != bed) return;
        if (surgeon.CurJob?.bill == bill) return;
        if (surgeon.CurJob != null && surgeon.CurJob.def != JobDefOf.Wait_Combat) return;
        surgeon.jobs.StopAll();
        Job job = FindJob(surgeon, bill);
        if (job == null) return;
        surgeon.jobs.TryTakeOrderedJob(job);
    }

    static void Finish()
    {
        phase = 99;
        CloseDialog();
        Find.TickManager.Pause();
        string status = failures == 0 ? "SUCCESS" : "FAILED " + failures;
        Write(status + "; elapsed game ticks=" + (Find.TickManager.TicksGame - startedTick));
        File.WriteAllText(Path.Combine(Root, "android-tiers-stage.txt"), status);
    }
}
