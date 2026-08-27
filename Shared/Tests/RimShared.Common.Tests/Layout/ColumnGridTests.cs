using RimShared.Common;

namespace RimShared.Common.Tests;

public class ColumnGridTests
{
    [Test]
    public async Task ColumnCountRoundsUpAndHandlesEmpty()
    {
        await Assert.That(ColumnGrid.ColumnCount(0, 20)).IsEqualTo(0);
        await Assert.That(ColumnGrid.ColumnCount(1, 20)).IsEqualTo(1);
        await Assert.That(ColumnGrid.ColumnCount(20, 20)).IsEqualTo(1);
        await Assert.That(ColumnGrid.ColumnCount(21, 20)).IsEqualTo(2);
        await Assert.That(ColumnGrid.ColumnCount(40, 20)).IsEqualTo(2);
        await Assert.That(ColumnGrid.ColumnCount(41, 20)).IsEqualTo(3);
    }

    [Test]
    public async Task ItemsFillColumnMajor()
    {
        // Item 19 ends column 0; item 20 starts column 1 at row 0.
        await Assert.That(ColumnGrid.ColumnOf(19, 20)).IsEqualTo(0);
        await Assert.That(ColumnGrid.RowOf(19, 20)).IsEqualTo(19);
        await Assert.That(ColumnGrid.ColumnOf(20, 20)).IsEqualTo(1);
        await Assert.That(ColumnGrid.RowOf(20, 20)).IsEqualTo(0);
    }

    [Test]
    public async Task LastColumnMayBeShort()
    {
        await Assert.That(ColumnGrid.RowsInColumn(0, 21, 20)).IsEqualTo(20);
        await Assert.That(ColumnGrid.RowsInColumn(1, 21, 20)).IsEqualTo(1);
        await Assert.That(ColumnGrid.RowsInColumn(2, 21, 20)).IsEqualTo(0);
        await Assert.That(ColumnGrid.RowsInColumn(0, 5, 20)).IsEqualTo(5);
    }

    [Test]
    public async Task RowsPerColumnLimitsAResourceGridToThreeColumns()
    {
        await Assert.That(ColumnGrid.RowsPerColumn(0, 3)).IsEqualTo(0);
        await Assert.That(ColumnGrid.RowsPerColumn(2, 3)).IsEqualTo(1);
        await Assert.That(ColumnGrid.RowsPerColumn(30, 3)).IsEqualTo(10);
        await Assert.That(ColumnGrid.RowsPerColumn(31, 3)).IsEqualTo(11);
    }
}
