using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

public class Rgba32MathTests
{
    [Test]
    public async Task HalfAlphaColorRoundTripsThroughPremultiplication()
    {
        PixelRgba premultiplied = Rgba32Math.Premultiply(200, 100, 50, 128);

        await Assert.That(premultiplied)
            .IsEqualTo(new PixelRgba(100, 50, 25, 128));
        await Assert.That(Rgba32Math.Unpremultiply(
                premultiplied.R, premultiplied.G,
                premultiplied.B, premultiplied.A))
            .IsEqualTo(new PixelRgba(199, 100, 50, 128));
    }

    [Test]
    public async Task TransparentPixelUnpremultipliesToClearBlack()
    {
        await Assert.That(Rgba32Math.Unpremultiply(91, 72, 53, 0))
            .IsEqualTo(new PixelRgba(0, 0, 0, 0));
    }

    [Test]
    public async Task OpaqueSourceCompletelyReplacesDestination()
    {
        var source = new PixelRgba(13, 27, 99, 255);
        var destination = new PixelRgba(201, 155, 33, 255);

        await Assert.That(Rgba32Math.SourceOver(source, destination))
            .IsEqualTo(source);
    }

    [Test]
    public async Task HalfTransparentSourceBlendsOverOpaqueDestination()
    {
        var source = new PixelRgba(200, 100, 50, 128);
        var destination = new PixelRgba(20, 40, 80, 255);

        await Assert.That(Rgba32Math.SourceOver(source, destination))
            .IsEqualTo(new PixelRgba(110, 70, 65, 255));
    }
}
