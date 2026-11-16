namespace WorkRoles.Core.Tests.UI;

public class TraitRarityTiersTests
{
    [Test]
    [Arguments(0f, 1)]
    [Arguments(0.1f, 1)]
    [Arguments(0.2f, 2)]
    [Arguments(0.5f, 2)]
    [Arguments(0.9f, 3)]
    [Arguments(1f, 4)]
    [Arguments(1.9f, 4)]
    [Arguments(2f, 5)]
    [Arguments(2.5f, 6)]
    public async Task TierFollowsTheTraitRarityColorsDefaultLimits(float commonality, int tier)
    {
        await Assert.That(TraitRarityTiers.Tier(commonality)).IsEqualTo(tier);
    }

    [Test]
    public async Task GenderSpecificCommonalityIsAveraged()
    {
        await Assert.That(TraitRarityTiers.Commonality(1f, 1f, 1f)).IsEqualTo(1f);
        await Assert.That(TraitRarityTiers.Commonality(1f, 0.2f, 0.6f)).IsEqualTo(0.4f);
    }
}
