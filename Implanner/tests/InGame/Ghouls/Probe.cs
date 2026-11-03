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

namespace GhoulsProbe;

// Ghoul plans (Anomaly). A colonist is turned into a ghoul on the Fisso
// save, a plan made for ghouls is created on top of an ordinary plan, and
// the mod's real reconcile pass plus the game's own doctor job install the
// planned ghoul plating.
//  G1 the catalog carries the ghoul kit (mutant-only kinds) and marks the
//     revenant vertebrae as refused for ghouls; the corrosive heart stays
//     out (part efficiency below natural).
//  G2 the ghoul is a plannable colonist; the ghoul plan enlists it and
//     refuses a human, the ordinary plan refuses the ghoul.
//  G3 per-pawn applicability: plating is a target for the ghoul, the
//     inherited bionic arm too, the vertebrae are refused; the plating is
//     outside a human's target.
//  G4 the reconcile pass reserves the plating item and schedules the
//     operation (owned bill on the ghoul), nothing for the vertebrae.
//  G5 production queues a ghoul barbs craft at the bioferrite shaper.
//  G6 the game's doctor completes the surgery: the plating hediff is on
//     the ghoul and the item is consumed. Captures: ghoul-plans (editor with
//     the ghoul plan selected), ghoul-overview (ghoul details).
[StaticConstructorOnStartup]
public static class Probe
{
    const string Package = "eprime.implanner.ghouls";
    static int phase, failures, captureSeq, waitFrames, startTick, updates;
    static string pendingCapture;
    static DateTime captureHeldUntil, deadline;
    static Window dialog;
    static Map map;
    static Pawn ghoul, human, doctor;
    static Building_Bed bed;
    static Thing plating;
    static Plan everyone, ghoulPlan;
    static Bill_Medical bill;
    static string Root => GenFilePaths.SaveDataFolderPath;

    static Probe() => new Harmony(Package).Patch(
        AccessTools.Method(typeof(Root_Play), "Update"),
        postfix: new HarmonyMethod(typeof(Probe), nameof(Update)));

    static void Write(string message)
    {
        Log.Message("[Ghouls] " + message);
        File.AppendAllText(Path.Combine(Root, "ghouls.txt"), message + Environment.NewLine);
    }

    static void Check(bool condition, string message)
    {
        Write((condition ? "PASS " : "FAIL ") + message);
        if (!condition) failures++;
    }

    static Type ModType(string name) => typeof(ImplannerStore).Assembly.GetType(name);

    static void Reconcile() => AccessTools.Method(ModType("Implanner.PlannerReconciler"), "Reconcile")
        .Invoke(null, new object[] { ImplannerStore.Current, true });

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
                    File.WriteAllText(Path.Combine(Root, "ghouls-capture.txt"),
                        captureSeq + " " + pendingCapture);
                    captureHeldUntil = DateTime.UtcNow.AddSeconds(2.5);
                }
                if (DateTime.UtcNow < captureHeldUntil) return;
                pendingCapture = null;
            }
            if (phase > 0 && DateTime.UtcNow > deadline)
                throw new Exception("Timed out at phase " + phase + "; doctor=" + doctor.CurJob
                    + "; ghoul=" + ghoul.CurJob + "; bed=" + ghoul.CurrentBed()
                    + "; plating hediff=" + HasPlating() + "; bill=" + ghoul.BillStack.Count);
            switch (phase)
            {
                case 0:
                    if (updates++ < 8) return;
                    Setup();
                    phase = 1;
                    return;
                case 1:
                    ShowPlans();
                    Capture("ghoul-plans");
                    phase = 2;
                    return;
                case 2:
                    CloseDialog();
                    startTick = Find.TickManager.TicksGame;
                    Find.TickManager.CurTimeSpeed = TimeSpeed.Ultrafast;
                    phase = 3;
                    return;
                case 3:
                    if (HasPlating())
                    {
                        Find.TickManager.Pause();
                        Check(true, "G6 the doctor installed the ghoul plating on " + ghoul.LabelShort
                            + " after " + (Find.TickManager.TicksGame - startTick) + " ticks");
                        Check(plating.Destroyed, "G6 the reserved plating item was consumed by the operation");
                        ShowOverview();
                        Capture("ghoul-overview");
                        phase = 4;
                        return;
                    }
                    if (Find.TickManager.TicksGame - startTick > 7000)
                        throw new Exception("operation did not finish; doctor=" + doctor.CurJob
                            + " ghoul=" + ghoul.CurJob + " bed=" + ghoul.CurrentBed()
                            + " plating spawned=" + plating.Spawned + " bills=" + ghoul.BillStack.Count);
                    if (updates++ % 8 == 0) Drive();
                    return;
                case 4: Finish(); return;
            }
        }
        catch (Exception ex)
        {
            Find.TickManager.Pause();
            Write("ERROR " + ex);
            File.WriteAllText(Path.Combine(Root, "ghouls-stage.txt"), "FAILED " + ex.Message);
            phase = 99;
        }
    }

    static bool HasPlating() => ghoul.health.hediffSet.HasHediff(HediffDef.Named("GhoulPlating"));

    static void Capture(string name, int frames = 3)
    {
        captureSeq++;
        pendingCapture = name;
        waitFrames = frames;
    }

    static void Setup()
    {
        Find.TickManager.Pause();
        map = Find.CurrentMap;
        ResearchProjectDef research = ResearchProjectDef.Named("GhoulEnhancements");
        if (!research.IsFinished) Find.ResearchManager.FinishProject(research, false);
        List<Pawn> pawns = map.mapPawns.FreeColonistsSpawned
            .Where(p => p.RaceProps.body == BodyDefOf.Human && !p.Downed
                && !p.health.HasHediffsNeedingTend() && !p.IsMutant)
            .OrderBy(p => p.thingIDNumber).ToList();
        doctor = pawns.Where(p => !p.skills.GetSkill(SkillDefOf.Medicine).TotallyDisabled
                && p.workSettings != null && !p.WorkTypeIsDisabled(WorkTypeDefOf.Doctor))
            .OrderByDescending(p => p.skills.GetSkill(SkillDefOf.Medicine).Level).First();
        doctor.workSettings.SetPriority(WorkTypeDefOf.Doctor, 1);
        ghoul = pawns.First(p => p != doctor
            && !p.health.hediffSet.hediffs.Any(h => h is Hediff_AddedPart || h is Hediff_MissingPart));
        human = pawns.First(p => p != doctor && p != ghoul);
        Write("doctor " + doctor.LabelShort + " (Medical " + doctor.skills.GetSkill(SkillDefOf.Medicine).Level
            + "), ghoul-to-be " + ghoul.LabelShort + ", human " + human.LabelShort);
        foreach (Bill b in ghoul.BillStack.Bills.ToArray()) ghoul.BillStack.Delete(b);
        MutantUtility.SetPawnAsMutantInstantly(ghoul, MutantDefOf.Ghoul);
        if (!ghoul.IsGhoul) throw new Exception("fixture: " + ghoul.LabelShort + " is not a ghoul");
        Write(ghoul.LabelShort + " is now a ghoul: faction " + ghoul.Faction?.Name
            + ", free colonist " + ghoul.IsFreeColonist + ", subhuman " + ghoul.IsSubhuman
            + ", skills tracker " + (ghoul.skills != null) + ", medCare " + ghoul.playerSettings.medCare);

        // G1: the catalog.
        Type catalogs = ModType("Implanner.Catalogs");
        var byName = AccessTools.Method(catalogs, "ImplantByDefName");
        object Entry(string def) => byName.Invoke(null, new object[] { def });
        List<string> Required(object entry) => (List<string>)Traverse.Create(entry).Property("RequiredMutants").GetValue();
        List<string> Blacklisted(object entry) => (List<string>)Traverse.Create(entry).Property("BlacklistedMutants").GetValue();
        foreach (string kind in new[] { "GhoulPlating", "GhoulBarbs", "AdrenalHeart", "MetalbloodHeart" })
        {
            object entry = Entry(kind);
            var item = (ThingDef)Traverse.Create(entry).Property("Item").GetValue();
            Check(entry != null && Required(entry).SequenceEqual(new[] { "Ghoul" }) && item?.defName == kind,
                "G1 " + kind + " is in the catalog as a ghoul-only kind with its item"
                + (entry == null ? " (absent)" : " (requires " + string.Join(",", Required(entry)) + ", item " + item?.defName + ")"));
        }
        object vertebrae = Entry("RevenantVertebrae");
        Check(vertebrae != null && Required(vertebrae).Count == 0 && Blacklisted(vertebrae).SequenceEqual(new[] { "Ghoul" }),
            "G1 revenant vertebrae are in the catalog, refused for ghouls");
        Check(Entry("CorrosiveHeart") == null && Entry("BloodWarmer") == null,
            "G1 the corrosive heart (0.85) and Odyssey's blood warmer (0.8 efficiency) stay out as downgrades");
        object arm = Entry("BionicArm");
        Check(arm != null && Required(arm).Count == 0 && Blacklisted(arm).Count == 0,
            "G1 a bionic arm has no mutant rule");
        Check((bool)AccessTools.Method(catalogs, "GhoulsPlannable").Invoke(null, null),
            "G1 the new-plan prompt would offer a ghoul plan");

        // G2: scope and enlisting.
        Type scope = ModType("Implanner.ColonyScope");
        var roster = (List<Pawn>)AccessTools.Method(scope, "AllPlanableColonists", new[] { typeof(Faction) })
            .Invoke(null, new object[] { Faction.OfPlayer });
        Check(roster.Contains(ghoul), "G2 the ghoul is a plannable colonist");
        Check(roster.Contains(human) && roster.Contains(doctor), "G2 ordinary colonists stay plannable");

        var store = ImplannerStore.Current;
        var model = new PlannerModel();
        store.Model = model;
        model.SetAutomationPaused(false);
        model.SetAutoProduction(true);
        model.SetSurgeryConcurrency(20);
        model.SetCountHospitalized(false);
        model.SetAutoDoctorFloor(false);
        model.SetManualDoctorFloor(0);
        model.SetProductionSkill(0);
        // ASAP: the plating is scheduled as soon as its item is reserved,
        // while the arm and the barbs (no items yet) wait for production.
        model.SetIteration(IterationStrategy.Asap);
        everyone = model.CreatePlan("Everyone", store.TakePlanId)!;
        model.SetImplantSlot(everyone.Id, "BionicArm", new PartAddress("Shoulder", 0), true);
        ghoulPlan = model.CreatePlan("Ghoul kit", store.TakePlanId, everyone.Id, "", "Ghoul")!;
        Check(ghoulPlan.Mutant == "Ghoul" && ghoulPlan.BasePlanId == everyone.Id,
            "G2 a ghoul plan extending the ordinary plan is created");
        Check(model.CreatePlan("Humans of ghouls", store.TakePlanId, ghoulPlan.Id) == null,
            "G2 an ordinary plan cannot extend the ghoul plan");
        model.SetImplantSlot(ghoulPlan.Id, "GhoulPlating", new PartAddress("Torso", 0), true);
        model.SetImplantSlot(ghoulPlan.Id, "GhoulBarbs", new PartAddress("Torso", 0), true);
        model.SetImplantSlot(ghoulPlan.Id, "RevenantVertebrae", new PartAddress("Spine", 0), true);
        Check(ghoulPlan.Implants.Count == 3, "G2 plating, barbs and vertebrae are picked (" + ghoulPlan.Implants.Count + ")");
        store.Bump(PlannerChange.All);
        PlannerCommands.AssignPlan(ghoul.thingIDNumber, ghoulPlan.Id);
        Check(model.AssignedPlan(ghoul.thingIDNumber) == ghoulPlan, "G2 the ghoul plan enlists the ghoul");
        PlannerCommands.AssignPlan(human.thingIDNumber, ghoulPlan.Id);
        Check(model.AssignedPlan(human.thingIDNumber) == null, "G2 the ghoul plan refuses a human");
        PlannerCommands.AssignPlan(human.thingIDNumber, everyone.Id);
        Check(model.AssignedPlan(human.thingIDNumber) == everyone, "G2 the ordinary plan enlists the human");
        PlannerCommands.AssignPlan(doctor.thingIDNumber, everyone.Id);
        model.AssignPlan(doctor.thingIDNumber, 0);
        PlannerCommands.AssignPlan(ghoul.thingIDNumber, everyone.Id);
        Check(model.AssignedPlan(ghoul.thingIDNumber) == ghoulPlan, "G2 the ordinary plan refuses the ghoul (assignment unchanged)");
        Type bodies = ModType("Implanner.PlanBodies");
        Check((string)AccessTools.Method(bodies, "TargetLabel").Invoke(null, new object[] { ghoulPlan }) == "Ghouls"
            && (string)AccessTools.Method(bodies, "TargetLabel").Invoke(null, new object[] { everyone }) == "colonists",
            "G2 the greyed plan menu names who each plan is for");

        // G3: applicability.
        Type projection = ModType("Implanner.PawnProjection");
        var evaluate = AccessTools.Method(projection, "Evaluate");
        IReadOnlyList<ImplantGoal> goals = model.EffectiveImplants(ghoulPlan);
        int Goal(string def) { for (int i = 0; i < goals.Count; i++) if (goals[i].ImplantDefName == def) return i; return -1; }
        var onGhoul = (PlanEvaluation)evaluate.Invoke(null, new object[] { model, ghoul, goals, false, 0, "", "Ghoul" });
        Check(onGhoul.Contexts[Goal("GhoulPlating")].Block(0) == SlotBlock.None
            && onGhoul.Contexts[Goal("GhoulBarbs")].Block(0) == SlotBlock.None,
            "G3 plating and barbs are targets on the ghoul");
        Check(onGhoul.Contexts[Goal("BionicArm")].Block(0) == SlotBlock.None,
            "G3 the inherited bionic arm is a target on the ghoul");
        Check(onGhoul.Contexts[Goal("RevenantVertebrae")].Block(0) == SlotBlock.RefusesMutant,
            "G3 the revenant vertebrae are refused on the ghoul (" + onGhoul.Contexts[Goal("RevenantVertebrae")].Block(0) + ")");
        Check(onGhoul.TotalUnits == 3, "G3 the ghoul's target is three slots (" + onGhoul.TotalUnits + ")");
        // The same goals judged for a human on an ordinary plan: the ghoul
        // kit is outside the target, the arm and the vertebrae are in.
        var onHuman = (PlanEvaluation)evaluate.Invoke(null, new object[] { model, human, goals, false, 0, "", "" });
        Check(onHuman.Contexts[Goal("GhoulPlating")].Block(0) == SlotBlock.RequiresMutant
            && onHuman.Contexts[Goal("RevenantVertebrae")].Block(0) == SlotBlock.None
            && onHuman.TotalUnits == 2,
            "G3 the ghoul kit is outside a human's target, the vertebrae are in (" + onHuman.TotalUnits + " slots)");

        // G4: the real pass over spawned stock.
        bed = SpawnBed();
        plating = ThingMaker.MakeThing(ThingDef.Named("GhoulPlating"));
        GenPlace.TryPlaceThing(plating, bed.Position, map, ThingPlaceMode.Near);
        plating.SetForbidden(false, false);
        // Production fixture: bioferrite in a stockpile (the resource
        // counter counts storage only) and a crafting spot, which the
        // plating and barbs recipes name as a user beside the shaper.
        List<IntVec3> cells = FreeCells(4);
        var stockpile = new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile, map.zoneManager);
        map.zoneManager.RegisterZone(stockpile);
        for (int i = 0; i < 3; i++) stockpile.AddCell(cells[i]);
        Thing bioferrite = ThingMaker.MakeThing(ThingDef.Named("Bioferrite"));
        bioferrite.stackCount = 200;
        GenPlace.TryPlaceThing(bioferrite, cells[0], map, ThingPlaceMode.Direct);
        bioferrite.SetForbidden(false, false);
        Thing spot = ThingMaker.MakeThing(ThingDef.Named("CraftingSpot"));
        GenSpawn.Spawn(spot, cells[3], map);
        spot.SetFaction(Faction.OfPlayer);
        map.resourceCounter.UpdateResourceCounts();
        foreach (Thing t in map.listerThings.ThingsOfDef(ThingDef.Named("RevenantVertebrae")).ToArray()) t.Destroy();
        store.Bump(PlannerChange.All);
        Reconcile();
        Check(model.Reservations.TryGetValue(plating.thingIDNumber, out ItemReservation reservation)
            && reservation.PawnId == ghoul.thingIDNumber,
            "G4 the plating item is reserved for the ghoul");
        bill = ghoul.BillStack.Bills.OfType<Bill_Medical>().FirstOrDefault(b => b.recipe.defName == "InstallGhoulPlating");
        IReadOnlyDictionary<string, string> owned = model.OwnedBillsFor(ghoul.thingIDNumber);
        Check(bill != null && owned != null && owned.Count == 1,
            "G4 the plating operation is scheduled on the ghoul as an owned bill (bills "
            + string.Join(",", ghoul.BillStack.Bills.Select(b => b.recipe.defName)) + ")");
        Check(bill != null && bill.Part != null && bill.Part.def == BodyPartDefOf.Torso
            && bill.IsFixedOrAllowedIngredient(plating),
            "G4 the bill targets the torso and accepts the reserved item");
        Check(!ghoul.BillStack.Bills.Any(b => b.recipe.defName == "InstallRevenantVertebrae"),
            "G4 nothing is scheduled for the refused vertebrae");

        // G5: production of the missing barbs (the plating is in stock) at
        // a bench the recipe names, with the game's own recipe and bill.
        RecipeDef barbs = DefDatabase<RecipeDef>.AllDefsListForReading
            .FirstOrDefault(r => !r.IsSurgery && r.ProducedThingDef?.defName == "GhoulBarbs");
        Write("barbs recipe " + barbs?.defName + " available " + barbs?.AvailableNow
            + ", bioferrite counted " + map.resourceCounter.GetCount(ThingDef.Named("Bioferrite")));
        Bill_Production craft = map.listerBuildings.allBuildingsColonist.OfType<Building_WorkTable>()
            .SelectMany(b => b.BillStack.Bills).OfType<Bill_Production>()
            .FirstOrDefault(b => b.recipe == barbs && model.OwnedProductionBills.ContainsKey(b.GetUniqueLoadID()));
        Check(craft != null, "G5 a ghoul barbs craft is queued for the ghoul plan (owned bills "
            + string.Join(",", model.OwnedProductionBills.Keys) + ") at "
            + (craft?.billStack.billGiver as Thing)?.LabelCap);
        Check(craft != null && craft.recipe.ProducedThingDef.defName == "GhoulBarbs" && craft is Bill_ProductionWithUft,
            "G5 the craft is the game's own unfinished-thing bill for the ghoul barbs item");

        bill.recipe.workAmount = 60;
        ghoul.playerSettings.medCare = MedicalCareCategory.NoMeds;
        deadline = DateTime.UtcNow.AddSeconds(50);
    }

    /// Free, unzoned, standable cells near the bed the doctor can reach.
    static List<IntVec3> FreeCells(int count)
    {
        var cells = new List<IntVec3>();
        foreach (IntVec3 c in GenRadial.RadialCellsAround(bed.Position, 8, false))
        {
            if (!c.InBounds(map) || !c.Standable(map) || c.GetEdifice(map) != null
                || c.GetFirstItem(map) != null || c.GetFirstPawn(map) != null
                || map.zoneManager.ZoneAt(c) != null || c.GetThingList(map).Count > 0
                || !doctor.CanReach(c, PathEndMode.OnCell, Danger.Deadly)) continue;
            cells.Add(c);
            if (cells.Count == count) return cells;
        }
        throw new Exception("not enough free cells near the bed");
    }

    static Building_Bed SpawnBed()
    {
        foreach (IntVec3 cell in GenRadial.RadialCellsAround(ghoul.Position, 25, true))
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
    /// bill (WorkGiver_DoBill.JobOnThing), as the reservations probe does.
    static void Drive()
    {
        if (ghoul.CurrentBed() == null && ghoul.Spawned && !ghoul.Downed)
        {
            ghoul.jobs.StopAll();
            ghoul.Position = bed.GetSleepingSlotPos(0);
            ghoul.Notify_Teleported(true, false);
            foreach (Need n in ghoul.needs.AllNeeds) n.CurLevelPercentage = 1;
            Job rest = JobMaker.MakeJob(JobDefOf.LayDown, bed);
            rest.playerForced = true;
            rest.expiryInterval = 60000;
            ghoul.jobs.StartJob(rest, JobCondition.InterruptForced);
        }
        if (ghoul.CurrentBed() == null || doctor.CurJob?.bill != null) return;
        doctor.jobs.StopAll();
        foreach (WorkGiverDef def in DefDatabase<WorkGiverDef>.AllDefsListForReading)
        {
            if (!def.billGiversAllHumanlikes || !(def.Worker is WorkGiver_DoBill giver)) continue;
            bill.nextTickToSearchForIngredients = 0;
            Job job = giver.JobOnThing(doctor, ghoul, true);
            if (job?.bill != bill) continue;
            bool uses = job.targetQueueB != null && job.targetQueueB.Any(t => t.Thing == plating);
            Check(uses, "G6 the game's doctor job selects the reserved plating item");
            doctor.jobs.TryTakeOrderedJob(job);
            Write("doctor starts the plating operation at tick " + Find.TickManager.TicksGame);
            return;
        }
    }

    static void ShowPlans()
    {
        Type type = ModType("Implanner.UI.Dialog_Implanner");
        Type view = ModType("Implanner.UI.ImplannerView");
        Type tabs = view.GetNestedType("Tab", AccessTools.all);
        AccessTools.Field(view, "curTab").SetValue(null, Enum.Parse(tabs, "Plans"));
        dialog = (Window)Activator.CreateInstance(type);
        Find.WindowStack.Add(dialog);
        object plans = Traverse.Create(dialog).Field("view").Field("plans").GetValue();
        Traverse.Create(plans).Field("SelectedPlanId").SetValue(ghoulPlan.Id);
        Traverse.Create(plans).Field("Region").SetValue(1);
    }

    static void ShowOverview()
    {
        CloseDialog();
        Type type = ModType("Implanner.UI.Dialog_Implanner");
        Type view = ModType("Implanner.UI.ImplannerView");
        Type tabs = view.GetNestedType("Tab", AccessTools.all);
        AccessTools.Field(view, "curTab").SetValue(null, Enum.Parse(tabs, "Overview"));
        dialog = (Window)Activator.CreateInstance(type);
        Find.WindowStack.Add(dialog);
        object overview = Traverse.Create(dialog).Field("view").Field("overview").GetValue();
        Traverse.Create(overview).Field("SelectedPawnId").SetValue(ghoul.thingIDNumber);
    }

    static void CloseDialog()
    {
        if (dialog == null) return;
        dialog.Close(false);
        dialog = null;
    }

    static void Finish()
    {
        phase = 99;
        Write(failures == 0 ? "SUCCESS" : failures + " failure(s)");
        File.WriteAllText(Path.Combine(Root, "ghouls-stage.txt"),
            failures == 0 ? "SUCCESS" : "FAILED " + failures + " check(s)");
    }
}
