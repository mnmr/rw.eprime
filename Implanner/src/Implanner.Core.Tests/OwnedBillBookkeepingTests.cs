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

        await Assert.That(model.SetOwnedBill(7, "i1:0", "Bill_InstallBionicLeg_5"))
            .IsEqualTo(PlannerChange.Surgery);
        await Assert.That(model.SetOwnedBill(7, "i1:0", "Bill_InstallBionicLeg_5"))
            .IsEqualTo(PlannerChange.None);
        // A recreated bill replaces the record.
        await Assert.That(model.SetOwnedBill(7, "i1:0", "Bill_InstallBionicLeg_9"))
            .IsEqualTo(PlannerChange.Surgery);
        await Assert.That(model.OwnedBill(7, "i1:0")).IsEqualTo("Bill_InstallBionicLeg_9");

        await Assert.That(model.RemoveOwnedBill(7, "i1:0")).IsEqualTo(PlannerChange.Surgery);
        await Assert.That(model.OwnedBill(7, "i1:0")).IsNull();
        await Assert.That(model.RemoveOwnedBill(7, "i1:0")).IsEqualTo(PlannerChange.None);
    }

    [Test]
    public async Task CleanupDropsOwnedBillsOfUnassignedPawns()
    {
        var model = new PlannerModel();
        int next = 1;
        var plan = model.CreatePlan("Test", () => next++)!;
        model.AssignPlan(7, plan.Id);
        model.SetOwnedBill(7, "i1:0", "Bill_A_1");
        model.SetOwnedBill(9, "i1:0", "Bill_B_2"); // pawn 9 has no assignment

        var change = model.CleanupMissing(pawnExists: _ => true);

        await Assert.That((change & PlannerChange.Surgery) != 0).IsTrue();
        await Assert.That(model.OwnedBill(7, "i1:0")).IsEqualTo("Bill_A_1");
        await Assert.That(model.OwnedBill(9, "i1:0")).IsNull();
    }
}
