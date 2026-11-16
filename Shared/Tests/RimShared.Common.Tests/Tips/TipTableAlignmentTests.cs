using RimShared.Common;

namespace RimShared.Common.Tests;

public class TipTableAlignmentTests
{
    [Test]
    public async Task NumericCellsAlignToTheRightEdgeOfTheirColumn()
    {
        await Assert.That(TipTableLayout.TextX(
                columnX: 108f, columnWidth: 24f, textWidth: 18f,
                TipColumnAlignment.Right))
            .IsEqualTo(114f);
        await Assert.That(TipTableLayout.TextX(
                columnX: 108f, columnWidth: 24f, textWidth: 18f,
                TipColumnAlignment.Left))
            .IsEqualTo(108f);
    }

    [Test]
    public async Task TableThatFitsKeepsItsNaturalColumns()
    {
        var natural = new[] { 150f, 200f, 220f, 140f };
        await Assert.That(TipTableLayout.FitColumns(natural, 710f, 48f))
            .IsSameReferenceAs(natural);
    }

    // A signal table: short name and source columns, long effect and
    // condition columns, 740 of text room. The long ones share what is left.
    [Test]
    public async Task OverWideTableNarrowsOnlyItsWidestColumns()
    {
        float[] fitted = TipTableLayout.FitColumns(
            new[] { 150f, 420f, 380f, 140f }, 740f, 48f);
        await Assert.That(fitted).IsEquivalentTo(new[] { 150f, 225f, 225f, 140f });
    }

    [Test]
    public async Task ColumnsNeverNarrowBelowTheMinimum()
    {
        float[] fitted = TipTableLayout.FitColumns(
            new[] { 300f, 300f, 300f }, 90f, 48f);
        await Assert.That(fitted).IsEquivalentTo(new[] { 48f, 48f, 48f });
    }
}
