using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

public class TierBlobCodecTests
{
    [Test]
    public async Task RoundTripsMultiTierLayout()
    {
        var tiers = new List<List<string>>
        {
            new() { "Steel", "WoodLog" },
            new() { "Silver" },
        };
        var decoded = TierBlobCodec.Decode(TierBlobCodec.Encode(tiers));
        await Assert.That(decoded.Count).IsEqualTo(2);
        await Assert.That(string.Join(",", decoded[0])).IsEqualTo("Steel,WoodLog");
        await Assert.That(string.Join(",", decoded[1])).IsEqualTo("Silver");
    }

    [Test]
    public async Task EmptyLayoutRoundTripsToEmpty()
    {
        await Assert.That(TierBlobCodec.Encode(new List<List<string>>())).IsEqualTo("");
        await Assert.That(TierBlobCodec.Decode("").Count).IsEqualTo(0);
        await Assert.That(TierBlobCodec.Decode(null).Count).IsEqualTo(0);
        await Assert.That(TierBlobCodec.Encode(null)).IsEqualTo("");
    }

    [Test]
    public async Task DecodeDropsEmptyNamesAndTiers()
    {
        var decoded = TierBlobCodec.Decode("Steel,,WoodLog||Silver");
        await Assert.That(decoded.Count).IsEqualTo(2);
        await Assert.That(string.Join(",", decoded[0])).IsEqualTo("Steel,WoodLog");
        await Assert.That(string.Join(",", decoded[1])).IsEqualTo("Silver");
    }
}
