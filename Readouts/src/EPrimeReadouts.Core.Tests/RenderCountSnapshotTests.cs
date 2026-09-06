using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

public class RenderCountSnapshotTests
{
    [Test]
    public async Task SearchCountOfFallsBackToTheRawCountForDefsOutsideTheBreakdown()
    {
        // Tooltip breakdowns and slot sums read the same resolution: a def
        // the breakdown never saw but the game counts shows its raw count
        // as stored and unforbidden; a def in neither shows zero.
        var snapshot = new RenderCountSnapshot(
            new Dictionary<string, int> { ["Steel"] = 40, ["WoodLog"] = 12 },
            fingerprint: 1L,
            new Dictionary<string, SearchCount>
            {
                ["Steel"] = new SearchCount(55, 40, 50, 35),
            });

        await Assert.That(snapshot.SearchCountOf("Steel").StoredUnforbidden).IsEqualTo(35);
        SearchCount wood = snapshot.SearchCountOf("WoodLog");
        await Assert.That(wood.Total).IsEqualTo(12);
        await Assert.That(wood.StoredUnforbidden).IsEqualTo(12);
        await Assert.That(snapshot.SearchCountOf("Plasteel").Total).IsEqualTo(0);
    }

    [Test]
    public async Task EqualSnapshotsCompareByTheirCountContents()
    {
        var first = new RenderCountSnapshot(
            new Dictionary<string, int> { ["Steel"] = 40, ["WoodLog"] = 12 },
            fingerprint: 1234L);
        var second = new RenderCountSnapshot(
            new Dictionary<string, int> { ["WoodLog"] = 12, ["Steel"] = 40 },
            fingerprint: 1234L);

        await Assert.That(second).IsEqualTo(first);
    }

    [Test]
    public async Task ASharedFingerprintDoesNotHideDifferentCounts()
    {
        var first = new RenderCountSnapshot(
            new Dictionary<string, int> { ["Steel"] = 40 },
            fingerprint: 1234L);
        var collision = new RenderCountSnapshot(
            new Dictionary<string, int> { ["Steel"] = 41 },
            fingerprint: 1234L);

        await Assert.That(collision).IsNotEqualTo(first);
    }

    [Test]
    public async Task ConstructionIsolatedTheSnapshotFromLaterDictionaryMutation()
    {
        var source = new Dictionary<string, int> { ["Steel"] = 40 };
        var snapshot = new RenderCountSnapshot(source, fingerprint: 1234L);

        source["Steel"] = 99;

        await Assert.That(snapshot.Counts["Steel"]).IsEqualTo(40);
    }

    [Test]
    public async Task IdenticalCountsRemainEqualWhenTraversalOrderChangesTheFingerprint()
    {
        var first = new RenderCountSnapshot(
            new Dictionary<string, int> { ["Steel"] = 40, ["WoodLog"] = 12 },
            fingerprint: 1234L);
        var reordered = new RenderCountSnapshot(
            new Dictionary<string, int> { ["WoodLog"] = 12, ["Steel"] = 40 },
            fingerprint: 9876L);

        await Assert.That(reordered.Equals(first)).IsTrue();
    }

    [Test]
    public async Task PublishedCountDictionaryRejectsConsumerMutation()
    {
        var snapshot = new RenderCountSnapshot(
            new Dictionary<string, int> { ["Steel"] = 40 }, 1L);

        await Assert.That(() => ((IDictionary<string, int>)snapshot.Counts)["Steel"] = 99)
            .Throws<NotSupportedException>();
    }
}
