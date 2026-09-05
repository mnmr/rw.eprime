using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace QualityJobs.Patches
{
    /// High-priority finish (store.highPriorityFinish): a dispatched finisher
    /// takes the finish job before JobGiver_Work walks the work-giver list, so
    /// neither vanilla work-type priorities nor role orders from other mods can
    /// push it behind other work. Running inside JobGiver_Work keeps needs,
    /// emergencies, player-prioritized work (the emergency pass) and the
    /// timetable ahead of it. Care work (Doctor, Patient, Patient bed rest) is
    /// walked first from the pawn's own giver list, so rescue, tending and bed
    /// rest also stay ahead; everything else on the list comes after. The care
    /// walk runs only once a finish job is actually available for this pawn.
    /// Common path: one store lookup and one integer read. Off (mode B) leaves
    /// vanilla untouched and the scanner path active.
    [HarmonyPatch(typeof(JobGiver_Work), nameof(JobGiver_Work.TryIssueJobPackage))]
    public static class Patch_FinisherPriority
    {
        public static bool Prefix(JobGiver_Work __instance, Pawn pawn, ref ThinkResult __result)
        {
            if (__instance.emergency) return true;
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null || !store.highPriorityFinish || !store.HasDispatchedFinishers)
                return true;
            if (pawn.workSettings == null) return true;
            // Vanilla's own early exit (JobGiver_Work.cs:51); let it answer.
            if (pawn.RaceProps.Humanlike && pawn.health.hediffSet.InLabor()) return true;

            Job? finish = null;
            WorkGiverDef? finishDef = null;
            WorkGiverDef[] givers = FinishWorkGivers.All;
            for (int i = 0; i < givers.Length; i++)
            {
                WorkGiverDef def = givers[i];
                if (!(def.Worker is WorkGiver_FinishQualityWork giver)) continue;
                finish = giver.TryIssueDirectly(store, pawn);
                if (finish == null) continue;
                finishDef = def;
                break;
            }
            if (finish == null || finishDef == null) return true;

            if (CareWork.TryIssue(pawn, out Job careJob, out WorkGiverDef careDef))
            {
                JobMaker.ReturnToPool(finish);
                __result = new ThinkResult(careJob, __instance, careDef.tagToGive);
                return false;
            }

            finish.workGiverDef = finishDef;
            __result = new ThinkResult(finish, __instance, finishDef.tagToGive);
            return false;
        }
    }

    /// The care slice of vanilla's list walk (JobGiver_Work.cs:82-263),
    /// restricted to the Doctor, Patient and Patient bed rest work types and
    /// read from the pawn's own giver list, so the pawn's exclusions and any
    /// role order from other mods are honored. Mirrors vanilla's non-scan,
    /// thing-scan and cell-scan branches; the per-scanner closures are the
    /// same allocations vanilla makes and occur only for a dispatched finisher
    /// at one job search. Re-verify against the decompile on game updates.
    internal static class CareWork
    {
        internal static bool TryIssue(Pawn pawn, out Job job, out WorkGiverDef def)
        {
            job = null!;
            def = null!;
            List<WorkGiver> list = pawn.workSettings.WorkGiversInOrderNormal;
            for (int i = 0; i < list.Count; i++)
            {
                WorkGiver giver = list[i];
                if (!FinishWorkGivers.IsCareWorkType(giver.def.workType)) continue;
                if (!WorkGiverGates.PawnCanUseWorkGiver(pawn, giver, checkShouldSkip: true))
                    continue;
                Job? found;
                try
                {
                    found = TryGiver(pawn, giver);
                }
                catch (Exception ex)
                {
                    Log.Error(pawn + " threw exception in WorkGiver " + giver.def.defName
                        + ": " + ex);
                    continue;
                }
                if (found == null) continue;
                job = found;
                def = giver.def;
                return true;
            }
            return false;
        }

        private static Job? TryGiver(Pawn pawn, WorkGiver giver)
        {
            Job? nonScan = giver.NonScanJob(pawn);
            if (nonScan != null) return nonScan;
            if (!(giver is WorkGiver_Scanner scanner)) return null;

            TargetInfo best = TargetInfo.Invalid;
            if (scanner.def.scanThings)
            {
                Thing? thing = FindThing(pawn, scanner);
                if (thing != null) best = thing;
            }
            if (scanner.def.scanCells)
            {
                IntVec3 pawnPosition = pawn.Position;
                float closestDistSquared = 99999f;
                float bestPriority = float.MinValue;
                bool prioritized = scanner.Prioritized;
                bool allowUnreachable = scanner.AllowUnreachable;
                Danger maxPathDanger = scanner.MaxPathDanger(pawn);
                IEnumerable<IntVec3> cells = scanner.PotentialWorkCellsGlobal(pawn);
                if (cells is IList<IntVec3> cellList)
                {
                    for (int k = 0; k < cellList.Count; k++) ProcessCell(cellList[k]);
                }
                else
                {
                    foreach (IntVec3 c in cells) ProcessCell(c);
                }

                void ProcessCell(IntVec3 c)
                {
                    bool take = false;
                    float dist = (c - pawnPosition).LengthHorizontalSquared;
                    float priority = 0f;
                    if (prioritized)
                    {
                        if (!c.IsForbidden(pawn) && scanner.HasJobOnCell(pawn, c))
                        {
                            if (!allowUnreachable
                                && !pawn.CanReach(c, scanner.PathEndMode, maxPathDanger))
                                return;
                            priority = scanner.GetPriority(pawn, c);
                            if (priority > bestPriority
                                || (priority == bestPriority && dist < closestDistSquared))
                                take = true;
                        }
                    }
                    else if (dist < closestDistSquared && !c.IsForbidden(pawn)
                        && scanner.HasJobOnCell(pawn, c))
                    {
                        if (!allowUnreachable
                            && !pawn.CanReach(c, scanner.PathEndMode, maxPathDanger))
                            return;
                        take = true;
                    }
                    if (take)
                    {
                        best = new TargetInfo(c, pawn.Map);
                        closestDistSquared = dist;
                        bestPriority = priority;
                    }
                }
            }

            if (!best.IsValid) return null;
            Job? job = best.HasThing
                ? scanner.JobOnThing(pawn, best.Thing)
                : scanner.JobOnCell(pawn, best.Cell);
            if (job != null) job.workGiverDef = scanner.def;
            return job;
        }

        private static Thing? FindThing(Pawn pawn, WorkGiver_Scanner scanner)
        {
            IEnumerable<Thing>? enumerable = scanner.PotentialWorkThingsGlobal(pawn);
            Thing? carried = pawn.carryTracker?.CarriedThing;
            bool carriedOk = carried != null
                && scanner.PotentialWorkThingRequest.Accepts(carried)
                && Valid(pawn, scanner, carried);
            Thing? thing;
            if (scanner.Prioritized)
            {
                IEnumerable<Thing> searchSet = enumerable
                    ?? pawn.Map.listerThings.ThingsMatching(scanner.PotentialWorkThingRequest);
                thing = !scanner.AllowUnreachable
                    ? GenClosest.ClosestThing_Global_Reachable(pawn.Position, pawn.Map,
                        searchSet, scanner.PathEndMode,
                        TraverseParms.For(pawn, scanner.MaxPathDanger(pawn)), 9999f,
                        t => Valid(pawn, scanner, t), x => scanner.GetPriority(pawn, x))
                    : GenClosest.ClosestThing_Global(pawn.Position, searchSet, 99999f,
                        t => Valid(pawn, scanner, t), x => scanner.GetPriority(pawn, x));
                if (carriedOk)
                {
                    if (thing == null
                        || scanner.GetPriority(pawn, carried!) >= scanner.GetPriority(pawn, thing))
                        thing = carried;
                }
            }
            else if (carriedOk)
            {
                thing = carried;
            }
            else if (scanner.AllowUnreachable)
            {
                IEnumerable<Thing> searchSet = enumerable
                    ?? pawn.Map.listerThings.ThingsMatching(scanner.PotentialWorkThingRequest);
                thing = GenClosest.ClosestThing_Global(pawn.Position, searchSet, 99999f,
                    t => Valid(pawn, scanner, t));
            }
            else
            {
                thing = GenClosest.ClosestThingReachable(pawn.Position, pawn.Map,
                    scanner.PotentialWorkThingRequest, scanner.PathEndMode,
                    TraverseParms.For(pawn, scanner.MaxPathDanger(pawn)), 9999f,
                    t => Valid(pawn, scanner, t), enumerable, 0,
                    scanner.MaxRegionsToScanBeforeGlobalSearch, enumerable != null);
            }
            return thing;
        }

        private static bool Valid(Pawn pawn, WorkGiver_Scanner scanner, Thing t)
            => !t.IsForbidden(pawn) && scanner.HasJobOnThing(pawn, t);
    }
}
