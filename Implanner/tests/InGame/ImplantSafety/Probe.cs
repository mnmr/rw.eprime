using System;
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

namespace ImplantSafetyProbe;

// Real game objects, QBR's live settings, ordinary medical bills and native
// doctor jobs. A rejected job must preserve both the limb and its ingredient.
[StaticConstructorOnStartup]
public static class Probe
{
    static Map map;
    static Pawn patient, doctor;
    static Building_Bed bed;
    static BodyPartRecord shoulder;
    static Thing poor, normal;
    static Bill_Medical active;
    static int phase, failures, startedTick;
    static DateTime deadline;
    static Dictionary<QualityCategory, float> multipliers;
    static string legacyKey;
    static string Root => GenFilePaths.SaveDataFolderPath;

    static Probe() => new Harmony("eprime.implanner.implantsafety").Patch(
        AccessTools.Method(typeof(Root_Play), "Update"),
        postfix: new HarmonyMethod(typeof(Probe), nameof(Update)));

    static void Write(string message)
    {
        Log.Message("[ImplantSafety] " + message);
        File.AppendAllText(Path.Combine(Root, "implant-safety.txt"), message + Environment.NewLine);
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
                if (!(window is MainTabWindow)) window.Close(false);
            if (phase == 0) { Setup(); return; }
            if (DateTime.UtcNow > deadline) throw new Exception("Native job timed out: " + doctor.CurJob);
            Find.TickManager.CurTimeSpeed = TimeSpeed.Ultrafast;
            if (phase == 10)
            {
                if (patient.CurrentBed() != bed) return;
                Job selected = FindDoctorJob(active);
                if (selected == null) return;
                var model = ImplannerStore.Current.Model;
                model.Reserve(poor.thingIDNumber, patient.thingIDNumber, legacyKey);
                model.SetOwnedBill(patient.thingIDNumber, legacyKey, active.GetUniqueLoadID());
                ImplannerStore.Current.Bump(PlannerChange.All);
                doctor.jobs.TryTakeOrderedJob(selected);
                Require(doctor.CurJob?.bill == active, "legacy doctor job really started");
                Write("Legacy job started with an unsafe real implant already selected");
                phase = 1;
                return;
            }
            if (phase == 1)
            {
                // Let a legacy in-flight job reach the game's actual finish
                // boundary. Do not call ApplyOnPawn or fake the result.
                if (doctor.CurJob?.bill == active) return;
                Check(!patient.health.hediffSet.HasHediff(HediffDef.Named("BionicArm"), shoulder),
                    "IR2: selected legacy inferior implant never replaces the healed natural arm");
                Check(!poor.Destroyed, "IR2: rejected surgery preserves its actual implant ingredient");
                BeginSafeSurgery();
                return;
            }
            if (phase == 2)
            {
                if (!patient.health.hediffSet.HasHediff(HediffDef.Named("BionicArm"), shoulder))
                {
                    if (doctor.CurJob?.bill != active) StartDoctor(active);
                    return;
                }
                Check(normal.Destroyed, "Safe surgery: native doctor consumed the Normal implant");
                Check(PawnCapacityUtility.CalculatePartEfficiency(patient.health.hediffSet, shoulder) >= 1.249f,
                    "Safe surgery: the actual installed arm supplies 125% efficiency");
                Finish();
            }
        }
        catch (Exception ex)
        {
            Write("ERROR " + ex);
            failures++;
            Finish();
        }
    }

    static void Setup()
    {
        Find.TickManager.Pause();
        map = Find.CurrentMap;
        IntVec3 near = map.mapPawns.FreeColonistsSpawned.First().Position;
        do { doctor = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer); }
        while (doctor.skills.GetSkill(SkillDefOf.Medicine).TotallyDisabled);
        patient = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
        foreach (Pawn pawn in new[] { patient, doctor })
        {
            foreach (Hediff hediff in pawn.health.hediffSet.hediffs.ToArray()) pawn.health.RemoveHediff(hediff);
            GenSpawn.Spawn(pawn, CellFinder.RandomClosewalkCellNear(near, map, 6), map);
            foreach (Need need in pawn.needs.AllNeeds) need.CurLevelPercentage = 1;
        }
        doctor.skills.GetSkill(SkillDefOf.Medicine).Level = 20;
        doctor.workSettings.SetPriority(WorkTypeDefOf.Doctor, 1);
        patient.playerSettings.medCare = MedicalCareCategory.Best;
        shoulder = patient.RaceProps.body.GetPartsWithDef(DefDatabase<BodyPartDef>.GetNamed("Shoulder"))[0];
        bed = SpawnBed();
        doctor.Position = CellFinder.RandomClosewalkCellNear(bed.Position, map, 2);
        doctor.Notify_Teleported(true, false);
        foreach (Thing item in map.listerThings.ThingsOfDef(ThingDef.Named("BionicArm")).ToArray()) item.Destroy();
        multipliers = (Dictionary<QualityCategory, float>)AccessTools.Field(
            GenTypes.GetTypeInAnyAssembly("QualityBionicsRemastered.Settings"), "qualityMultipliers").GetValue(null);
        Defaults();
        poor = Spawn("BionicArm", QualityCategory.Poor);
        Spawn("MedicineIndustrial", QualityCategory.Normal).stackCount = 25;
        var injury = (Hediff_Injury)patient.health.AddHediff(HediffDefOf.Cut, shoulder);
        injury.Severity = 6f;
        injury.Tended(1f, 1f);
        Require(!injury.IsPermanent() && !patient.Downed && patient.health.hediffSet.BleedRateTotal == 0f
            && !patient.health.HasHediffsNeedingTend(), "tended, non-bleeding, naturally healable wound passes old health gate");
        var model = ResetModel();
        AddPlan(model);
        Reconcile();
        Check(!model.Reservations.ContainsKey(poor.thingIDNumber) && patient.BillStack.Count == 0,
            "IR1: a tended healing arm rejects the 93.75% Poor bionic before scheduling");

        ClearBills();
        injury.Heal(injury.Severity + 1f);
        poor.Destroy();
        Thing good = Spawn("BionicArm", QualityCategory.Good);
        multipliers[QualityCategory.Good] = 0.5f;
        model = ResetModel();
        AddPlan(model);
        Reconcile();
        Check(!model.Reservations.ContainsKey(good.thingIDNumber) && patient.BillStack.Count == 0,
            "IR3: healthy arm rejects Good at 62.5% despite Normal being 125%");

        ClearBills();
        var worn = patient.health.AddHediff(HediffDef.Named("BionicArm"), shoulder);
        var comp = ((HediffWithComps)worn).comps.Single(c => c.GetType().Name == "HediffCompQualityBionics");
        AccessTools.Field(comp.GetType(), "quality").SetValue(comp, QualityCategory.Normal);
        model = ResetModel();
        AddPlan(model);
        model.SetUpgradeByPriority(true);
        ImplannerStore.Current.Bump(PlannerChange.All);
        Reconcile();
        Check(patient.BillStack.Count == 0,
            "IR3: quality upgrade never removes a 125% Normal arm for a 62.5% Good arm");
        ClearBills();
        patient.health.RestorePart(shoulder);
        good.Destroy();
        Defaults();

        // A pre-existing save/job can already hold unsafe ingredients. Let
        // vanilla select the manual bill, then restore Implanner ownership
        // before continuing it; this does not bypass the finish boundary.
        poor = Spawn("BionicArm", QualityCategory.Poor);
        model = ResetModel();
        Plan plan = AddPlan(model);
        RecipeDef recipe = DefDatabase<RecipeDef>.AllDefsListForReading.First(r => r.addsHediff == HediffDef.Named("BionicArm")
            && r.appliedOnFixedBodyParts != null && r.appliedOnFixedBodyParts.Contains(shoulder.def));
        recipe.workAmount = 60f;
        recipe.surgerySuccessChanceFactor = 100f;
        active = new Bill_Medical(recipe, null);
        patient.BillStack.AddBill(active);
        active.Part = shoulder;
        legacyKey = GoalKeys.ImplantSlot(plan.Implants[0], 0);
        model.SetAutomationPaused(true);
        ImplannerStore.Current.Bump(PlannerChange.All);
        PutPatientInBed();
        startedTick = Find.TickManager.TicksGame;
        phase = 10;
        deadline = DateTime.UtcNow.AddSeconds(35);
        Find.TickManager.CurTimeSpeed = TimeSpeed.Ultrafast;
    }

    static void BeginSafeSurgery()
    {
        ClearBills();
        patient.health.RestorePart(shoulder);
        if (!poor.Destroyed) poor.Destroy();
        normal = Spawn("BionicArm", QualityCategory.Normal);
        var wound = (Hediff_Injury)patient.health.AddHediff(HediffDefOf.Cut, shoulder);
        wound.Severity = 6f;
        wound.Tended(1f, 1f);
        PlannerModel model = ResetModel();
        AddPlan(model);
        Reconcile();
        active = patient.BillStack.Bills.OfType<Bill_Medical>().Single(b => b.Part == shoulder);
        Check(model.Reservations.ContainsKey(normal.thingIDNumber), "Safe surgery: healing arm reserves a genuinely better implant");
        active.recipe.workAmount = 60f;
        active.recipe.surgerySuccessChanceFactor = 100f;
        phase = 2;
        StartDoctor(active);
    }

    static PlannerModel ResetModel()
    {
        var model = new PlannerModel();
        ImplannerStore.Current.Model = model;
        model.SetAutomationPaused(false);
        model.SetAutoProduction(false);
        model.SetSurgeryConcurrency(20);
        model.SetCountHospitalized(false);
        model.SetAutoDoctorFloor(false);
        model.SetManualDoctorFloor(0);
        model.SetIteration(IterationStrategy.Asap);
        return model;
    }

    static Plan AddPlan(PlannerModel model)
    {
        var store = ImplannerStore.Current;
        Plan plan = model.CreatePlan("Healing limb safety", store.TakePlanId);
        model.SetImplantSlot(plan.Id, "BionicArm", 0, true);
        model.AssignPlan(patient.thingIDNumber, plan.Id);
        store.Bump(PlannerChange.All);
        return plan;
    }

    static void Reconcile() => AccessTools.Method(typeof(ImplannerStore).Assembly.GetType("Implanner.PlannerReconciler"),
        "Reconcile").Invoke(null, new object[] { ImplannerStore.Current, true });

    static void ClearBills()
    {
        doctor.jobs.StopAll();
        patient.jobs.StopAll();
        foreach (Bill bill in patient.BillStack.Bills.ToArray()) patient.BillStack.Delete(bill);
    }

    static void Defaults()
    {
        float[] values = { .5f, .75f, 1f, 1.25f, 1.5f, 1.7f, 2f };
        for (int q = 0; q < values.Length; q++) multipliers[(QualityCategory)q] = values[q];
    }

    static Thing Spawn(string defName, QualityCategory quality)
    {
        Thing item = ThingMaker.MakeThing(ThingDef.Named(defName));
        item.TryGetComp<CompQuality>()?.SetQuality(quality, ArtGenerationContext.Colony);
        GenPlace.TryPlaceThing(item, bed.Position, map, ThingPlaceMode.Near);
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
        patient.jobs.StopAll();
        patient.Position = bed.GetSleepingSlotPos(0);
        patient.Notify_Teleported(true, false);
        Job rest = JobMaker.MakeJob(JobDefOf.LayDown, bed);
        rest.playerForced = true;
        rest.expiryInterval = 60000;
        patient.jobs.StartJob(rest, JobCondition.InterruptForced);
    }

    static Job FindDoctorJob(Bill_Medical bill)
    {
        bill.nextTickToSearchForIngredients = 0;
        foreach (WorkGiverDef def in DefDatabase<WorkGiverDef>.AllDefsListForReading)
        {
            if (!def.billGiversAllHumanlikes || !(def.Worker is WorkGiver_DoBill giver)) continue;
            Job job = giver.JobOnThing(doctor, patient, true);
            if (job?.bill == bill) return job;
        }
        return null;
    }

    static void StartDoctor(Bill_Medical bill)
    {
        PutPatientInBed();
        if (patient.CurrentBed() != bed) return;
        if (doctor.CurJob?.bill == bill) return;
        doctor.jobs.StopAll();
        Job job = FindDoctorJob(bill);
        if (job == null) return;
        doctor.jobs.TryTakeOrderedJob(job);
    }

    static void Finish()
    {
        phase = 99;
        Find.TickManager.Pause();
        string status = failures == 0 ? "SUCCESS" : "FAILED " + failures;
        Write(status + "; elapsed game ticks=" + (Find.TickManager.TicksGame - startedTick));
        File.WriteAllText(Path.Combine(Root, "implant-safety-stage.txt"), status);
    }
}
