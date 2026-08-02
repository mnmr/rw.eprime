namespace QualityJobs.Core
{
    /// <summary>Construction plan lifecycle (spec §10). Active = blueprint/frame
    /// being built normally; Paused = frame at 100% work, quality-locked;
    /// Dispatched = finisher assigned; AwaitingRebuild = completed building
    /// rolled below the minimum quality and carries a Deconstruct designation.
    /// Plans are REMOVED (not transitioned) when the outcome is accepted or
    /// the target dies.</summary>
    public enum ConstructionPlanState
    {
        Active = 0,
        Paused = 1,
        Dispatched = 2,
        AwaitingRebuild = 3,
    }

    public static class ConstructionPlanStates
    {
        public static bool CanTransition(ConstructionPlanState from, ConstructionPlanState to)
        {
            switch (from)
            {
                case ConstructionPlanState.Active:
                    // Gate pauses, or a completing roll below minimum retries.
                    return to == ConstructionPlanState.Paused
                        || to == ConstructionPlanState.AwaitingRebuild;
                case ConstructionPlanState.Paused:
                    return to == ConstructionPlanState.Dispatched;
                case ConstructionPlanState.Dispatched:
                    // Dispatched -> Active: vanilla's construction fail roll
                    // runs BEFORE the completion check each work interval
                    // (JobDriver_ConstructFinishFrame.cs:59-79), so even a
                    // finisher on a 100% frame can fail; FailConstruction then
                    // re-places the blueprint and the plan returns to Active.
                    return to == ConstructionPlanState.Paused
                        || to == ConstructionPlanState.AwaitingRebuild
                        || to == ConstructionPlanState.Active;
                case ConstructionPlanState.AwaitingRebuild:
                    return to == ConstructionPlanState.Active;
                default:
                    return false;
            }
        }
    }
}
