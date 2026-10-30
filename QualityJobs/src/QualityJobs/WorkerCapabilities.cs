using System.Collections;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace QualityJobs
{
    /// Shared membership and capability rules for ranking, dispatch and job
    /// admission. Availability is separate: a charging/away worker must not
    /// lower auto-best's quality bar. Preparation permission is deliberately
    /// absent here; it never excludes an otherwise capable finisher.
    internal static class WorkerCapabilities
    {
        internal static bool IsMechanical(Pawn pawn)
            => pawn.RaceProps.IsMechanoid || MiscRobotSupport.IsRobot(pawn);

        internal static bool IsColonyWorker(Pawn pawn)
        {
            if (pawn.Dead || pawn.Destroyed || pawn.Faction != Faction.OfPlayer)
                return false;
            if (pawn.IsColonist && (pawn.HostFaction == null || pawn.IsSlave)
                || pawn.IsColonySubhuman) return true;
            if (MiscRobotSupport.IsRobot(pawn)) return true;
            return pawn.RaceProps.IsMechanoid && pawn.OverseerSubject != null
                && pawn.OverseerSubject.State == OverseerSubjectState.Overseen;
        }

        internal static bool Available(Pawn pawn)
            => IsColonyWorker(pawn) && pawn.Spawned && !pawn.Downed
                && !pawn.InMentalState
                && NativeScheduleAllowsWork(pawn);

        private static bool NativeScheduleAllowsWork(Pawn pawn)
        {
            // Both native robot trees honor drafted orders before work. AIRobot
            // otherwise owns its scheduler independently, including without Bio.
            if (MiscRobotSupport.IsRobot(pawn)) return !pawn.Drafted;
            if (!pawn.RaceProps.IsMechanoid) return true;
            // These are the native mech tree's exclusive work gates. Ordinary
            // busy jobs (including charging) remain eligible, as for humans.
            // Keep this availability separate from the full-colony quality pool.
            return !pawn.Drafted && pawn.IsColonyMechPlayerControlled
                && pawn.GetMechWorkMode() == MechWorkModeDefOf.Work
                && !pawn.IsFormingCaravan()
                && pawn.needs?.energy?.IsLowEnergySelfShutdown != true;
        }

        internal static void CollectColonyWorkers(List<Pawn> result)
        {
            result.Clear();
            List<Pawn> all = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive;
            for (int i = 0; i < all.Count; i++)
                if (IsColonyWorker(all[i])) result.Add(all[i]);

            // AIRobot's plain-list station containers are absent from the game's
            // pawn traversal. Apply the same colony population policy to them.
            IReadOnlyList<Thing> stations = MiscRobotSupport.Stations;
            for (int s = 0; s < stations.Count; s++)
            {
                Thing station = stations[s];
                if (station.Destroyed || !InColonyPopulation(station)) continue;
                IList? robots = MiscRobotSupport.ContainedRobots(station);
                if (robots == null) continue;
                for (int r = 0; r < robots.Count; r++)
                    if (robots[r] is Pawn pawn && IsColonyWorker(pawn)
                        && !result.Contains(pawn)) result.Add(pawn);
            }
        }

        private static bool InColonyPopulation(Thing thing)
        {
            Map? map = thing.MapHeld;
            if (map != null) return Find.Maps.Contains(map);
            Thing root = thing;
            for (IThingHolder? holder = thing.ParentHolder; holder != null; holder = holder.ParentHolder)
            {
                if (holder is Caravan caravan)
                    return !caravan.Destroyed && caravan.Faction == Faction.OfPlayer;
                if (holder is TravellingTransporters transporters)
                    return !transporters.Destroyed && transporters.Faction == Faction.OfPlayer;
                if (holder is Thing heldThing) root = heldThing;
            }
            // Travelling gravships use placement tables rather than ThingOwners.
            Gravship? gravship = Find.CurrentGravship;
            return gravship != null && (gravship.ContainsThing(root)
                || root is Pawn pawn && gravship.ContainsPawn(pawn));
        }

        internal static void RegisterRobotStation(Thing station)
        {
            if (MiscRobotSupport.RegisterStation(station))
                QualityJobsStore.Active?.NotifyExternalPawnFactsChanged();
        }

        internal static void RemoveRobotStation(Thing station)
        {
            if (MiscRobotSupport.RemoveStation(station))
                QualityJobsStore.Active?.NotifyExternalPawnFactsChanged();
        }

        internal static void ReleaseMap(Map map)
        {
            if (MiscRobotSupport.ReleaseMap(map))
                QualityJobsStore.Active?.NotifyExternalPawnFactsChanged();
        }

        internal static List<WorkGiver>? WorkGiversFor(Pawn pawn)
            => MiscRobotSupport.IsRobot(pawn) ? MiscRobotSupport.WorkGivers(pawn)
                : pawn.workSettings?.WorkGiversInOrderNormal;

        internal static bool WorkEnabled(Pawn pawn, WorkTypeDef? workType)
            => workType != null && pawn.workSettings != null
                && !pawn.WorkTypeIsDisabled(workType)
                && pawn.workSettings.WorkIsActive(workType);

        internal static bool CanUse(Pawn pawn, WorkGiverDef? giver)
        {
            if (giver == null || !WorkEnabled(pawn, giver.workType)) return false;
            if (!PawnCanUseWorkGiver(pawn, giver.Worker,
                    checkShouldSkip: false)) return false;
            // Misc. Robots builds its own list from robotWorkTypes. Work-tab
            // settings alone do not prove that its scheduler can do this work.
            return !MiscRobotSupport.IsRobot(pawn)
                || MiscRobotSupport.HasWorkGiver(pawn, giver);
        }

        // Vanilla JobGiver_Work gates, with the optional robot scheduler's
        // capability rules. High-priority finishing checks the target's work
        // category separately, so it can skip the generated giver's category.
        internal static bool PawnCanUseWorkGiver(Pawn pawn, WorkGiver giver,
            bool checkShouldSkip, bool checkWorkType = true)
        {
            WorkGiverDef def = giver.def;
            if (!def.nonColonistsCanDo && !IsColonyWorker(pawn)) return false;
            if (pawn.WorkTagIsDisabled(def.workTags)) return false;
            if (checkWorkType && def.workType != null && pawn.WorkTypeIsDisabled(def.workType)) return false;
            if (checkShouldSkip && giver.ShouldSkip(pawn)) return false;
            if (giver.MissingRequiredCapacity(pawn) != null) return false;
            // AIRobot authorizes its declared work types independently of the
            // vanilla-mech flag, including care work.
            return !pawn.RaceProps.IsMechanoid || def.canBeDoneByMechs
                || MiscRobotSupport.IsRobot(pawn);
        }

        /// Same resolution as vanilla BoundWorker (Bill_ProductionWithUft.cs:36-53):
        /// the work type of the first work giver whose fixedBillGiverDefs cover
        /// benches for this recipe.
        ///
        /// Memo cache — Owner: process (def-derived only). Key: RecipeDef identity.
        /// Value: WorkGiverDef? (nullable; null = no bench giver found). The def is
        /// externally owned; its current workType is read when requested. Dependencies:
        /// def database contents (stable after startup; ManagedRecipes.Invalidate()
        /// calls InvalidateWorkTypeCache() to clear this when a definition reload
        /// occurs). Refresh: lazy on first call per recipe; cleared by
        /// InvalidateWorkTypeCache(). Equality: n/a (single value per key).
        /// Teardown/reset: InvalidateWorkTypeCache clears all def references.
        /// Cache hits are plain Dictionary lookups — no allocation.
        private static readonly Dictionary<RecipeDef, WorkGiverDef?> s_workTypeCache =
            new Dictionary<RecipeDef, WorkGiverDef?>();

        /// Clears the recipe→workType memo cache. Called by ManagedRecipes.Invalidate()
        /// after a definition reload so both caches stay coherent.
        internal static void InvalidateWorkTypeCache() => s_workTypeCache.Clear();

        internal static WorkTypeDef? WorkTypeForRecipe(RecipeDef recipe)
            => WorkGiverForRecipe(recipe)?.workType;

        internal static WorkGiverDef? WorkGiverForRecipe(RecipeDef recipe)
        {
            // Runtime clones (unresolved defNameHash) would all collide on one
            // key and miss ThingDef.recipes benches; resolve to the registered def.
            recipe = ManagedRecipes.Registered(recipe) ?? recipe;
            if (s_workTypeCache.TryGetValue(recipe, out WorkGiverDef? cached))
                return cached;
            WorkGiverDef? result = ResolveWorkGiver(recipe,
                DefDatabase<WorkGiverDef>.AllDefsListForReading);
            s_workTypeCache[recipe] = result;
            return result;
        }

        /// Unmemoized resolution: the first giver whose fixedBillGiverDefs cover
        /// a bench for this recipe. Finisher work-giver generation calls it at
        /// PreResolve, where the memo above must not be filled yet (other mods'
        /// defs are still loading) and ManagedRecipes must not be touched.
        internal static WorkGiverDef? ResolveWorkGiver(RecipeDef recipe,
            List<WorkGiverDef> givers)
        {
            foreach (ThingDef benchDef in recipe.AllRecipeUsers)
                for (int i = 0; i < givers.Count; i++)
                    if (givers[i].fixedBillGiverDefs != null
                        && givers[i].fixedBillGiverDefs.Contains(benchDef))
                        return givers[i];
            return null;
        }

        internal static bool CanCraft(Pawn pawn, RecipeDef recipe)
            => CanUse(pawn, WorkGiverForRecipe(recipe))
                && (!recipe.mechanitorOnlyRecipe || MechanitorUtility.IsMechanitor(pawn));

        internal static bool CanConstruct(Pawn pawn)
            => CanUse(pawn, FinishWorkGiverDefOf.ConstructFinishFrames);
    }
}
