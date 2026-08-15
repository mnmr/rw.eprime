namespace QualityJobs.Core.Tests;

using QualityJobs.Core;

public class SnapshotPublicationTests
{
    private sealed class Snapshot : IContentSnapshot<Snapshot>
    {
        internal Snapshot(int value) => Value = value;
        internal int Value { get; }
        public bool HasSameContent(Snapshot other) => Value == other.Value;
    }

    [Test]
    public async Task EqualCandidatePreservesPublishedIdentity()
    {
        var current = new Snapshot(7);
        var equalCandidate = new Snapshot(7);

        Snapshot published = SnapshotPublication.Publish(
            current, equalCandidate);

        await Assert.That(published).IsSameReferenceAs(current);
    }

    [Test]
    public async Task ChangedCandidateBecomesPublishedValue()
    {
        var current = new Snapshot(7);
        var changedCandidate = new Snapshot(8);

        Snapshot published = SnapshotPublication.Publish(
            current, changedCandidate);

        await Assert.That(published).IsSameReferenceAs(changedCandidate);
    }
}
