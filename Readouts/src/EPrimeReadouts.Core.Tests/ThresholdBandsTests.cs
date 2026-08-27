using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

public class ThresholdBandsTests
{
    [Test]
    public async Task BandsSplitAtLowAndCritical()
    {
        var spec = new ThresholdSpec(100, 20);
        await Assert.That(ThresholdBands.For(101, spec)).IsEqualTo(Band.Normal);
        await Assert.That(ThresholdBands.For(100, spec)).IsEqualTo(Band.Low);
        await Assert.That(ThresholdBands.For(21, spec)).IsEqualTo(Band.Low);
        await Assert.That(ThresholdBands.For(20, spec)).IsEqualTo(Band.Critical);
        await Assert.That(ThresholdBands.For(0, spec)).IsEqualTo(Band.Critical);
    }
}
