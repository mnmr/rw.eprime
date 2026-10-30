using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using EPrimeReadouts;
using EPrimeReadouts.Core;
using EPrimeReadouts.UI;
using HarmonyLib;
using LudeonTK;
using RimWorld;
using Strata;
using Verse;
using Verse.AI;

namespace ReadoutsBugFixProbe;

[StaticConstructorOnStartup]
public static class Probe
{
    private static readonly List<string> results = new();
    private static readonly Type RenderData = typeof(ReadoutStore).Assembly.GetType("EPrimeReadouts.GameRenderData");
    private static int stage, frames, started, poolId;
    private static bool done;
    private static Map lower, upper;
    private static MapPortal stairs;
    private static Pawn worker;
    private static Building_WorkTable bench;
    private static Bill_Production bill;
    private static WorkGiver_DoBill workGiver;
    private static string Root => GenFilePaths.SaveDataFolderPath;
    private static readonly IntVec3 origin = new(30, 0, 30);
    static Probe()
    {
        var harmony = new Harmony("eprime.readouts.bugfixprobe");
        harmony.Patch(AccessTools.Method(typeof(Root_Play), "Update"), postfix: new HarmonyMethod(typeof(Probe), nameof(Update)));
        // Same native quick-test setup as the existing Setup fixture. The run's
        // copied source save remains untouched; no player profile is used.
        harmony.Patch(AccessTools.Method(typeof(SavedGameLoaderNow), "LoadGameFromSaveFileNow"), prefix: new HarmonyMethod(typeof(Probe), nameof(NewColony)));
    }
    private static bool NewColony()
    {
        Root_Play.SetupForQuickTestPlay();
        Find.GameInitData.PrepForMapGen();
        Find.Scenario.PreMapGenerate();
        Current.Game.InitNewGame();
        return false;
    }
    private static object Call(Type type, string method, params object[] args) => AccessTools.Method(type, method).Invoke(null, args);
    private static void Check(bool condition, string message)
    {
        results.Add((condition ? "PASS " : "FAIL ") + message);
        File.WriteAllLines(Path.Combine(Root, "fix-results.txt"), results);
        if (!condition) throw new Exception(message);
    }
    private static void Finish(Exception error = null)
    {
        Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
        done = true;
        if (error != null) results.Add("ERROR " + error);
        File.WriteAllLines(Path.Combine(Root, "fix-results.txt"), results);
        File.WriteAllText(Path.Combine(Root, "fix-done.txt"), error == null ? "PASS" : "FAIL");
    }
    private static void Update()
    {
        if (done || Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null
            || LongEventHandler.AnyEventNowOrWaiting || ++frames < 10) return;
        frames = 0;
        try
        {
            AccessTools.Field(typeof(EditWindow_Log), "canAutoOpen").SetValue(null, false);
            EditWindow_Log.wantsToOpen = false;
            if (stage == 0)
            {
                if (File.ReadAllText(Path.Combine(Root, "fix-mode.txt")) == "ui")
                {
                    Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                    lower = upper = Find.CurrentMap;
                    TestPool(); stage = 4;
                    File.WriteAllText(Path.Combine(Root, "capture-request.txt"), "pool-cleared");
                    return;
                }
                SetupFloors(); TestFloors(); SetupCraft("HandTailoringBench", "Make_Apparel_Duster", "Cloth", 160);
                Check(Display(lower, "Cloth", true) == 80, "RD001 before work: 160 cloth minus one 80-cloth duster = 80");
                StrataResources.Combined = true;
                Check(Snapshot(upper, true).DebtOf("Cloth").Bills == 80, "RD003 upper floor sees lower floor's planned bill debt");
                StrataResources.Combined = false;
                TakeJob(); stage = 1;
            }
            else if (stage == 1)
            {
                var uft = ((Bill_ProductionWithUft)bill).BoundUft;
                if (uft == null) { Timeout(); return; }
                Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                Check(uft.ingredients.Sum(t => t.stackCount) == 80, "RD001 native recipe toils transferred all 80 cloth into unfinished duster");
                worker.jobs.EndCurrentJob(JobCondition.InterruptForced, startNewJob: false);
                Check(Display(lower, "Cloth", true) == 80, "RD001 interrupted unfinished duster leaves all 80 loose cloth available");
                bill.repeatCount = 2;
                Check(Display(lower, "Cloth", true) == 0, "RD001 next duster still reserves 80 cloth");
                bill.repeatCount = 1;
                Check(Display(lower, "Cloth", false) == 80, "RD001 reservations disabled keeps raw loose stock");
                DebugSettings.fastCrafting = true;
                TakeJob(); stage = 2;
            }
            else if (stage == 2)
            {
                if (bill.repeatCount != 0) { Timeout(); return; }
                Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                Check(lower.listerThings.ThingsOfDef(ThingDef.Named("Apparel_Duster")).Count == 1,
                    "RD001 pawn completed native duster job and produced apparel (no debug completion)");
                Check(Display(lower, "Cloth", true) == 80, "RD001 completed duster keeps available cloth at 80");
                bench.BillStack.Clear();
                SetupCraft("TableStonecutter", "Make_StoneBlocksAny", "ChunkGranite", 20);
                bill.repeatMode = BillRepeatModeDefOf.TargetCount; bill.targetCount = 100;
                Check(Display(lower, "ChunkGranite", true) == 15, "RD002 granite-only generic target100 with20 chunks reserves5");
                TakeJob(); worker.workSettings.SetPriority(workGiver.def.workType, 0); stage = 3;
            }
            else if (stage == 3)
            {
                int blocks = lower.listerThings.ThingsOfDef(ThingDef.Named("BlocksGranite")).Sum(t => t.stackCount);
                if (blocks < 20) { Timeout(); return; }
                Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                worker.jobs.EndCurrentJob(JobCondition.InterruptForced, startNewJob: false);
                Check(blocks == 20, "RD002 pawn completed native generic stonecutting job: one chunk produced20 blocks");
                // The bill counts stored blocks, so store its dropped output.
                foreach (Thing thing in lower.listerThings.ThingsOfDef(ThingDef.Named("BlocksGranite")).ToArray())
                { thing.DeSpawn(); GenPlace.TryPlaceThing(thing, origin + new IntVec3(5,0,0), lower, ThingPlaceMode.Near); }
                Check(Display(lower, "ChunkGranite", true) == 15, "RD002 after one craft:19 chunks minus4 future crafts remains15");
                TestPool(); stage = 4;
                File.WriteAllText(Path.Combine(Root, "capture-request.txt"), "pool-cleared");
            }
            else if (stage == 4)
            {
                if (!File.Exists(Path.Combine(Root, "capture-pool-cleared.done"))) return;
                CloseWindows(); SetupSearch(); stage = 5; frames = -20;
            }
            else if (stage == 5)
            {
                object draw = AccessTools.Field(typeof(ReadoutPanel), "draw").GetValue(null);
                var model = (RenderModel)AccessTools.Field(draw.GetType(), "Model").GetValue(draw);
                Check(model.Cells.Any(c => c.Kind == CellKind.Highlight && c.Token == "#" + poolId),
                    "RD005 real German panel query Leder highlights seeded pool displayed as Leder");
                File.WriteAllText(Path.Combine(Root, "capture-request.txt"), "german-search"); stage = 6;
            }
            else if (stage == 6)
            {
                if (!File.Exists(Path.Combine(Root, "capture-german-search.done"))) return;
                ReadoutPanel.SearchText = "Leathers"; ReadoutPanel.BumpView(); stage = 7; frames = -20;
            }
            else if (stage == 7)
            {
                object draw = AccessTools.Field(typeof(ReadoutPanel), "draw").GetValue(null);
                var model = (RenderModel)AccessTools.Field(draw.GetType(), "Model").GetValue(draw);
                Check(!model.Cells.Any(c => c.Kind == CellKind.Highlight && c.Token == "#" + poolId),
                    "RD005 hidden English seed name does not highlight German pool");
                File.WriteAllText(Path.Combine(Root, "capture-request.txt"), "english-search"); stage = 8;
            }
            else if (stage == 8)
            {
                if (!File.Exists(Path.Combine(Root, "capture-english-search.done"))) return;
                Finish();
            }
        }
        catch (Exception error) { Finish(error); }
    }
    private static void Timeout()
    {
        if (Find.TickManager.TicksGame - started > 25000)
            throw new Exception("Native crafting timed out; job=" + worker.CurJob + "; driver=" + worker.jobs.curDriver);
    }
    private static void CloseWindows() { for (int i = Find.WindowStack.Count - 1; i >= 0; i--) Find.WindowStack[i].Close(false); }
    private static void PrepareArea(Map map)
    {
        foreach (IntVec3 cell in CellRect.CenteredOn(origin, 12))
        {
            foreach (Thing thing in cell.GetThingList(map).ToArray())
                if (thing is not Pawn && thing is not MapPortal) thing.Destroy(DestroyMode.Vanish);
            map.terrainGrid.SetTerrain(cell, TerrainDefOf.Concrete);
            map.roofGrid.SetRoof(cell, null);
            map.fogGrid.Unfog(cell);
        }
        var zone = new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile, map.zoneManager);
        zone.settings.filter.SetAllowAll(null); map.zoneManager.RegisterZone(zone);
        foreach (IntVec3 cell in new CellRect(origin.x + 3, origin.z - 2, 8, 5)) zone.AddCell(cell);
    }
    private static void SetupFloors()
    {
        Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
        CloseWindows(); lower = Find.CurrentMap; PrepareArea(lower);
        foreach (Pawn pawn in lower.mapPawns.FreeColonistsSpawned) pawn.drafter.Drafted = true;
        DebugSettings.godMode = true;
        stairs = (MapPortal)ThingMaker.MakeThing(ThingDef.Named("Strata_StairsBuildUp"));
        stairs.SetFaction(Faction.OfPlayer); GenSpawn.Spawn(stairs, origin + new IntVec3(-8,0,0), lower);
        upper = stairs.GetOtherMap();
        Check(upper != null && LevelGraph.ReachableLevels(lower).Any(link => link.map == upper), "RD003 real Strata tower opens linked upper floor");
        PrepareArea(upper); Current.Game.CurrentMap = upper;
        foreach (Map map in new[] { lower, upper })
            foreach (string def in new[] { "Steel", "Cloth", "ChunkGranite", "BlocksGranite" })
                foreach (Thing thing in map.listerThings.ThingsOfDef(ThingDef.Named(def)).ToArray()) thing.Destroy(DestroyMode.Vanish);
        StrataResources.Combined = true;
    }
    private static Thing Spawn(Map map, string def, int count, IntVec3 position, bool forbidden = false)
    {
        var thing = ThingMaker.MakeThing(ThingDef.Named(def)); thing.stackCount = count;
        GenSpawn.Spawn(thing, position, map); thing.SetForbidden(forbidden, false); return thing;
    }
    private static RenderCountSnapshot Snapshot(Map map, bool reserve = false)
    {
        foreach (Map level in new[] { lower, upper }) level.resourceCounter.UpdateResourceCounts();
        return (RenderCountSnapshot)Call(typeof(GameCounts), "BuildSnapshot", map,
            new CountSnapshotOptions(false, true, new PlannedWorkOptions(reserve, false, false)));
    }
    private static int Display(Map map, string def, bool reserve)
    {
        var snapshot = Snapshot(map, reserve);
        return CountBasis.Displayed(snapshot.SearchCountOf(def), false, false, snapshot.DebtOf(def).Bills, true);
    }
    private static void TestFloors()
    {
        Thing high = Spawn(upper, "Steel", 75, origin + new IntVec3(5,0,0));
        Spawn(upper, "Steel", 15, origin + new IntVec3(6,0,0));
        Check(Snapshot(lower).SearchCountOf("Steel").Total == 90, "RD003 upstairs-only90 steel appears downstairs");
        high.stackCount = 74; Spawn(lower, "Steel", 1, origin + new IntVec3(5,0,0));
        var snapshot = Snapshot(lower); var stock = snapshot.SearchCountOf("Steel");
        Check(stock.Total == 90 && stock.Stored == 90, "RD003 moving1 steel downstairs retains90 total and stored");
        Spawn(upper, "Steel", 7, origin + new IntVec3(-5,0,3), true);
        Spawn(upper, "Steel", 3, origin + new IntVec3(7,0,0), true);
        stock = Snapshot(lower).SearchCountOf("Steel");
        Check(stock.Total == 100 && stock.Stored == 93 && stock.Unforbidden == 90 && stock.StoredUnforbidden == 90,
            "RD003 linked floors preserve stored, scattered and forbidden distinctions");
        var settings = EPrimeReadoutsMod.Settings;
        settings.searchStorageOnly = false; settings.searchHideForbidden = true;
        settings.reserveForBills = false; settings.reserveForBuildables = false;
        Call(RenderData, "Reset");
        object first = Call(RenderData, "Get", lower, ReadoutStore.Current);
        object second = Call(RenderData, "Get", upper, ReadoutStore.Current);
        Check(ReferenceEquals(first, second), "RD003 switching floors reuses one producer snapshot");
        StrataResources.Combined = false;
        object local = Call(RenderData, "Get", lower, ReadoutStore.Current);
        var localCounts = (RenderCountSnapshot)local.GetType().GetProperty("Counts").GetValue(local);
        Check(localCounts.SearchCountOf("Steel").Total == 1, "RD003 combined OFF updates immediately while paused");
        StrataResources.Combined = true;
        object combined = Call(RenderData, "Get", lower, ReadoutStore.Current);
        var combinedCounts = (RenderCountSnapshot)combined.GetType().GetProperty("Counts").GetValue(combined);
        Check(combinedCounts.SearchCountOf("Steel").Total == 100, "RD003 combined ON restores linked counts while paused");
        Check(Snapshot(lower).Equals(Snapshot(upper)), "RD003 both linked floors produce equal complete counts");
        int tick = Find.TickManager.TicksGame;
        high.stackCount = 73;
        Find.TickManager.DebugSetTicksGame(tick + 203);
        object before = Call(RenderData, "Get", lower, ReadoutStore.Current);
        Check(ReferenceEquals(before, combined), "RD003 boundary-minus-one reuses published snapshot");
        Find.TickManager.DebugSetTicksGame(tick + 204);
        object refreshed = Call(RenderData, "Get", lower, ReadoutStore.Current);
        var refreshedCounts = (RenderCountSnapshot)refreshed.GetType().GetProperty("Counts").GetValue(refreshed);
        Check(refreshedCounts.SearchCountOf("Steel").Total == 99, "RD003 204-tick boundary publishes changed floor stock");
        // Recount raw totals before comparing an equal refresh.
        Snapshot(lower);
        Find.TickManager.DebugSetTicksGame(tick + 408);
        refreshed = Call(RenderData, "Get", lower, ReadoutStore.Current);
        refreshedCounts = (RenderCountSnapshot)refreshed.GetType().GetProperty("Counts").GetValue(refreshed);
        Find.TickManager.DebugSetTicksGame(tick + 612);
        object equal = Call(RenderData, "Get", lower, ReadoutStore.Current);
        Check(ReferenceEquals(refreshed, equal), "RD003 equal refresh preserves producer identity");

        var landing = stairs.exit;
        IntVec3 shaftCell = stairs.Position, landingCell = landing.Position;
        // Use the same scoped portal-move lifecycle as a gravship transition.
        StrataPortalUtility.BeginPortalMove();
        try { stairs.DeSpawn(); landing.DeSpawn(); }
        finally { StrataPortalUtility.EndPortalMove(); }
        object detached = Call(RenderData, "Get", lower, ReadoutStore.Current);
        var detachedCounts = (RenderCountSnapshot)detached.GetType().GetProperty("Counts").GetValue(detached);
        Check(detachedCounts.SearchCountOf("Steel").Total == 1, "RD003 portal despawn invalidates topology while paused");
        GenSpawn.Spawn(stairs, shaftCell, lower);
        PocketMapUtility.currentlyGeneratingPortal = stairs;
        try { GenSpawn.Spawn(landing, landingCell, upper); }
        finally { PocketMapUtility.currentlyGeneratingPortal = null; }
        object relinked = Call(RenderData, "Get", lower, ReadoutStore.Current);
        var relinkedCounts = (RenderCountSnapshot)relinked.GetType().GetProperty("Counts").GetValue(relinked);
        Check(relinkedCounts.SearchCountOf("Steel").Total == 99, "RD003 portal respawn restores linked stock while paused");
    }
    private static void SetupCraft(string table, string recipeName, string material, int amount)
    {
        StrataResources.Combined = false;
        Current.Game.CurrentMap = lower;
        var recipe = DefDatabase<RecipeDef>.GetNamed(recipeName);
        var tableDef = ThingDef.Named(table);
        bench = (Building_WorkTable)ThingMaker.MakeThing(tableDef, tableDef.MadeFromStuff ? ThingDefOf.WoodLog : null);
        bench.SetFaction(Faction.OfPlayer);
        GenSpawn.Spawn(bench, origin + new IntVec3(0,0,stage == 0 ? 0 : 5), lower, Rot4.North);
        workGiver = DefDatabase<WorkGiverDef>.AllDefsListForReading
            .Where(d => d.fixedBillGiverDefs != null && d.fixedBillGiverDefs.Contains(tableDef))
            .Select(d => d.Worker).OfType<WorkGiver_DoBill>().First();
        if (worker == null)
        {
            worker = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist, Faction.OfPlayer,
                forceGenerateNewPawn: true, canGeneratePawnRelations: false, forceNoBackstory: true, forceNoIdeo: true,
                fixedBiologicalAge: 30f, forbidAnyTitle: true));
            GenSpawn.Spawn(worker, bench.InteractionCell, lower);
            worker.skills.GetSkill(SkillDefOf.Crafting).Level = 20;
        }
        if (worker.needs.food != null) worker.needs.food.CurLevelPercentage = 1;
        if (worker.needs.rest != null) worker.needs.rest.CurLevelPercentage = 1;
        worker.workSettings.SetPriority(workGiver.def.workType, 1);
        bill = (Bill_Production)recipe.MakeNewBill(); bench.BillStack.AddBill(bill);
        bill.ingredientFilter.SetDisallowAll(); bill.ingredientFilter.SetAllow(ThingDef.Named(material), true);
        bill.repeatMode = BillRepeatModeDefOf.RepeatCount; bill.repeatCount = 1;
        bill.SetPawnRestriction(worker); bill.SetStoreMode(BillStoreModeDefOf.DropOnFloor);
        int stack = ThingDef.Named(material).stackLimit;
        for (int remaining = amount, cell = 0; remaining > 0; cell++)
        {
            int count = Math.Min(stack, remaining); remaining -= count;
            Spawn(lower, material, count, origin + new IntVec3(4 + cell % 7,0,-2 + cell / 7));
        }
    }
    private static void TakeJob()
    {
        Job job = workGiver.JobOnThing(worker, bench, true);
        Check(job != null, "native workgiver accepted " + bill.recipe.defName);
        worker.jobs.TryTakeOrderedJob(job);
        started = Find.TickManager.TicksGame; Find.TickManager.CurTimeSpeed = TimeSpeed.Superfast;
    }
    private static void TestPool()
    {
        var catalog = GameResourceCatalog.Instance;
        ReadoutCommands.CreatePool("Audit textiles");
        var pool = ReadoutStore.Current.Model.PoolByName("Audit textiles");
        string textiles = PoolMembersCodec.Encode(catalog.CountedDefsIn("Textiles"));
        string leathers = PoolMembersCodec.Encode(catalog.CountedDefsIn("Leathers"));
        ReadoutCommands.SetPoolCategoryScopeSelected(pool.Id, "Textiles", textiles, true);
        ReadoutCommands.SetPoolCategoryScopeSelected(pool.Id, "Leathers", leathers, false);
        Check(PoolTriState.CategoryState(pool.Members, "Leathers", catalog) == TriState.Off
            && PoolTriState.IsSelected(pool.Members, "Cloth", catalog), "RD004 clearing Leathers removes inherited coverage but retains Cloth");
        int revision = ReadoutStore.Current.PoolsVersion;
        ReadoutCommands.SetPoolCategoryScopeSelected(pool.Id, "Leathers", leathers, false);
        Check(ReadoutStore.Current.PoolsVersion == revision, "RD004 repeated clear is a revision-preserving no-op");
        ReadoutCommands.CreatePool("Audit nested selection");
        var other = ReadoutStore.Current.Model.PoolByName("Audit nested selection");
        ReadoutCommands.SetPoolCategoryScopeSelected(other.Id, "Leathers", leathers, true);
        ReadoutCommands.SetPoolCategoryScopeSelected(other.Id, "Textiles", textiles, true);
        ReadoutCommands.SetPoolCategoryScopeSelected(other.Id, "Textiles", textiles, false);
        Check(PoolTriState.CategoryState(other.Members, "Textiles", catalog) == TriState.Off, "RD004 clearing Textiles also clears previously selected Leathers");
        CloseWindows(); var editor = new Dialog_ReadoutConfig(); editor.selectedPoolId = pool.Id;
        var mode = AccessTools.Field(typeof(Dialog_ReadoutConfig), "centerMode");
        mode.SetValue(editor, Enum.Parse(mode.FieldType, "ResourcePools")); Find.WindowStack.Add(editor);
    }
    private static void SetupSearch()
    {
        Check(LanguageDatabase.activeLanguage.folderName.StartsWith("German", StringComparison.Ordinal), "RD005 real German language loaded");
        var pool = new ResourcePool { Id = 1, Name = "Leathers", SeedName = "Leathers", Members = new() { "Leather_Plain" } };
        var group = new ReadoutGroup { Id = 1, Name = "Audit Leder", Tiers = new() { new() { "#1" } } };
        ReadoutCommands.ImportAll(ReadoutsXml.Export(new[] { pool }, new[] { group }));
        poolId = ReadoutStore.Current.Model.PoolByName("Leathers").Id;
        EPrimeReadoutsMod.Settings.useVanillaReadout = false;
        EPrimeReadoutsMod.Settings.showSearchFilter = true;
        ReadoutPanel.SearchText = "Leder"; ReadoutPanel.BumpView();
        Find.CameraDriver.JumpToCurrentMapLoc(origin);
    }
}
