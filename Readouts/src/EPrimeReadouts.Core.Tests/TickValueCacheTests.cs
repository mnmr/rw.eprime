using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

public class TickValueCacheTests
{
    [Test]
    public async Task RebuildsAtTheConfiguredTickInterval()
    {
        var cache = new TickValueCache<string, int>(refreshIntervalTicks: 1020);
        int builds = 0;

        int initial = cache.Get("map", tick: 100, state: 0,
            _ => ++builds);
        int beforeBoundary = cache.Get("map", tick: 1119, state: 0,
            _ => ++builds);
        int atBoundary = cache.Get("map", tick: 1120, state: 0,
            _ => ++builds);

        await Assert.That(initial).IsEqualTo(1);
        await Assert.That(beforeBoundary).IsEqualTo(1);
        await Assert.That(atBoundary).IsEqualTo(2);
        await Assert.That(builds).IsEqualTo(2);
    }

    [Test]
    public async Task RemovalForcesAnImmediateRebuild()
    {
        var cache = new TickValueCache<string, int>(refreshIntervalTicks: 1020);
        int builds = 0;
        cache.Get("map", tick: 100, state: 0, _ => ++builds);

        cache.Remove("map");
        int rebuilt = cache.Get("map", tick: 101, state: 0,
            _ => ++builds);

        await Assert.That(rebuilt).IsEqualTo(2);
    }
}
