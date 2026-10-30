using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using WorkRoles;
using WorkRoles.Core;

namespace RuntimeAuthority;

// Single-use observer and scenario setup. All recipes/jobs use native factories
// and normal pawn scheduling. This assembly is never deployed to player mods.
public sealed class Probe : GameComponent
{
    private int frames, stage, startTick, revokedTick, controlDuration;
    private string mode;
    private Building_WorkTable bench;
    private Pawn pawn;
    private Map map;
    private Role primary, secondary, assignedPrimary;
    private WorkGiverDef giver;
    private Job observed;
    private int observedLoadId;
    private JobDef observedDef;
    private JobDriver observedDriver;
    private WorkGiverDef observedOrigin;
    private Bill_Production bill;
    private RecipeDef recipe;
    private Zone_Fishing fishingZone;
    private int productsBefore;
    private DateTime nextProgressUtc;
    public Probe(Game game) { }
    private static void Record(string value)
    {
        File.AppendAllText(Path.Combine(GenFilePaths.SaveDataFolderPath, "runtime-authority-results.txt"),
            "tick=" + Find.TickManager.TicksGame + " " + value + Environment.NewLine);
        Log.Message("[RuntimeAuthority] " + value);
    }
    private static void Require(bool condition, string text)
    {
        if (!condition) throw new InvalidOperationException(text);
        Record("PASS " + text);
    }
    public override void GameComponentUpdate()
    {
        if (Current.ProgramState != ProgramState.Playing || LongEventHandler.AnyEventNowOrWaiting) return;
        if (stage != 0)
        {
            if (stage >= 1 && stage <= 6 && DateTime.UtcNow >= nextProgressUtc)
            {
                nextProgressUtc = DateTime.UtcNow.AddSeconds(2);
                Record("PROGRESS mode=" + mode + " stage=" + stage
                    + " elapsedTicks=" + (Find.TickManager.TicksGame - startTick)
                    + " speed=" + Find.TickManager.CurTimeSpeed
                    + " rate=" + Find.TickManager.TickRateMultiplier
                    + " paused=" + Find.TickManager.Paused
                    + " forcePaused=" + Find.TickManager.ForcePaused
                    + " current=" + pawn?.CurJob?.def?.defName);
            }
            return;
        }
        if (++frames < 45) return;
        try
        {
            AccessTools.Field(typeof(LudeonTK.EditWindow_Log), "canAutoOpen").SetValue(null, false);
            mode = File.ReadAllText(Path.Combine(GenFilePaths.SaveDataFolderPath, "runtime-authority-mode.txt")).Trim();
            var durationFile = Path.Combine(GenFilePaths.SaveDataFolderPath, "runtime-authority-controlticks.txt");
            if (File.Exists(durationFile)) controlDuration = int.Parse(File.ReadAllText(durationFile));
            if (mode.StartsWith("fish-", StringComparison.Ordinal))
            {
                RoleCommands.SetAutoOptimize(false);
                giver = DefDatabase<WorkGiverDef>.AllDefsListForReading.First(g => g.giverClass == typeof(WorkGiver_Fish));
                pawn = Find.Maps.SelectMany(m => m.mapPawns.FreeColonistsSpawned).First(p =>
                    p.LabelShort == "Takeo" && !p.Downed && !p.Dead && !p.InMentalState
                    && !p.WorkTypeIsDisabled(giver.workType) && !p.WorkTagIsDisabled(giver.workTags));
                BeginFishingSetup();
            }
            else
            {
                SetupBill();
                stage = 1;
                startTick = Find.TickManager.TicksGame;
                Find.TickManager.CurTimeSpeed = TimeSpeed.Ultrafast;
            }
        }
        catch (Exception ex) { Finish("FAILED setup " + ex); }
    }
    public override void GameComponentTick()
    {
        if (stage < 1 || stage > 6) return;
        try
        {
            KeepReady();
            int tick = Find.TickManager.TicksGame;
            if (tick - startTick > 30000) { Finish("FAILED timeout mode=" + mode + " job=" + pawn.CurJob); return; }
            if (stage == 1)
            {
                if (mode == "bill-enabled" || mode == "bill-import-enabled")
                {
                    if (observed == null && pawn.CurJob?.bill == bill
                        && pawn.jobs.curDriver is JobDriver_DoBill control
                        && control.ticksSpentDoingRecipeWork > 5 && control.workLeft > 40)
                    {
                        CaptureObserved();
                        Require(!observed.playerForced && observed.workGiverDef == giver,
                            "WR12 enabled control is actual automatically scheduled native work");
                        if (mode == "bill-import-enabled")
                        {
                            CheckNoOpImport();
                            ApplyImportedWork(clearPrimary: false, clearSecondary: true);
                        }
                        else RoleCommands.SetAssignmentState(pawn, secondary.id, (int)AssignmentState.Disabled);
                        Drain();
                        Require(ObservedJobIsCurrent(),
                            "WR12 revoking Smelter preserves current authorized Stonecut sibling");
                    }
                    if (bill.repeatCount != 0) return;
                    Require(observed != null, "WR12 enabled native work observed before completion");
                    Require(CountProducts() >= productsBefore + recipe.products[0].count,
                        "WR12 enabled automatic bill consumed ingredients and produced native blocks");
                    Record("CONTROL bill ticks=" + (tick - startTick));
                    Finish("SUCCESS " + mode);
                    return;
                }
                if (mode == "bill-forced" || mode == "bill-import-forced")
                {
                    pawn.jobs.StopAll();
                    var forced = ((WorkGiver_Scanner)giver.Worker).JobOnThing(pawn, bench, true);
                    Require(forced != null && pawn.jobs.TryTakeOrderedJobPrioritizedWork(forced, giver.Worker, bench.Position),
                        "WR12 native player order starts bill work");
                    CaptureObserved();
                    Require(observed?.playerForced == true && observed.workGiverDef == giver, "WR12 native player-order metadata retained");
                    if (mode == "bill-import-forced")
                    {
                        ApplyImportedWork(clearPrimary: true, clearSecondary: true);
                        Drain();
                    }
                    else DisableBothRoles();
                    Require(CompiledJobOrders.PriorityFor(pawn, giver.workType) == 0
                        && ObservedJobIsCurrent(), "WR12 player-forced bill survives complete Crafting revocation");
                    stage = 5;
                    return;
                }
                if (pawn.CurJob?.bill != bill || !(pawn.jobs.curDriver is JobDriver_DoBill driver)
                    || driver.ticksSpentDoingRecipeWork <= 5 || driver.workLeft <= 40) return;
                CaptureObserved();
                Require(!observed.playerForced && observed.workGiverDef == giver,
                    "WR12 automatic native bill has exact giver provenance");
                bool imported = mode.StartsWith("bill-import-", StringComparison.Ordinal);
                if (imported)
                {
                    CheckNoOpImport();
                    ApplyImportedWork(clearPrimary: true, clearSecondary: false);
                }
                else RoleCommands.SetAssignmentState(pawn, primary.id, (int)AssignmentState.Disabled);
                Drain();
                Require(!CompiledJobOrders.NormalFor(pawn).Any(g => g.def == giver), "WR12 revoked stonecut giver absent");
                Require(CompiledJobOrders.PriorityFor(pawn, giver.workType) > 0, "WR12 Smelter preserves Crafting authority");
                RecordReconciliation("WR12");
                Require(!ObservedJobIsCurrent() && observedDriver.ended,
                    "WR12 revoked exact giver driver ended and native job identity replaced immediately");
                if (imported) CheckImportedQueueAuthority();
                else CheckQueueAuthority();
                Require(controlDuration > 0, "WR12 enabled native control duration supplied");
                revokedTick = tick;
                stage = 2;
                return;
            }
            if (stage == 2)
            {
                if (bill.repeatCount != 1 || CountProducts() != productsBefore)
                    throw new InvalidOperationException("WR12 revoked bill consumed ingredients or produced blocks");
                if (tick - revokedTick <= controlDuration + 120) return;
                Record("PASS WR12 revoked bill produced no blocks over a complete native control duration");
                Finish("SUCCESS " + mode);
                return;
            }
            if (stage == 5)
            {
                if (bill.repeatCount != 0) return;
                Require(CountProducts() >= productsBefore + recipe.products[0].count,
                    "WR12 player-forced revoked bill completed native production");
                Finish("SUCCESS " + mode);
                return;
            }
            if (stage == 3)
            {
                if (mode == "fish-enabled")
                {
                    if (observed == null && pawn.CurJob?.def == JobDefOf.Fish
                        && pawn.jobs.curDriver.CurToilIndex > 0)
                    {
                        CaptureObserved();
                        Require(!observed.playerForced && observed.workGiverDef == giver,
                            "WR13 enabled non-scan control retains actual native origin");
                    }
                    if (fishingZone.repeatCount != 0) return;
                    Require(observed != null, "WR13 enabled native fishing observed before completion");
                    Require(map.listerThings.ThingsInGroup(ThingRequestGroup.Fish).Sum(t => t.stackCount) > 0,
                        "WR13 enabled automatic fishing caught native fish");
                    Record("CONTROL fish ticks=" + (tick - startTick));
                    Finish("SUCCESS " + mode);
                    return;
                }
                if (mode == "fish-forced")
                {
                    pawn.jobs.StopAll();
                    // Actual native non-scan factory, then the native player
                    // order API. No shortened toil timers or fabricated fish.
                    var forced = giver.Worker.NonScanJob(pawn);
                    Require(forced != null && pawn.jobs.TryTakeOrderedJobPrioritizedWork(forced, giver.Worker, pawn.Position),
                        "WR13 native player-forced fishing starts");
                    CaptureObserved();
                    DisableBothRoles();
                    Require(CompiledJobOrders.PriorityFor(pawn, giver.workType) == 0
                        && observed?.playerForced == true && ObservedJobIsCurrent(),
                        "WR13 player-forced fishing survives complete revocation");
                    stage = 6;
                    return;
                }
                if (pawn.CurJob?.def != JobDefOf.Fish || pawn.jobs.curDriver.ticksLeftThisToil <= 200
                    || pawn.jobs.curDriver.CurToilIndex <= 0) return;
                CaptureObserved();
                Require(!observed.playerForced && observed.workGiverDef == giver,
                    "WR13 automatic non-scan fishing retains actual issuing giver");
                productsBefore = map.listerThings.ThingsInGroup(ThingRequestGroup.Fish).Sum(t => t.stackCount);
                RoleCommands.SetAssignmentState(pawn, primary.id, (int)AssignmentState.Disabled);
                Drain();
                Require(CompiledJobOrders.PriorityFor(pawn, giver.workType) == 0, "WR13 fishing fully revoked");
                RecordReconciliation("WR13");
                Require(!ObservedJobIsCurrent() && observedDriver.ended,
                    "WR13 revoked automatic fishing driver ended and native job identity replaced immediately");
                Require(controlDuration > 0, "WR13 enabled native control duration supplied");
                revokedTick = tick;
                stage = 4;
                return;
            }
            if (stage == 4)
            {
                if (fishingZone.repeatCount != 1
                    || map.listerThings.ThingsInGroup(ThingRequestGroup.Fish).Sum(t => t.stackCount) != productsBefore)
                    throw new InvalidOperationException("WR13 revoked fishing completed or produced fish");
                if (tick - revokedTick <= controlDuration + 120) return;
                Record("PASS WR13 revoked fishing produced no catch over a complete native control duration");
                Finish("SUCCESS " + mode);
                return;
            }
            if (stage == 6 && fishingZone.repeatCount == 0)
            {
                Require(map.listerThings.ThingsInGroup(ThingRequestGroup.Fish).Sum(t => t.stackCount) > productsBefore,
                    "WR13 player-forced fishing completed and caught native fish");
                Finish("SUCCESS " + mode);
            }
        }
        catch (Exception ex) { Finish("FAILED mode=" + mode + " stage=" + stage + " " + ex); }
    }
    private void CaptureObserved()
    {
        observed = pawn.CurJob;
        observedLoadId = observed?.loadID ?? -1;
        observedDef = observed?.def;
        observedOrigin = observed?.workGiverDef;
        observedDriver = pawn.jobs.curDriver;
    }
    private bool ObservedJobIsCurrent() => pawn.CurJob != null
        && pawn.CurJob.loadID == observedLoadId && pawn.CurJob.def == observedDef;
    private void RecordReconciliation(string issue)
    {
        var current = pawn.CurJob;
        Record(issue + " RECONCILE beforeLoadID=" + observedLoadId
            + " beforeDef=" + observedDef?.defName
            + " beforeGiver=" + observedOrigin?.defName
            + " beforeDriver=" + observedDriver?.GetType().FullName
            + " oldDriverEnded=" + observedDriver?.ended
            + " afterLoadID=" + (current?.loadID ?? -1)
            + " afterDef=" + (current?.def?.defName ?? "<null>")
            + " afterGiver=" + (current?.workGiverDef?.defName ?? "<null>")
            + " afterDriver=" + (pawn.jobs.curDriver?.GetType().FullName ?? "<null>")
            + " samePooledReference=" + ReferenceEquals(observed, current));
    }
    private void DisableBothRoles()
    {
        RoleCommands.SetAssignmentState(pawn, primary.id, (int)AssignmentState.Disabled);
        RoleCommands.SetAssignmentState(pawn, secondary.id, (int)AssignmentState.Disabled);
        Drain();
    }
    private void ApplyImportedWork(bool clearPrimary, bool clearSecondary)
    {
        var document = RoleIO.Parse(RoleIO.BuildXml(RoleStore.Current));
        var rows = new List<int>();
        foreach (Role role in new[] { primary, secondary })
        {
            if (role == primary ? !clearPrimary : !clearSecondary) continue;
            int row = document.roles.FindIndex(r => r.label == role.label);
            Require(row >= 0, "WR20 native export contains selected work role");
            document.roles[row].entries.Clear();
            rows.Add(row);
        }
        RoleCommands.ApplyImport(new ImportSelection { xml = RoleFile.Build(document), roles = true, roleRows = rows });
    }
    private void CheckNoOpImport()
    {
        string xml = RoleIO.BuildXml(RoleStore.Current);
        var document = RoleIO.Parse(xml);
        var orders = CompiledJobOrders.NormalFor(pawn);
        int revision = UiVersion.Current;
        RoleCommands.ApplyImport(new ImportSelection
        {
            xml = xml, roles = true,
            roleRows = new List<int> { document.roles.FindIndex(r => r.label == primary.label) },
        });
        Drain();
        Require(UiVersion.Current == revision && ReferenceEquals(orders, CompiledJobOrders.NormalFor(pawn))
            && ObservedJobIsCurrent() && !observedDriver.ended,
            "WR20 no-op import preserves revisions, compiled identity and running native work");
    }
    private void CheckImportedQueueAuthority()
    {
        // Restore permission first, then import its removal. Queue fixtures
        // test the import notification path; the real bill proves consumption.
        RoleCommands.AddEntry(primary.id, new JobEntry(JobEntryKind.WorkGiver, giver.defName));
        Drain();
        var revoked = QueueFixture(giver, false);
        var sibling = QueueFixture(DefDatabase<WorkGiverDef>.GetNamed("DoBillsSmelter"), false);
        var forced = QueueFixture(giver, true);
        var unknown = QueueFixture(null, false);
        ApplyImportedWork(clearPrimary: true, clearSecondary: false);
        Drain();
        Require(!pawn.jobs.jobQueue.Contains(revoked), "WR20 import removes revoked queued work through the normal pending drain");
        Require(pawn.jobs.jobQueue.Count == 3
            && ReferenceEquals(pawn.jobs.jobQueue[0].job, sibling)
            && ReferenceEquals(pawn.jobs.jobQueue[1].job, forced)
            && ReferenceEquals(pawn.jobs.jobQueue[2].job, unknown),
            "WR20 import preserves valid sibling, forced and unknown queued jobs in order");
        pawn.jobs.jobQueue.Clear(pawn, true);
    }
    private void CheckQueueAuthority()
    {
        // JobMaker fixtures exercise only queue authority and cleanup. These
        // short waits never run; separate native bills provide production proof.
        var revoked = QueueFixture(giver, false);
        var sibling = QueueFixture(DefDatabase<WorkGiverDef>.GetNamed("DoBillsSmelter"), false);
        var forced = QueueFixture(giver, true);
        var unknown = QueueFixture(null, false);
        AccessTools.Method(typeof(CompiledJobOrders), "ReconcileInFlightWork").Invoke(null, new object[] { pawn });
        Require(!pawn.jobs.jobQueue.Contains(revoked), "WR12 revoked queued giver removed");
        Require(pawn.jobs.jobQueue.Count == 3
            && ReferenceEquals(pawn.jobs.jobQueue[0].job, sibling)
            && ReferenceEquals(pawn.jobs.jobQueue[1].job, forced)
            && ReferenceEquals(pawn.jobs.jobQueue[2].job, unknown),
            "WR12 valid sibling, forced and unknown queued jobs retain instances and order");
        pawn.jobs.jobQueue.Clear(pawn, true);
    }
    private void KeepReady()
    {
        pawn.needs.food.CurLevelPercentage = 1f;
        pawn.needs.rest.CurLevelPercentage = 1f;
        if (pawn.needs.joy != null) pawn.needs.joy.CurLevelPercentage = 1f;
    }
    private Job QueueFixture(WorkGiverDef origin, bool forced)
    {
        var job = JobMaker.MakeJob(JobDefOf.Wait);
        job.expiryInterval = 1;
        job.workGiverDef = origin;
        job.playerForced = forced;
        pawn.jobs.jobQueue.EnqueueLast(job);
        return job;
    }
    private void Drain() => AccessTools.Method(typeof(CompiledJobOrders), "DrainPendingReconciles").Invoke(null, new object[] { map });
    private Role NewRole(string label, WorkGiverDef workGiver = null)
    {
        RoleCommands.CreateRole(label);
        var role = RoleStore.Current.roles.Single(r => r.label == label);
        if (workGiver != null) RoleCommands.AddEntry(role.id, new JobEntry(JobEntryKind.WorkGiver, workGiver.defName));
        return role;
    }
    private void Assign()
    {
        RoleCommands.PasteRoleSet(pawn, new List<RoleAssignment> { new RoleAssignment { roleId = (assignedPrimary ?? primary).id }, new RoleAssignment { roleId = secondary.id } });
        for (int i = 0; i < 24; i++) pawn.timetable.SetAssignment(i, TimeAssignmentDefOf.Work);
        pawn.drafter.Drafted = false;
        pawn.playerSettings.AreaRestrictionInPawnCurrentMap = null;
        pawn.jobs.StopAll();
        Drain();
        KeepReady();
    }
    private int CountProducts() => map.listerThings.ThingsOfDef(recipe.products[0].thingDef).Sum(t => t.stackCount)
        + (pawn.carryTracker.CarriedThing?.def == recipe.products[0].thingDef ? pawn.carryTracker.CarriedThing.stackCount : 0);
    private void SetupBill()
    {
        RoleCommands.SetAutoOptimize(false);
        map = Find.Maps.First(m => m.mapPawns.FreeColonistsSpawned.Any(p => !p.Downed && !p.Dead && !p.InMentalState && p.ageTracker.AgeBiologicalYears >= 18));
        // Exact installed Core defs: Various_Stone.xml declares the concrete
        // Make_StoneBlocksSandstone recipe; MakeStoneBlocksBase is abstract.
        var benchDef = DefDatabase<ThingDef>.GetNamed("TableStonecutter");
        recipe = benchDef.AllRecipes.Single(r => r.defName == "Make_StoneBlocksSandstone");
        bench = (Building_WorkTable)ThingMaker.MakeThing(benchDef, benchDef.MadeFromStuff ? ThingDefOf.WoodLog : null);
        var position = map.AllCells.First(c => !c.Fogged(map) && c.Standable(map) && c.GetEdifice(map) == null
            && GenAdj.OccupiedRect(c, Rot4.North, benchDef.size).ExpandedBy(2).Cells.All(v => v.InBounds(map) && v.Standable(map) && v.GetEdifice(map) == null));
        GenSpawn.Spawn(bench, position, map); bench.SetFaction(Faction.OfPlayer);
        giver = DefDatabase<WorkGiverDef>.AllDefsListForReading.First(g => typeof(WorkGiver_DoBill).IsAssignableFrom(g.giverClass)
            && ((WorkGiver_DoBill)g.Worker).ThingIsUsableBillGiver(bench));
        var other = DefDatabase<WorkGiverDef>.GetNamed("DoBillsSmelter");
        Require(other.workType == giver.workType, "native Stonecut and Smelter givers share Crafting");
        var fishGiver = DefDatabase<WorkGiverDef>.AllDefsListForReading.First(g => g.giverClass == typeof(WorkGiver_Fish));
        pawn = map.mapPawns.FreeColonistsSpawned.First(p => !p.Downed && !p.Dead && !p.InMentalState && p.ageTracker.AgeBiologicalYears >= 18 && !p.WorkTypeIsDisabled(giver.workType) && !p.WorkTagIsDisabled(giver.workTags)
            && !p.WorkTypeIsDisabled(fishGiver.workType) && !p.WorkTagIsDisabled(fishGiver.workTags));
        bill = (Bill_Production)BillUtility.MakeNewBill(recipe);
        bill.repeatMode = BillRepeatModeDefOf.RepeatCount; bill.repeatCount = 1;
        bill.SetPawnRestriction(pawn); bill.SetStoreMode(BillStoreModeDefOf.DropOnFloor);
        bench.BillStack.AddBill(bill);
        foreach (var ingredient in recipe.ingredients)
        {
            var def = ingredient.filter.AllowedThingDefs.First(d => recipe.fixedIngredientFilter.Allows(d));
            int count = ingredient.CountRequiredOfFor(def, recipe, bill);
            for (int i = 0; i < count; i++)
            {
                var thing = ThingMaker.MakeThing(def); thing.stackCount = 1;
                GenPlace.TryPlaceThing(thing, bench.InteractionCell, map, ThingPlaceMode.Near); thing.SetForbidden(false);
            }
        }
        primary = NewRole("Review Runtime Stonecut", giver); secondary = NewRole("Review Runtime Other Craft", other);
        if (mode == "bill-import-composite")
        {
            assignedPrimary = NewRole("Review Runtime Combined Stonecut");
            RoleCommands.SetRoleComposite(assignedPrimary.id, true);
            RoleCommands.AddCompositeMember(assignedPrimary.id, primary.id);
        }
        MoveTo(bench.InteractionCell);
        Assign(); productsBefore = CountProducts();
        Record("T1 SETUP pawn=" + pawn.LabelShort + " bench=" + bench.def.defName + " recipe=" + recipe.defName + " giver=" + giver.defName + " secondary=" + other.defName + " map=" + map.uniqueID);
    }
    private void MoveTo(IntVec3 cell)
    {
        pawn.jobs.StopAll(); pawn.DeSpawn(); GenSpawn.Spawn(pawn, cell, map);
    }
    private void BeginFishingSetup()
    {
        stage = 7;
        Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
        LongEventHandler.QueueLongEvent(() =>
        {
            try
            {
                SetupFishing(); stage = 3; startTick = Find.TickManager.TicksGame;
                Find.TickManager.CurTimeSpeed = TimeSpeed.Ultrafast;
            }
            catch (Exception ex) { Finish("FISHING FAILED setup " + ex); }
        }, "GeneratingMap", false, ex => Finish("FAILED fishing map " + ex));
    }
    private static bool Shore(Map candidate, IntVec3 c) => c.GetTerrain(candidate).IsWater && c.GetWaterBodyType(candidate) != WaterBodyType.None
        && candidate.waterBodyTracker.AnyFishPopulationAt(c) && GenAdj.CardinalDirections.Any(d => (c + d).InBounds(candidate)
            && (c + d).Standable(candidate) && !(c + d).GetTerrain(candidate).IsWater);
    private void SetupFishing()
    {
        pawn.jobs.StopAll();
        giver = DefDatabase<WorkGiverDef>.AllDefsListForReading.First(g => g.giverClass == typeof(WorkGiver_Fish));
        Require(!pawn.WorkTypeIsDisabled(giver.workType), "T2 test pawn capable of fishing");
        Map existing = Find.Maps.FirstOrDefault(m => m.waterBodyTracker.AnyBodyContainsFish && m.AllCells.Any(c => Shore(m, c)));
        if (existing != null) map = existing;
        else
        {
            // The source save has one orbital map and zero native waterBodies.
            // Generate ordinary native terrain on a real warm surface river
            // tile. No terrain/fish populations or job results are fabricated.
            var grid = Find.WorldGrid;
            PlanetTile tile = Enumerable.Range(0, grid.TilesCount).Select(i => new PlanetTile(i, grid.Surface))
                .First(t => grid[t] is SurfaceTile s && !s.WaterCovered && s.temperature > 25f
                    && s.MaxFishPopulation > 0f && s.Rivers != null && s.Rivers.Count > 0 && Find.WorldObjects.MapParentAt(t) == null);
            var parent = (MapParent)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.Settlement);
            parent.Tile = tile; parent.SetFaction(Faction.OfPlayer); Find.WorldObjects.Add(parent);
            map = MapGenerator.GenerateMap(new IntVec3(150, 1, 150), parent, MapGeneratorDefOf.Base_Player);
            Record("T2 generated ordinary native river map tile=" + tile + " biome=" + map.Biome.defName + " nativeWaterBodies=" + map.waterBodyTracker.Bodies.Count);
        }
        Current.Game.CurrentMap = map;
        map.fogGrid.ClearAllFog();
        IntVec3 water = map.AllCells.First(c => Shore(map, c));
        var oldZone = water.GetZone(map); if (oldZone != null) oldZone.RemoveCell(water);
        fishingZone = new Zone_Fishing(map.zoneManager) { repeatMode = FishRepeatMode.RepeatCount, repeatCount = 1, targetPopulationPct = 0f };
        map.zoneManager.RegisterZone(fishingZone); fishingZone.AddCell(water);
        Require(fishingZone.ShouldFishNow && fishingZone.HasAnyFishableCells, "T2 real native fishing zone fishable with population; water=" + water);
        primary = NewRole("Review Runtime Fisher", giver); secondary = NewRole("Review Runtime Keeper");
        MoveTo(GenAdj.CardinalDirections.Select(d => water + d).First(c => c.InBounds(map) && c.Standable(map) && !c.GetTerrain(map).IsWater));
        Assign(); Record("T2 SETUP fishing zone, auto scheduling, native Fish job only; pawn=" + pawn.LabelShort);
    }
    private void Finish(string text)
    {
        Record(text); stage = 9; Find.TickManager.CurTimeSpeed = TimeSpeed.Paused; Record("DONE");
    }
}
