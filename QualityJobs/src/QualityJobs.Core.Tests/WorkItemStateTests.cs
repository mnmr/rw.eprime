namespace QualityJobs.Core.Tests;

using QualityJobs.Core;

public class WorkItemStateTests
{
    [Test]
    [Arguments(WorkItemState.Paused, WorkItemState.Dispatched, true)]
    [Arguments(WorkItemState.Dispatched, WorkItemState.Paused, true)]   // revert
    [Arguments(WorkItemState.Paused, WorkItemState.Shared, false)]      // quality lock never pools
    [Arguments(WorkItemState.Shared, WorkItemState.Paused, false)]      // shared exits via handover, re-enters via gate
    [Arguments(WorkItemState.Shared, WorkItemState.Dispatched, false)]
    [Arguments(WorkItemState.Paused, WorkItemState.Paused, false)]
    [Arguments(WorkItemState.Dispatched, WorkItemState.Dispatched, false)]
    [Arguments(WorkItemState.Shared, WorkItemState.Shared, false)]
    [Arguments(WorkItemState.Dispatched, WorkItemState.Shared, false)]
    public async Task TransitionLegality(WorkItemState from, WorkItemState to, bool legal)
    {
        await Assert.That(WorkItemStates.CanTransition(from, to)).IsEqualTo(legal);
    }
}
