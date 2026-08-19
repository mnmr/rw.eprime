using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

public class IconScaleMathTests
{
    [Test]
    public async Task PartialOpaqueExtentUsesFractionalCoverage()
    {
        float correction = IconScaleMath.CorrectionFor(
            opaqueExtent: 27,
            sampleSize: 34,
            vanillaDrawScale: 1f);

        await Assert.That(correction).IsGreaterThan(1.10f);
        await Assert.That(correction).IsLessThan(1.12f);
    }
}
