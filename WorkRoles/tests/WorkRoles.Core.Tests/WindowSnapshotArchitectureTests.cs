namespace WorkRoles.Core.Tests;

public class WindowSnapshotArchitectureTests
{
    [Test]
    public async Task PawnSignalsContainNoPollingOrLiveSignaturePath()
    {
        string cache = Source("Signals", "PawnSignalSnapshotCache.cs");
        string collector = Source("Signals", "PawnSignalCollector.cs");
        string stats = Source("UI", "ColonistStatsState.cs");
        string tab = Source("UI", "ColonistsTabView.cs");
        string workPatches = Source("Patches", "Patch_PawnWorkSettings.cs");

        await Assert.That(cache).DoesNotContain("ObservationEpoch");
        await Assert.That(cache).DoesNotContain("TickManager");
        await Assert.That(cache).DoesNotContain("realtimeSinceStartup");
        await Assert.That(cache).Contains("WR-001 — INVALID REPORT");
        await Assert.That(collector).DoesNotContain("Signature(Pawn");
        await Assert.That(stats).DoesNotContain("ObservationEpochGate");
        await Assert.That(tab).DoesNotContain("ObserveSignalChanges");
        await Assert.That(workPatches).DoesNotContain(
            "PawnSignalSnapshotCache.Invalidate");
        await Assert.That(File.Exists(Path.Combine(
            RepoRoot(), "src", "WorkRoles.Core", "Signals",
            "MutableSignalObservationEpoch.cs"))).IsFalse();
    }

    [Test]
    public async Task UiRevisionRecapturesAllExternalPawnSignalsOnceAtLayoutBoundary()
    {
        string stats = Source("UI", "ColonistStatsState.cs");
        string tab = Source("UI", "ColonistsTabView.cs");
        string window = Source("UI", "MainTabWindow_WorkRoles.cs");

        await Assert.That(stats).Contains("RefreshExternalSnapshot");
        await Assert.That(stats).Contains("PawnSignalSnapshotCache.Clear()");
        await Assert.That(stats).Contains("foreach (Pawn pawn in pawns)");
        await Assert.That(tab).Contains("RefreshExternalSnapshotIfNeeded");
        string refresh = MethodBody(tab,
            "internal void RefreshExternalSnapshotIfNeeded()");
        await Assert.That(refresh)
            .Contains("if (!statsState.NeedsExternalSnapshotRefresh) return;")
            .Because("the revision guard must run before SnapshotPawns enumerates live pawns");
        await Assert.That(refresh).Contains("recommendationState.InvalidatePlan()");
        await Assert.That(refresh).Contains("roleCapabilityState.Invalidate()");
        await Assert.That(refresh).Contains("rosterState.InvalidateSnapshotConsumers()");
        await Assert.That(refresh).Contains("chipLayouts.Clear()");
        await Assert.That(window).Contains("EventType.Layout");
        await Assert.That(window).Contains("colonistsTab.RefreshExternalSnapshotIfNeeded()");
    }

    [Test]
    public async Task SkillsAndRecommendationFactsComeFromTheExplicitGeneration()
    {
        string stats = Source("UI", "ColonistStatsState.cs");
        string tab = Source("UI", "ColonistsTabView.cs");
        string adapter = Source("RecsAdapter.cs");

        string refresh = MethodBody(stats,
            "internal bool RefreshExternalSnapshot(IEnumerable<Pawn> pawns)");
        await Assert.That(refresh).Contains("SkillsTip.Lines(pawn)");
        await Assert.That(refresh).Contains("RecsAdapter.CapturePawnSnapshot");
        await Assert.That(tab).DoesNotContain("SkillsTip.Line(sr)");

        string projection = MethodBody(adapter,
            "internal static PawnView PawnViewOf(\n            Pawn pawn,\n            RoleStore store,\n            PawnExternalSnapshot snapshot)");
        await Assert.That(projection).Contains("snapshot.RecommendationFacts");
        await Assert.That(projection).DoesNotContain("pawn.skills");
        await Assert.That(projection).DoesNotContain("pawn.equipment");
        await Assert.That(projection).DoesNotContain("pawn.genes");
        await Assert.That(projection).DoesNotContain("pawn.WorkTypeIsDisabled");
    }

    [Test]
    public async Task TimeRuleInvalidationsStillAdvanceTheUiRevision()
    {
        string orders = Source("CompiledJobOrders.cs");
        string timezone = Source("Patches", "Patch_CaravanTimezone.cs");

        string hourly = MethodBody(orders, "public static void InvalidateAllTimeRuled()");
        string batch = MethodBody(orders,
            "internal static void InvalidateBatch(IEnumerable<Pawn> pawns)");
        await Assert.That(hourly).Contains("UiVersion.Bump()");
        await Assert.That(batch).Contains("UiVersion.Bump()");
        await Assert.That(timezone).Contains("CompiledJobOrders.InvalidateBatch");
    }

    private static string Source(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { RepoRoot(), "src", "WorkRoles" }
            .Concat(parts).ToArray()));

    private static string MethodBody(string source, string declaration)
    {
        int start = source.IndexOf(declaration, StringComparison.Ordinal);
        if (start < 0) return "";
        int open = source.IndexOf('{', start);
        int depth = 0;
        for (int i = open; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}' && --depth == 0)
                return source.Substring(open, i - open + 1);
        }
        return "";
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "WorkRoles.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repo root not found");
    }
}
