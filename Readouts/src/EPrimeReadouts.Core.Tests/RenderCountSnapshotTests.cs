using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

public class RenderCountSnapshotTests
{
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
}
