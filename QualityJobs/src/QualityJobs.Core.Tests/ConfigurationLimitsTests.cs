using QualityJobs.Core;

namespace QualityJobs.Core.Tests;

public class ConfigurationLimitsTests
{
    [Test]
    [Arguments(-1, 0)]
    [Arguments(0, 0)]
    [Arguments(15, 15)]
    [Arguments(20, 20)]
    [Arguments(21, 20)]
    public async Task SkillThresholdIsNormalizedToTheGameRange(int input, int expected)
    {
        await Assert.That(ConfigurationLimits.Skill(input)).IsEqualTo(expected);
    }

    [Test]
    [Arguments(-1, 0)]
    [Arguments(0, 0)]
    [Arguments(6, 6)]
    [Arguments(7, 6)]
    public async Task QualityTargetIsNormalizedToTheQualityRange(int input, int expected)
    {
        await Assert.That(ConfigurationLimits.Quality(input)).IsEqualTo(expected);
    }

    [Test]
    [Arguments(-1, 0)]
    [Arguments(0, 0)]
    [Arguments(50, 50)]
    [Arguments(51, 50)]
    public async Task StockCapIsNormalizedToTheSupportedRange(int input, int expected)
    {
        await Assert.That(ConfigurationLimits.StockCap(input)).IsEqualTo(expected);
    }
}
