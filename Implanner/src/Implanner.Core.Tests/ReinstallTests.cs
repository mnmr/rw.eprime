using System.Collections.Generic;
using Implanner.Core;

namespace Implanner.Core.Tests;

/// Implants pushed out by a part-wiping install (Vanilla Genetics
/// Expanded's neuron reinforcement pushes every brain implant out) go
/// straight back in. What the pawn carries on the part is recorded when the
/// wiping operation is scheduled; the records wake up once the wiper is
/// installed and each lasts until its implant is back.
public class ReinstallTests
{
    const string Wiper = "p1:GR_NeuronReinforcement:0";

    static List<ReinstallRecord> Brain(params string[] implants)
    {
        var records = new List<ReinstallRecord>();
        foreach (string implant in implants)
            records.Add(new ReinstallRecord(Wiper, implant, partIndex: 3, quality: -1, active: false));
        return records;
    }

    [Test]
    public async Task PushedOutImplantsAreRecordedThenReinstalledOnceTheWiperIsIn()
    {
        var model = new PlannerModel();

        // Scheduling the reinforcement records the brain's implants,
        // planned or not; recording the same set again changes nothing.
        await Assert.That(model.SetPendingReinstalls(7, Wiper,
            Brain("CircadianAssistant", "Joywire"))).IsEqualTo(PlannerChange.Surgery);
        await Assert.That(model.SetPendingReinstalls(7, Wiper,
            Brain("CircadianAssistant", "Joywire"))).IsEqualTo(PlannerChange.None);
        string circadian = GoalKeys.Reinstall("CircadianAssistant", 3);
        await Assert.That(model.IsActiveReinstall(7, circadian)).IsFalse();

        // The reinforcement is in: both records now ask for surgery.
        await Assert.That(model.ActivateReinstalls(7, Wiper)).IsEqualTo(PlannerChange.Surgery);
        await Assert.That(model.ActivateReinstalls(7, Wiper)).IsEqualTo(PlannerChange.None);
        await Assert.That(model.IsActiveReinstall(7, circadian)).IsTrue();

        // An active record keeps its reservation even though the key is no
        // plan goal and the pawn has no plan any more.
        model.Reserve(501, 7, circadian);
        model.CleanupMissing(pawnExists: id => true);
        await Assert.That(model.Reservations.ContainsKey(501)).IsTrue();

        // Back in: the record goes.
        await Assert.That(model.RemoveReinstall(7, circadian)).IsEqualTo(PlannerChange.Surgery);
        await Assert.That(model.RemoveReinstall(7, circadian)).IsEqualTo(PlannerChange.None);
        await Assert.That(model.ReinstallsFor(7)!.Count).IsEqualTo(1);

        // A departed pawn's records go with them.
        model.CleanupMissing(pawnExists: id => id != 7);
        await Assert.That(model.ReinstallsFor(7)).IsNull();
    }

    /// A quality upgrade reserves its better item before the old implant
    /// comes out: the reservation lives with the pending record, plan or
    /// not, and goes when the record is dropped.
    [Test]
    public async Task AnUpgradeHoldsItsItemFromTheStart()
    {
        var model = new PlannerModel();
        string removal = GoalKeys.Upgrade("BionicArm", 12);
        var record = new ReinstallRecord(removal, "BionicArm", 12, quality: 5, active: false);
        model.SetPendingReinstalls(7, removal, new[] { record });
        model.Reserve(601, 7, record.Key);

        model.CleanupMissing(pawnExists: id => true);
        await Assert.That(model.Reservations.ContainsKey(601)).IsTrue();
        await Assert.That(model.IsActiveReinstall(7, record.Key)).IsFalse();

        model.DropReinstalls(7, removal);
        model.CleanupMissing(pawnExists: id => true);
        await Assert.That(model.Reservations.ContainsKey(601)).IsFalse();
    }

    /// The wiping operation was cancelled before it ran: nothing was pushed
    /// out, so its pending records are dropped.
    [Test]
    public async Task ACancelledWiperDropsItsPendingRecords()
    {
        var model = new PlannerModel();
        model.SetPendingReinstalls(7, Wiper, Brain("CircadianAssistant"));

        await Assert.That(model.DropReinstalls(7, Wiper)).IsEqualTo(PlannerChange.Surgery);
        await Assert.That(model.DropReinstalls(7, Wiper)).IsEqualTo(PlannerChange.None);
        await Assert.That(model.ReinstallsFor(7)).IsNull();
    }
}
