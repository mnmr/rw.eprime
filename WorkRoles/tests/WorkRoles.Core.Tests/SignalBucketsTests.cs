using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

/// The six-tier signal classifier: precedence between signal kinds, the
/// apathy override, and the neutral/poor split on bare skill level.
public class SignalBucketsTests
{
    [Test]
    public async Task TiersOrderAsSpecified()
    {
        // Rung order is load-bearing: rules compare buckets numerically.
        await Assert.That((int)SignalBucket.Awful).IsEqualTo(0);
        await Assert.That((int)SignalBucket.Poor).IsEqualTo(1);
        await Assert.That((int)SignalBucket.Neutral).IsEqualTo(2);
        await Assert.That((int)SignalBucket.Strong).IsEqualTo(3);
        await Assert.That((int)SignalBucket.Great).IsEqualTo(4);
        await Assert.That((int)SignalBucket.Exceptional).IsEqualTo(5);
    }

    [Test]
    public async Task ExpertiseBeatsPassionBeatsAptitude()
    {
        await Assert.That(SignalBuckets.Classify(5, 2, 1, expertise: true))
            .IsEqualTo(SignalBucket.Exceptional);
        await Assert.That(SignalBuckets.Classify(5, 2, 0, expertise: false))
            .IsEqualTo(SignalBucket.Great);
        await Assert.That(SignalBuckets.Classify(5, 1, 0, expertise: false))
            .IsEqualTo(SignalBucket.Strong);
        await Assert.That(SignalBuckets.Classify(5, 0, 1, expertise: false))
            .IsEqualTo(SignalBucket.Strong);
    }

    [Test]
    public async Task ApathyIsAwfulRegardlessOfEverythingElse()
    {
        await Assert.That(SignalBuckets.Classify(15, 2, -1, expertise: true))
            .IsEqualTo(SignalBucket.Awful);
        await Assert.That(SignalBuckets.Classify(0, 0, -3, expertise: false))
            .IsEqualTo(SignalBucket.Awful);
    }

    [Test]
    public async Task NoInterestSplitsNeutralFromPoorOnLevel()
    {
        await Assert.That(SignalBuckets.Classify(1, 0, 0, expertise: false))
            .IsEqualTo(SignalBucket.Neutral);
        await Assert.That(SignalBuckets.Classify(0, 0, 0, expertise: false))
            .IsEqualTo(SignalBucket.Poor);
    }
}
