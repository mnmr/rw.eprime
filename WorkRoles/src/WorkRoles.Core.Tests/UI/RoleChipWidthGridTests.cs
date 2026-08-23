namespace WorkRoles.Core.Tests.UI;

public class RoleChipWidthGridTests
{
    [Test]
    public async Task GridUsesTheWidestAssignedChipAcrossRows()
    {
        var widths = new RoleChipWidthGrid();
        widths.Include(34f);
        widths.Include(57f);
        widths.Include(41f);

        await Assert.That(widths.WidthFor(34f, grid: true)).IsEqualTo(57f);
        await Assert.That(widths.WidthFor(41f, grid: true)).IsEqualTo(57f);
        await Assert.That(widths.UnwrappedWidth(
            chipCount: 3, naturalWidth: 144f, gap: 4f, grid: true))
            .IsEqualTo(183f);
    }

    [Test]
    public async Task NaturalModePreservesEachChipAndRowWidth()
    {
        var widths = new RoleChipWidthGrid();
        widths.Include(57f);

        await Assert.That(widths.WidthFor(34f, grid: false)).IsEqualTo(34f);
        await Assert.That(widths.UnwrappedWidth(
            chipCount: 3, naturalWidth: 144f, gap: 4f, grid: false))
            .IsEqualTo(144f);
    }
}
