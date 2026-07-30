using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

public class RenderDataCacheTests
{
    private sealed class BuildState
    {
        public string Prefix;
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

}
