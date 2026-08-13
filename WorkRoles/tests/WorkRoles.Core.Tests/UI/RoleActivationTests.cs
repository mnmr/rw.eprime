namespace WorkRoles.Core.Tests.UI;

public class RoleActivationTests
{
    [Test]
    [Arguments(true, AssignmentState.Enabled, true)]
    [Arguments(true, AssignmentState.Disabled, false)]
    [Arguments(true, AssignmentState.ForceOn, true)]
    [Arguments(false, AssignmentState.Enabled, false)]
    [Arguments(false, AssignmentState.Disabled, false)]
    [Arguments(false, AssignmentState.ForceOn, true)]
    public async Task ActiveExactlyWhenForcedOrGloballyOnAndEnabled(bool roleEnabled, AssignmentState state, bool expected)
    {
        await Assert.That(RoleActivation.IsActive(roleEnabled, state)).IsEqualTo(expected);
    }

    [Test]
    [Arguments(AssignmentState.Enabled, AssignmentState.Disabled)]
    [Arguments(AssignmentState.Disabled, AssignmentState.ForceOn)]
    [Arguments(AssignmentState.ForceOn, AssignmentState.Enabled)]
    public async Task ClickAdvancesToTheNextAssignmentState(AssignmentState current, AssignmentState expected)
    {
        AssignmentState actual = RoleActivation.Next(current);

        await Assert.That(actual).IsEqualTo(expected);
    }
}
