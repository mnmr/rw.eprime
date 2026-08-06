namespace QualityJobs.Core.Tests;

using QualityJobs.Core;

public class TipBalancePolicyTests
{
    [Test]
    public async Task WideShortTipNarrowsTowardTheSqrtAreaTarget()
    {
        // A 500px-wide two-liner (two Small lines ≈ 44px) rebalances to
        // roughly 300px: 2·√(500·44) = 296.65, ceiled.
        await Assert.That(TipBalancePolicy.BalancedWidth(500f, 44f, 0f)).IsEqualTo(297f);
    }

    [Test]
    public async Task NarrowingStopsAtMinWidth()
    {
        // 2·√(400·22) = 187.6 → ceil 188, below the 280 minimum.
        await Assert.That(TipBalancePolicy.BalancedWidth(400f, 22f, 0f)).IsEqualTo(280f);
    }

    [Test]
    public async Task ThePolicyNeverWidens()
    {
        // Natural width already below MinWidth: stays at natural width.
        await Assert.That(TipBalancePolicy.BalancedWidth(200f, 22f, 0f)).IsEqualTo(200f);
    }

    [Test]
    public async Task AlreadyTallTipsKeepTheirWidth()
    {
        // 2·√(300·600) = 848.5: the target exceeds the width, so no change.
        await Assert.That(TipBalancePolicy.BalancedWidth(300f, 600f, 0f)).IsEqualTo(300f);
    }

    [Test]
    public async Task NonWrappableContentFloorsTheNarrowing()
    {
        // Target would be 2·√(600·44) = 325, but a 500px table cannot wrap.
        await Assert.That(TipBalancePolicy.BalancedWidth(600f, 44f, 500f)).IsEqualTo(500f);
    }

    [Test]
    public async Task AFloorAboveTheWidthStillNeverWidens()
    {
        await Assert.That(TipBalancePolicy.BalancedWidth(300f, 44f, 400f)).IsEqualTo(300f);
    }

    [Test]
    public async Task DegenerateSizesPassThroughUnchanged()
    {
        await Assert.That(TipBalancePolicy.BalancedWidth(0f, 44f, 0f)).IsEqualTo(0f);
        await Assert.That(TipBalancePolicy.BalancedWidth(300f, 0f, 0f)).IsEqualTo(300f);
    }
}
