using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

public class RoleChipStrikesTests
{
    [Test]
    public async Task FullyEnabledChipDrawsNoStrike()
    {
        await Assert.That(RoleChipStrikes.Count(
            globalEnabled: true, AssignmentState.Enabled)).IsEqualTo(RoleChipStrikes.None);
    }

    [Test]
    public async Task PawnOffAloneDrawsTheSingleCenterStrike()
    {
        await Assert.That(RoleChipStrikes.Count(
            globalEnabled: true, AssignmentState.Disabled)).IsEqualTo(RoleChipStrikes.PawnOff);
    }

    [Test]
    public async Task GlobalOffAloneDrawsTheDoubleStrike()
    {
        await Assert.That(RoleChipStrikes.Count(
            globalEnabled: false, AssignmentState.Enabled)).IsEqualTo(RoleChipStrikes.GlobalOff);
    }

    [Test]
    public async Task BothOffDrawsTheTripleStrike()
    {
        await Assert.That(RoleChipStrikes.Count(
            globalEnabled: false, AssignmentState.Disabled)).IsEqualTo(RoleChipStrikes.BothOff);
    }

    [Test]
    public async Task ForcedOnChipIsNeverStruckUnderEitherGlobalState()
    {
        await Assert.That(RoleChipStrikes.Count(
            globalEnabled: true, AssignmentState.ForceOn)).IsEqualTo(RoleChipStrikes.None);
        await Assert.That(RoleChipStrikes.Count(
            globalEnabled: false, AssignmentState.ForceOn)).IsEqualTo(RoleChipStrikes.None);
    }
}
