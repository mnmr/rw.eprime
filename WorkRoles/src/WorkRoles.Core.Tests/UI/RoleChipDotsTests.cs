namespace WorkRoles.Core.Tests.UI;

public class RoleChipDotsTests
{
    [Test]
    public async Task ADotAppearsOnlyForAFactThatHolds()
    {
        await Assert.That(RoleChipDots.Mask(false, false, false)).IsEqualTo(0);
        await Assert.That(RoleChipDots.Count(0)).IsEqualTo(0);
        await Assert.That(RoleChipDots.Mask(true, false, false)).IsEqualTo(RoleChipDots.AlwaysOn);
        await Assert.That(RoleChipDots.Mask(false, true, false)).IsEqualTo(RoleChipDots.Rules);
        await Assert.That(RoleChipDots.Mask(false, false, true)).IsEqualTo(RoleChipDots.Pinned);
        await Assert.That(RoleChipDots.Count(RoleChipDots.Mask(true, false, true))).IsEqualTo(2);
        await Assert.That(RoleChipDots.Count(RoleChipDots.Mask(true, true, true))).IsEqualTo(3);
        await Assert.That(RoleChipDots.Mask(true, true, true)).IsEqualTo(RoleChipDots.Combinations - 1);
    }
}
