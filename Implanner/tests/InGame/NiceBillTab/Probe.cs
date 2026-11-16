using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Implanner;
using Implanner.Core;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace NiceBillTabProbe;

// The Bills tab of an Implanner bench, with and without Nice Bill Tab
// (Workshop 3520130671, Andromeda.NiceBillTab) on the Fisso save. A bench the
// player hands to Implanner shows Implanner's production queue instead of the
// vanilla tab or Nice Bill Tab's layout; every other bench keeps what it had.
// The fixture is a fabrication bench holding two of the player's own bills,
// a two-arm plan for a colonist with a long plan name, and stock for both
// arms, so the dispatcher queues two real bills and a crafter starts one.
// Captures:
//  A  the bench before designation: Nice Bill Tab (or vanilla), toggle offered
//  B  designated: the queue, two waiting bills, three paused bills
//  C  a crafter has started the first bill (unfinished item bound)
//  C-scale-1 / C-scale-1.5 / C-no-tiny  the same at other UI metrics
//  D  a bench that crafts no implant: untouched, tab back at its own size
//  E  the bench released: the previous layout again, bills resumed
//  F  Nice Bill Tab switched off, designated: the queue (Nice Bill Tab runs only)
// Then Nice Bill Tab's "create task on available workbench" (no capture).
[StaticConstructorOnStartup]
public static class Probe
{
    const string Package = "eprime.implanner.nicebilltab";
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    const string PlanName = "Frontline soldiers, bionic arms and legs";
    static readonly Assembly Mod = typeof(ImplannerStore).Assembly;
    static int phase, failures, updates, step, captureSeq, waitFrames, craftTicks;
    static string pendingCapture;
    static DateTime captureHeldUntil, deadline;
    static Building_WorkTable implantBench, otherBench, queueBench;
    static Pawn patient, crafter;
    static Type drawer, nbtSettings;
    static bool nbt;
    static float scale;
    static readonly List<Bill> playerBills = new List<Bill>();
    static readonly List<Thing> stock = new List<Thing>();
    static string Root => GenFilePaths.SaveDataFolderPath;

    static Probe() => new Harmony(Package).Patch(
        AccessTools.Method(typeof(Root_Play), "Update"),
        postfix: new HarmonyMethod(typeof(Probe), nameof(Update)));

    static void Write(string message)
    {
        Log.Message("[NiceBillTab] " + message);
        File.AppendAllText(Path.Combine(Root, "nicebilltab.txt"), message + Environment.NewLine);
    }

    static void Check(bool condition, string message)
    {
        Write((condition ? "PASS " : "FAIL ") + message);
        if (!condition) failures++;
    }

    static object Call(Type type, string method, params object[] args) => type.GetMethods(All)
        .Single(m => m.Name == method && m.GetParameters().Length == args.Length).Invoke(null, args);

    static bool CraftsImplants(ThingDef def) =>
        (bool)Call(Mod.GetType("Implanner.PlannerProduction"), "CraftsImplants", def);

    static bool Designated(Thing bench) =>
        (bool)Call(Mod.GetType("Implanner.BenchDesignations"), "Contains", bench);

    static ITab_Bills Tab() =>
        (ITab_Bills)Find.Selector.SingleSelectedThing.GetInspectTabs().First(t => t is ITab_Bills);

    static Vector2 TabSize() => (Vector2)AccessTools.Field(typeof(InspectTabBase), "size").GetValue(Tab());

    static Vector2 WinSize() => (Vector2)AccessTools.Field(typeof(ITab_Bills), "WinSize").GetValue(null);

    static Vector2 QueueSize() =>
        (Vector2)AccessTools.Field(Mod.GetType("Implanner.UI.BenchQueue"), "TabSize").GetValue(null);

    static void SetNbt(bool on)
    {
        AccessTools.Field(nbtSettings, "EnabledMod").SetValue(null, on);
        // What NBT's own switch does after toggling: restore the tab size.
        AccessTools.Method(GenTypes.GetTypeInAnyAssembly("NiceBillTab.ITab_Bills_Constructor_Patch"), "Prefix")
            .Invoke(null, new object[] { Tab() });
    }

    static void Show(Building_WorkTable bench)
    {
        Find.Selector.ClearSelection();
        Find.Selector.Select(bench, false, false);
        CameraJumper.TryJump(bench);
    }

    static List<Bill> Owned() => queueBench.BillStack.Bills
        .Where(b => ImplannerStore.Current.Model.OwnedProductionBills.ContainsKey(b.GetUniqueLoadID())).ToList();

    static void Dispatch()
    {
        ImplannerStore store = ImplannerStore.Current;
        object index = Call(Mod.GetType("Implanner.ColonyIndex"), "Build", store.Model);
        object pass = Activator.CreateInstance(Mod.GetType("Implanner.ReconcilePass"), All, null,
            new object[] { store.Model, index, true }, null);
        var change = (PlannerChange)Call(Mod.GetType("Implanner.PlannerProduction"), "Reconcile", store, pass);
        AccessTools.Method(typeof(ImplannerStore), "PublishPass").Invoke(store, new object[] { change });
    }

    // The queue snapshot the tab last drew, as "label | status | caption=value; ...".
    static List<string> QueueRows(out string footer)
    {
        var lines = new List<string>();
        footer = "";
        object data = AccessTools.Field(Mod.GetType("Implanner.UI.BenchQueue"), "snapshot").GetValue(null);
        if (data == null) return lines;
        footer = (string)AccessTools.Field(data.GetType(), "Footer").GetValue(data);
        foreach (object row in (IEnumerable)AccessTools.Field(data.GetType(), "Rows").GetValue(data))
        {
            string F(string name) => (string)AccessTools.Field(row.GetType(), name).GetValue(row);
            var captions = (string[])AccessTools.Field(row.GetType(), "Captions").GetValue(row);
            var values = (string[])AccessTools.Field(row.GetType(), "Values").GetValue(row);
            var tips = (string[])AccessTools.Field(row.GetType(), "ValueTips").GetValue(row);
            lines.Add(F("Label") + " | " + F("Status") + " | " + string.Join("; ",
                captions.Select((c, i) => c + "=" + (tips[i] ?? values[i]))));
        }
        return lines;
    }

    static void Update()
    {
        if (phase == 99 || Current.ProgramState != ProgramState.Playing
            || Find.CurrentMap == null || LongEventHandler.AnyEventNowOrWaiting) return;
        try
        {
            AccessTools.Field(typeof(LudeonTK.EditWindow_Log), "canAutoOpen").SetValue(null, false);
            foreach (Window window in Find.WindowStack.Windows.ToArray())
                if (!(window is MainTabWindow) && !(window is ImmediateWindow)) window.Close(false);
            if (pendingCapture != null)
            {
                if (waitFrames-- > 0) return;
                if (waitFrames == -1)
                {
                    File.WriteAllText(Path.Combine(Root, "nicebilltab-capture.txt"), captureSeq + " " + pendingCapture);
                    captureHeldUntil = DateTime.UtcNow.AddSeconds(1.6);
                }
                if (DateTime.UtcNow < captureHeldUntil) return;
                pendingCapture = null;
            }
            if (phase > 0 && DateTime.UtcNow > deadline) throw new Exception("Timed out at step " + step);
            if (phase == 0)
            {
                if (updates++ < 8) return;
                Setup();
                phase = 1;
                return;
            }
            string footer;
            List<string> rows;
            switch (step++)
            {
                case 0:
                    Show(queueBench);
                    return;
                case 1:
                    InspectPaneUtility.OpenTab(typeof(ITab_Bills));
                    Capture("A-before-designation");
                    return;
                case 2:
                    Check(!Designated(queueBench), "A the bench starts undesignated");
                    Write("A tab size " + TabSize() + ", WinSize " + WinSize());
                    PlannerCommands.SetBenchDesignated(queueBench.thingIDNumber, true);
                    Check(playerBills.All(b => b.suspended), "B the player's bills are suspended on designation");
                    Bill stray = playerBills[0].recipe.MakeNewBill();
                    queueBench.BillStack.AddBill(stray);
                    playerBills.Add(stray);
                    Check(stray.suspended, "B a bill added by BillStack.AddBill is suspended at once");
                    Dispatch();
                    Capture("B-queue-waiting");
                    return;
                case 3:
                    Check(Designated(queueBench), "B the bench is designated");
                    Check(TabSize() == QueueSize(), "B the tab has the queue's own size: " + TabSize());
                    List<Bill> owned = Owned();
                    Check(owned.Count == 2 && owned.All(b => !b.suspended && b is Bill_ProductionWithUft),
                        "B Implanner queued two bills here through the game's factory, not suspended (" + owned.Count + ")");
                    foreach (Bill bill in owned)
                    {
                        ProductionBillOrigin origin = ImplannerStore.Current.Model
                            .ProductionBillOriginOf(bill.GetUniqueLoadID());
                        Check(origin.PawnId == patient.thingIDNumber && origin.CreatedTick >= 0
                            && Find.TickManager.TicksGame - origin.CreatedTick <= 2,
                            "B the bill records its colonist and tick (" + origin.PawnId + ", " + origin.CreatedTick + ")");
                    }
                    rows = QueueRows(out footer);
                    foreach (string row in rows) Write("B row: " + row);
                    Write("B footer: " + footer);
                    Check(rows.Count == 2 && rows.All(r => r.Contains("=" + PlanName) && r.Contains("=" + patient.LabelShortCap)),
                        "B both rows name the plan and the colonist");
                    Check(footer.Contains("3"), "B the footer counts the three paused bills");
                    StartCrafting();
                    return;
                case 4:
                    // Real ticks until the crafter has made the unfinished item.
                    for (int i = 0; i < 120 && !Started(); i++, craftTicks++) Tick();
                    if (!Started())
                    {
                        if (craftTicks > 9000) throw new Exception("crafter never started: job=" + crafter.CurJob);
                        step--;
                        return;
                    }
                    Write("C crafting started after " + craftTicks + " ticks, job=" + crafter.CurJob);
                    // Past the next refresh boundary, with the crafter still at work.
                    for (int i = 0; i < 1100; i++) Tick();
                    Capture("C-queue-crafting");
                    return;
                case 5:
                    var started = (Bill_ProductionWithUft)Owned().First(b => b is Bill_ProductionWithUft u && u.BoundUft != null);
                    Check(started.BoundUft != null, "C the game is crafting Implanner's bill (unfinished item bound)");
                    rows = QueueRows(out footer);
                    foreach (string row in rows) Write("C row: " + row);
                    Check(rows.Any(r => r.Contains("%") && r.Contains("=" + crafter.LabelShortCap)),
                        "C a row shows progress and the worker");
                    scale = Prefs.UIScale;
                    Prefs.UIScale = 1f;
                    Capture("C-scale-1");
                    return;
                case 6:
                    Write("scale 1: effective " + Prefs.UIScale);
                    Prefs.UIScale = 1.5f;
                    Capture("C-scale-1.5");
                    return;
                case 7:
                    Write("scale 1.5: effective " + Prefs.UIScale);
                    Prefs.UIScale = scale;
                    Prefs.DisableTinyText = true;
                    Capture("C-no-tiny");
                    return;
                case 8:
                    Prefs.DisableTinyText = false;
                    if (otherBench == null) { Write("D skipped: no bench without implant recipes"); step++; goto case 10; }
                    Show(otherBench);
                    return;
                case 9:
                    InspectPaneUtility.OpenTab(typeof(ITab_Bills));
                    Capture("D-other-bench");
                    return;
                case 10:
                    if (otherBench != null)
                        Check(TabSize() != QueueSize() || QueueSize() == WinSize(),
                            "D another bench's tab is back at its own size: " + TabSize());
                    Show(queueBench);
                    return;
                case 11:
                    InspectPaneUtility.OpenTab(typeof(ITab_Bills));
                    PlannerCommands.SetBenchDesignated(queueBench.thingIDNumber, false);
                    Capture("E-released");
                    return;
                case 12:
                    Check(!Designated(queueBench), "E the bench is released");
                    Check(playerBills.All(b => !b.suspended), "E the player's bills resume on release");
                    Check(TabSize() != QueueSize(), "E the tab is back at its previous size: " + TabSize());
                    if (nbt)
                    {
                        var listed = (List<Bill>)AccessTools.Field(drawer, "filteredBills").GetValue(null);
                        Check(listed != null && listed.Count == queueBench.BillStack.Count,
                            "E Nice Bill Tab lists all " + queueBench.BillStack.Count + " bills after release (listed "
                            + listed?.Count + ")");
                    }
                    else
                    {
                        Check(TabSize() == WinSize(), "E the vanilla tab has the vanilla size");
                        step++;
                        goto default;
                    }
                    SetNbt(false);
                    PlannerCommands.SetBenchDesignated(queueBench.thingIDNumber, true);
                    Capture("F-nbt-off-designated");
                    return;
                case 13:
                    Check(TabSize() == QueueSize(), "F with Nice Bill Tab switched off the queue still shows: " + TabSize());
                    SetNbt(true);
                    PlannerCommands.SetBenchDesignated(queueBench.thingIDNumber, false);
                    CraftRequest();
                    Finish();
                    return;
                default:
                    Finish();
                    return;
            }
        }
        catch (Exception ex)
        {
            Write("ERROR " + ex);
            File.WriteAllText(Path.Combine(Root, "nicebilltab-stage.txt"), "FAILED " + ex.Message);
            phase = 99;
        }
    }

    static void Tick()
    {
        queueBench.GetComp<CompPowerTrader>().PowerOn = true;
        Find.TickManager.DoSingleTick();
    }

    static bool Started() => Owned().Any(b => b is Bill_ProductionWithUft u && u.BoundUft != null);

    // The game's own work giver hands the crafter the bill, as a colonist
    // looking for work would get it.
    static void StartCrafting()
    {
        Bill bill = Owned()[0];
        crafter.jobs.StopAll();
        // The fixture stands outside the saved colony's allowed areas.
        crafter.playerSettings.AreaRestrictionInPawnCurrentMap = null;
        crafter.Position = queueBench.InteractionCell;
        crafter.Notify_Teleported(true, false);
        foreach (Need need in crafter.needs.AllNeeds) need.CurLevelPercentage = 1f;
        foreach (WorkGiverDef def in DefDatabase<WorkGiverDef>.AllDefsListForReading)
        {
            if (!(def.Worker is WorkGiver_DoBill giver) || def.billGiversAllHumanlikes) continue;
            bill.nextTickToSearchForIngredients = 0;
            Job job = giver.JobOnThing(crafter, queueBench, true);
            if (job == null) continue;
            crafter.jobs.TryTakeOrderedJob(job);
            Write("C the game's work giver starts " + job.bill?.recipe.defName + ": " + job);
            return;
        }
        // The saved orbital map's region walk does not find fixture stock
        // (as in BugFixes/production): the game's own ingredient matcher and
        // job factory over the reachable items instead. The real DoBill
        // driver still hauls, works and makes the unfinished item.
        Map map = queueBench.Map;
        List<Thing> reachable = map.listerThings.AllThings.Where(t => t.def.category == ThingCategory.Item
            && !t.IsForbidden(crafter) && bill.IsFixedOrAllowedIngredient(t)
            && crafter.CanReserveAndReach(t, PathEndMode.ClosestTouch, Danger.Deadly)).ToList();
        var picked = new List<ThingCount>();
        var absent = new List<IngredientCount>();
        if ((bool)Call(typeof(WorkGiver_DoBill), "TryFindBestBillIngredientsInSet",
                reachable, bill, picked, queueBench.Position, false, absent))
        {
            Job native = WorkGiver_DoBill.TryStartNewDoBillJob(crafter, bill, queueBench, picked, out _);
            if (native != null)
            {
                crafter.jobs.TryTakeOrderedJob(native);
                Write("C the game's ingredient matcher and job factory start " + bill.recipe.defName + ": " + native);
                return;
            }
        }
        foreach (Thing t in stock)
            Write("stock " + t.LabelCap + " spawned=" + t.Spawned + " forbidden=" + t.IsForbidden(crafter)
                + " reach=" + crafter.CanReach(t, PathEndMode.ClosestTouch, Danger.Deadly)
                + " reserve=" + crafter.CanReserve(t) + " allowed=" + bill.IsFixedOrAllowedIngredient(t));
        throw new Exception("no work giver offered the bill: reachable=" + reachable.Count + " absent="
            + string.Join(",", absent.Select(a => a.Summary)) + " allowed=" + bill.PawnAllowedToStartAnew(crafter)
            + " usable=" + queueBench.UsableForBillsAfterFueling());
    }

    static IEnumerable<IntVec3> FreeNear(Map map, IntVec3 origin, int clear) => GenRadial.RadialCellsAround(origin, 30f, true)
        .Where(c => c.InBounds(map) && c.Standable(map) && !c.Fogged(map) && c.GetZone(map) == null
            && crafter.CanReach(c, PathEndMode.OnCell, Danger.Some)
            && !GenAdj.OccupiedRect(c, Rot4.North, new IntVec2(clear, clear)).Cells
                .Any(x => !x.InBounds(map) || !x.Standable(map) || x.GetEdifice(map) != null || x.GetThingList(map).Any()));

    // Nice Bill Tab's "create task on available workbench" (Utils.QueryCraft)
    // for an item only the implant bench's type can make: with every bench
    // of that type designated nothing is added; with a free bench of the
    // type on the map the task goes there, unsuspended.
    static void CraftRequest()
    {
        Map map = implantBench.Map;
        ThingDef benchDef = implantBench.def;
        RecipeDef recipe = benchDef.AllRecipes.First(r => r.AvailableNow && !r.mechanitorOnlyRecipe
            && r.products.Count > 0 && r.AllRecipeUsers.Count() == 1
            && DefDatabase<RecipeDef>.AllDefs.Count(o => o.products.Any(p => p.thingDef == r.products[0].thingDef)) == 1
            && map.mapPawns.FreeColonists.Any(p => r.PawnSatisfiesSkillRequirements(p)));
        ThingDef item = recipe.products[0].thingDef;
        MethodInfo query = AccessTools.Method(GenTypes.GetTypeInAnyAssembly("NiceBillTab.Utils"), "QueryCraft");
        FieldInfo last = AccessTools.Field(drawer, "LastSelTable");
        if (last.GetValue(null) == null) last.SetValue(null, implantBench);
        List<Building_WorkTable> same = map.listerBuildings.allBuildingsColonist
            .OfType<Building_WorkTable>().Where(b => b.def == benchDef).ToList();
        Write("H item " + item.defName + " via " + recipe.defName + ", " + same.Count + " " + benchDef.defName + " on the map");
        foreach (Building_WorkTable bench in same) PlannerCommands.SetBenchDesignated(bench.thingIDNumber, true);
        int total = same.Sum(b => b.BillStack.Count);
        query.Invoke(null, new object[] { item, 3 });
        Check(same.Sum(b => b.BillStack.Count) == total,
            "H with every bench of the type designated, the craft request adds no bill");

        var free = (Building_WorkTable)ThingMaker.MakeThing(benchDef, GenStuff.DefaultStuffFor(benchDef));
        free.SetFaction(Faction.OfPlayer);
        GenSpawn.Spawn(free, FreeNear(map, crafter.Position, 5).First(), map);
        query.Invoke(null, new object[] { item, 3 });
        Check(same.Sum(b => b.BillStack.Count) == total, "H the designated benches still get no bill");
        Check(free.BillStack.Count == 1 && free.BillStack[0].recipe == recipe && !free.BillStack[0].suspended,
            "H the craft request lands on the free bench, not suspended (" + free.BillStack.Count + " bill)");
        Check(Find.Selector.SingleSelectedThing == free, "H Nice Bill Tab jumps to the free bench");
        foreach (Building_WorkTable bench in same) PlannerCommands.SetBenchDesignated(bench.thingIDNumber, false);
    }

    static void Capture(string name)
    {
        // One tick per capture: the run's stall guard needs advancing ticks.
        Find.TickManager.DoSingleTick();
        captureSeq++;
        pendingCapture = name;
        waitFrames = 4;
    }

    static void Setup()
    {
        Find.TickManager.Pause();
        Map map = Find.CurrentMap;
        drawer = GenTypes.GetTypeInAnyAssembly("NiceBillTab.TabBillsDrawer");
        nbtSettings = GenTypes.GetTypeInAnyAssembly("NiceBillTab.Settings");
        nbt = drawer != null && nbtSettings != null;
        Write(nbt ? "Nice Bill Tab is loaded" : "Nice Bill Tab is not loaded: vanilla bills tab");
        if (nbt)
        {
            Check((bool)AccessTools.Field(nbtSettings, "EnabledMod").GetValue(null), "Nice Bill Tab is switched on");
            Patches info = Harmony.GetPatchInfo(AccessTools.Method(drawer, "DrawLeftPart"));
            Check(info != null && info.Prefixes.Any(p => p.owner == "EPrime.Implanner"),
                "Implanner's prefix is on NBT's DrawLeftPart");
            info = Harmony.GetPatchInfo(AccessTools.Method(
                GenTypes.GetTypeInAnyAssembly("NiceBillTab.ITab_Bills_FillTab_Patch"), "Prefix"));
            Check(info != null && info.Prefixes.Any(p => p.owner == "EPrime.Implanner"),
                "Implanner's prefix is on NBT's FillTab prefix");
        }

        List<Building_WorkTable> benches = map.listerBuildings.allBuildingsColonist
            .OfType<Building_WorkTable>().OrderBy(b => b.thingIDNumber).ToList();
        implantBench = benches.FirstOrDefault(b => CraftsImplants(b.def));
        otherBench = benches.FirstOrDefault(b => !CraftsImplants(b.def) && b.def.AllRecipes.Count > 0);
        if (implantBench == null) throw new Exception("fixture: no bench crafts implants");

        // A fresh model: one plan, one colonist, one Implanner bench.
        ImplannerStore store = ImplannerStore.Current;
        store.Model = new PlannerModel();
        patient = map.mapPawns.FreeColonistsSpawned.First(p => p.RaceProps.Humanlike
            && p.health.hediffSet.GetNotMissingParts().Count(x => x.def.defName == "Shoulder"
                && !p.health.hediffSet.HasDirectlyAddedPartFor(x)) == 2);
        crafter = map.mapPawns.FreeColonistsSpawned.First(p => p != patient && p.RaceProps.Humanlike
            && !p.Downed && !p.WorkTypeIsDisabled(WorkTypeDefOf.Crafting));
        foreach (Thing arm in map.listerThings.ThingsOfDef(ThingDef.Named("BionicArm")).ToArray()) arm.Destroy();

        queueBench = (Building_WorkTable)ThingMaker.MakeThing(ThingDef.Named("FabricationBench"));
        queueBench.SetFaction(Faction.OfPlayer);
        GenSpawn.Spawn(queueBench, FreeNear(map, crafter.Position, 7).First(), map);
        queueBench.GetComp<CompPowerTrader>().PowerOn = true;
        RecipeDef recipe = DefDatabase<RecipeDef>.GetNamed("Make_BionicArm");
        foreach (ResearchProjectDef r in recipe.researchPrerequisites ?? new List<ResearchProjectDef>())
            Find.ResearchManager.FinishProject(r, false);
        if (recipe.researchPrerequisite != null) Find.ResearchManager.FinishProject(recipe.researchPrerequisite, false);
        var zone = new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile, map.zoneManager);
        map.zoneManager.RegisterZone(zone);
        zone.GetStoreSettings().filter.SetAllowAll(null);
        foreach (IngredientCount ing in recipe.ingredients)
        {
            if (!ing.IsFixedIngredient) continue;
            ThingDef d = ing.FixedIngredient;
            PlannerCommands.SetResourceReserve(d.defName, 0);
            int n = ing.CountRequiredOfFor(d, recipe) * 4;
            while (n > 0)
            {
                IntVec3 c = FreeNear(map, queueBench.InteractionCell, 1).First();
                zone.AddCell(c);
                Thing t = ThingMaker.MakeThing(d);
                t.stackCount = Math.Min(n, d.stackLimit);
                stock.Add(GenSpawn.Spawn(t, c, map));
                n -= t.stackCount;
            }
        }
        map.resourceCounter.UpdateResourceCounts();

        // The player's own bills, there before Implanner takes the bench.
        foreach (RecipeDef own in queueBench.def.AllRecipes.Where(r => r.AvailableNow && r.products.Count > 0
            && r != recipe).Take(2))
        {
            Bill bill = own.MakeNewBill();
            queueBench.BillStack.AddBill(bill);
            playerBills.Add(bill);
        }
        if (playerBills.Count != 2) throw new Exception("fixture: the bench offers no other recipes");

        PlannerCommands.CreatePlan(PlanName, 0, "", "");
        var plan = store.Model.Plans.Single();
        PlannerCommands.SetImplantSlot(plan.Id, "BionicArm", "Shoulder#0", true);
        PlannerCommands.SetImplantSlot(plan.Id, "BionicArm", "Shoulder#1", true);
        PlannerCommands.AssignPlan(patient.thingIDNumber, plan.Id);
        PlannerCommands.SetProductionConcurrency(1);
        PlannerCommands.SetProductionSkill(0);
        PlannerCommands.SetOnlyDesignatedBenches(true);
        Write("queue bench #" + queueBench.thingIDNumber + ", patient " + patient.LabelShortCap + ", crafter "
            + crafter.LabelShortCap + ", implant bench " + implantBench.def.defName + ", other bench "
            + otherBench?.def.defName);
        deadline = DateTime.UtcNow.AddSeconds(52);
    }

    static void Finish()
    {
        phase = 99;
        Write(failures == 0 ? "SUCCESS" : failures + " failure(s)");
        File.WriteAllText(Path.Combine(Root, "nicebilltab-stage.txt"),
            failures == 0 ? "SUCCESS" : "FAILED " + failures + " check(s)");
    }
}
