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
    public async Task ScaleRequiredTotalIsBandLookupCappedByMaxAndColony()
    {
        var scale = new HolderScale();
        for (int i = 0; i < HolderScale.Bands; i++)
        {
            scale.RequiredTotals[i] = i + 1;
            scale.Max[i] = 10;      // flat cap
        }
        var role = RecsTestBed.Role(1, "Cooking");
        role.Scale = scale;
        var scaling = new RecommendationScaling(
            RecommendationsTuningOptions.Default);

        await Assert.That(scaling.Requirement(role, 3).RequiredTotal).IsEqualTo(1);
        await Assert.That(scaling.Requirement(role, 12).RequiredTotal).IsEqualTo(4);
        await Assert.That(scaling.Requirement(role, 34).RequiredTotal).IsEqualTo(10);
        await Assert.That(scaling.Requirement(role, 2).RequiredTotal).IsEqualTo(1);
    }

    [Test]
    public async Task RolesWithoutScalesUseTheDefaultConfiguredFormula()
    {
        var role = RecsTestBed.Role(1, "Cooking");
        role.RequiredTotal = 2;
        var scaling = new RecommendationScaling(
            RecommendationsTuningOptions.Default);
        await Assert.That(scaling.Requirement(role, 12).RequiredTotal).IsEqualTo(4);
    }
}
