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
    public async Task ZeroRegistrationPublishesTheDefWithoutDisturbingTotals()
    {
        var accumulator = new CountAccumulator();
        accumulator.AddZero("Chemfuel", defHash: 33);
        accumulator.Add("Steel", defHash: 11, count: 40);
        accumulator.AddZero("Steel", defHash: 11);

        var snapshot = accumulator.ToSnapshot();

        await Assert.That(snapshot.Counts["Chemfuel"]).IsEqualTo(0);
        await Assert.That(snapshot.Counts["Steel"]).IsEqualTo(40);
    }

    [Test]
    public async Task EmptyAccumulatorProducesAnEmptySnapshot()
    {
        var snapshot = new CountAccumulator().ToSnapshot();

        await Assert.That(snapshot.Counts.Count).IsEqualTo(0);
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
}
