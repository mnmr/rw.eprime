using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

public class PanelReadbackLifetimeTests
{
    [Test]
    public async Task TeardownWaitsForAnOutstandingReadAndTransfersReleaseExactlyOnce()
    {
        // The GPU cannot be used by a Core test. This independently meaningful
        // lifetime boundary decides when native destruction becomes safe.
        var lifetime = new PanelReadbackLifetime();
        await Assert.That(lifetime.TryBegin()).IsTrue();
        await Assert.That(lifetime.IsPending).IsTrue();
        await Assert.That(lifetime.TryBegin()).IsFalse();
        await Assert.That(lifetime.RequestRelease()).IsFalse();
        await Assert.That(lifetime.Complete()).IsTrue();
        await Assert.That(lifetime.IsPending).IsFalse();
        await Assert.That(lifetime.Complete()).IsFalse();
        await Assert.That(lifetime.RequestRelease()).IsFalse();
        await Assert.That(lifetime.TryBegin()).IsFalse();
    }

    [Test]
    public async Task CompletedChecksReuseTheTargetUntilItsOwnerReleasesIt()
    {
        var lifetime = new PanelReadbackLifetime();
        await Assert.That(lifetime.TryBegin()).IsTrue();
        await Assert.That(lifetime.Complete()).IsFalse();
        await Assert.That(lifetime.TryBegin()).IsTrue();
        await Assert.That(lifetime.Complete()).IsFalse();
        await Assert.That(lifetime.RequestRelease()).IsTrue();
        await Assert.That(lifetime.RequestRelease()).IsFalse();
        await Assert.That(lifetime.TryBegin()).IsFalse();
    }

    [Test]
    public async Task RacingTeardownAndCompletionNeverLoseOrDuplicateRelease()
    {
        int badReleases = 0;
        for (int i = 0; i < 500; i++)
        {
            var lifetime = new PanelReadbackLifetime();
            lifetime.TryBegin();
            int releases = 0;
            Parallel.Invoke(
                () => { if (lifetime.RequestRelease()) Interlocked.Increment(ref releases); },
                () => { if (lifetime.Complete()) Interlocked.Increment(ref releases); });
            if (releases != 1) badReleases++;
        }
        await Assert.That(badReleases).IsEqualTo(0);
    }
}
