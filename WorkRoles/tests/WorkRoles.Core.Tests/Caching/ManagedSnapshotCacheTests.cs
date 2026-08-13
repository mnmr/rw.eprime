namespace WorkRoles.Core.Tests.Caching;

public class ManagedSnapshotCacheTests
{
    [Test]
    public async Task CachedManagedHitSkipsTheOwnershipProbe()
    {
        var owner = new Owner();
        int ownershipProbes = 0;
        int builds = 0;
        var cache = new ManagedSnapshotCache<Owner, int>();

        bool first = cache.TryGetManaged(
            owner,
            _ =>
            {
                ownershipProbes++;
                return true;
            },
            _ => ++builds,
            out int firstValue
        );
        bool repeated = cache.TryGetManaged(
            owner,
            _ =>
            {
                ownershipProbes++;
                return false;
            },
            _ => ++builds,
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
    public async Task UnmanagedMissFallsThroughWithoutPublishingASnapshot()
    {
        var owner = new Owner();
        int builds = 0;
        var cache = new ManagedSnapshotCache<Owner, int>();

        bool found = cache.TryGetManaged(owner, _ => false, _ => ++builds, out int value);

        await Assert.That(found).IsFalse();
        await Assert.That(value).IsEqualTo(0);
        await Assert.That(builds).IsEqualTo(0);
        await Assert.That(cache.Count).IsEqualTo(0);
    }

    [Test]
    public async Task RemovalRequiresOwnershipToBeProvenAgain()
    {
        var owner = new Owner();
        int ownershipProbes = 0;
        var cache = new ManagedSnapshotCache<Owner, int>();
        cache.TryGetManaged(
            owner,
            _ =>
            {
                ownershipProbes++;
                return true;
            },
            _ => 7,
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
            _ => 9,
            out _
        );

        await Assert.That(found).IsFalse();
        await Assert.That(ownershipProbes).IsEqualTo(2);
    }

    [Test]
    public async Task GetOrBuildPublishesOnceForAlreadyAuthorizedCallers()
    {
        var owner = new Owner();
        int builds = 0;
        var cache = new ManagedSnapshotCache<Owner, int>();

        int first = cache.GetOrBuild(owner, _ => ++builds);
        int repeated = cache.GetOrBuild(owner, _ => ++builds);

        await Assert.That(first).IsEqualTo(1);
        await Assert.That(repeated).IsEqualTo(1);
        await Assert.That(builds).IsEqualTo(1);
    }

    private sealed class Owner { }
}
