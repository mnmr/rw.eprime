using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

public class RoleChipStrikesTests
{
    [Test]
    public async Task FullyEnabledChipDrawsNoStrike()
    {
        await Assert.That(RoleChipStrikes.Count(
            globalEnabled: true, assignmentEnabled: true)).IsEqualTo(RoleChipStrikes.None);
    }

    [Test]
    public async Task PawnOffAloneDrawsTheSingleCenterStrike()
    {
        await Assert.That(RoleChipStrikes.Count(
            globalEnabled: true, assignmentEnabled: false)).IsEqualTo(RoleChipStrikes.PawnOff);
    }

    [Test]
    public async Task GlobalOffAloneDrawsTheDoubleStrike()
    {
        await Assert.That(RoleChipStrikes.Count(
            globalEnabled: false, assignmentEnabled: true)).IsEqualTo(RoleChipStrikes.GlobalOff);
    }

    [Test]
    public async Task BothOffDrawsTheTripleStrike()
    {
        await Assert.That(RoleChipStrikes.Count(
            globalEnabled: false, assignmentEnabled: false)).IsEqualTo(RoleChipStrikes.BothOff);
    }
}
