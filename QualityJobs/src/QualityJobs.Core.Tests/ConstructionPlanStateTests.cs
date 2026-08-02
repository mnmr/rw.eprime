namespace QualityJobs.Core.Tests;

using QualityJobs.Core;

public class ConstructionPlanStateTests
{
    [Test]
    [Arguments(ConstructionPlanState.Active, ConstructionPlanState.Paused, true)]        // gate fires
    [Arguments(ConstructionPlanState.Paused, ConstructionPlanState.Dispatched, true)]    // finisher found
    [Arguments(ConstructionPlanState.Dispatched, ConstructionPlanState.Paused, true)]    // revert
    [Arguments(ConstructionPlanState.Paused, ConstructionPlanState.AwaitingRebuild, false)] // only completion can retry
    [Arguments(ConstructionPlanState.Active, ConstructionPlanState.AwaitingRebuild, true)]  // self-finish rolled below min
    [Arguments(ConstructionPlanState.Dispatched, ConstructionPlanState.AwaitingRebuild, true)] // finisher rolled below min
    [Arguments(ConstructionPlanState.AwaitingRebuild, ConstructionPlanState.Active, true)]  // blueprint re-placed
    [Arguments(ConstructionPlanState.AwaitingRebuild, ConstructionPlanState.Paused, false)]
    [Arguments(ConstructionPlanState.Active, ConstructionPlanState.Dispatched, false)]      // must pause first
    [Arguments(ConstructionPlanState.Active, ConstructionPlanState.Active, false)]
    // Full-matrix pins. Dispatched -> Active IS reachable: vanilla's fail roll
    // precedes the completion check each work interval, so a dispatched
    // finisher can trigger FailConstruction (frame -> blueprint -> Active).
    // Paused -> Active is unreachable (paused frames get no workers).
    [Arguments(ConstructionPlanState.Paused, ConstructionPlanState.Active, false)]
    [Arguments(ConstructionPlanState.Dispatched, ConstructionPlanState.Active, true)]
    [Arguments(ConstructionPlanState.Paused, ConstructionPlanState.Paused, false)]
    [Arguments(ConstructionPlanState.Dispatched, ConstructionPlanState.Dispatched, false)]
    [Arguments(ConstructionPlanState.AwaitingRebuild, ConstructionPlanState.AwaitingRebuild, false)]
    [Arguments(ConstructionPlanState.AwaitingRebuild, ConstructionPlanState.Dispatched, false)]
    public async Task TransitionLegality(ConstructionPlanState from, ConstructionPlanState to, bool legal)
    {
        await Assert.That(ConstructionPlanStates.CanTransition(from, to)).IsEqualTo(legal);
    }
}
