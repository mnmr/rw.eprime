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
        ///   - if the resulting values (including the auto-best flag) are all
        ///     neutral, removes any existing plan;
        ///   - otherwise creates a plan if needed and applies the values,
        ///     removing again if clamping/coercion left it neutral.
        public static void Apply(QualityJobsStore store, int thingId, int minSkill,
            bool requireInspired, bool requireSpecialist, int minQuality, bool autoBest)
        {
            store.ApplyPlanSettings(thingId, minSkill, requireInspired,
                requireSpecialist, minQuality, autoBest);
        }
    }
}
