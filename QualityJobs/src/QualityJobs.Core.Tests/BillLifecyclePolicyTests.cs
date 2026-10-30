namespace QualityJobs.Core.Tests;

using QualityJobs.Core;

public class BillLifecyclePolicyTests
{
    [Test]
    public async Task PreparedWorkReservesTheLastRequestedIterationUntilCompletionOrCancellation()
    {
        // Recorded native QJ007 reproduction: Do 1 prepared two full-material
        // tuques. Admission must close BEFORE the second ingredient collection.
        await Assert.That(BillLifecyclePolicy.CanStartPreparation(true, true, 1, 0, false)).IsTrue();
        await Assert.That(BillLifecyclePolicy.CanStartPreparation(true, true, 1, 1, false)).IsFalse();
        // Raising Do 1 to Do 2 permits exactly one further preparation.
        await Assert.That(BillLifecyclePolicy.CanStartPreparation(true, true, 2, 1, false)).IsTrue();
        await Assert.That(BillLifecyclePolicy.CanStartPreparation(true, true, 2, 2, false)).IsFalse();
        // Cancelling the prepared item releases the slot, without decrementing
        // the bill. A successful completion consumes the requested iteration.
        await Assert.That(BillLifecyclePolicy.CanStartPreparation(true, true, 1, 0, false)).IsTrue();
        SourceBillCompletion completed = BillLifecyclePolicy.CompleteSource(1, true, true, false, false);
        await Assert.That(BillLifecyclePolicy.CanStartPreparation(true, true, completed.RepeatCount, 0, false)).IsFalse();
        await Assert.That(BillLifecyclePolicy.CanStartPreparation(true, false, 1, 2, false)).IsTrue();
        await Assert.That(BillLifecyclePolicy.CanStartPreparation(false, true, 1, 2, false)).IsTrue();
    }

    [Test]
    public async Task FinishingOrderNeverCollectsANewIngredientBatchAfterItsItemDisappears()
    {
        // QJ006: cancellation removes the bound UFT before native job selection.
        // This invariant applies even if the inherited source is unmanaged.
        await Assert.That(BillLifecyclePolicy.CanStartPreparation(true, true, 1, 0, true)).IsFalse();
        await Assert.That(BillLifecyclePolicy.CanStartPreparation(false, true, 1, 0, true)).IsFalse();
    }

    [Test]
    public async Task AliasedFinishBillIsNotADeletionCandidate()
    {
        await Assert.That(BillLifecyclePolicy.CanDeleteFinishBill(aliasesSource: true)).IsFalse();
    }

    [Test]
    public async Task DistinctFinishBillIsADeletionCandidate()
    {
        await Assert.That(BillLifecyclePolicy.CanDeleteFinishBill(aliasesSource: false)).IsTrue();
    }

    [Test]
    [Arguments(false, false, true)]
    [Arguments(false, true, false)]
    [Arguments(true, false, false)]
    [Arguments(true, true, false)]
    public async Task VanillaCompletionRunsOnlyForAcceptedOrdinaryBills(
        bool isFinishBill, bool retry, bool expected)
    {
        await Assert.That(BillLifecyclePolicy.ShouldRunVanillaCompletion(
            isFinishBill, retry)).IsEqualTo(expected);
    }

    [Test]
    public async Task AcceptedFinalSourceIterationDecrementsAndNotifies()
    {
        SourceBillCompletion result = BillLifecyclePolicy.CompleteSource(
            repeatCount: 1,
            sourceAvailable: true,
            repeatCountMode: true,
            aliasesFinishBill: false,
            retry: false);

        await Assert.That(result.RepeatCount).IsEqualTo(0);
        await Assert.That(result.NotifyCompletion).IsTrue();
    }

    [Test]
    public async Task AcceptedNonFinalSourceIterationDecrementsWithoutNotification()
    {
        SourceBillCompletion result = BillLifecyclePolicy.CompleteSource(
            repeatCount: 2,
            sourceAvailable: true,
            repeatCountMode: true,
            aliasesFinishBill: false,
            retry: false);

        await Assert.That(result.RepeatCount).IsEqualTo(1);
        await Assert.That(result.NotifyCompletion).IsFalse();
    }

    [Test]
    [Arguments(1, true, true, false, true)]
    [Arguments(1, true, true, true, false)]
    [Arguments(1, false, true, false, false)]
    [Arguments(1, true, false, false, false)]
    [Arguments(0, true, true, false, false)]
    public async Task IneligibleSourceCompletionLeavesCounterAndMessageUnchanged(
        int repeatCount, bool sourceAvailable, bool repeatCountMode,
        bool aliasesFinishBill, bool retry)
    {
        SourceBillCompletion result = BillLifecyclePolicy.CompleteSource(
            repeatCount, sourceAvailable, repeatCountMode, aliasesFinishBill, retry);

        await Assert.That(result.RepeatCount).IsEqualTo(repeatCount);
        await Assert.That(result.NotifyCompletion).IsFalse();
    }
}
