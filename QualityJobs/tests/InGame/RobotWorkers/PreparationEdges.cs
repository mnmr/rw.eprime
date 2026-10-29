using System;
using HarmonyLib;
using QualityJobs;
using RimWorld;
using Verse;
using Verse.AI;

namespace RobotWorkersVerification
{
    public static partial class Probe
    {
        private static int pruneCalls;
        private static void CountPrune() => pruneCalls++;
        private static bool Uses(Job job, UnfinishedThing uft) => job?.targetB.Thing == uft
            || (job?.targetQueueB != null && job.targetQueueB.Count == 1 && job.targetQueueB[0].Thing == uft);

        private static void VerifyMechFlag()
        {
            bool allowed = billGiver.def.canBeDoneByMechs;
            try
            {
                billGiver.def.canBeDoneByMechs = false;
                Job job = BillJob(robot);
                if (misc) Check(job != null, scenario + " underlying bill scanner can offer this recipe with vanilla mech flag off");
                if (job != null) JobMaker.ReturnToPool(job);
                bool capable = Dispatcher.FactsFor(robot, recipe, Dispatcher.WorkTypeForRecipe(recipe)).WorkTypeEnabled;
                Check(capable == misc, scenario + " Misc robot uses its own capability while vanilla mech respects canBeDoneByMechs");
            }
            finally { billGiver.def.canBeDoneByMechs = allowed; }
        }

        private static void VerifyPreparationEdges()
        {
            string id = BillIds.IdOf(sourceBill);
            Commands.SetBillManaged(id, false); SetPreparation(false);
            Check(robot.CurJob?.def == JobDefOf.DoBill,
                "unmanaged robot preparation remains allowed while setting is OFF");
            Commands.ManageBillFromApi(id, 0);
            Check(robot.CurJob?.def != JobDefOf.DoBill,
                "API turning the active source bill managed stops robotic preparation while OFF");
            robot.jobs.EndCurrentJob(JobCondition.InterruptForced);
            SetPreparation(true); Commands.SetBillManaged(id, true);
            prepared.BoundBill = sourceBill;
            bool pooled = (bool)AccessTools.Method(typeof(QualityJobsStore), "TryPool")
                .Invoke(store, new object[] { map, prepared });
            Require(pooled && store.IsShared(prepared), "partial item enters actual sharing pool");

            var diagnostics = new Harmony("eprime.qualityjobs.robotworkersprobe.prunecounter");
            var prune = AccessTools.Method(typeof(QualityJobsStore), "PrunePreparationSources");
            diagnostics.Patch(prune, prefix: new HarmonyMethod(typeof(Probe), nameof(CountPrune)));
            try
            {
                pruneCalls = 0;
                for (int i = 0; i < 100; i++) bench.BillStack.RemoveIncompletableBills();
                Check(pruneCalls == 0, "100 unchanged bill scans perform no preparation-provenance pruning");
            }
            finally { diagnostics.Unpatch(prune, HarmonyPatchType.All, diagnostics.Id); }

            var destination = (Building_WorkTable)ThingMaker.MakeThing(bench.def, ThingDefOf.WoodLog);
            destination.SetFactionDirect(Faction.OfPlayer);
            GenSpawn.Spawn(destination, origin + new IntVec3(4, 0, 3), map, Rot4.South, WipeMode.FullRefund);
            var bill = (Bill_ProductionWithUft)BillUtility.MakeNewBill(recipe);
            bill.ingredientFilter.CopyAllowancesFrom(sourceBill.ingredientFilter);
            destination.BillStack.AddBill(bill);
            Commands.SetBillManaged(BillIds.IdOf(bill), false);
            SetPreparation(false);
            bench.BillStack.Clear();
            bill.nextTickToSearchForIngredients = 0;
            Job afterClear = billGiver.JobOnThing(robot, destination, true);
            Check(Uses(afterClear, prepared), "cleared source stack cannot preserve a stale managed-source restriction");
            if (afterClear != null) JobMaker.ReturnToPool(afterClear);
            bench.BillStack.AddBill(sourceBill);
            SetPreparation(true);
            bill.nextTickToSearchForIngredients = 0;
            Job resume = billGiver.JobOnThing(robot, destination, true);
            Require(Uses(resume, prepared), "unmanaged destination resumes actual shared managed-source item while ON");
            robot.jobs.TryTakeOrderedJob(resume);
            Check(!store.IsShared(prepared), "real job admission consumes the shared entry");
            SetPreparation(false);
            Check(robot.CurJob?.def != JobDefOf.DoBill,
                "OFF after shared handover still stops preparation through unmanaged destination");
            bill.nextTickToSearchForIngredients = 0;
            Job blocked = billGiver.JobOnThing(robot, destination, true);
            Check(!Uses(blocked, prepared), "OFF cannot restart the managed-source item through unmanaged destination");
            if (blocked != null) JobMaker.ReturnToPool(blocked);
            robot.jobs.EndCurrentJob(JobCondition.InterruptForced);
            if (!System.IO.File.Exists(System.IO.Path.Combine(Root, "robot-workers-edges-only.txt"))
                && !System.IO.File.Exists(System.IO.Path.Combine(Root, "robot-workers-workroles.txt")))
            {
                SetPreparation(true);
                bill.nextTickToSearchForIngredients = 0;
                Job savedResume = billGiver.JobOnThing(robot, destination, true);
                Require(Uses(savedResume, prepared), "shared-source preparation can resume for full save/reload verification");
                robot.jobs.TryTakeOrderedJob(savedResume);
                BeginProvenanceReload(destination, bill);
                return;
            }
            prepared.BoundBill = null; destination.Destroy(); SetPreparation(true);
        }
    }
}
