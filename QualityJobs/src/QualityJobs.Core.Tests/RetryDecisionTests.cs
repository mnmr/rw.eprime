namespace QualityJobs.Core.Tests;

using QualityJobs.Core;

public class RetryDecisionTests
{
    [Test]
    public async Task BelowMinimumRetries()
    {
        await Assert.That(RetryDecision.ShouldRetry(
            QualityLevel.Good, QualityLevel.Masterwork)).IsTrue();
    }

    [Test]
    public async Task AtOrAboveMinimumKeeps()
    {
        await Assert.That(RetryDecision.ShouldRetry(
            QualityLevel.Masterwork, QualityLevel.Masterwork)).IsFalse();
        await Assert.That(RetryDecision.ShouldRetry(
            QualityLevel.Legendary, QualityLevel.Masterwork)).IsFalse();
    }
}
