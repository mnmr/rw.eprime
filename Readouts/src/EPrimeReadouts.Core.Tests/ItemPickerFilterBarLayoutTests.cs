using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

public class ItemPickerFilterBarLayoutTests
{
    private const float Width = 1000f;
    private const float Gap = 6f;
    private const float Content = Width - 2f * Gap;

    [Test]
    public async Task LabelsThatFitKeepTheDefaultProportions()
    {
        var widths = ItemPickerFilterBarLayout.Solve(
            Width, Gap, searchMin: 150f, typeRequired: 100f, sourceRequired: 100f);

        await Assert.That(widths.Search).IsEqualTo(300f);
        await Assert.That(widths.Type).IsEqualTo(309f);
        await Assert.That(widths.Source).IsEqualTo(379f);
    }

    [Test]
    public async Task ALongTypeLabelBorrowsFromBothNeighboursWithoutBreakingTheirMinimums()
    {
        var widths = ItemPickerFilterBarLayout.Solve(
            Width, Gap, searchMin: 150f, typeRequired: 400f, sourceRequired: 100f);

        await Assert.That(widths.Type).IsEqualTo(400f);
        await Assert.That(widths.Search + widths.Type + widths.Source).IsEqualTo(Content);
        await Assert.That(widths.Search).IsLessThan(300f);
        await Assert.That(widths.Search).IsGreaterThanOrEqualTo(150f);
        await Assert.That(widths.Source).IsLessThan(379f);
        await Assert.That(widths.Source).IsGreaterThanOrEqualTo(100f);
        // The neighbour with more room to spare gives up more.
        await Assert.That(300f - widths.Search).IsLessThan(379f - widths.Source);
    }

    [Test]
    public async Task ALongSourceLabelIsServedTheSameWay()
    {
        var widths = ItemPickerFilterBarLayout.Solve(
            Width, Gap, searchMin: 150f, typeRequired: 100f, sourceRequired: 450f);

        await Assert.That(widths.Source).IsEqualTo(450f);
        await Assert.That(widths.Search + widths.Type + widths.Source).IsEqualTo(Content);
        await Assert.That(widths.Search).IsGreaterThanOrEqualTo(150f);
        await Assert.That(widths.Type).IsGreaterThanOrEqualTo(100f);
    }

    [Test]
    public async Task WhenNothingCanFitTheSearchFieldYieldsFirstAndButtonsShareTheRest()
    {
        var widths = ItemPickerFilterBarLayout.Solve(
            Width, Gap, searchMin: 150f, typeRequired: 600f, sourceRequired: 300f);

        await Assert.That(widths.Search).IsEqualTo(150f);
        await Assert.That(widths.Type + widths.Source).IsEqualTo(Content - 150f);
        await Assert.That(widths.Type).IsGreaterThan(widths.Source);
    }
}
