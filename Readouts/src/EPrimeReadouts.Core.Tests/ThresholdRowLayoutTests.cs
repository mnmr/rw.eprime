using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

/// Regression for clipped threshold-editor labels: fixed column offsets cut
/// off "Low"/"Critical"/button captions when the Small font substitutes for
/// Tiny or a translation runs longer than English.
public class ThresholdRowLayoutTests
{
    [Test]
    public async Task BaselineWidthsReproduceClassicPositions()
    {
        var row = ThresholdRowLayout.Compute(34f, 56f, 50f, 56f);
        await Assert.That(row.LowLabelW).IsEqualTo(34f);
        await Assert.That(row.LowFieldX).IsEqualTo(38f);
        await Assert.That(row.CriticalLabelX).IsEqualTo(106f);
        await Assert.That(row.CriticalLabelW).IsEqualTo(56f);
        await Assert.That(row.CriticalFieldX).IsEqualTo(166f);
        await Assert.That(row.SetX).IsEqualTo(234f);
        await Assert.That(row.ClearX).IsEqualTo(288f);
        await Assert.That(row.Width).IsEqualTo(344f);
    }

    [Test]
    public async Task MeasuredWidthsBelowBaselineClampUp()
    {
        var row = ThresholdRowLayout.Compute(10f, 10f, 10f, 10f);
        await Assert.That(row.LowLabelW).IsEqualTo(34f);
        await Assert.That(row.CriticalLabelX).IsEqualTo(106f);
        await Assert.That(row.CriticalFieldX).IsEqualTo(166f);
        await Assert.That(row.SetX).IsEqualTo(234f);
        await Assert.That(row.ClearX).IsEqualTo(288f);
        await Assert.That(row.Width).IsEqualTo(344f);
    }

    [Test]
    public async Task WiderTextShiftsEveryFollowingColumn()
    {
        var row = ThresholdRowLayout.Compute(44f, 80f, 60f, 70f);
        await Assert.That(row.LowFieldX).IsEqualTo(48f);
        await Assert.That(row.CriticalLabelX).IsEqualTo(116f);
        await Assert.That(row.CriticalFieldX).IsEqualTo(200f);
        await Assert.That(row.SetX).IsEqualTo(268f);
        await Assert.That(row.SetW).IsEqualTo(60f);
        await Assert.That(row.ClearX).IsEqualTo(332f);
        await Assert.That(row.Width).IsEqualTo(402f);
    }

    /// The editor's column is 332 wide in the 960-wide minimum window, 12
    /// short of the classic row: the Clear button was cut off at its edge.
    /// The two number fields give up the difference; the buttons keep their
    /// captions.
    [Test]
    public async Task NarrowColumnShrinksTheFieldsSoClearStaysInside()
    {
        var row = ThresholdRowLayout.Compute(34f, 56f, 50f, 56f, 332f);
        await Assert.That(row.Width).IsEqualTo(332f);
        await Assert.That(row.FieldWidth).IsEqualTo(54f);
        await Assert.That(row.SetW).IsEqualTo(50f);
        await Assert.That(row.ClearW).IsEqualTo(56f);
    }

    [Test]
    public async Task FieldsNeverShrinkBelowFourDigitsOfRoom()
    {
        var row = ThresholdRowLayout.Compute(34f, 56f, 50f, 56f, 200f);
        await Assert.That(row.FieldWidth).IsEqualTo(ThresholdRowLayout.MinFieldW);
    }

    [Test]
    public async Task RoomToSpareLeavesTheClassicRow()
    {
        var row = ThresholdRowLayout.Compute(34f, 56f, 50f, 56f, 470f);
        await Assert.That(row.FieldWidth).IsEqualTo(60f);
        await Assert.That(row.Width).IsEqualTo(344f);
    }

}
