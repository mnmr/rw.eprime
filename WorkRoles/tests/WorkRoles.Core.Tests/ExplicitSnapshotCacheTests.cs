using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

public class ExplicitSnapshotCacheTests
{
    [Test]
    public async Task ValuesRemainFrozenUntilExplicitClear()
    {
        int source = 10;
        int builds = 0;
        var cache = new ExplicitSnapshotCache<Owner, int>(
            _ => { builds++; return source; });
        var owner = new Owner();

        await Assert.That(cache.Get(owner)).IsEqualTo(10);
        source = 20;
        await Assert.That(cache.Get(owner)).IsEqualTo(10);
        await Assert.That(builds).IsEqualTo(1);

        cache.Clear();

        await Assert.That(cache.Get(owner)).IsEqualTo(20);
        await Assert.That(builds).IsEqualTo(2);
    }

    [Test]
    public async Task InvalidateRefreshesOnlyTheSpecifiedOwner()
    {
        int source = 10;
        int builds = 0;
        var cache = new ExplicitSnapshotCache<Owner, int>(
            _ => { builds++; return source; });
        var first = new Owner();
        var second = new Owner();

        cache.Get(first);
        cache.Get(second);
        source = 20;
        cache.Invalidate(first);

        await Assert.That(cache.Get(first)).IsEqualTo(20);
        await Assert.That(cache.Get(second)).IsEqualTo(10);
        await Assert.That(builds).IsEqualTo(3);
    }

    private sealed class Owner { }
}
