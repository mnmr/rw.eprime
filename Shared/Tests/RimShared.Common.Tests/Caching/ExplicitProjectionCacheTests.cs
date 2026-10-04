namespace RimShared.Common.Tests;

public class ExplicitProjectionCacheTests
{
    [Test]
    public async Task CacheMissBuildsWithoutPublishingProjection()
    {
        var owner = new Owner();
        int builds = 0;
        int publications = 0;
        var cache = new ExplicitProjectionCache<Owner, int>(_ => ++builds, (_, _) => publications++);

        int first = cache.GetOrBuild(owner);
        int repeated = cache.GetOrBuild(owner);

        await Assert.That(first).IsEqualTo(1);
        await Assert.That(repeated).IsEqualTo(1);
        await Assert.That(builds).IsEqualTo(1);
        await Assert.That(publications).IsEqualTo(0);
    }

    [Test]
    public async Task PublishFreshRebuildsBeforePublishingProjection()
    {
        var owner = new Owner();
        int source = 10;
        List<int> publications = [];
        var cache = new ExplicitProjectionCache<Owner, int>(_ => source, (_, snapshot) => publications.Add(snapshot));

        await Assert.That(cache.GetOrBuild(owner)).IsEqualTo(10);
        source = 20;

        cache.PublishFresh(owner);

        await Assert.That(publications).IsEquivalentTo([20]);
        await Assert.That(cache.GetOrBuild(owner)).IsEqualTo(20);
    }

    [Test]
    public async Task CachedManagedHitSkipsTheOwnershipProbe()
    {
        var owner = new Owner();
        int ownershipProbes = 0;
        int builds = 0;
        var cache = new ExplicitProjectionCache<Owner, int>(_ => ++builds, (_, _) => { });

        bool first = cache.TryGetManaged(
            owner,
            _ =>
            {
                ownershipProbes++;
                return true;
            },
            out int firstValue
        );
        bool repeated = cache.TryGetManaged(
            owner,
            _ =>
            {
                ownershipProbes++;
                return false;
            },
            out int repeatedValue
        );

        await Assert.That(first).IsTrue();
        await Assert.That(repeated).IsTrue();
        await Assert.That(firstValue).IsEqualTo(1);
        await Assert.That(repeatedValue).IsEqualTo(1);
        await Assert.That(ownershipProbes).IsEqualTo(1);
        await Assert.That(builds).IsEqualTo(1);
    }

    [Test]
    public async Task UnmanagedMissFallsThroughWithoutCachingASnapshot()
    {
        var owner = new Owner();
        int builds = 0;
        var cache = new ExplicitProjectionCache<Owner, int>(_ => ++builds, (_, _) => { });

        bool found = cache.TryGetManaged(owner, _ => false, out int value);
        // A cached snapshot would be a hit that skips the ownership probe.
        bool foundAgain = cache.TryGetManaged(owner, _ => false, out _);

        await Assert.That(found).IsFalse();
        await Assert.That(foundAgain).IsFalse();
        await Assert.That(value).IsEqualTo(0);
        await Assert.That(builds).IsEqualTo(0);
    }

    [Test]
    public async Task RemovalRequiresOwnershipToBeProvenAgain()
    {
        var owner = new Owner();
        int ownershipProbes = 0;
        var cache = new ExplicitProjectionCache<Owner, int>(_ => 7, (_, _) => { });
        cache.TryGetManaged(
            owner,
            _ =>
            {
                ownershipProbes++;
                return true;
            },
            out _
        );

        cache.Remove(owner);
        bool found = cache.TryGetManaged(
            owner,
            _ =>
            {
                ownershipProbes++;
                return false;
            },
            out _
        );

        await Assert.That(found).IsFalse();
        await Assert.That(ownershipProbes).IsEqualTo(2);
    }

    private sealed class Owner { }
}
