using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

/// Planned-work arithmetic: how many iterations a bill still owes, and how much
/// material a bill or an unfinished buildable takes off the displayed counter.
public class PlannedWorkMathTests
{
    [Test]
    public async Task QualityProbabilityConvertsToGeometricExpectedAttempts()
    {
        await Assert.That(PlannedWorkMath.ExpectedAttempts(0.25))
            .IsEqualTo(4f);
    }

    [Test]
    public async Task FailedQualityBuildCountsEveryRemainingRebuildAttempt()
    {
        await Assert.That(PlannedWorkMath.FailedBuildableDebt(
                fullCost: 100,
                expectedAttempts: 4f,
                returnedFraction: 0.5f))
            .IsEqualTo(200);
    }

    [Test]
    public async Task UnreachableQualityWithFullRefundOwesOnlyCurrentOutstanding()
    {
        await Assert.That(PlannedWorkMath.BuildableDebt(
                outstanding: 60,
                fullCost: 100,
                expectedAttempts: float.PositiveInfinity,
                returnedFraction: 1f))
            .IsEqualTo(60);
    }

    // ---- bill iterations ---------------------------------------------------

    [Test]
    public async Task ForeverBillOwesASingleIteration()
    {
        await Assert.That(PlannedWorkMath.BillIterations(
            BillRepeat.Forever, repeatCount: 0, targetCount: 0,
            produced: 0, yieldPerIteration: 1)).IsEqualTo(1);
    }

    [Test]
    public async Task ForeverBillIgnoresRepeatAndTargetFields()
    {
        await Assert.That(PlannedWorkMath.BillIterations(
            BillRepeat.Forever, repeatCount: 99, targetCount: 500,
            produced: 0, yieldPerIteration: 1)).IsEqualTo(1);
    }

    [Test]
    public async Task RepeatCountBillOwesItsRemainingCount()
    {
        await Assert.That(PlannedWorkMath.BillIterations(
            BillRepeat.RepeatCount, repeatCount: 7, targetCount: 0,
            produced: 0, yieldPerIteration: 1)).IsEqualTo(7);
    }

    [Test]
    public async Task RepeatCountBillAtZeroOwesNothing()
    {
        await Assert.That(PlannedWorkMath.BillIterations(
            BillRepeat.RepeatCount, repeatCount: 0, targetCount: 0,
            produced: 0, yieldPerIteration: 1)).IsEqualTo(0);
    }

    [Test]
    public async Task NegativeRepeatCountOwesNothing()
    {
        await Assert.That(PlannedWorkMath.BillIterations(
            BillRepeat.RepeatCount, repeatCount: -4, targetCount: 0,
            produced: 0, yieldPerIteration: 1)).IsEqualTo(0);
    }

    [Test]
    public async Task TargetCountBillOwesTheShortfall()
    {
        await Assert.That(PlannedWorkMath.BillIterations(
            BillRepeat.TargetCount, repeatCount: 0, targetCount: 50,
            produced: 20, yieldPerIteration: 1)).IsEqualTo(30);
    }

    [Test]
    public async Task SatisfiedTargetCountBillOwesNothing()
    {
        await Assert.That(PlannedWorkMath.BillIterations(
            BillRepeat.TargetCount, repeatCount: 0, targetCount: 50,
            produced: 50, yieldPerIteration: 1)).IsEqualTo(0);
    }

    [Test]
    public async Task OversatisfiedTargetCountBillOwesNothing()
    {
        await Assert.That(PlannedWorkMath.BillIterations(
            BillRepeat.TargetCount, repeatCount: 0, targetCount: 50,
            produced: 90, yieldPerIteration: 1)).IsEqualTo(0);
    }

    [Test]
    public async Task TargetCountBillDividesTheShortfallByTheYield()
    {
        // 30 short at 4 products per run: 8 runs (the last one overshoots).
        await Assert.That(PlannedWorkMath.BillIterations(
            BillRepeat.TargetCount, repeatCount: 0, targetCount: 50,
            produced: 20, yieldPerIteration: 4)).IsEqualTo(8);
    }

    [Test]
    public async Task NonPositiveYieldIsTreatedAsOnePerIteration()
    {
        await Assert.That(PlannedWorkMath.BillIterations(
            BillRepeat.TargetCount, repeatCount: 0, targetCount: 9,
            produced: 0, yieldPerIteration: 0)).IsEqualTo(9);
    }

    [Test]
    public async Task IterationsAreCappedSoAnAbsurdTargetCannotDominate()
    {
        await Assert.That(PlannedWorkMath.BillIterations(
            BillRepeat.RepeatCount, repeatCount: int.MaxValue, targetCount: 0,
            produced: 0, yieldPerIteration: 1))
            .IsEqualTo(PlannedWorkMath.MaxIterationsPerBill);
    }

    // ---- bill debt ---------------------------------------------------------

    [Test]
    public async Task BillDebtIsCostTimesIterations()
    {
        await Assert.That(PlannedWorkMath.BillDebt(
            perIterationCost: 15, iterations: 4, expectedAttempts: 1f))
            .IsEqualTo(60);
    }

    [Test]
    public async Task BillDebtScalesWithExpectedAttempts()
    {
        // Two attempts per delivered product doubles the ingredient draw.
        await Assert.That(PlannedWorkMath.BillDebt(
            perIterationCost: 15, iterations: 4, expectedAttempts: 2f))
            .IsEqualTo(120);
    }

    [Test]
    public async Task BillDebtRoundsFractionalAttemptsUp()
    {
        await Assert.That(PlannedWorkMath.BillDebt(
            perIterationCost: 10, iterations: 1, expectedAttempts: 2.4f))
            .IsEqualTo(24);
        await Assert.That(PlannedWorkMath.BillDebt(
            perIterationCost: 10, iterations: 1, expectedAttempts: 2.41f))
            .IsEqualTo(25);
    }

    [Test]
    public async Task BillDebtIsZeroWithoutIterations()
    {
        await Assert.That(PlannedWorkMath.BillDebt(
            perIterationCost: 15, iterations: 0, expectedAttempts: 3f))
            .IsEqualTo(0);
    }

    [Test]
    public async Task BillDebtIsNeverNegative()
    {
        await Assert.That(PlannedWorkMath.BillDebt(
            perIterationCost: -5, iterations: 3, expectedAttempts: 1f))
            .IsEqualTo(0);
    }

    [Test]
    public async Task BillDebtTreatsAttemptsBelowOneAsOne()
    {
        await Assert.That(PlannedWorkMath.BillDebt(
            perIterationCost: 10, iterations: 2, expectedAttempts: 0.25f))
            .IsEqualTo(20);
    }

    // ---- buildable debt ----------------------------------------------------

    [Test]
    public async Task BuildableDebtIsTheOutstandingDeliveryWithoutRework()
    {
        // 40 of 100 steel already hauled into the frame.
        await Assert.That(PlannedWorkMath.BuildableDebt(
            outstanding: 60, fullCost: 100,
            expectedAttempts: 1f, returnedFraction: 0.5f)).IsEqualTo(60);
    }

    [Test]
    public async Task BuildableReworkAddsTheUnrecoveredShareOfEachRebuild()
    {
        // Two attempts: one extra full build at 100, half of which comes back
        // out of the deconstruct. Net extra 50 on top of the outstanding 60.
        await Assert.That(PlannedWorkMath.BuildableDebt(
            outstanding: 60, fullCost: 100,
            expectedAttempts: 2f, returnedFraction: 0.5f)).IsEqualTo(110);
    }

    [Test]
    public async Task FullyRecoveredRebuildsAddNothing()
    {
        await Assert.That(PlannedWorkMath.BuildableDebt(
            outstanding: 60, fullCost: 100,
            expectedAttempts: 4f, returnedFraction: 1f)).IsEqualTo(60);
    }

    [Test]
    public async Task UnrecoverableRebuildsChargeTheFullCostEachTime()
    {
        await Assert.That(PlannedWorkMath.BuildableDebt(
            outstanding: 60, fullCost: 100,
            expectedAttempts: 3f, returnedFraction: 0f)).IsEqualTo(260);
    }

    [Test]
    public async Task BuildableDebtRoundsUp()
    {
        // 60 + 1.5 extra builds * 100 * 0.25 unrecovered = 97.5 -> 98.
        await Assert.That(PlannedWorkMath.BuildableDebt(
            outstanding: 60, fullCost: 100,
            expectedAttempts: 2.5f, returnedFraction: 0.75f)).IsEqualTo(98);
    }

    [Test]
    public async Task BuildableDebtClampsTheReturnedFractionToUnitRange()
    {
        await Assert.That(PlannedWorkMath.BuildableDebt(
            outstanding: 10, fullCost: 100,
            expectedAttempts: 2f, returnedFraction: 1.8f)).IsEqualTo(10);
        await Assert.That(PlannedWorkMath.BuildableDebt(
            outstanding: 10, fullCost: 100,
            expectedAttempts: 2f, returnedFraction: -0.4f)).IsEqualTo(110);
    }

    [Test]
    public async Task BuildableDebtIsNeverNegative()
    {
        await Assert.That(PlannedWorkMath.BuildableDebt(
            outstanding: -20, fullCost: 100,
            expectedAttempts: 1f, returnedFraction: 0.5f)).IsEqualTo(0);
    }

    [Test]
    public async Task BuildableDebtTreatsAttemptsBelowOneAsOne()
    {
        await Assert.That(PlannedWorkMath.BuildableDebt(
            outstanding: 60, fullCost: 100,
            expectedAttempts: 0.1f, returnedFraction: 0.5f)).IsEqualTo(60);
    }

    [Test]
    public async Task CarriedStackCreditsEachDestinationOnlyUpToItsOutstandingNeed()
    {
        var carried = new CappedMaterialCredit(60);

        int firstBlueprint = carried.Take(outstanding: 20);
        int queuedBlueprint = carried.Take(outstanding: 40);

        await Assert.That(firstBlueprint).IsEqualTo(20);
        await Assert.That(queuedBlueprint).IsEqualTo(40);
        await Assert.That(carried.Remaining).IsEqualTo(0);
    }

    [Test]
    public async Task ConstructionHaulPreservesPrimaryNeedBeforeCreditingCurrentTarget()
    {
        CappedMaterialCredit remainder = PlannedWorkMath.AllocateConstructionHaul(
            carried: 50,
            primaryOutstanding: 40,
            currentOutstanding: 40,
            out int primaryCredit,
            out int currentCredit);

        await Assert.That(primaryCredit).IsEqualTo(40);
        await Assert.That(currentCredit).IsEqualTo(10);
        await Assert.That(remainder.Remaining).IsEqualTo(0);
    }

    [Test]
    public async Task ConstructionHaulChoosesNearestEligibleQueuedDestination()
    {
        var selection = new ClosestPlannedDestination();
        selection.Consider(index: 0, distanceSquared: 100f, eligible: true);
        selection.Consider(index: 1, distanceSquared: 9f, eligible: true);
        selection.Consider(index: 2, distanceSquared: 1f, eligible: false);

        await Assert.That(selection.Index).IsEqualTo(1);
    }
}
