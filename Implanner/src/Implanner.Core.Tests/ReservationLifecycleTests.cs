using Implanner.Core;

namespace Implanner.Core.Tests;

/// Logical implant-item reservations: no-op-safe reserve/release change
/// reporting and lifecycle cleanup when the designated pawn loses its
/// assignment.
public class ReservationLifecycleTests
{
    [Test]
    public async Task ReservationsAreNoOpSafeAndReleasable()
    {
        var model = new PlannerModel();

        await Assert.That(model.Reserve(100, 7, "i1:0")).IsEqualTo(PlannerChange.Reservations);
        await Assert.That(model.Reserve(100, 7, "i1:0")).IsEqualTo(PlannerChange.None);
        await Assert.That(model.Reserve(100, 8, "i1:0")).IsEqualTo(PlannerChange.Reservations);
        await Assert.That(model.ReleaseReservation(100)).IsEqualTo(PlannerChange.Reservations);
        await Assert.That(model.ReleaseReservation(100)).IsEqualTo(PlannerChange.None);
    }

    [Test]
    public async Task CleanupDropsLatchesAndReservationsOfUnassignedPawns()
    {
        var model = new PlannerModel();
        int next = 1;
        var plan = model.CreatePlan("Test", () => next++)!;
        model.AssignPlan(7, plan.Id);
        model.Latch(7, "i1:0");
        model.Latch(9, "i1:0");            // pawn 9 has no assignment
        model.Reserve(100, 7, "i1:0");
        model.Reserve(101, 9, "i1:0");

        var change = model.CleanupMissing(pawnExists: _ => true);

        await Assert.That((change & PlannerChange.Latches) != 0).IsTrue();
        await Assert.That((change & PlannerChange.Reservations) != 0).IsTrue();
        await Assert.That(model.IsLatched(7, "i1:0")).IsTrue();
        await Assert.That(model.IsLatched(9, "i1:0")).IsFalse();
        await Assert.That(model.TryGetReservation(100, out _)).IsTrue();
        await Assert.That(model.TryGetReservation(101, out _)).IsFalse();
    }
}
