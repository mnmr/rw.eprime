using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

public class GlyphRasterMathTests
{
    // Pixel placement is independently testable here; the game captures also
    // verify that both real renderers pass the generated run anchor through.
    [Test]
    public async Task CenteredCounterSnapsWholeRunWithoutChangingGlyphSizeOrSpacing()
    {
        // 1.7 at UI scale 1.25: a centered three-digit counter starts at
        // x=13.625 inside its cell. Keep a fractional glyph advance intact.
        GlyphRasterPoint first = GlyphRasterMath.Place(
            18f, 115f, 13.625f, -7f, 1.25f, 13.625f, -7f);
        GlyphRasterPoint firstRight = GlyphRasterMath.Place(
            18f, 115f, 18.625f, -16f, 1.25f, 13.625f, -7f);
        GlyphRasterPoint next = GlyphRasterMath.Place(
            18f, 115f, 20.875f, -7f, 1.25f, 13.625f, -7f);

        await Assert.That(first.X).IsEqualTo(37f);
        await Assert.That(first.Y).IsEqualTo(151f);
        await Assert.That(firstRight.X - first.X).IsEqualTo(5f);
        await Assert.That(firstRight.Y - first.Y).IsEqualTo(9f);
        await Assert.That(next.X - first.X).IsEqualTo(7.25f);
        await Assert.That(next.Y).IsEqualTo(first.Y);
    }

    [Test]
    public async Task ScalesLogicalOriginWithoutScalingGeneratedGlyphOffset()
    {
        GlyphRasterPoint point = GlyphRasterMath.Place(
            logicalOriginX: 40f,
            logicalOriginY: 20f,
            generatedX: 10f,
            generatedY: -12f,
            rasterScale: 1.25f);

        await Assert.That(point.X).IsEqualTo(60f);
        await Assert.That(point.Y).IsEqualTo(37f);
    }

    [Test]
    public async Task SnapsFractionalPhysicalOriginBeforeAddingGlyphOffset()
    {
        GlyphRasterPoint point = GlyphRasterMath.Place(
            logicalOriginX: 33f,
            logicalOriginY: 298f,
            generatedX: 8f,
            generatedY: 0f,
            rasterScale: 1.25f);

        await Assert.That(point.X).IsEqualTo(49f);
        await Assert.That(point.Y).IsEqualTo(373f);
    }
}
