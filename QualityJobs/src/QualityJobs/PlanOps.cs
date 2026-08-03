using System.Collections.Generic;
using QualityJobs.Core;
using RimWorld;
using Verse;

namespace QualityJobs
{
    /// Non-synced core of plan application (Fix 2). Extracted from
    /// Commands.ApplyPlanSettings so deterministic simulation code (e.g. the
    /// blueprint spawn hook, Fix 3) can create/overwrite/remove plans WITHOUT
    /// routing through a [SyncMethod]. Direct store mutation is deterministic
    /// during synced replay: every client runs the same spawn/scan code against
    /// the same synced store, exactly as the gate/scan already mutate the store
    /// directly.
    ///
    /// The synced [SyncMethod] Commands.ApplyPlanSettings simply resolves the
    /// store and delegates here; the clamping and Ideology coercion live in this
    /// one place so both entry points behave identically.
    public static class PlanOps
    {
        /// Creates, overwrites, or removes-if-neutral the plan for thingId.
        /// Clamps skill to [0,20] and quality to [0,6] and coerces
        /// requireSpecialist off when Ideology is inactive, then:
        ///   - if the resulting values are all neutral, removes any existing plan;
        ///   - otherwise creates a plan if needed and applies the values,
        ///     removing again if clamping/coercion left it neutral.
        public static void Apply(QualityJobsStore store, int thingId, int minSkill,
            bool requireInspired, bool requireSpecialist, int minQuality)
        {
            // Clamp/coerce exactly as the individual setters do.
            minSkill = System.Math.Clamp(minSkill, 0, 20);
            minQuality = System.Math.Clamp(minQuality, 0, 6);
            requireSpecialist = requireSpecialist && ModsConfig.IdeologyActive;

            ConstructionPlan? plan = store.FindPlanById(thingId);

            bool incomingNeutral = minSkill == 0 && !requireInspired
                && !requireSpecialist && minQuality == 0;
            if (incomingNeutral)
            {
                if (plan != null)
                {
                    Dispatcher.RemoveOurDeconstructDesignation(plan);
                    store.RemovePlan(plan);
                }
                return;
            }

            if (plan == null)
            {
                plan = CreateNeutralPlan(store, thingId);
                if (plan == null) return;
            }

            plan.minSkill = minSkill;
            plan.requireInspired = requireInspired;
            plan.requireSpecialist = requireSpecialist;
            plan.minQuality = minQuality;

            // Covers the edge case where clamping/coercion turned all values
            // neutral after the check above (e.g. Ideology deactivated).
            RemoveIfNeutral(store, plan);
        }

        private static bool IsNeutral(ConstructionPlan plan)
            => plan.minSkill == 0 && !plan.requireInspired
               && !plan.requireSpecialist && plan.minQuality == 0;

        private static void RemoveIfNeutral(QualityJobsStore store, ConstructionPlan plan)
        {
            if (!IsNeutral(plan)) return;
            Dispatcher.RemoveOurDeconstructDesignation(plan);
            store.RemovePlan(plan);
        }

        /// Resolves or creates a neutral plan for the given thingId.
        /// Returns null if the thing cannot be found or is not a
        /// Blueprint_Build/Frame (the only gate-manageable thing types).
        private static ConstructionPlan? CreateNeutralPlan(QualityJobsStore store, int thingId)
        {
            Thing? target = FindSpawnedThing(thingId);
            if (target == null) return null;
            if (!(target is Blueprint_Build) && !(target is Frame)) return null;
            var plan = new ConstructionPlan
            {
                target = target,
                state = ConstructionPlanState.Active,
            };
            store.AddPlan(plan);
            return plan;
        }

        private static Thing? FindSpawnedThing(int thingId)
        {
            List<Map> maps = Find.Maps;
            for (int m = 0; m < maps.Count; m++)
            {
                List<Thing> things = maps[m].listerThings.AllThings;
                for (int i = 0; i < things.Count; i++)
                    if (things[i].thingIDNumber == thingId) return things[i];
            }
            return null;
        }
    }
}
