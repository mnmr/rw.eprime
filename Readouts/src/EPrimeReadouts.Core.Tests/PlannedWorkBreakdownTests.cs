using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

/// Observable planned-work provenance carried by the immutable count snapshot.
/// These scenarios protect the tooltip from falling back to per-resource lump
/// sums or leaking a work item's other materials into a custom pool.
public class PlannedWorkBreakdownTests
{
    [Test]
    public async Task MatchingWorkRowsAggregateTheirQueueAndDrain()
    {
        var accumulator = new CountAccumulator();
        accumulator.AddBuildableWork("Leather_Plain", defHash: 11,
            workDefName: "Armchair", stuffDefName: "Leather_Plain",
            queued: 1, unitCost: 110, drain: 110);
        accumulator.AddBuildableWork("Leather_Plain", defHash: 11,
            workDefName: "Armchair", stuffDefName: "Leather_Plain",
            queued: 3, unitCost: 110, drain: 330);

        RenderCountSnapshot snapshot = accumulator.ToSnapshot();
        PlannedWorkEntry row = snapshot.PlannedWork[0];

        await Assert.That(snapshot.PlannedWork.Count).IsEqualTo(1);
        await Assert.That(row.Kind).IsEqualTo(PlannedWorkKind.Buildable);
        await Assert.That(row.WorkDefName).IsEqualTo("Armchair");
        await Assert.That(row.StuffDefName).IsEqualTo("Leather_Plain");
        await Assert.That(row.ResourceDefName).IsEqualTo("Leather_Plain");
        await Assert.That(row.Queued).IsEqualTo(4);
        await Assert.That(row.UnitCost).IsEqualTo(110);
        await Assert.That(row.Drain).IsEqualTo(440);
        await Assert.That(snapshot.DebtOf("Leather_Plain").Buildables)
            .IsEqualTo(440);
    }

    [Test]
    public async Task PoolSelectionIncludesOnlyMemberResourcesFromMatchingWork()
    {
        var entries = new[]
        {
            Entry("FabricationBench", "Steel", drain: 200),
            Entry("FabricationBench", "ComponentIndustrial", drain: 12),
        };

        PlannedWorkSelection selected = PlannedWorkSelection.ForResources(
            entries, new[] { "Steel" }, maxRows: 8);

        await Assert.That(selected.Rows.Count).IsEqualTo(1);
        await Assert.That(selected.Rows[0].ResourceDefName).IsEqualTo("Steel");
        await Assert.That(selected.OverflowCount).IsEqualTo(0);
    }

    [Test]
    public async Task SelectionRanksByDrainAndLumpsMixedOverflowIntoTheLastRow()
    {
        var entries = new[]
        {
            Entry("Work100", "Steel", 100),
            Entry("Work900", "Steel", 900),
            Entry("Work200", "WoodLog", 200),
            Entry("Work800", "Steel", 800),
            Entry("Work300", "WoodLog", 300),
            Entry("Work700", "Steel", 700),
            Entry("Work400", "WoodLog", 400),
            Entry("Work600", "Steel", 600),
            Entry("Work500", "WoodLog", 500),
        };

        PlannedWorkSelection selected = PlannedWorkSelection.ForResources(
            entries, new[] { "Steel", "WoodLog" }, maxRows: 8);

        await Assert.That(selected.Rows.Count).IsEqualTo(7);
        await Assert.That(selected.Rows[0].Drain).IsEqualTo(900);
        await Assert.That(selected.Rows[6].Drain).IsEqualTo(300);
        await Assert.That(selected.OverflowCount).IsEqualTo(2);
        await Assert.That(selected.OverflowQueued).IsEqualTo(2);
        await Assert.That(selected.OverflowDrain).IsEqualTo(300);
        await Assert.That(selected.OverflowResourceDefName).IsNull();
    }

    [Test]
    public async Task ChangedWorkProvenanceChangesSnapshotIdentityAtEqualDebt()
    {
        var chair = new CountAccumulator();
        chair.AddBuildableWork("Leather_Plain", defHash: 11,
            workDefName: "Armchair", stuffDefName: "Leather_Plain",
            queued: 1, unitCost: 110, drain: 110);

        var sofa = new CountAccumulator();
        sofa.AddBuildableWork("Leather_Plain", defHash: 11,
            workDefName: "Sofa", stuffDefName: "Leather_Plain",
            queued: 1, unitCost: 110, drain: 110);

        await Assert.That(sofa.ToSnapshot().Equals(chair.ToSnapshot())).IsFalse();
    }

    [Test]
    public async Task QualityJobProvenancePreventsAggregationWithStandardWork()
    {
        var accumulator = new CountAccumulator();
        accumulator.AddBillWork("Leather_Plain", defHash: 11,
            workDefName: "Bed", queued: 1, unitCost: 45, drain: 45);
        accumulator.AddBillWork("Leather_Plain", defHash: 11,
            workDefName: "Bed", queued: 1, unitCost: 45, drain: 266,
            source: PlannedWorkSource.QualityJob);

        RenderCountSnapshot snapshot = accumulator.ToSnapshot();

        await Assert.That(snapshot.PlannedWork.Count).IsEqualTo(2);
        await Assert.That(snapshot.PlannedWork[0].Source)
            .IsEqualTo(PlannedWorkSource.Standard);
        await Assert.That(snapshot.PlannedWork[1].Source)
            .IsEqualTo(PlannedWorkSource.QualityJob);
        await Assert.That(snapshot.DebtOf("Leather_Plain").Bills)
            .IsEqualTo(311);
    }

    [Test]
    public async Task ChangedWorkSourceChangesSnapshotIdentityAtEqualDebt()
    {
        var standard = new CountAccumulator();
        standard.AddBillWork("Leather_Plain", defHash: 11,
            workDefName: "Bed", queued: 1, unitCost: 45, drain: 45);

        var qualityJob = new CountAccumulator();
        qualityJob.AddBillWork("Leather_Plain", defHash: 11,
            workDefName: "Bed", queued: 1, unitCost: 45, drain: 45,
            source: PlannedWorkSource.QualityJob);

        await Assert.That(qualityJob.ToSnapshot().Equals(
            standard.ToSnapshot())).IsFalse();
    }

    [Test]
    public async Task OverflowSourceIdentifiesQualityJobOnlyAggregate()
    {
        var entries = new[]
        {
            Entry("Standard", "Steel", drain: 1000),
            Entry("Quality900", "Steel", drain: 900,
                source: PlannedWorkSource.QualityJob),
            Entry("Quality800", "Steel", drain: 800,
                source: PlannedWorkSource.QualityJob),
        };

        PlannedWorkSelection selected = PlannedWorkSelection.ForResources(
            entries, new[] { "Steel" }, maxRows: 2);

        await Assert.That(selected.OverflowSource)
            .IsEqualTo(PlannedWorkSource.QualityJob);
    }

    [Test]
    public async Task OverflowSourceIsUnknownForMixedAggregate()
    {
        var entries = new[]
        {
            Entry("Detail", "Steel", drain: 1000),
            Entry("Quality", "Steel", drain: 900,
                source: PlannedWorkSource.QualityJob),
            Entry("Standard", "Steel", drain: 800),
        };

        PlannedWorkSelection selected = PlannedWorkSelection.ForResources(
            entries, new[] { "Steel" }, maxRows: 2);

        await Assert.That(selected.OverflowSource).IsNull();
    }

    [Test]
    public async Task UnboundedQualityDebtsSaturateInsteadOfOverflowing()
    {
        var accumulator = new CountAccumulator();
        accumulator.AddBillWork("Leather_Plain", defHash: 11,
            workDefName: "Armchair", queued: 1, unitCost: 110,
            drain: int.MaxValue);
        accumulator.AddBillWork("Leather_Plain", defHash: 11,
            workDefName: "Armchair", queued: 1, unitCost: 110,
            drain: int.MaxValue);

        RenderCountSnapshot snapshot = accumulator.ToSnapshot();

        await Assert.That(snapshot.DebtOf("Leather_Plain").Bills)
            .IsEqualTo(int.MaxValue);
        await Assert.That(snapshot.PlannedWork[0].Drain)
            .IsEqualTo(int.MaxValue);
    }

    [Test]
    public async Task CombinedDebtTotalSaturatesInsteadOfOverflowing()
    {
        var debt = new PlannedWorkDebt(int.MaxValue, int.MaxValue);

        await Assert.That(debt.Total).IsEqualTo(int.MaxValue);
    }

    [Test]
    public async Task OverflowRowSaturatesUnboundedQueuedAndDrainTotals()
    {
        var entries = new PlannedWorkEntry[9];
        for (int i = 0; i < entries.Length; i++)
            entries[i] = new PlannedWorkEntry(PlannedWorkKind.Bill,
                "QualityWork" + i, stuffDefName: null, "Leather_Plain",
                queued: int.MaxValue, unitCost: 110, drain: int.MaxValue);

        PlannedWorkSelection selected = PlannedWorkSelection.ForResources(
            entries, new[] { "Leather_Plain" }, maxRows: 8);

        await Assert.That(selected.OverflowCount).IsEqualTo(2);
        await Assert.That(selected.OverflowQueued).IsEqualTo(int.MaxValue);
        await Assert.That(selected.OverflowDrain).IsEqualTo(int.MaxValue);
    }

    private static PlannedWorkEntry Entry(
        string workDefName,
        string resourceDefName,
        int drain,
        PlannedWorkSource source = PlannedWorkSource.Standard) =>
        new PlannedWorkEntry(PlannedWorkKind.Buildable,
            workDefName, stuffDefName: null, resourceDefName,
            queued: 1, unitCost: drain, drain, source);
}
