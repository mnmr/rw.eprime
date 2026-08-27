using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

public class ReadoutRevisionsTests
{
    [Test]
    public async Task ThresholdChangeDoesNotInvalidateGroupOrPoolDomains()
    {
        var revisions = new ReadoutRevisions();

        revisions.Bump(ReadoutChange.Thresholds);

        await Assert.That(revisions.Version).IsEqualTo(1);
        await Assert.That(revisions.Groups).IsEqualTo(0);
        await Assert.That(revisions.Pools).IsEqualTo(0);
        await Assert.That(revisions.Thresholds).IsEqualTo(1);
    }

    [Test]
    public async Task MixedChangeInvalidatesOnlyItsSelectedDomains()
    {
        var revisions = new ReadoutRevisions();

        revisions.Bump(ReadoutChange.Groups | ReadoutChange.Pools);

        await Assert.That(revisions.Version).IsEqualTo(1);
        await Assert.That(revisions.Groups).IsEqualTo(1);
        await Assert.That(revisions.Pools).IsEqualTo(1);
        await Assert.That(revisions.Thresholds).IsEqualTo(0);
    }

    [Test]
    public async Task EmptyChangeDoesNotInvalidateAnyDomain()
    {
        var revisions = new ReadoutRevisions();

        revisions.Bump(ReadoutChange.None);

        await Assert.That(revisions.Version).IsEqualTo(0);
        await Assert.That(revisions.Groups).IsEqualTo(0);
        await Assert.That(revisions.Pools).IsEqualTo(0);
        await Assert.That(revisions.Thresholds).IsEqualTo(0);
    }
}
