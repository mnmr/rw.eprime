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

    /// The draw code must consume the measured layout — hardcoded column
    /// offsets are exactly the bug this guards against. Source-text check
    /// because the game-assembly draw path cannot execute in tests.
    [Test]
    public async Task DrawOptionsBodyUsesMeasuredRowLayout()
    {
        var source = ArchitectureTestSupport.Source("UI", "EditorView.cs");
        var body = ArchitectureTestSupport.Method(source, "private void DrawOptionsBody(");
        await Assert.That(body).Contains("ThresholdRow");
        await Assert.That(body).DoesNotContain("x + 106f");
        await Assert.That(body).DoesNotContain("x + 166f");
        await Assert.That(body).DoesNotContain("x + 234f");
        await Assert.That(body).DoesNotContain("x + 288f");
    }
}
