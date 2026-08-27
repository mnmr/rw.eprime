using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

public class CountAccumulatorTests
{
    [Test]
    public async Task ContributionsFromMultipleSourcesSumPerDef()
    {
        var accumulator = new CountAccumulator();
        accumulator.Add("Steel", defHash: 11, count: 40);
        accumulator.Add("WoodLog", defHash: 22, count: 12);
        accumulator.Add("Steel", defHash: 11, count: 35);

        var snapshot = accumulator.ToSnapshot();

        await Assert.That(snapshot.Counts["Steel"]).IsEqualTo(75);
        await Assert.That(snapshot.Counts["WoodLog"]).IsEqualTo(12);
        await Assert.That(snapshot.Counts.Count).IsEqualTo(2);
    }

    [Test]
    public async Task EqualTotalsAcrossDifferentDistributionsProduceEqualSnapshots()
    {
        var oneFloor = new CountAccumulator();
        oneFloor.Add("Steel", defHash: 11, count: 10);

        var twoFloors = new CountAccumulator();
        twoFloors.Add("Steel", defHash: 11, count: 4);
        twoFloors.Add("Steel", defHash: 11, count: 6);

        await Assert.That(twoFloors.ToSnapshot().Equals(oneFloor.ToSnapshot()))
            .IsTrue();
    }

    [Test]
    public async Task EmptyAccumulatorProducesAnEmptySnapshot()
    {
        var snapshot = new CountAccumulator().ToSnapshot();

        await Assert.That(snapshot.Counts.Count).IsEqualTo(0);
    }

    [Test]
    public async Task SearchContributionsSumPerDefAcrossStackDispositions()
    {
        var accumulator = new CountAccumulator();
        accumulator.AddSearch("Steel", defHash: 11, count: 40, stored: true, forbidden: false);
        accumulator.AddSearch("Steel", defHash: 11, count: 10, stored: true, forbidden: true);
        accumulator.AddSearch("Steel", defHash: 11, count: 25, stored: false, forbidden: false);
        accumulator.AddSearch("Steel", defHash: 11, count: 5, stored: false, forbidden: true);

        var search = accumulator.ToSnapshot().SearchCounts["Steel"];

        await Assert.That(search.Total).IsEqualTo(80);
        await Assert.That(search.Stored).IsEqualTo(50);
        await Assert.That(search.Unforbidden).IsEqualTo(65);
        await Assert.That(search.StoredUnforbidden).IsEqualTo(40);
    }

    [Test]
    public async Task EqualTotalsWithDifferentSearchBreakdownsProduceUnequalSnapshots()
    {
        // Same group-count basis and same search total, but the stacks moved
        // out of storage — a refresh must publish a new snapshot so the
        // storage/forbidden filters see the change.
        var stored = new CountAccumulator();
        stored.Add("Steel", defHash: 11, count: 10);
        stored.AddSearch("Steel", defHash: 11, count: 10, stored: true, forbidden: false);

        var scattered = new CountAccumulator();
        scattered.Add("Steel", defHash: 11, count: 10);
        scattered.AddSearch("Steel", defHash: 11, count: 10, stored: false, forbidden: false);

        await Assert.That(scattered.ToSnapshot().Equals(stored.ToSnapshot()))
            .IsFalse();
    }

    [Test]
    public async Task EqualSearchBreakdownsPreserveSnapshotEquality()
    {
        var first = new CountAccumulator();
        first.Add("Steel", defHash: 11, count: 10);
        first.AddSearch("Steel", defHash: 11, count: 10, stored: true, forbidden: true);

        var second = new CountAccumulator();
        second.Add("Steel", defHash: 11, count: 10);
        second.AddSearch("Steel", defHash: 11, count: 10, stored: true, forbidden: true);

        await Assert.That(second.ToSnapshot().Equals(first.ToSnapshot())).IsTrue();
    }

    [Test]
    public async Task DifferentTotalsProduceUnequalSnapshots()
    {
        var first = new CountAccumulator();
        first.Add("Steel", defHash: 11, count: 10);

        var second = new CountAccumulator();
        second.Add("Steel", defHash: 11, count: 11);

        await Assert.That(second.ToSnapshot().Equals(first.ToSnapshot()))
            .IsFalse();
    }

    [Test]
    public async Task PublishingDoesNotCopyTheAccumulatorOwnedCountBuffer()
    {
        var accumulator = new CountAccumulator();
        for (int i = 0; i < 4096; i++)
            accumulator.Add("Resource" + i, defHash: i, count: i);

        long before = GC.GetAllocatedBytesForCurrentThread();
        RenderCountSnapshot snapshot = accumulator.ToSnapshot();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        GC.KeepAlive(snapshot);

        await Assert.That(allocated).IsLessThan(8192L);
    }

    [Test]
    public async Task PublishedAccumulatorCannotMutateTransferredBuffers()
    {
        var accumulator = new CountAccumulator();
        accumulator.Add("Steel", defHash: 11, count: 40);
        RenderCountSnapshot snapshot = accumulator.ToSnapshot();

        await Assert.That(() => accumulator.Add("Steel", defHash: 11, count: 1))
            .Throws<InvalidOperationException>();
        await Assert.That(snapshot.Counts["Steel"]).IsEqualTo(40);
    }

    [Test]
    public async Task CachedPlannedWorkCanBeReplayedIntoANewSnapshot()
    {
        var scanned = new CountAccumulator();
        scanned.AddBillWork("Steel", defHash: 11,
            workDefName: "ComponentIndustrial", queued: 3,
            unitCost: 12, drain: 36);
        scanned.AddBuildableWork("WoodLog", defHash: 22,
            workDefName: "DiningChair", stuffDefName: "WoodLog",
            queued: 2, unitCost: 45, drain: 90,
            source: PlannedWorkSource.QualityJob);
        RenderCountSnapshot cached = scanned.ToSnapshot();

        var refreshed = new CountAccumulator();
        for (int i = 0; i < cached.PlannedWork.Count; i++)
        {
            PlannedWorkEntry entry = cached.PlannedWork[i];
            refreshed.AddCachedPlannedWork(entry,
                entry.ResourceDefName == "Steel" ? 11 : 22);
        }

        await Assert.That(refreshed.ToSnapshot().Equals(cached)).IsTrue();
    }
}
