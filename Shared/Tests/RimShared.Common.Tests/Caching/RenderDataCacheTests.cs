using RimShared.Common;

namespace RimShared.Common.Tests;

public class RenderDataCacheTests
{
    [Test]
    public async Task RefreshTimestampTracksWorkEvenWhenSnapshotIdentityIsPreserved()
    {
        var cache = new RenderDataCache<string, int, string, string>(204);
        await Assert.That(cache.TryGetLastCountRefreshTick("a", out _)).IsFalse();
        var first = cache.Get("a", 1, 100, () => "structure", () => "equal");
        cache.Get("b", 1, 200, () => "other", () => "equal");
        await Assert.That(cache.TryGetLastCountRefreshTick("a", out int firstTick)).IsTrue();
        await Assert.That(firstTick).IsEqualTo(100);
        var unchanged = cache.Get("a", 2, 303, () => "changed", () => "equal");
        cache.TryGetLastCountRefreshTick("a", out int before);
        await Assert.That(before).IsEqualTo(100);
        var refreshed = cache.Get("a", 2, 304, () => "unused", () => "equal");
        await Assert.That(refreshed).IsSameReferenceAs(unchanged);
        cache.TryGetLastCountRefreshTick("a", out int after);
        await Assert.That(after).IsEqualTo(304);
        cache.TryGetLastCountRefreshTick("b", out int other);
        await Assert.That(other).IsEqualTo(200);
        cache.InvalidateCounts("a");
        cache.Get("a", 2, 305, () => "unused", () => "equal");
        cache.TryGetLastCountRefreshTick("a", out int forced);
        await Assert.That(forced).IsEqualTo(305);
        cache.Remove("a");
        await Assert.That(cache.TryGetLastCountRefreshTick("a", out _)).IsFalse();
        cache.Clear();
        await Assert.That(cache.TryGetLastCountRefreshTick("b", out _)).IsFalse();
    }

    private sealed class BuildState
    {
        public string Prefix = null!; // Assigned by each test that uses it.
    }

    [Test]
    public async Task CountsAreSharedUntilTheRefreshIntervalElapses()
    {
        var cache = new RenderDataCache<string, int, string, string>(204);
        int structureBuilds = 0;
        int countBuilds = 0;

        var first = cache.Get(
            "map-a", 1, 100,
            () => $"structure-{++structureBuilds}",
            () => $"counts-{++countBuilds}");
        var beforeInterval = cache.Get(
            "map-a", 1, 303,
            () => $"structure-{++structureBuilds}",
            () => $"counts-{++countBuilds}");

        await Assert.That(beforeInterval).IsSameReferenceAs(first);
        await Assert.That(structureBuilds).IsEqualTo(1);
        await Assert.That(countBuilds).IsEqualTo(1);
    }

    [Test]
    public async Task CountsRefreshWhenTheIntervalElapses()
    {
        var cache = new RenderDataCache<string, int, string, string>(204);
        int countBuilds = 0;

        var first = cache.Get(
            "map-a", 1, 100,
            () => "structure",
            () => $"counts-{++countBuilds}");
        var refreshed = cache.Get(
            "map-a", 1, 304,
            () => "unexpected-structure",
            () => $"counts-{++countBuilds}");

        await Assert.That(refreshed).IsNotSameReferenceAs(first);
        await Assert.That(refreshed.Structure).IsEqualTo("structure");
        await Assert.That(refreshed.Counts).IsEqualTo("counts-2");
        await Assert.That(countBuilds).IsEqualTo(2);
    }

    [Test]
    public async Task StructuralChangesApplyImmediatelyWithoutRefreshingCounts()
    {
        var cache = new RenderDataCache<string, int, string, string>(204);
        int structureBuilds = 0;
        int countBuilds = 0;

        var first = cache.Get(
            "map-a", 1, 100,
            () => $"structure-{++structureBuilds}",
            () => $"counts-{++countBuilds}");
        var structurallyChanged = cache.Get(
            "map-a", 2, 101,
            () => $"structure-{++structureBuilds}",
            () => $"counts-{++countBuilds}");

        await Assert.That(structurallyChanged).IsNotSameReferenceAs(first);
        await Assert.That(structurallyChanged.Structure).IsEqualTo("structure-2");
        await Assert.That(structurallyChanged.Counts).IsEqualTo("counts-1");
        await Assert.That(structureBuilds).IsEqualTo(2);
        await Assert.That(countBuilds).IsEqualTo(1);
    }

    [Test]
    public async Task EqualRefreshedCountsKeepTheExistingSnapshotIdentity()
    {
        var cache = new RenderDataCache<string, int, string, string>(
            204, StringComparer.Ordinal);
        int countBuilds = 0;

        var first = cache.Get(
            "map-a", 1, 100,
            () => "structure",
            () => { countBuilds++; return "stable-counts"; });
        var unchanged = cache.Get(
            "map-a", 1, 304,
            () => "unexpected-structure",
            () => { countBuilds++; return "stable-counts"; });

        await Assert.That(unchanged).IsSameReferenceAs(first);
        await Assert.That(countBuilds).IsEqualTo(2);
    }

    [Test]
    public async Task BuildersCanReceiveStateWithoutCapturedCallbacks()
    {
        var cache = new RenderDataCache<string, int, string, string>(204);
        var state = new BuildState { Prefix = "map-a" };

        var snapshot = cache.Get(
            "map-a", 1, 100, state,
            static s => s.Prefix + "-structure",
            static (s, structure) => s.Prefix + "-counts-from-" + structure);

        await Assert.That(snapshot.Structure).IsEqualTo("map-a-structure");
        await Assert.That(snapshot.Counts).IsEqualTo(
            "map-a-counts-from-map-a-structure");
    }

    [Test]
    public async Task RemovingAMapReleasesOnlyThatMapsSnapshot()
    {
        var cache = new RenderDataCache<string, int, string, string>(204);
        var mapA = cache.Get("map-a", 1, 100, () => "a-structure", () => "a-counts");
        var mapB = cache.Get("map-b", 1, 100, () => "b-structure", () => "b-counts");

        bool removed = cache.Remove("map-a");
        var mapBAgain = cache.Get("map-b", 1, 101,
            () => "unexpected-structure", () => "unexpected-counts");
        var mapARebuilt = cache.Get("map-a", 1, 101,
            () => "a-structure-2", () => "a-counts-2");

        await Assert.That(removed).IsTrue();
        await Assert.That(mapBAgain).IsSameReferenceAs(mapB);
        await Assert.That(mapARebuilt).IsNotSameReferenceAs(mapA);
        await Assert.That(cache.Count).IsEqualTo(2);
    }

    [Test]
    public async Task ClearReleasesEveryMapSnapshot()
    {
        var cache = new RenderDataCache<string, int, object, object>(204);
        object mapA = cache.Get("map-a", 1, 0, () => new object(), () => new object());
        object mapB = cache.Get("map-b", 1, 0, () => new object(), () => new object());

        cache.Clear();
        var rebuiltA = cache.Get("map-a", 1, 1, () => new object(), () => new object());

        await Assert.That(cache.Count).IsEqualTo(1);
        await Assert.That(rebuiltA).IsNotSameReferenceAs(mapA);
        await Assert.That(rebuiltA).IsNotSameReferenceAs(mapB);
    }

}
