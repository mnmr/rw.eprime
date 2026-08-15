namespace QualityJobs.Core.Tests;

using QualityJobs.Core;

public class ManagedJobsPolicyTests
{
    [Test]
    public async Task ForeverRepresentsOneAcceptedIteration()
    {
        int iterations = ManagedBillWorkload.Iterations(
            ManagedBillRepeat.Forever, repeatCount: 99,
            targetCount: 0, currentCount: 0, yieldPerIteration: 1);

        await Assert.That(iterations).IsEqualTo(1);
    }

    [Test]
    public async Task RepeatCountUsesTheRemainingCounter()
    {
        int iterations = ManagedBillWorkload.Iterations(
            ManagedBillRepeat.RepeatCount, repeatCount: 7,
            targetCount: 0, currentCount: 0, yieldPerIteration: 1);

        await Assert.That(iterations).IsEqualTo(7);
    }

    [Test]
    public async Task TargetCountRoundsTheShortfallUpByRecipeYield()
    {
        int iterations = ManagedBillWorkload.Iterations(
            ManagedBillRepeat.TargetCount, repeatCount: 0,
            targetCount: 10, currentCount: 3, yieldPerIteration: 3);

        await Assert.That(iterations).IsEqualTo(3);
    }

    [Test]
    public async Task SatisfiedTargetCountHasNoRemainingIterations()
    {
        int iterations = ManagedBillWorkload.Iterations(
            ManagedBillRepeat.TargetCount, repeatCount: 0,
            targetCount: 10, currentCount: 10, yieldPerIteration: 1);

        await Assert.That(iterations).IsEqualTo(0);
    }

    [Test]
    public async Task EqualIterationCountsFromDifferentModesAreDifferentSnapshotContent()
    {
        var forever = new ManagedBillCounter(
            ManagedBillRepeat.Forever, remainingAcceptedIterations: 1);
        var targetCount = new ManagedBillCounter(
            ManagedBillRepeat.TargetCount, remainingAcceptedIterations: 1);

        await Assert.That(forever.HasSameContent(targetCount)).IsFalse();
    }

    [Test]
    public async Task SuspendedManagedBillIsExcluded()
    {
        bool included = ManagedJobPolicy.IncludeBill(
            managed: true, suspended: true, paused: false,
            deleted: false, finishBill: false);

        await Assert.That(included).IsFalse();
    }

    [Test]
    public async Task ActiveManagedSourceBillIsIncluded()
    {
        bool included = ManagedJobPolicy.IncludeBill(
            managed: true, suspended: false, paused: false,
            deleted: false, finishBill: false);

        await Assert.That(included).IsTrue();
    }

    [Test]
    public async Task FinisherBillIsExcluded()
    {
        bool included = ManagedJobPolicy.IncludeBill(
            managed: true, suspended: false, paused: false,
            deleted: false, finishBill: true);

        await Assert.That(included).IsFalse();
    }

    [Test]
    public async Task ForbiddenConstructionTargetIsExcluded()
    {
        await Assert.That(ManagedJobPolicy.IncludeConstruction(
            forbidden: true, destroyed: false)).IsFalse();
    }

    [Test]
    public async Task LiveAllowedConstructionTargetIsIncluded()
    {
        await Assert.That(ManagedJobPolicy.IncludeConstruction(
            forbidden: false, destroyed: false)).IsTrue();
    }
}
