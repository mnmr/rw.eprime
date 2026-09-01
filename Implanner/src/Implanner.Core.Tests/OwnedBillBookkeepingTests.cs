using Implanner.Core;

namespace Implanner.Core.Tests;

/// Implanner-owned operation-bill bookkeeping: exact change reporting and
/// lifecycle cleanup alongside reservations.
public class OwnedBillBookkeepingTests
{
    [Test]
    public async Task SetAndRemoveOwnedBillsAreNoOpSafe()
    {
        var model = new PlannerModel();

        await Assert.That(model.SetOwnedBill(7, "p1:BionicLeg:0", "Bill_InstallBionicLeg_5"))
            .IsEqualTo(PlannerChange.Surgery);
        await Assert.That(model.SetOwnedBill(7, "p1:BionicLeg:0", "Bill_InstallBionicLeg_5"))
            .IsEqualTo(PlannerChange.None);
        // A recreated bill replaces the record.
        await Assert.That(model.SetOwnedBill(7, "p1:BionicLeg:0", "Bill_InstallBionicLeg_9"))
            .IsEqualTo(PlannerChange.Surgery);
        await Assert.That(model.OwnedBill(7, "p1:BionicLeg:0")).IsEqualTo("Bill_InstallBionicLeg_9");

        await Assert.That(model.RemoveOwnedBill(7, "p1:BionicLeg:0")).IsEqualTo(PlannerChange.Surgery);
        await Assert.That(model.OwnedBill(7, "p1:BionicLeg:0")).IsNull();
        await Assert.That(model.RemoveOwnedBill(7, "p1:BionicLeg:0")).IsEqualTo(PlannerChange.None);
    }

    /// A bill record is the only link between the model and the
    /// Bill_Medical object the game keeps on the pawn. Load cleanup may
    /// drop the record only when the pawn itself is gone: a present pawn
    /// that merely lost its assignment keeps the record, so the reconcile
    /// sweep can still find and delete the bill object together with it
    /// (unassign while paused, save, reload must not leave an orphaned
    /// operation on the pawn).
    [Test]
    public async Task CleanupKeepsBillRecordsOfPresentPawnsAndDropsDepartedOnes()
    {
        var model = new PlannerModel();
        int next = 1;
        var plan = model.CreatePlan("Test", () => next++)!;
        model.AssignPlan(7, plan.Id);
        model.SetOwnedBill(7, "p1:BionicLeg:0", "Bill_A_1");
        model.SetOwnedBill(9, "p1:BionicLeg:0", "Bill_B_2"); // present, unassigned
        model.SetOwnedBill(11, "p1:BionicLeg:0", "Bill_C_3"); // no longer exists

        var change = model.CleanupMissing(pawnExists: id => id != 11);

        await Assert.That((change & PlannerChange.Surgery) != 0).IsTrue();
        await Assert.That(model.OwnedBill(7, "p1:BionicLeg:0")).IsEqualTo("Bill_A_1");
        await Assert.That(model.OwnedBill(9, "p1:BionicLeg:0")).IsEqualTo("Bill_B_2");
        await Assert.That(model.OwnedBill(11, "p1:BionicLeg:0")).IsNull();
        await Assert.That(model.OwnedBills.ContainsKey(11)).IsFalse();
    }
}
