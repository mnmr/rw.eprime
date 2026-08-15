using static EPrimeReadouts.Core.Tests.ArchitectureTestSupport;

namespace EPrimeReadouts.Core.Tests;

/// MultiFloors stack aggregation lives in the game layer, which tests cannot
/// execute; these source-text tests pin the architectural requirements that
/// have no executable boundary (reflection binding, cache keying, lifecycle).
public class LevelStackArchitectureTests
{
    [Test]
    public async Task RenderDataIsKeyedByTheCanonicalGroundMap()
    {
        string renderData = Source("GameRenderData.cs");
        string get = Method(renderData,
            "internal static RenderDataSnapshot<PoolSnapshot, RenderCountSnapshot> Get(");

        await Assert.That(get).Contains("map = LevelStacks.CanonicalOrSelf(map);");
    }

    [Test]
    public async Task StackMembershipChangesInvalidateRenderDataEntries()
    {
        string renderData = Source("GameRenderData.cs");
        string get = Method(renderData,
            "internal static RenderDataSnapshot<PoolSnapshot, RenderCountSnapshot> Get(");
        string reset = Method(renderData, "internal static void Reset()");

        await Assert.That(get).Contains("LevelStacks.MapSetStamp");
        await Assert.That(get).Contains("cache.Clear();");
        await Assert.That(reset).Contains("cacheMapSetStamp = -1;");
    }

    [Test]
    public async Task CountSnapshotAggregatesEveryStackLevelDeterministically()
    {
        string counts = Source("GameCounts.cs");
        string build = Method(counts,
            "internal static RenderCountSnapshot BuildSnapshot(");

        await Assert.That(build).Contains("LevelStacks.LevelsOf(map)");
        await Assert.That(build).Contains("order.Sort();");
        await Assert.That(build).Contains("sawQueriedMap");
        // Every level goes through the one per-map pass, so a stack's counts
        // and its planned-work reservations are gathered the same way.
        await Assert.That(counts).Contains("private static void AccumulateMap(");
        await Assert.That(counts).Contains(
            "Map map, CountAccumulator accumulator, PlannedWorkOptions plannedWork)");
    }

    [Test]
    public async Task MultiFloorsBindingIsOneTimeAndCompiledToACachedDelegate()
    {
        string stacks = Source("LevelStacks.cs");
        string resolve = Method(stacks, "private static void Resolve()");

        // One-time probe: the resolved flag flips before any early return so
        // a failed probe never retries or re-logs.
        await Assert.That(resolve).Contains("if (resolved) return;");
        await Assert.That(resolve).Contains("resolved = true;");

        // Steady-state lookups go through a compiled cached delegate, never
        // MethodInfo.Invoke.
        await Assert.That(stacks).Contains(
            "private static Func<Map, Dictionary<int, Map>> stackOf;");
        await Assert.That(stacks).Contains("Expression.Lambda<Func<Map, Dictionary<int, Map>>>");
        await Assert.That(Method(stacks, "internal static Dictionary<int, Map> LevelsOf(Map map)"))
            .DoesNotContain(".Invoke(");

        // A runtime failure disables the integration instead of throwing into
        // the render path.
        await Assert.That(Method(stacks, "internal static Dictionary<int, Map> LevelsOf(Map map)"))
            .Contains("stackOf = null;");
    }

    [Test]
    public async Task CanonicalLookupIsStampGatedAndCached()
    {
        string stacks = Source("LevelStacks.cs");
        string canonical = Method(stacks, "internal static Map CanonicalOrSelf(Map map)");

        await Assert.That(canonical).Contains("cachedStamp != mapSetStamp");
        await Assert.That(canonical).Contains("canonicalCache.TryGetValue(map, out Map canonical)");
    }

    [Test]
    public async Task MapLifecycleBumpsTheMapSetStampAndTeardownReleasesTheCache()
    {
        string lifecycle = Source("RuntimeTeardown.cs");
        string stacks = Source("LevelStacks.cs");

        await Assert.That(Method(lifecycle, "public ReadoutRenderMapComponent(Map map)"))
            .Contains("LevelStacks.BumpMapSet();");
        await Assert.That(Method(lifecycle, "public override void MapRemoved()"))
            .Contains("LevelStacks.BumpMapSet();");
        await Assert.That(Method(lifecycle, "internal static void ResetAll()"))
            .Contains("LevelStacks.Reset();");
        await Assert.That(Method(stacks, "internal static void Reset()"))
            .Contains("canonicalCache.Clear();");
    }
}
