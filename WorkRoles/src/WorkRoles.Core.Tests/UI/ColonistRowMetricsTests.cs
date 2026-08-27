namespace WorkRoles.Core.Tests.UI;

/// Colonist-table row heights: the row's first pixel is its separator line,
/// so the centered chip strip shows equal visible padding only when the
/// height-minus-strip slack is odd. These scenarios pin the published heights
/// for strip-driven rows and both even-slack normalizations.
public class ColonistRowMetricsTests
{
    /// A two-line strip drives the row: strip + 7 (3 above, 3 below, 1
    /// separator) is already odd-slack and passes through unchanged.
    [Test]
    public async Task StripDrivenRowKeepsItsPadding()
    {
        await Assert.That(ColonistRowMetrics.Height(
            minRowHeight: 35f, textBlockHeight: 34f, stripHeight: 52f))
            .IsEqualTo(59f);
    }

    /// Text-driven odd-slack row is already balanced and unchanged.
    [Test]
    public async Task OddSlackTextRowIsUnchanged()
    {
        await Assert.That(ColonistRowMetrics.Height(
            minRowHeight: 35f, textBlockHeight: 34f, stripHeight: 24f))
            .IsEqualTo(35f);
    }

    /// Text-driven even-slack row (the single-chip-row case that rendered one
    /// extra pixel below the strip) shrinks by one pixel while the text block
    /// still fits.
    [Test]
    public async Task EvenSlackRowShrinksWhenTextStillFits()
    {
        await Assert.That(ColonistRowMetrics.Height(
            minRowHeight: 36f, textBlockHeight: 34f, stripHeight: 24f))
            .IsEqualTo(35f);
    }

    /// When the text block is height-tight, an even-slack row grows instead
    /// of clipping glyph rows.
    [Test]
    public async Task EvenSlackRowGrowsInsteadOfClippingText()
    {
        await Assert.That(ColonistRowMetrics.Height(
            minRowHeight: 36f, textBlockHeight: 36f, stripHeight: 24f))
            .IsEqualTo(37f);
    }
}
