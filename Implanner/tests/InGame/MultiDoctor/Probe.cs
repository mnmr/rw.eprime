using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using Implanner;
using Implanner.Core;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Plan = Implanner.Core.Plan;

namespace MultiDoctorProbe;

// Multi-Doctor Surgery (Workshop 3356249192, MasterPNJ.MultiDoctor) beside
// Implanner on the Fisso 1.7 save. Multi-Doctor's team surgery is its own
// BillMedicalEx (lead surgeon + assistants); all its job/stat patches act
// only on that type. Implanner's "Surgery teams" option picks Solo (the
// default), Default team or Best team. Three bionic eye operations run
// through Implanner's real reconcile pass and the game's own doctor job:
//  D1 Multi-Doctor is live: a health-tab bill is its BillMedicalEx.
//  E  an operation Multi-Doctor's settings exclude stays a plain bill.
//  S  Solo (default): a plain Bill_Medical, installed by one doctor, the
//     item consumed and the record dropped.
//  B  Best team: a BillMedicalEx led by the best doctor with assistants,
//     an assistant joins the operation, the eye is installed.
//  F  Default team with a doctor floor above its lead: the bill carries
//     Multi-Doctor's default lead and assistants, the lead may still start
//     it (pawn restriction beats the skill range) and installs the eye.
// Captures of the Options tab: Solo at the start, Default team at the end.
// run.ps1 -Control loads the same probe without Multi-Doctor Surgery:
//  C  the option is locked to Solo (capture options-without-mds), and a
//     saved Best team mode still yields a plain, unrestricted bill that one
//     doctor completes.
[StaticConstructorOnStartup]
public static class Probe
{
    const string Package = "eprime.implanner.multidoctor";
    static int phase, failures, updates, startTick;
    static DateTime deadline;
    static Map map;
    static Pawn doctor, lead2, other;
    static Type billEx;
    static RecipeDef install;
    static PlannerModel model;
    static ImplannerStore store;
    static Plan plan;
    static readonly List<Pawn> patients = new List<Pawn>();
    static Case current;
    static bool assistantSeen, control;
    static int captureSeq, waitFrames;
    static string pendingCapture;
    static DateTime captureHeldUntil;
    static Window dialog;
    static string Root => GenFilePaths.SaveDataFolderPath;

    sealed class Case
    {
        public string Name;
        public Pawn Patient, Surgeon;
        public Building_Bed Bed;
        public Thing Eye;
        public Bill_Medical Bill;
    }

    static Probe() => new Harmony(Package).Patch(
        AccessTools.Method(typeof(Root_Play), "Update"),
        postfix: new HarmonyMethod(typeof(Probe), nameof(Update)));

    static void Write(string message)
    {
        Log.Message("[MultiDoctor] " + message);
        File.AppendAllText(Path.Combine(Root, "multidoctor.txt"), message + Environment.NewLine);
    }

    static void Check(bool condition, string message)
    {
        Write((condition ? "PASS " : "FAIL ") + message);
        if (!condition) failures++;
    }

    static Type ModType(string name) => typeof(ImplannerStore).Assembly.GetType(name);

    static void Reconcile() => AccessTools.Method(ModType("Implanner.PlannerReconciler"), "Reconcile")
        .Invoke(null, new object[] { ImplannerStore.Current, true });

    static bool HasEye(Pawn pawn) => pawn.health.hediffSet.hediffs
        .Any(h => h.def == HediffDef.Named("BionicEye"));

    static Pawn Surgeon(Bill bill) => (Pawn)AccessTools.Field(billEx, "surgeon").GetValue(bill);

    static List<Pawn> Team(Bill bill) => (List<Pawn>)AccessTools.Field(billEx, "assignedDoctors").GetValue(bill);

    static int Medicine(Pawn pawn) => pawn.skills?.GetSkill(SkillDefOf.Medicine).Level
        ?? pawn.RaceProps.mechFixedSkillLevel;

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
                    File.WriteAllText(Path.Combine(Root, "multidoctor-capture.txt"), captureSeq + " " + pendingCapture);
                    captureHeldUntil = DateTime.UtcNow.AddSeconds(2.5);
                }
                if (DateTime.UtcNow < captureHeldUntil) return;
                pendingCapture = null;
            }
            if (phase > 0 && DateTime.UtcNow > deadline)
                throw new Exception("Timed out at phase " + phase + " (" + current?.Name + "); surgeon="
                    + current?.Surgeon.CurJob + "; patient=" + current?.Patient.CurJob
                    + "; bed=" + current?.Patient.CurrentBed() + "; bills=" + current?.Patient.BillStack.Count);
            if (phase == 0)
            {
                if (updates++ < 8) return;
                Setup();
                OpenOptions();
                Capture(control ? "options-without-mds" : "options-with-mds-solo");
                phase = 10;
                return;
            }
            if (phase == 10)
            {
                CloseOptions();
                if (control)
                {
                    // A save from a game with Multi-Doctor Surgery keeps its
                    // team mode; without the mod it must change nothing.
                    Start(SurgeryTeamMode.BestTeam, "C", patients[0]);
                    phase = 4;
                }
                else
                {
                    Start(SurgeryTeamMode.Solo, "S", patients[0]);
                    phase = 1;
                }
                return;
            }
            if (phase == 5)
            {
                CloseOptions();
                Finish();
                return;
            }
            if (!HasEye(current.Patient))
            {
                if (Find.TickManager.TicksGame - startTick > 9000)
                    throw new Exception(current.Name + " operation did not finish; surgeon=" + current.Surgeon.CurJob
                        + " patient=" + current.Patient.CurJob + " eye spawned=" + current.Eye.Spawned);
                if (current.Bill != null && billEx != null && billEx.IsInstanceOfType(current.Bill))
                    foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned)
                        if (pawn != current.Surgeon && pawn.CurJob?.def.defName == "AssistSurgeryLoop"
                            && pawn.CurJob.bill == current.Bill)
                            assistantSeen = true;
                if (updates++ % 8 == 0) Drive();
                return;
            }
            Find.TickManager.Pause();
            Check(true, current.Name + " " + current.Surgeon.LabelShort + " installed the bionic eye on "
                + current.Patient.LabelShort + " after " + (Find.TickManager.TicksGame - startTick) + " ticks");
            Check(current.Eye.Destroyed, current.Name + " the reserved bionic eye item was consumed");
            Reconcile();
            IReadOnlyDictionary<string, string> owned = model.OwnedBillsFor(current.Patient.thingIDNumber);
            Check(owned == null || owned.Count == 0, current.Name + " the finished operation's record is dropped");
            switch (phase)
            {
                case 1:
                    Start(SurgeryTeamMode.BestTeam, "B", patients[1]);
                    phase = 2;
                    return;
                case 2:
                    Check(assistantSeen, "B an assistant joined the operation (AssistSurgeryLoop)");
                    model.SetManualDoctorFloor(25);
                    SetDefaultTeam(lead2, new List<Pawn> { doctor });
                    Start(SurgeryTeamMode.DefaultTeam, "F", patients[2]);
                    phase = 3;
                    return;
                case 3:
                    OpenOptions();
                    Capture("options-with-mds-default-team");
                    phase = 5;
                    return;
                default:
                    Finish();
                    return;
            }
        }
        catch (Exception ex)
        {
            Find.TickManager.Pause();
            Write("ERROR " + ex);
            File.WriteAllText(Path.Combine(Root, "multidoctor-stage.txt"), "FAILED " + ex.Message);
            phase = 99;
        }
    }

    static void Capture(string name)
    {
        captureSeq++;
        pendingCapture = name;
        waitFrames = 3;
    }

    static void OpenOptions()
    {
        Type type = typeof(ImplannerStore).Assembly.GetType("Implanner.UI.Dialog_Implanner");
        Type view = typeof(ImplannerStore).Assembly.GetType("Implanner.UI.ImplannerView");
        Type tabs = view.GetNestedType("Tab", AccessTools.all);
        AccessTools.Field(view, "curTab").SetValue(null, Enum.Parse(tabs, "Options"));
        dialog = (Window)Activator.CreateInstance(type);
        Find.WindowStack.Add(dialog);
    }

    static void CloseOptions()
    {
        dialog?.Close(false);
        dialog = null;
    }

    static void Setup()
    {
        Find.TickManager.Pause();
        map = Find.CurrentMap;
        billEx = GenTypes.GetTypeInAnyAssembly("MultiDoctorSurgery.BillMedicalEx");
        control = billEx == null;
        Write(control ? "control run: Multi-Doctor Surgery is not loaded" : "Multi-Doctor Surgery is loaded");
        install = DefDatabase<RecipeDef>.GetNamed("InstallBionicEye");

        List<Pawn> pawns = map.mapPawns.FreeColonistsSpawned
            .Where(p => p.RaceProps.body == BodyDefOf.Human && !p.Downed && !p.IsMutant
                && !p.health.HasHediffsNeedingTend())
            .OrderBy(p => p.thingIDNumber).ToList();
        List<Pawn> medics = pawns.Where(p => !p.skills.GetSkill(SkillDefOf.Medicine).TotallyDisabled
                && p.workSettings != null && !p.WorkTypeIsDisabled(WorkTypeDefOf.Doctor)
                && Medicine(p) >= 8)
            .OrderByDescending(Medicine).ThenBy(p => p.thingIDNumber).ToList();
        doctor = medics[0];
        lead2 = medics.First(p => Medicine(p) < Medicine(doctor));
        foreach (Pawn medic in medics) medic.workSettings.SetPriority(WorkTypeDefOf.Doctor, 1);
        foreach (Pawn pawn in pawns)
            if (pawn != doctor && pawn != lead2 && patients.Count < 3
                && pawn.health.hediffSet.GetNotMissingParts().Count(p => p.def == BodyPartDefOf.Eye) == 2
                && !pawn.health.hediffSet.hediffs.Any(h => h.Part?.def == BodyPartDefOf.Eye
                    && (h is Hediff_AddedPart || h is Hediff_Implant)))
                patients.Add(pawn);
        if (patients.Count < 3) throw new Exception("fixture: only " + patients.Count + " patients with natural eyes");
        other = pawns.First(p => p != doctor && p != lead2 && !patients.Contains(p));
        foreach (Pawn patient in patients)
            foreach (Bill b in patient.BillStack.Bills.ToArray()) patient.BillStack.Delete(b);
        Write("doctor " + doctor.LabelShort + " (Medical " + Medicine(doctor) + "), second lead "
            + lead2.LabelShort + " (Medical " + Medicine(lead2) + "), medics " + medics.Count
            + ", patients " + string.Join(",", patients.Select(p => p.LabelShort)));

        Prepare();
        if (control)
        {
            Check(!(bool)AccessTools.Property(ModType("Implanner.MultiDoctor"), "Loaded").GetValue(null),
                "C the bridge reports Multi-Doctor Surgery absent");
            return;
        }

        // D1: the health tab's own bill path is Multi-Doctor's.
        BodyPartRecord otherEye = other.health.hediffSet.GetNotMissingParts()
            .First(p => p.def == BodyPartDefOf.Eye);
        Bill_Medical manual = HealthCardUtility.CreateSurgeryBill(other, install, otherEye, null, false);
        Check(manual.GetType() == billEx, "D1 a health-tab bill is Multi-Doctor's " + manual.GetType().Name);
        if (other.BillStack.Bills.Contains(manual)) other.BillStack.Delete(manual);

        // E: Multi-Doctor's exclusion list keeps Implanner's bill plain.
        Type modType = GenTypes.GetTypeInAnyAssembly("MultiDoctorSurgery.MultiDoctorSurgeryMod");
        object settings = AccessTools.Field(modType, "settings").GetValue(null);
        var excluded = (List<string>)AccessTools.Field(settings.GetType(), "excludedOperations").GetValue(settings);
        var newBill = AccessTools.Method(ModType("Implanner.MultiDoctor"), "NewBill");
        excluded.Add(install.defName);
        Bill_Medical excludedBill = (Bill_Medical)newBill.Invoke(null, new object[] { SurgeryTeamMode.BestTeam, other, install });
        excluded.Remove(install.defName);
        Bill_Medical teamBill = (Bill_Medical)newBill.Invoke(null, new object[] { SurgeryTeamMode.BestTeam, other, install });
        Check(excludedBill.GetType() == typeof(Bill_Medical) && teamBill.GetType() == billEx,
            "E an excluded operation is a plain bill (" + excludedBill.GetType().Name + "), the same one not excluded is "
            + teamBill.GetType().Name);
    }

    static void Prepare()
    {
        store = ImplannerStore.Current;
        model = new PlannerModel();
        store.Model = model;
        model.SetAutomationPaused(false);
        model.SetAutoProduction(false);
        model.SetSurgeryConcurrency(20);
        model.SetCountHospitalized(false);
        model.SetAutoDoctorFloor(false);
        model.SetManualDoctorFloor(0);
        model.SetIteration(IterationStrategy.Asap);
        Check(model.SurgeryTeams == SurgeryTeamMode.Solo, "S a new game's surgery teams default to Solo");
        plan = model.CreatePlan("Eyes", store.TakePlanId)!;
        model.SetImplantSlot(plan.Id, "BionicEye", new PartAddress("Eye", 0), true);
        store.Bump(PlannerChange.All);

        // Fixture speed-ups only: no random failure, short work.
        install.surgerySuccessChanceFactor = 99f;
        install.workAmount = 60;
        deadline = DateTime.UtcNow.AddSeconds(50);
    }

    static void SetDefaultTeam(Pawn lead, List<Pawn> assistants)
    {
        Type type = GenTypes.GetTypeInAnyAssembly("MultiDoctorSurgery.DefaultSurgeryTeamComponent");
        WorldComponent component = Find.World.GetComponent(type);
        AccessTools.Field(type, "defaultLeadSurgeon").SetValue(component, lead);
        AccessTools.Field(type, "defaultAssistants").SetValue(component, assistants);
    }

    /// One operation: the mode, the plan on the patient, a medical bed and
    /// a bionic eye beside it, and Implanner's real pass.
    static void Start(SurgeryTeamMode mode, string name, Pawn patient)
    {
        PlannerCommands.SetSurgeryTeams((int)mode);
        current = new Case { Name = name, Patient = patient };
        current.Bed = SpawnBed(patient);
        current.Eye = ThingMaker.MakeThing(ThingDef.Named("BionicEye"));
        GenPlace.TryPlaceThing(current.Eye, current.Bed.Position, map, ThingPlaceMode.Near);
        current.Eye.SetForbidden(false, false);
        Thing medicine = ThingMaker.MakeThing(ThingDefOf.MedicineIndustrial);
        medicine.stackCount = 5;
        GenPlace.TryPlaceThing(medicine, current.Bed.Position, map, ThingPlaceMode.Near);
        medicine.SetForbidden(false, false);
        PlannerCommands.AssignPlan(patient.thingIDNumber, plan.Id);
        store.Bump(PlannerChange.All);
        Reconcile();
        Check(model.SurgeryTeams == mode, name + " surgery teams mode is " + model.SurgeryTeams);
        Check(model.Reservations.TryGetValue(current.Eye.thingIDNumber, out ItemReservation reservation)
            && reservation.PawnId == patient.thingIDNumber, name + " the bionic eye is reserved for " + patient.LabelShort);
        current.Bill = patient.BillStack.Bills.OfType<Bill_Medical>().FirstOrDefault(b => b.recipe == install);
        IReadOnlyDictionary<string, string> owned = model.OwnedBillsFor(patient.thingIDNumber);
        Check(current.Bill != null && owned != null && owned.Count == 1
            && owned.Values.First() == current.Bill.GetUniqueLoadID(), name + " the eye operation is an owned bill");
        Check(current.Bill.IsFixedOrAllowedIngredient(current.Eye), name + " the bill accepts the reserved item");
        switch (control ? (SurgeryTeamMode)(-1) : mode)
        {
            case (SurgeryTeamMode)(-1):
                Check(current.Bill.GetType() == typeof(Bill_Medical) && current.Bill.PawnRestriction == null,
                    "C without Multi-Doctor Surgery a saved Best team mode still makes a plain, unrestricted Bill_Medical");
                current.Surgeon = doctor;
                break;
            case SurgeryTeamMode.Solo:
                Check(current.Bill.GetType() == typeof(Bill_Medical),
                    "S Implanner's bill is a plain Bill_Medical (" + current.Bill.GetType().Name + ")");
                current.Surgeon = doctor;
                break;
            case SurgeryTeamMode.BestTeam:
                Check(current.Bill.GetType() == billEx, "B the bill is Multi-Doctor's " + current.Bill.GetType().Name);
                List<Pawn> team = Team(current.Bill);
                Check(Surgeon(current.Bill) == doctor && current.Bill.PawnRestriction == doctor,
                    "B the best doctor leads: " + Surgeon(current.Bill)?.LabelShort);
                Check(team.Count >= 2 && !team.Contains(patient),
                    "B assistants assigned: " + string.Join(",", team.Select(p => p.LabelShort + "(" + Medicine(p) + ")")));
                current.Surgeon = doctor;
                break;
            case SurgeryTeamMode.DefaultTeam:
                Check(current.Bill.GetType() == billEx, "F the bill is Multi-Doctor's " + current.Bill.GetType().Name);
                Check(Surgeon(current.Bill) == lead2 && Team(current.Bill).Contains(doctor),
                    "F Multi-Doctor's default team is assigned: lead " + Surgeon(current.Bill)?.LabelShort
                    + ", team " + string.Join(",", Team(current.Bill).Select(p => p.LabelShort)));
                Check(current.Bill.allowedSkillRange.min == 25 && Medicine(lead2) < 25,
                    "F the doctor floor (" + current.Bill.allowedSkillRange.min + ") is above the lead's Medical "
                    + Medicine(lead2));
                current.Surgeon = lead2;
                break;
        }
        assistantSeen = false;
        startTick = Find.TickManager.TicksGame;
        Find.TickManager.CurTimeSpeed = TimeSpeed.Ultrafast;
    }

    static Building_Bed SpawnBed(Pawn patient)
    {
        foreach (IntVec3 cell in GenRadial.RadialCellsAround(patient.Position, 25, true))
        {
            bool free = true;
            foreach (IntVec3 c in GenAdj.OccupiedRect(cell, Rot4.North, ThingDefOf.Bed.size))
                if (!c.InBounds(map) || !c.Standable(map) || c.GetEdifice(map) != null || c.GetFirstItem(map) != null
                    || c.GetFirstPawn(map) != null || !doctor.CanReach(c, PathEndMode.OnCell, Danger.Deadly))
                { free = false; break; }
            if (!free) continue;
            var b = (Building_Bed)ThingMaker.MakeThing(ThingDefOf.Bed, ThingDefOf.WoodLog);
            GenSpawn.Spawn(b, cell, map);
            b.SetFaction(Faction.OfPlayer);
            b.Medical = true;
            return b;
        }
        throw new Exception("no bed location");
    }

    /// The patient to the medical bed and the game's own doctor job for the
    /// bill (WorkGiver_DoBill.JobOnThing) for the case's surgeon.
    static void Drive()
    {
        Pawn patient = current.Patient, surgeon = current.Surgeon;
        if (patient.CurrentBed() != current.Bed && patient.Spawned && !patient.Downed)
        {
            patient.jobs.StopAll();
            patient.Position = current.Bed.GetSleepingSlotPos(0);
            patient.Notify_Teleported(true, false);
            foreach (Need n in patient.needs.AllNeeds) n.CurLevelPercentage = 1;
            Job rest = JobMaker.MakeJob(JobDefOf.LayDown, current.Bed);
            rest.playerForced = true;
            rest.expiryInterval = 60000;
            patient.jobs.StartJob(rest, JobCondition.InterruptForced);
        }
        if (patient.CurrentBed() != current.Bed || surgeon.CurJob?.bill != null) return;
        surgeon.jobs.StopAll();
        foreach (WorkGiverDef def in DefDatabase<WorkGiverDef>.AllDefsListForReading)
        {
            if (!def.billGiversAllHumanlikes || !(def.Worker is WorkGiver_DoBill giver)) continue;
            current.Bill.nextTickToSearchForIngredients = 0;
            Job job = giver.JobOnThing(surgeon, patient, true);
            if (job?.bill != current.Bill) continue;
            Check(job.targetQueueB != null && job.targetQueueB.Any(t => t.Thing == current.Eye),
                current.Name + " the game's doctor job for " + surgeon.LabelShort + " selects the reserved bionic eye");
            surgeon.jobs.TryTakeOrderedJob(job);
            Write(current.Name + " " + surgeon.LabelShort + " starts the eye operation at tick " + Find.TickManager.TicksGame);
            return;
        }
    }

    static void Finish()
    {
        phase = 99;
        Write(failures == 0 ? "SUCCESS" : failures + " failure(s)");
        File.WriteAllText(Path.Combine(Root, "multidoctor-stage.txt"),
            failures == 0 ? "SUCCESS" : "FAILED " + failures + " check(s)");
    }
}
