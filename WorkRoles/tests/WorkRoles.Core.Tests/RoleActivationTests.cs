using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

public class RoleActivationTests
{
    [Test]
    [Arguments(true, AssignmentState.Enabled, true)]
    [Arguments(true, AssignmentState.Disabled, false)]
    [Arguments(true, AssignmentState.ForceOn, true)]
    [Arguments(false, AssignmentState.Enabled, false)]
    [Arguments(false, AssignmentState.Disabled, false)]
    [Arguments(false, AssignmentState.ForceOn, true)]
    public async Task ActiveExactlyWhenForcedOrGloballyOnAndEnabled(
        bool roleEnabled, AssignmentState state, bool expected)
    {
        await Assert.That(RoleActivation.IsActive(roleEnabled, state))
            .IsEqualTo(expected);
    }

    [Test]
    public async Task ClickCycleVisitsEveryStateInOrder()
    {
        await Assert.That(RoleActivation.Next(AssignmentState.Enabled))
            .IsEqualTo(AssignmentState.Disabled);
        await Assert.That(RoleActivation.Next(AssignmentState.Disabled))
            .IsEqualTo(AssignmentState.ForceOn);
        await Assert.That(RoleActivation.Next(AssignmentState.ForceOn))
            .IsEqualTo(AssignmentState.Enabled);
    }
}
