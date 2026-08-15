using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

/// A count dependency that lives outside game state — a player option that
/// changes what the count pass gathers — must apply immediately, including
/// while the game is paused and the tick never advances.
public class RenderDataCacheInvalidationTests
{
    private const int Interval = 204;

    private static RenderDataCache<string, int, string, string> NewCache() =>
        new(Interval);

    [Test]
    public async Task CountsStayThrottledWithoutAnInvalidation()
    {
        var cache = NewCache();
        int builds = 0;
        string Counts() { builds++; return "counts" + builds; }

        cache.Get("map", 1, tick: 0, () => "pools", Counts);
        cache.Get("map", 1, tick: Interval - 1, () => "pools", Counts);

        await Assert.That(builds).IsEqualTo(1);
    }

    [Test]
    public async Task InvalidatingCountsRebuildsBeforeTheIntervalElapses()
    {
        var cache = NewCache();
        int builds = 0;
        string Counts() { builds++; return "counts" + builds; }

        cache.Get("map", 1, tick: 0, () => "pools", Counts);
        cache.InvalidateCounts();
        var snapshot = cache.Get("map", 1, tick: 0, () => "pools", Counts);

        await Assert.That(builds).IsEqualTo(2);
        await Assert.That(snapshot.Counts).IsEqualTo("counts2");
    }

    [Test]
    public async Task InvalidationAppliesWhileTheGameIsPaused()
    {
        // Same tick throughout: a paused game must still show the change.
        var cache = NewCache();
        int builds = 0;
        string Counts() { builds++; return "counts" + builds; }

        cache.Get("map", 1, tick: 5000, () => "pools", Counts);
        cache.InvalidateCounts();
        cache.Get("map", 1, tick: 5000, () => "pools", Counts);
        cache.Get("map", 1, tick: 5000, () => "pools", Counts);

        // Rebuilt exactly once for the invalidation, then throttled again.
        await Assert.That(builds).IsEqualTo(2);
    }

    [Test]
    public async Task InvalidationDoesNotRebuildTheStructure()
    {
        var cache = NewCache();
        int structureBuilds = 0;
        string Pools() { structureBuilds++; return "pools"; }

        cache.Get("map", 1, tick: 0, Pools, () => "counts");
        cache.InvalidateCounts();
        cache.Get("map", 1, tick: 0, Pools, () => "counts");

        await Assert.That(structureBuilds).IsEqualTo(1);
    }

    [Test]
    public async Task AnEqualRebuildAfterInvalidationPreservesSnapshotIdentity()
    {
        var cache = NewCache();
        var first = cache.Get("map", 1, tick: 0, () => "pools", () => "counts");
        cache.InvalidateCounts();
        var second = cache.Get("map", 1, tick: 0, () => "pools", () => "counts");

        await Assert.That(ReferenceEquals(second, first)).IsTrue();
    }

    [Test]
    public async Task InvalidationCoversEveryCachedMap()
    {
        var cache = NewCache();
        int builds = 0;
        string Counts() { builds++; return "counts" + builds; }

        cache.Get("ground", 1, tick: 0, () => "pools", Counts);
        cache.Get("orbit", 1, tick: 0, () => "pools", Counts);
        cache.InvalidateCounts();
        cache.Get("ground", 1, tick: 0, () => "pools", Counts);
        cache.Get("orbit", 1, tick: 0, () => "pools", Counts);

        await Assert.That(builds).IsEqualTo(4);
    }

    [Test]
    public async Task InvalidationIsConsumedOnceNotStandingForever()
    {
        var cache = NewCache();
        int builds = 0;
        string Counts() { builds++; return "counts" + builds; }

        cache.Get("map", 1, tick: 0, () => "pools", Counts);
        cache.InvalidateCounts();
        cache.Get("map", 1, tick: 0, () => "pools", Counts);
        cache.Get("map", 1, tick: 1, () => "pools", Counts);
        cache.Get("map", 1, tick: 2, () => "pools", Counts);

        await Assert.That(builds).IsEqualTo(2);
    }

    [Test]
    public async Task InvalidationOnAnEmptyCacheIsSafe()
    {
        var cache = NewCache();
        cache.InvalidateCounts();

        var snapshot = cache.Get("map", 1, tick: 0, () => "pools", () => "counts");

        await Assert.That(snapshot.Counts).IsEqualTo("counts");
    }

    [Test]
    public async Task InvalidationAlsoAppliesToTheStatefulOverload()
    {
        var cache = NewCache();
        int builds = 0;

        cache.Get("map", 1, 0, "state", s => "pools",
            (s, structure) => { builds++; return "counts" + builds; });
        cache.InvalidateCounts();
        cache.Get("map", 1, 0, "state", s => "pools",
            (s, structure) => { builds++; return "counts" + builds; });

        await Assert.That(builds).IsEqualTo(2);
    }

    [Test]
    public async Task FailedInvalidationRebuildIsRetriedAtTheSameTick()
    {
        var cache = NewCache();
        cache.Get("map", 1, tick: 5000, () => "pools", () => "counts1");
        cache.InvalidateCounts();

        await Assert.That(() => cache.Get(
                "map", 1, tick: 5000, () => "pools",
                () => throw new InvalidOperationException("count scan failed")))
            .Throws<InvalidOperationException>();

        var recovered = cache.Get(
            "map", 1, tick: 5000, () => "pools", () => "counts2");

        await Assert.That(recovered.Counts).IsEqualTo("counts2");
    }

    [Test]
    public async Task FailedStatefulInvalidationRebuildIsRetriedAtTheSameTick()
    {
        var cache = NewCache();
        cache.Get("map", 1, 5000, "initial", s => "pools",
            (s, structure) => "counts1");
        cache.InvalidateCounts();

        await Assert.That(() => cache.Get(
                "map", 1, 5000, "failing", s => "pools",
                (s, structure) => throw new InvalidOperationException("count scan failed")))
            .Throws<InvalidOperationException>();

        var recovered = cache.Get(
            "map", 1, 5000, "recovered", s => "pools",
            (s, structure) => "counts2");

        await Assert.That(recovered.Counts).IsEqualTo("counts2");
    }
}
