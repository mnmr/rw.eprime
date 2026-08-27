using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

public class GlyphRasterMathTests
{
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
