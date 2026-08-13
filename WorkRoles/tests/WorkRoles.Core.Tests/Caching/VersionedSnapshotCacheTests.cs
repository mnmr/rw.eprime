namespace WorkRoles.Core.Tests.Caching;

public class VersionedSnapshotCacheTests
{
    [Test]
    public async Task RepeatedReadsReuseSnapshotUntilItsOwnerIsInvalidated()
    {
        var owner = new Owner();
        int builds = 0;
        var cache = new VersionedSnapshotCache<Owner, int>(_ => ++builds);

        int first = cache.Get(owner);
        int repeated = cache.Get(owner);
        cache.Invalidate(owner);
        int rebuilt = cache.Get(owner);

        await Assert.That(first).IsEqualTo(1);
        await Assert.That(repeated).IsEqualTo(1);
        await Assert.That(rebuilt).IsEqualTo(2);
        await Assert.That(cache.Revision).IsEqualTo(1);
    }

    [Test]
    public async Task OwnerInvalidationPreservesOtherOwnersSnapshot()
    {
        var first = new Owner();
        var second = new Owner();
        int builds = 0;
        var cache = new VersionedSnapshotCache<Owner, int>(_ => ++builds);
        cache.Get(first);
        cache.Get(second);

        cache.Invalidate(first);

        await Assert.That(cache.Get(second)).IsEqualTo(2);
        await Assert.That(builds).IsEqualTo(2);
    }

    [Test]
    public async Task ClearAdvancesRevisionAndReleasesEverySnapshot()
    {
        var first = new Owner();
        var second = new Owner();
        int builds = 0;
        var cache = new VersionedSnapshotCache<Owner, int>(_ => ++builds);
        cache.Get(first);
        cache.Get(second);

        cache.Clear();

        await Assert.That(cache.Revision).IsEqualTo(1);
        await Assert.That(cache.Get(first)).IsEqualTo(3);
        await Assert.That(cache.Get(second)).IsEqualTo(4);
    }

    private sealed class Owner { }
}
