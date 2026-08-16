namespace WorkRoles.Core.Tests.UI;

public class RoleChipStrikesTests
{
    [Test]
    public async Task FullyEnabledChipDrawsNoStrike()
    {
        await Assert.That(RoleChipStrikes.Count(globalEnabled: true, AssignmentState.Enabled)).IsEqualTo(RoleChipStrikes.None);
    }

    [Test]
    public async Task PawnOffAloneDrawsTheSingleCenterStrike()
    {
        await Assert.That(RoleChipStrikes.Count(globalEnabled: true, AssignmentState.Disabled)).IsEqualTo(RoleChipStrikes.PawnOff);
    }

    [Test]
    public async Task GlobalOffAloneDrawsTheDoubleStrike()
    {
        await Assert.That(RoleChipStrikes.Count(globalEnabled: false, AssignmentState.Enabled)).IsEqualTo(RoleChipStrikes.GlobalOff);
    }

    [Test]
    public async Task BothOffDrawsTheTripleStrike()
    {
        await Assert.That(RoleChipStrikes.Count(globalEnabled: false, AssignmentState.Disabled)).IsEqualTo(RoleChipStrikes.BothOff);
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task ForcedOnChipIsNeverStruck(bool globalEnabled)
    {
        int actual = RoleChipStrikes.Count(globalEnabled, AssignmentState.ForceOn);

        await Assert.That(actual).IsEqualTo(RoleChipStrikes.None);
    }
}
