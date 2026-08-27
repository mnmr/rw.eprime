namespace QualityJobs.Core.Tests;

using QualityJobs.Core;

public class StockPolicyTests
{
    [Test]
    public async Task BelowCapAllowsNewStart()
    {
        await Assert.That(StockPolicy.CanStartNewItem(spawnedUftCount: 2, cap: 3,
            isFinishBill: false)).IsTrue();
    }

    [Test]
    public async Task AtOrOverCapBlocksNewStart()
    {
        await Assert.That(StockPolicy.CanStartNewItem(3, 3, false)).IsFalse();
        await Assert.That(StockPolicy.CanStartNewItem(4, 3, false)).IsFalse();
    }

    [Test]
    public async Task FinishBillsAreExempt()
    {
        await Assert.That(StockPolicy.CanStartNewItem(99, 3, isFinishBill: true)).IsTrue();
    }

    [Test]
    public async Task ZeroCapMeansUnlimited()
    {
        await Assert.That(StockPolicy.CanStartNewItem(99, 0, false)).IsTrue();
    }

    [Test]
    public async Task NegativeCapMeansUnlimited()
    {
        await Assert.That(StockPolicy.CanStartNewItem(99, -1, false)).IsTrue();
    }
}
