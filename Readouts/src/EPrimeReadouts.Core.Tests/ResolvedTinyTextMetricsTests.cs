using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

public class ResolvedTinyTextMetricsTests
{
    [Test]
    public async Task SubstitutedSmallFontRaisesUndersizedContainers()
    {
        var metrics = new ResolvedTinyTextMetrics(
            measuredLineHeight: 21.2f,
            substitutedSmall: true);

        await Assert.That(metrics.LineHeight).IsEqualTo(22f);
        await Assert.That(metrics.MinHeight(16f)).IsEqualTo(22f);
        await Assert.That(metrics.MinHeight(28f)).IsEqualTo(28f);
    }

    [Test]
    public async Task SubstitutedSmallFontUsesTwoPixelCaptionOffset()
    {
        var metrics = new ResolvedTinyTextMetrics(
            measuredLineHeight: 22f,
            substitutedSmall: true);

        await Assert.That(metrics.CaptionOffsetY).IsEqualTo(2f);
    }

    [Test]
    public async Task NativeTinyFontKeepsItsMeasuredHeightWithoutOffset()
    {
        var metrics = new ResolvedTinyTextMetrics(
            measuredLineHeight: 15.1f,
            substitutedSmall: false);

        await Assert.That(metrics.LineHeight).IsEqualTo(16f);
        await Assert.That(metrics.CaptionOffsetY).IsEqualTo(0f);
    }
}
