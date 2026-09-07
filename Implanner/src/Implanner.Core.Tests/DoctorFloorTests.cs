using Implanner.Core;

namespace Implanner.Core.Tests;

/// The doctor-skill floor: the automatic mode publishes each colony's
/// current best doctor (up, down, or gone), the manual minimum applies only
/// while auto is off, and stale colonies prune away.
public class DoctorFloorTests
{
    [Test]
    public async Task SetDoctorFloorPublishesCurrentBestUpDownOrGone()
    {
        var model = new PlannerModel();

        await Assert.That(model.SetDoctorFloor("home", 8)).IsEqualTo(PlannerChange.Surgery);
        await Assert.That(model.SetDoctorFloor("home", 8)).IsEqualTo(PlannerChange.None);
        // Losing the best doctor lowers the published floor.
        await Assert.That(model.SetDoctorFloor("home", 5)).IsEqualTo(PlannerChange.Surgery);
        await Assert.That(model.DoctorFloorOf("home")).IsEqualTo(5);
        // No eligible doctor left: the entry disappears.
        await Assert.That(model.SetDoctorFloor("home", 0)).IsEqualTo(PlannerChange.Surgery);
        await Assert.That(model.DoctorFloors.Count).IsEqualTo(0);
        await Assert.That(model.SetDoctorFloor("home", 0)).IsEqualTo(PlannerChange.None);
    }

    /// The automatic floor (on by default) uses only the per-colony current
    /// best; the manual minimum applies only while auto is off.
    [Test]
    public async Task EffectiveFloorFollowsTheActiveMode()
    {
        var model = new PlannerModel();
        model.SetManualDoctorFloor(4);
        model.SetDoctorFloor("home", 10);

        // Auto on by default: the published best, never the manual value; a
        // colony without an entry has no floor.
        await Assert.That(model.EffectiveDoctorFloor("home")).IsEqualTo(10);
        await Assert.That(model.EffectiveDoctorFloor("ship")).IsEqualTo(0);

        model.SetAutoDoctorFloor(false);
        await Assert.That(model.EffectiveDoctorFloor("home")).IsEqualTo(4);
        await Assert.That(model.EffectiveDoctorFloor("ship")).IsEqualTo(4);
    }

    /// Nobody operates on themselves: the best doctor's own operations
    /// fall back to the runner-up so they never wait on a floor only the
    /// patient meets.
    [Test]
    public async Task PatientFloorFallsBackToTheRunnerUpForTheBestDoctor()
    {
        var model = new PlannerModel();
        model.SetDoctorFloor("home", 12);

        // An ordinary patient: the colony floor stands.
        await Assert.That(model.PatientDoctorFloor("home", 6, 12)).IsEqualTo(12);
        await Assert.That(model.PatientDoctorFloor("home", -1, 12)).IsEqualTo(12);
        // The best doctor as patient: the second best operates.
        await Assert.That(model.PatientDoctorFloor("home", 12, 7)).IsEqualTo(7);
        // A peer of equal skill keeps the floor where it is.
        await Assert.That(model.PatientDoctorFloor("home", 12, 12)).IsEqualTo(12);
        // The colony's only doctor: no floor at all.
        await Assert.That(model.PatientDoctorFloor("home", 12, -1)).IsEqualTo(0);

        // The manual minimum is the player's explicit choice: never lowered.
        model.SetManualDoctorFloor(9);
        model.SetAutoDoctorFloor(false);
        await Assert.That(model.PatientDoctorFloor("home", 12, 7)).IsEqualTo(9);
    }

    [Test]
    public async Task SurgeryOptionSettersPreserveNoOps()
    {
        var model = new PlannerModel();

        // Tier iteration is the default; only leaving it reports a change.
        await Assert.That(model.SetIteration(IterationStrategy.ImplantTier))
            .IsEqualTo(PlannerChange.None);
        await Assert.That(model.SetIteration(IterationStrategy.Colonist))
            .IsEqualTo(PlannerChange.Options);
        await Assert.That(model.SetManualDoctorFloor(0)).IsEqualTo(PlannerChange.None);
        await Assert.That(model.SetManualDoctorFloor(25)).IsEqualTo(PlannerChange.Options);
        await Assert.That(model.ManualDoctorFloor).IsEqualTo(20);
        // Auto is the default; only switching it off reports a change.
        await Assert.That(model.SetAutoDoctorFloor(true)).IsEqualTo(PlannerChange.None);
        await Assert.That(model.SetAutoDoctorFloor(false)).IsEqualTo(PlannerChange.Options);
    }

    [Test]
    public async Task PruneDropsFloorsOfMissingColonies()
    {
        var model = new PlannerModel();
        model.SetDoctorFloor("home", 8);
        model.SetDoctorFloor("ship", 9);

        var live = new System.Collections.Generic.HashSet<string>(
            System.StringComparer.Ordinal) { "ship" };
        await Assert.That(model.PruneDoctorFloors(live))
            .IsEqualTo(PlannerChange.Surgery);
        await Assert.That(model.DoctorFloorOf("home")).IsEqualTo(0);
        await Assert.That(model.DoctorFloorOf("ship")).IsEqualTo(9);
        await Assert.That(model.PruneDoctorFloors(live))
            .IsEqualTo(PlannerChange.None);
    }
}
