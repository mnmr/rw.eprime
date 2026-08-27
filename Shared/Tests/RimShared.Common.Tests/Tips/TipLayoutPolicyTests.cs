using RimShared.Common;

namespace RimShared.Common.Tests;

public class TipLayoutPolicyTests
{
    [Test]
    public async Task ContentCapIsAppliedBeforeTheSymmetricFrame()
    {
        float withinCap = TipLayoutPolicy.ContentWidth(792f, 800f);
        float capped = TipLayoutPolicy.ContentWidth(900f, 800f);

        await Assert.That(TipLayoutPolicy.FramedExtent(withinCap, 8f))
            .IsEqualTo(808f);
        await Assert.That(TipLayoutPolicy.FramedExtent(capped, 8f))
            .IsEqualTo(816f);
    }

    [Test]
    public async Task SingleColumnPoolHeaderAndRowsShareColumnWidths()
    {
        TipColumnLayout columns = TipLayoutPolicy.SharedColumns(
            headerLabelWidth: 96f,
            headerValueWidth: 18f,
            rowLabelWidth: 64f,
            rowValueWidth: 24f,
            gap: 12f);

        await Assert.That(columns.LabelWidth).IsEqualTo(96f);
        await Assert.That(columns.ValueWidth).IsEqualTo(24f);
        await Assert.That(columns.ValueX).IsEqualTo(108f);
        await Assert.That(columns.TotalWidth).IsEqualTo(132f);
        await Assert.That(TipLayoutPolicy.RightAlignedTextX(
            columns.ValueX, columns.ValueWidth, textWidth: 18f))
            .IsEqualTo(114f);
        await Assert.That(TipLayoutPolicy.RightAlignedTextX(
            columns.ValueX, columns.ValueWidth, textWidth: 12f))
            .IsEqualTo(120f);
    }

    [Test]
    public async Task SingleResourceWorkMovesDifferingStockIntoTheHeader()
    {
        PlannedWorkTipLayout layout = PlannedWorkTipLayout.For(
            pooled: false, available: 1647, inStock: 1815);

        await Assert.That(layout.ColumnCount).IsEqualTo(4);
        await Assert.That(layout.ShowResourceColumn).IsFalse();
        await Assert.That(layout.ShowInStockColumn).IsFalse();
        await Assert.That(layout.ShowStockInHeader).IsTrue();
    }

    [Test]
    public async Task SingleResourceWithoutAStockDifferenceKeepsItsCompactHeader()
    {
        PlannedWorkTipLayout layout = PlannedWorkTipLayout.For(
            pooled: false, available: 1815, inStock: 1815);

        await Assert.That(layout.ShowStockInHeader).IsFalse();
    }

    [Test]
    public async Task PoolWorkKeepsItsPerResourceStockColumns()
    {
        PlannedWorkTipLayout layout = PlannedWorkTipLayout.For(
            pooled: true, available: 1647, inStock: 1815);

        await Assert.That(layout.ColumnCount).IsEqualTo(6);
        await Assert.That(layout.ShowResourceColumn).IsTrue();
        await Assert.That(layout.ShowInStockColumn).IsTrue();
        await Assert.That(layout.ShowStockInHeader).IsFalse();
    }
}
