using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

/// The count snapshot carries planned-work debt alongside the raw counts, split
/// by source so a tooltip can explain where a counter went.
public class PlannedWorkSnapshotTests
{
    [Test]
    public async Task DebtFreeSnapshotsReportNoDebt()
    {
        var accumulator = new CountAccumulator();
        accumulator.Add("Steel", defHash: 11, count: 40);

        var snapshot = accumulator.ToSnapshot();

        await Assert.That(snapshot.Debts.Count).IsEqualTo(0);
        await Assert.That(snapshot.DebtOf("Steel").Total).IsEqualTo(0);
    }

    [Test]
    public async Task BillAndBuildableDebtStayInSeparateBuckets()
    {
        var accumulator = new CountAccumulator();
        accumulator.Add("Steel", defHash: 11, count: 400);
        accumulator.AddBillDebt("Steel", defHash: 11, amount: 75);
        accumulator.AddBuildableDebt("Steel", defHash: 11, amount: 120);

        PlannedWorkDebt debt = accumulator.ToSnapshot().DebtOf("Steel");

        await Assert.That(debt.Bills).IsEqualTo(75);
        await Assert.That(debt.Buildables).IsEqualTo(120);
        await Assert.That(debt.Total).IsEqualTo(195);
    }

    [Test]
    public async Task DebtContributionsAccumulatePerDef()
    {
        var accumulator = new CountAccumulator();
        accumulator.AddBillDebt("Steel", defHash: 11, amount: 10);
        accumulator.AddBillDebt("Steel", defHash: 11, amount: 15);
        accumulator.AddBuildableDebt("WoodLog", defHash: 22, amount: 30);

        var snapshot = accumulator.ToSnapshot();

        await Assert.That(snapshot.DebtOf("Steel").Bills).IsEqualTo(25);
        await Assert.That(snapshot.DebtOf("WoodLog").Buildables).IsEqualTo(30);
    }

    [Test]
    public async Task ZeroAndNegativeDebtContributionsAreIgnored()
    {
        var accumulator = new CountAccumulator();
        accumulator.AddBillDebt("Steel", defHash: 11, amount: 0);
        accumulator.AddBuildableDebt("Steel", defHash: 11, amount: -25);

        var snapshot = accumulator.ToSnapshot();

        await Assert.That(snapshot.Debts.Count).IsEqualTo(0);
        await Assert.That(snapshot.DebtOf("Steel").Total).IsEqualTo(0);
    }

    [Test]
    public async Task DebtOnADefWithoutStockStillPublishes()
    {
        // Nothing of the def is on the map, but a bill still owes it: the
        // counter must be able to show the overrun.
        var accumulator = new CountAccumulator();
        accumulator.AddBillDebt("Plasteel", defHash: 44, amount: 60);

        await Assert.That(accumulator.ToSnapshot().DebtOf("Plasteel").Bills)
            .IsEqualTo(60);
    }

    [Test]
    public async Task ChangedDebtProducesAnUnequalSnapshot()
    {
        // Stock is unchanged; only the planned work moved. A refresh must
        // publish a new snapshot so the readout picks the change up.
        var before = new CountAccumulator();
        before.Add("Steel", defHash: 11, count: 400);
        before.AddBillDebt("Steel", defHash: 11, amount: 75);

        var after = new CountAccumulator();
        after.Add("Steel", defHash: 11, count: 400);
        after.AddBillDebt("Steel", defHash: 11, amount: 90);

        await Assert.That(after.ToSnapshot().Equals(before.ToSnapshot())).IsFalse();
    }

    [Test]
    public async Task DebtMovingBetweenBucketsProducesAnUnequalSnapshot()
    {
        var bills = new CountAccumulator();
        bills.AddBillDebt("Steel", defHash: 11, amount: 75);

        var buildables = new CountAccumulator();
        buildables.AddBuildableDebt("Steel", defHash: 11, amount: 75);

        await Assert.That(buildables.ToSnapshot().Equals(bills.ToSnapshot()))
            .IsFalse();
    }

    [Test]
    public async Task EqualDebtPreservesSnapshotEquality()
    {
        var first = new CountAccumulator();
        first.Add("Steel", defHash: 11, count: 400);
        first.AddBillDebt("Steel", defHash: 11, amount: 40);
        first.AddBuildableDebt("Steel", defHash: 11, amount: 35);

        var second = new CountAccumulator();
        second.Add("Steel", defHash: 11, count: 400);
        second.AddBillDebt("Steel", defHash: 11, amount: 15);
        second.AddBillDebt("Steel", defHash: 11, amount: 25);
        second.AddBuildableDebt("Steel", defHash: 11, amount: 35);

        await Assert.That(second.ToSnapshot().Equals(first.ToSnapshot())).IsTrue();
    }

    // ---- option gate -------------------------------------------------------

    [Test]
    public async Task OptionsWithNothingEnabledNeedNoScan()
    {
        await Assert.That(new PlannedWorkOptions(
            reserveBills: false, reserveBuildables: false,
            qualityRework: true).Any).IsFalse();
    }

    [Test]
    public async Task AnyReservationOptionRequiresAScan()
    {
        await Assert.That(new PlannedWorkOptions(true, false, false).Any).IsTrue();
        await Assert.That(new PlannedWorkOptions(false, true, false).Any).IsTrue();
    }

    [Test]
    public async Task EqualOptionsCompareEqual()
    {
        await Assert.That(new PlannedWorkOptions(true, false, true)
            .Equals(new PlannedWorkOptions(true, false, true))).IsTrue();
    }

    [Test]
    public async Task EachOptionFlagIsPartOfTheGate()
    {
        var baseline = new PlannedWorkOptions(true, true, true);

        await Assert.That(baseline.Equals(new PlannedWorkOptions(false, true, true)))
            .IsFalse();
        await Assert.That(baseline.Equals(new PlannedWorkOptions(true, false, true)))
            .IsFalse();
        await Assert.That(baseline.Equals(new PlannedWorkOptions(true, true, false)))
            .IsFalse();
    }
}
