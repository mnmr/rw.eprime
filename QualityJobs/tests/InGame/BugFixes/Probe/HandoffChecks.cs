using System;
using System.Collections.Generic;
using HarmonyLib;
using QualityJobs;
using QualityJobs.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace QualityJobsBugReview
{
    // Review-only probe. The staged case lets native DoBill toils create and
    // consume the ingredients/UFTs. Only the game's own fastCrafting switch
    // accelerates work; it never marks an item debug-complete.
    public static class HandoffChecks
    {
        private static Map map;
        private static QualityJobsStore store;
        private static Pawn worker;
        private static Building_WorkTable bench;
        private static RecipeDef recipe;
        private static WorkGiver_DoBill giver;
        private static WorkTypeDef workType;
        private static ThingDef previousMaterial, nextMaterial;
        private static Bill_ProductionWithUft bill;
        private static readonly List<Thing> fixtures = new List<Thing>();
        private static readonly Dictionary<Pawn, int> priorities = new Dictionary<Pawn, int>();
        private static readonly HashSet<int> baselineProducts = new HashSet<int>();
        private static bool originalSharing, originalFastCrafting, originalPriority;
        private static bool capWasSpecific;
        private static int originalCap, stage, startedTick, lastOrderedTick = -1;
        private static int preparedCount;
        private static int rawBeforePreparation, rawAfterPreparation, batchSize;
        private static bool suspensionRecorded;
        private static bool noJobRecorded;
        private static DateTime startedUtc;
        private static string lastJobFailure;

        public static void Run(Map targetMap, QualityJobsStore targetStore)
        {
            map = targetMap;
            store = targetStore;
            recipe = DefDatabase<RecipeDef>.GetNamed("Make_Apparel_Tuque");
            previousMaterial = ThingDefOf.Cloth;
            nextMaterial = DefDatabase<ThingDef>.GetNamed("DevilstrandCloth");
            ThingDef benchDef = DefDatabase<ThingDef>.GetNamed("HandTailoringBench");
            foreach (WorkGiverDef def in DefDatabase<WorkGiverDef>.AllDefsListForReading)
                if (def.Worker is WorkGiver_DoBill candidate && def.fixedBillGiverDefs != null
                    && def.fixedBillGiverDefs.Contains(benchDef))
                { giver = candidate; workType = def.workType; break; }
            if (giver == null) throw new InvalidOperationException("No sculpting work giver.");
            foreach (Pawn candidate in map.mapPawns.FreeColonistsSpawned)
                if (!candidate.Downed && !candidate.InMentalState && !candidate.WorkTypeIsDisabled(workType)
                    && candidate.skills != null && candidate.workSettings != null)
                { worker = candidate; break; }
            if (worker == null) throw new InvalidOperationException("No capable sculpture worker.");

            originalSharing = store.shareUnfinishedWork;
            originalFastCrafting = DebugSettings.fastCrafting;
            originalPriority = store.highPriorityFinish;
            capWasSpecific = store.productCaps.TryGetValue(recipe.ProducedThingDef.defName, out originalCap);
            foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned)
                if (pawn.workSettings != null && !pawn.WorkTypeIsDisabled(workType))
                {
                    priorities[pawn] = pawn.workSettings.GetPriority(workType);
                    pawn.workSettings.SetPriority(workType, 0);
                }
            Commands.SetShareUnfinishedWork(true);
            Commands.SetProductCap(recipe.ProducedThingDef.defName,
                store.SpawnedUftCount(map, recipe.ProducedThingDef.defName) + 3);
            Commands.SetHighPriorityFinish(true);

            IntVec3 position = FindSpace(worker.Position, benchDef.size);
            bench = (Building_WorkTable)ThingMaker.MakeThing(benchDef, ThingDefOf.WoodLog);
            bench.SetFaction(Faction.OfPlayer);
            GenSpawn.Spawn(bench, position, map, Rot4.North);
            fixtures.Add(bench);
            worker = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,
                Faction.OfPlayer, forceGenerateNewPawn: true, canGeneratePawnRelations: false,
                fixedBiologicalAge: 30f, forceNoIdeo: true, forceNoBackstory: true, forbidAnyTitle: true));
            GenSpawn.Spawn(worker, FreeCell(position + new IntVec3(4, 0, -2)), map);
            fixtures.Add(worker);
            if (worker.WorkTypeIsDisabled(workType))
                throw new InvalidOperationException("Generated review worker cannot sculpt.");
            worker.workSettings.SetPriority(workType, 1);
            worker.skills.GetSkill(recipe.workSkill).Level = 8;
            if (worker.needs.food != null) worker.needs.food.CurLevel = worker.needs.food.MaxLevel;
            if (worker.needs.rest != null) worker.needs.rest.CurLevel = worker.needs.rest.MaxLevel;
            foreach (string name in new[] { "Apparel_Vacsuit", "Apparel_VacsuitHelmet" })
            {
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(name);
                if (def != null) worker.apparel.Wear((Apparel)ThingMaker.MakeThing(def));
            }
            bill = (Bill_ProductionWithUft)recipe.MakeNewBill();
            bench.BillStack.AddBill(bill);
            bill.repeatMode = BillRepeatModeDefOf.RepeatCount;
            bill.repeatCount = 1;
            bill.SetPawnRestriction(worker);
            bill.SetStoreMode(BillStoreModeDefOf.DropOnFloor);
            bill.ingredientSearchRadius = 12f;
            bill.ingredientFilter.SetDisallowAll();
            bill.ingredientFilter.SetAllow(previousMaterial, true);
            Configure(false, 0, false);

            // The native UFT construction used by MakeUnfinishedThingIfNeeded.
            var uft = (UnfinishedThing)ThingMaker.MakeThing(recipe.unfinishedThingDef,
                recipe.unfinishedThingDef.MadeFromStuff ? previousMaterial : null);
            uft.Creator = worker;
            uft.BoundBill = bill;
            Thing wood = ThingMaker.MakeThing(previousMaterial);
            wood.stackCount = recipe.ingredients[0].CountRequiredOfFor(previousMaterial, recipe, bill);
            uft.ingredients.Add(wood);
            uft.workLeft = bill.GetWorkAmount(uft) / 2f;
            GenSpawn.Spawn(uft, FreeCell(position + new IntVec3(4, 0, 0)), map);
            fixtures.Add(uft);
            AccessTools.Method(typeof(QualityJobsStore), "PoolIdleUnfinishedWork").Invoke(store, null);
            WorkItemEntry pooled = store.FindByUft(uft);
            if (pooled == null || pooled.state != WorkItemState.Shared || bill.BoundUft != null)
                throw new InvalidOperationException("Native pooling did not adopt fixture.");

            // Player changes the bill from wood to steel after the work is idle.
            bill.ingredientFilter.SetDisallowAll();
            bill.ingredientFilter.SetAllow(nextMaterial, true);
            SpawnSteel(4 * recipe.ingredients[0].CountRequiredOfFor(nextMaterial, recipe, bill));
            map.regionAndRoomUpdater.TryRebuildDirtyRegionsAndRooms();
            Job blocked = JobForBill();
            string blockedReason = lastJobFailure;
            store.RemoveEntry(pooled);
            Job control = JobForBill();
            bool startsSteel = control != null && control.bill == bill
                && control.targetQueueB != null && control.targetQueueB.Count > 0
                && control.targetQueueB[0].Thing != uft;
            Probe.Record("QJ005", blocked != null && blocked.bill == bill
                && blocked.targetQueueB != null && blocked.targetQueueB[0].Thing.def == nextMaterial
                && startsSteel,
                "Cloth UFT pooled; devilstrand-only source ShouldDoNow=" + bill.ShouldDoNow()
                + "; pooled job=" + Describe(blocked) + "; identical state after pool-entry release="
                + Describe(control) + "; recipe=" + recipe.defName
                + "; blockedReason=" + blockedReason + "; controlReason=" + lastJobFailure
                + "; admission=" + Admission());
            if (blocked != null) JobMaker.ReturnToPool(blocked);
            if (control != null) JobMaker.ReturnToPool(control);
            // A cancelled dispatched UFT leaves a live one-shot bill until the
            // sweep; probe its *native new-start admission* before that sweep.
            Configure(true, 0, false);
            uft.workLeft = 0f;
            store.RegisterPaused(uft, worker, bill, StyleSnapshot.From(bill));
            UftAuthor.Clear(uft);
            WorkItemEntry cancelling = store.FindByUft(uft);
            Dispatcher.TryDispatch(store, cancelling);
            Bill_ProductionWithUft finishBill = cancelling.finishBill;
            bill.suspended = true;
            Job beforeCancel = JobForBill();
            uft.Destroy(DestroyMode.Cancel);
            map.regionAndRoomUpdater.TryRebuildDirtyRegionsAndRooms();
            Job afterCancel = JobForBill();
            bool newStartAfterCancel = finishBill != null && afterCancel != null
                && afterCancel.bill == finishBill && afterCancel.targetQueueB != null
                && afterCancel.targetQueueB.Count > 0
                && afterCancel.targetQueueB[0].Thing is not UnfinishedThing;
            Probe.Record("QJ006", beforeCancel?.bill == finishBill && afterCancel == null
                && finishBill != null && finishBill.DeletedOrDereferenced && !newStartAfterCancel,
                "Cancelled dispatched native UFT; before=" + Describe(beforeCancel)
                + "; after=" + Describe(afterCancel) + "; temporary bill live="
                + (finishBill != null && !finishBill.DeletedOrDereferenced)
                + "; source suspended=" + bill.suspended + "; failReason=" + lastJobFailure);
            if (beforeCancel != null) JobMaker.ReturnToPool(beforeCancel);
            if (afterCancel != null) JobMaker.ReturnToPool(afterCancel);
            if (finishBill != null && !finishBill.DeletedOrDereferenced)
                finishBill.billStack.Delete(finishBill);
            store.RemoveEntry(cancelling);
            bill.suspended = false;

            // Begin two real craft attempts on a Do-1 source. An unattainable
            // skill threshold reserves each final touch for later finishing.
            // The selected pawn is the only art-enabled worker in this fixture.
            Configure(true, 20, true);
            batchSize = recipe.ingredients[0].CountRequiredOfFor(nextMaterial, recipe, bill);
            rawBeforePreparation = CountRawMaterial();
            foreach (Thing product in map.listerThings.AllThings)
                if (product.GetInnerIfMinified().def == recipe.ProducedThingDef)
                    baselineProducts.Add(product.thingIDNumber);
            worker.jobs.EndCurrentJob(JobCondition.InterruptForced);
            worker.Position = FreeCell(bench.InteractionCell + new IntVec3(0, 0, -1));
            DebugSettings.fastCrafting = true;
            startedTick = Find.TickManager.TicksGame;
            startedUtc = DateTime.UtcNow;
            stage = 1;
            Find.TickManager.CurTimeSpeed = TimeSpeed.Ultrafast;
            OrderNextJob();
        }

        public static bool Update()
        {
            if (stage == 0 || stage == 99) return true;
            if (Find.TickManager.CurTimeSpeed == TimeSpeed.Superfast)
                Find.TickManager.CurTimeSpeed = TimeSpeed.Ultrafast;
            int tick = Find.TickManager.TicksGame;
            if (tick - startedTick > 6000 || (DateTime.UtcNow - startedUtc).TotalSeconds > 45)
            {
                Probe.Record("HAND-RUNTIME-TIMEOUT", false,
                    "stage=" + stage + "; prepared=" + preparedCount + "; job=" + worker.CurJob
                    + "; entries=" + CountEntries() + "; products=" + CountNewProducts());
                Cleanup();
                return true;
            }
            if (stage == 1)
            {
                preparedCount = CountEntries();
                if (preparedCount >= 1)
                {
                    rawAfterPreparation = CountRawMaterial();
                    Job extra = JobForBill();
                    Probe.Record("QJ007-ADMISSION", preparedCount == 1 && bill.repeatCount == 1
                        && extra == null && rawBeforePreparation - rawAfterPreparation == batchSize,
                        "Do 1 committed one full ingredient batch at preparation; raw before="
                        + rawBeforePreparation + "; after=" + rawAfterPreparation
                        + "; batch=" + batchSize + "; additional job=" + Describe(extra));
                    if (extra != null) JobMaker.ReturnToPool(extra);
                    // Suspend before any finisher exists, then relax its gate.
                    bill.suspended = true;
                    worker.jobs.EndCurrentJob(JobCondition.InterruptForced);
                    worker.skills.GetSkill(recipe.workSkill).Level = 20;
                    Configure(true, 0, false);
                    stage = 2;
                    Probe.Record("QJ007-PREPARED", bill.repeatCount == 1 && preparedCount == 1,
                        "Do-1 source naturally prepared " + preparedCount
                        + " distinct paused UFTs; remaining count=" + bill.repeatCount
                        + "; all native crafting, fastCrafting=true, debugCompleted=false.");
                }
                else if (worker.CurJob == null || worker.CurJob.def != JobDefOf.DoBill)
                    OrderNextJob();
            }
            if (stage == 2)
            {
                for (int i = 0; i < store.entries.Count; i++)
                {
                    WorkItemEntry entry = store.entries[i];
                    if (entry.sourceBill != bill) continue;
                    if (entry.state == WorkItemState.Paused) Dispatcher.TryDispatch(store, entry);
                    if (!suspensionRecorded && entry.state == WorkItemState.Dispatched)
                    {
                        Job finish = giver.JobOnThing(worker, bench, true);
                        bool accepted = finish != null && finish.bill == entry.finishBill;
                        Log.Message("[QualityJobsBugReview] Suspended source finishing admission=" + Describe(finish));
                        if (finish != null) JobMaker.ReturnToPool(finish);
                        suspensionRecorded = true;
                    }
                }
                if (CountNewProducts() >= 1)
                {
                    Probe.Record("QJ007", bill.repeatCount == 0 && CountNewProducts() == 1
                        && rawAfterPreparation == CountRawMaterial(),
                        "One requested tuque produced " + CountNewProducts()
                        + " native finished products from " + preparedCount
                        + " naturally prepared UFTs; remaining source count=" + bill.repeatCount
                        + "; raw after completion=" + CountRawMaterial()
                        + "; finishing consumed no additional batch; source remained suspended.");
                    Cleanup();
                    return true;
                }
                if (worker.CurJob == null || worker.CurJob.def != JobDefOf.DoBill)
                    OrderNextJob();
            }
            return false;
        }

        private static void Configure(bool managed, int skill, bool inspired)
        {
            string id = BillIds.IdOf(bill);
            Commands.SetBillManaged(id, managed);
            Commands.SetBillAutoBest(id, false);
            Commands.SetBillMinSkill(id, skill);
            Commands.SetBillRequireInspired(id, inspired);
            Commands.SetBillRequireSpecialist(id, false);
            Commands.SetBillTargetQuality(id, 0);
        }

        private static Job JobForBill()
        {
            bill.nextTickToSearchForIngredients = -1;
            Pawn previous = FloatMenuMakerMap.makingFor;
            FloatMenuMakerMap.makingFor = worker;
            JobFailReason.Clear();
            try
            {
                Job result = giver.JobOnThing(worker, bench, true);
                lastJobFailure = JobFailReason.Reason;
                return result;
            }
            finally { FloatMenuMakerMap.makingFor = previous; }
        }

        private static void OrderNextJob()
        {
            int tick = Find.TickManager.TicksGame;
            if (lastOrderedTick == tick) return;
            lastOrderedTick = tick;
            Job job = JobForBill();
            if (job != null) worker.jobs.TryTakeOrderedJob(job);
            else if (!noJobRecorded)
            {
                noJobRecorded = true;
                Log.Message("[QualityJobsBugReview] " +
                    "first failed native job; reason=" + lastJobFailure + "; " + Admission());
            }
        }

        private static int CountRawMaterial()
        {
            int count = 0;
            foreach (Thing thing in fixtures)
                if (thing.def == nextMaterial && !thing.Destroyed && thing.Spawned)
                    count += thing.stackCount;
            return count;
        }

        private static int CountEntries()
        {
            int count = 0;
            for (int i = 0; i < store.entries.Count; i++)
                if (store.entries[i].sourceBill == bill && store.entries[i].state == WorkItemState.Paused)
                    count++;
            return count;
        }

        private static int CountNewProducts()
        {
            int count = 0;
            foreach (Thing product in map.listerThings.AllThings)
                if (!baselineProducts.Contains(product.thingIDNumber)
                    && product.GetInnerIfMinified().def == recipe.ProducedThingDef)
                    count += product.stackCount;
            return count;
        }

        private static string Admission()
            => "giver=" + giver.def.defName + "; worker=" + worker
                + "; workerCell=" + worker.Position + "; benchCell=" + bench.Position
                + "; interaction=" + bench.InteractionCell
                + "; rootRegion=" + (bench.InteractionCell.GetRegion(map) != null)
                + "; usable=" + bench.CurrentlyUsableForBills()
                + "; reserve=" + worker.CanReserve(bench, 1, -1, null, true)
                + "; seat=" + worker.CanReserveSittableOrSpot(bench.InteractionCell, bench, true)
                + "; pawnAllowed=" + bill.PawnAllowedToStartAnew(worker)
                + "; skill=" + recipe.PawnSatisfiesSkillRequirements(worker)
                + "; shouldDo=" + bill.ShouldDoNow()
                + "; nextMaterialAllowed=" + bill.IsFixedOrAllowedIngredient(nextMaterial)
                + "; recipeNextMaterial=" + recipe.ingredients[0].filter.Allows(nextMaterial)
                + "; capCount=" + store.SpawnedUftCount(map, recipe.ProducedThingDef.defName)
                + "; cap=" + store.CapFor(recipe.ProducedThingDef.defName);

        private static string Describe(Job job)
            => job == null ? "null" : job.def.defName + "/bill=" + job.bill
                + "/ingredient=" + (job.targetQueueB != null && job.targetQueueB.Count > 0
                    ? job.targetQueueB[0].Thing.ToString() : "none");

        private static void SpawnSteel(int count)
        {
            while (count > 0)
            {
                Thing steel = ThingMaker.MakeThing(nextMaterial);
                steel.stackCount = Math.Min(count, steel.def.stackLimit);
                count -= steel.stackCount;
                GenSpawn.Spawn(steel, FreeCell(bench.Position + new IntVec3(4, 0, 2)), map);
                fixtures.Add(steel);
            }
        }

        private static IntVec3 FreeCell(IntVec3 near)
        {
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(near, 12f, true))
                if (cell.InBounds(map) && cell.Standable(map) && !cell.Fogged(map)
                    && cell.GetThingList(map).Count == 0) return cell;
            throw new InvalidOperationException("No empty fixture cell.");
        }

        private static IntVec3 FindSpace(IntVec3 near, IntVec2 size)
        {
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(near, 45f, true))
            {
                bool valid = true;
                foreach (IntVec3 test in GenAdj.OccupiedRect(cell, Rot4.North, size).ExpandedBy(3))
                    if (!test.InBounds(map) || !test.Standable(map) || test.Fogged(map)
                        || test.GetEdifice(map) != null) { valid = false; break; }
                if (valid) return cell;
            }
            throw new InvalidOperationException("No sculpture fixture space.");
        }

        public static void Cleanup()
        {
            stage = 99;
            if (worker?.CurJob?.bill != null && worker.CurJob.targetA.Thing == bench)
                worker.jobs.EndCurrentJob(JobCondition.InterruptForced);
            if (store != null && bill != null)
                for (int i = store.entries.Count - 1; i >= 0; i--)
                    if (store.entries[i].sourceBill == bill)
                    {
                        WorkItemEntry entry = store.entries[i];
                        if (entry.finishBill != null && !entry.finishBill.DeletedOrDereferenced)
                            entry.finishBill.billStack.Delete(entry.finishBill);
                        if (entry.uft != null && !entry.uft.Destroyed) entry.uft.Destroy();
                        store.RemoveEntry(entry);
                    }
            for (int i = fixtures.Count - 1; i >= 0; i--)
                if (!fixtures[i].Destroyed) fixtures[i].Destroy();
            foreach (KeyValuePair<Pawn, int> pair in priorities)
                pair.Key.workSettings.SetPriority(workType, pair.Value);
            DebugSettings.fastCrafting = originalFastCrafting;
            Commands.SetShareUnfinishedWork(originalSharing);
            Commands.SetHighPriorityFinish(originalPriority);
            if (capWasSpecific) Commands.SetProductCap(recipe.ProducedThingDef.defName, originalCap);
            else store.productCaps.Remove(recipe.ProducedThingDef.defName);
        }
    }
}
