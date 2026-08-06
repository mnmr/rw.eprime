namespace QualityJobs.Core.Tests;

using QualityJobs.Core;

public class TipSupportTests
{
    [Test]
    public async Task GatherPolicyGathersWhenTextIsMissing()
    {
        await Assert.That(TipGatherPolicy.ShouldGather(TipRefresh.Pinned, false, 10, 9)).IsTrue();
        await Assert.That(TipGatherPolicy.ShouldGather(TipRefresh.PerSession, false, 10, 9)).IsTrue();
    }

    [Test]
    public async Task PinnedTextNeverRegathers()
    {
        await Assert.That(TipGatherPolicy.ShouldGather(TipRefresh.Pinned, true, 500, 10)).IsFalse();
    }

    [Test]
    public async Task SessionTextRegathersOnlyAfterAFrameGap()
    {
        await Assert.That(TipGatherPolicy.ShouldGather(TipRefresh.PerSession, true, 11, 10)).IsFalse();
        await Assert.That(TipGatherPolicy.ShouldGather(TipRefresh.PerSession, true, 12, 10)).IsTrue();
    }

    [Test]
    public async Task MetricRevisionBumpsOnlyOnActualChange()
    {
        var rev = new UiMetricRevision();
        rev.Observe(1f, false, "English");
        int initial = rev.Current;
        rev.Observe(1f, false, "English");
        await Assert.That(rev.Current).IsEqualTo(initial);
        rev.Observe(1.25f, false, "English");
        await Assert.That(rev.Current).IsEqualTo(initial + 1);
        rev.Observe(1.25f, false, "German");
        await Assert.That(rev.Current).IsEqualTo(initial + 2);
    }

    [Test]
    public async Task FirstMetricObservationDoesNotBump()
    {
        var rev = new UiMetricRevision();
        rev.Observe(2f, true, "English");
        await Assert.That(rev.Current).IsEqualTo(0);
    }

    [Test]
    public async Task ColumnGridPartitionsColumnMajor()
    {
        await Assert.That(ColumnGrid.ColumnCount(0, 20)).IsEqualTo(0);
        await Assert.That(ColumnGrid.ColumnCount(20, 20)).IsEqualTo(1);
        await Assert.That(ColumnGrid.ColumnCount(21, 20)).IsEqualTo(2);
        await Assert.That(ColumnGrid.RowsInColumn(0, 21, 20)).IsEqualTo(20);
        await Assert.That(ColumnGrid.RowsInColumn(1, 21, 20)).IsEqualTo(1);
        await Assert.That(ColumnGrid.RowsInColumn(2, 21, 20)).IsEqualTo(0);
    }

    [Test]
    public async Task RegistryRetiresUntouchedEntriesAndFlushes()
    {
        var registry = new OwnerGenerationRegistry<object, string, string, string>();
        var owner = new object();

        registry.Begin(owner);
        registry.Touch("a", "lookup-a", "value-a");
        registry.Touch("b", "lookup-b", "value-b");
        registry.End(owner);
        await Assert.That(registry.Count).IsEqualTo(2);

        registry.Begin(owner);
        registry.Touch("a", "lookup-a", "value-a2");
        registry.End(owner);
        // b is retired but still resolvable until the flush.
        await Assert.That(registry.TryGet("lookup-b", out string? beforeFlush)).IsTrue();
        await Assert.That(beforeFlush).IsEqualTo("value-b");
        await Assert.That(registry.RetiredCount).IsEqualTo(1);

        registry.FlushRetired();
        await Assert.That(registry.TryGet("lookup-b", out _)).IsFalse();
        await Assert.That(registry.TryGet("lookup-a", out string? kept)).IsTrue();
        await Assert.That(kept).IsEqualTo("value-a2");
    }

    [Test]
    public async Task RegistryReleaseDropsAnOwnersEntries()
    {
        var registry = new OwnerGenerationRegistry<object, string, string, string>();
        var owner = new object();
        registry.Begin(owner);
        registry.Touch("a", "lookup-a", "value-a");
        registry.End(owner);

        registry.Release(owner);
        await Assert.That(registry.Count).IsEqualTo(0);
        await Assert.That(registry.TryGet("lookup-a", out _)).IsFalse();
    }
}
