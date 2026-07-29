using WorkRoles.Core;
using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests;

public class HolderScaleScalingTests
{
    [Test]
    public async Task CodecRoundTripsAndDecodesLeniently()
    {
        var values = new int[HolderScale.Bands];
        for (int i = 0; i < values.Length; i++) values[i] = i;
        string encoded = HolderScaleCodec.EncodeRow(values);
        await Assert.That(encoded).IsEqualTo("0,1,2,3,4,5,6,7,8,9,10,11");
        int[] decoded = HolderScaleCodec.DecodeRow(encoded, fallback: 0);
        await Assert.That(string.Join(",", decoded)).IsEqualTo(encoded);

        // Short rows extend flat from the last value; garbage falls back.
        int[] extended = HolderScaleCodec.DecodeRow("1,2", fallback: 0);
        await Assert.That(extended[1]).IsEqualTo(2);
        await Assert.That(extended[11]).IsEqualTo(2);
        int[] fallback = HolderScaleCodec.DecodeRow("x", fallback: 7);
        await Assert.That(fallback[0]).IsEqualTo(7);
    }

    [Test]
    public async Task ScaleWantIsDirectBandLookupCappedByMaxAndColony()
    {
        var scale = new HolderScale();
        for (int i = 0; i < HolderScale.Bands; i++)
        {
            scale.Min[i] = i + 1;   // band 0 wants 1 ... band 11 wants 12
            scale.Max[i] = 10;      // flat cap
        }
        var role = RecsTestBed.Role(1, "Cooking");
        role.Scale = scale;
        var scaling = new UnitScaling();

        await Assert.That(scaling.Want(role, 3)).IsEqualTo(1);   // band 0
        await Assert.That(scaling.Want(role, 12)).IsEqualTo(4);  // band 3
        await Assert.That(scaling.Want(role, 34)).IsEqualTo(10); // band 11 wants 12, max caps at 10
        await Assert.That(scaling.Want(role, 2)).IsEqualTo(1);   // colony floor keeps min(colony, want)
    }

    [Test]
    public async Task RolesWithoutScalesKeepTheLegacyFormula()
    {
        var role = RecsTestBed.Role(1, "Cooking");
        role.MinHolders = 2;
        var scaling = new UnitScaling();
        await Assert.That(scaling.Want(role, 12)).IsEqualTo(4); // 2 per 6-unit, legacy
    }
}
