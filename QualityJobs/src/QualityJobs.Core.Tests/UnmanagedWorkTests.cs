namespace QualityJobs.Core.Tests;

using QualityJobs.Core;

/// Hand-back when management ends: on load for recipes Quality Jobs no
/// longer manages (their product quality comes from an ingredient, not the
/// crafter), and when a consumer unmanages a bill through the API. Only work
/// the gate locked is released; shared work never depended on management.
public class UnmanagedWorkTests
{
    [Test]
    [Arguments(false, WorkItemState.Paused, true)]
    [Arguments(false, WorkItemState.Dispatched, true)]
    [Arguments(false, WorkItemState.Shared, false)]
    [Arguments(true, WorkItemState.Paused, false)]
    [Arguments(true, WorkItemState.Dispatched, false)]
    public async Task OnlyGateLockedWorkIsReleasedWhenManagementEnds(
        bool managed, WorkItemState state, bool expected)
    {
        await Assert.That(UnmanagedWork.Releases(managed, state))
            .IsEqualTo(expected);
    }
}
