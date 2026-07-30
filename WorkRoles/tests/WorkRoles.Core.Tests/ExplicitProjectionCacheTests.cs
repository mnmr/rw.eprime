using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

public class ExplicitProjectionCacheTests
{
    [Test]
    public async Task CacheMissBuildsWithoutPublishingProjection()
    {
        var owner = new Owner();
        int builds = 0;
        int publications = 0;
        var cache = new ExplicitProjectionCache<Owner, int>(
            _ => ++builds,
            (_, _) => publications++);

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
        var publications = new List<int>();
        var cache = new ExplicitProjectionCache<Owner, int>(
            _ => source,
            (_, snapshot) => publications.Add(snapshot));

        await Assert.That(cache.GetOrBuild(owner)).IsEqualTo(10);
        source = 20;

        cache.PublishFresh(owner);

        await Assert.That(publications).IsEquivalentTo(new[] { 20 });
        await Assert.That(cache.GetOrBuild(owner)).IsEqualTo(20);
    }

    private sealed class Owner { }
}
