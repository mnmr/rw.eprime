using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

public class HolderScaleTests
{
    [Test]
    public async Task BandOfMapsThreeColonistsPerBandWithOpenTop()
    {
        await Assert.That(HolderScale.BandOf(1)).IsEqualTo(0);
        await Assert.That(HolderScale.BandOf(3)).IsEqualTo(0);
        await Assert.That(HolderScale.BandOf(4)).IsEqualTo(1);
        await Assert.That(HolderScale.BandOf(33)).IsEqualTo(10);
        await Assert.That(HolderScale.BandOf(34)).IsEqualTo(11);
        await Assert.That(HolderScale.BandOf(500)).IsEqualTo(11);
        await Assert.That(HolderScale.BandOf(0)).IsEqualTo(0);
    }

    [Test]
    public async Task NormalizeClampsPerBandAndKeepsBandsIndependent()
    {
        var scale = new HolderScale();
        scale.Min[0] = 2;
        scale.Min[1] = 1;      // dips below band 0: stays 1 (bands independent)
        scale.Min[2] = -3;     // negative: clamped to 0
        scale.Train[0] = 5;    // above min: capped at 2
        scale.Train[1] = -1;   // negative: clamped to 0
        scale.Max[0] = 1;      // below min: raised to 2
        scale.Normalize();

        await Assert.That(scale.Min[1]).IsEqualTo(1);
        await Assert.That(scale.Min[2]).IsEqualTo(0);
        await Assert.That(scale.Train[0]).IsEqualTo(2);
        await Assert.That(scale.Train[1]).IsEqualTo(0);
        await Assert.That(scale.Max[0]).IsEqualTo(2);
        // Untouched bands stay uncapped and are never raised to min.
        await Assert.That(scale.Max[5]).IsEqualTo(RoleHolderRange.Uncapped);
    }

    [Test]
    public async Task CopyIsIndependentAndSameValuesIgnoresName()
    {
        var scale = new HolderScale { Name = "Doctors" };
        scale.Min[3] = 4;
        HolderScale copy = scale.Copy();
        copy.Name = "Renamed";
        await Assert.That(copy.SameValuesAs(scale)).IsTrue();
        copy.Min[3] = 5;
        await Assert.That(copy.SameValuesAs(scale)).IsFalse();
        await Assert.That(scale.Min[3]).IsEqualTo(4);
    }
}
