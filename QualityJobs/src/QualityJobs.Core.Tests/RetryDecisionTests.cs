namespace QualityJobs.Core.Tests;

using QualityJobs.Core;

public class RetryDecisionTests
{
    [Test]
    public async Task BelowMinimumRetries()
    {
        await Assert.That(RetryDecision.ShouldRetry(managed: true,
            QualityLevel.Good, QualityLevel.Masterwork)).IsTrue();
    }

    [Test]
    public async Task AtOrAboveMinimumKeeps()
    {
        await Assert.That(RetryDecision.ShouldRetry(managed: true,
            QualityLevel.Masterwork, QualityLevel.Masterwork)).IsFalse();
        await Assert.That(RetryDecision.ShouldRetry(managed: true,
            QualityLevel.Legendary, QualityLevel.Masterwork)).IsFalse();
    }

    /// Unchecking "Manage this bill" must restore vanilla completion even when
    /// a target quality (per bill or per-save default) is still stored.
    [Test]
    public async Task UnmanagedNeverRetriesBelowMinimum()
    {
        await Assert.That(RetryDecision.ShouldRetry(managed: false,
            QualityLevel.Awful, QualityLevel.Masterwork)).IsFalse();
    }
}
