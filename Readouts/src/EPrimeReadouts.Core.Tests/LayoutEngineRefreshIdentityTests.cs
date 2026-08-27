namespace EPrimeReadouts.Core.Tests;

/// A count refresh over an unchanged snapshot must be a no-op that preserves
/// cell payload identity: counter text strings must not be re-formatted when
/// their sums did not change, so the glyph surface's content revision (and
/// downstream pixel caches) see identical content.
public class LayoutEngineRefreshIdentityTests
{
    private static ReadoutGroup Group(int id, params string[] tokens)
    {
        var group = new ReadoutGroup { Id = id, Name = "G" + id };
        group.Tiers.Add(tokens.ToList());
        return group;
    }

    // Counts above 10000 format through the compact path, whose strings are
    // never runtime-cached — small integers can come from the BCL's cached
    // digit strings on modern runtimes, which would fake identity here.
    private static LayoutInput Input(params ReadoutGroup[] groups) => new()
    {
        Groups = groups.ToList(),
        Counts = StaticResources.Counts(("Steel", 12786), ("WoodLog", 20500)),
        Thresholds = new Dictionary<string, ThresholdSpec>(),
        Catalog = StaticResources.Catalog(),
        Width = 140f,
        DepthOf = g => 1,
    };

    private static List<int> CounterIndices(RenderModel model)
    {
        var indices = new List<int>();
        for (int i = 0; i < model.Cells.Count; i++)
            if (model.Cells[i].Kind == CellKind.Counter) indices.Add(i);
        return indices;
    }

    [Test]
    public async Task UnchangedCountsPreserveCounterTextIdentity()
    {
        var input = Input(Group(1, "Steel", "WoodLog"));
        var model = ReadoutLayoutEngine.Build(input);
        var counters = CounterIndices(model);
        var before = counters.Select(i => model.Cells[i].Text).ToList();

        // Fresh-but-equal dictionary models a new snapshot with equal counts.
        input.Counts = StaticResources.Counts(("Steel", 12786), ("WoodLog", 20500));
        await Assert.That(ReadoutLayoutEngine.TryRefreshCounts(input, model)).IsTrue();

        for (int i = 0; i < counters.Count; i++)
            await Assert.That(ReferenceEquals(
                model.Cells[counters[i]].Text, before[i])).IsTrue();
    }

    [Test]
    public async Task ChangedCountUpdatesOnlyThatCounter()
    {
        var input = Input(Group(1, "Steel", "WoodLog"));
        var model = ReadoutLayoutEngine.Build(input);
        var counters = CounterIndices(model);
        string steelBefore = model.Cells[counters[0]].Text!;
        string woodBefore = model.Cells[counters[1]].Text!;

        input.Counts = StaticResources.Counts(("Steel", 12900), ("WoodLog", 20500));
        await Assert.That(ReadoutLayoutEngine.TryRefreshCounts(input, model)).IsTrue();

        await Assert.That(model.Cells[counters[0]].Text).IsEqualTo("12.9k");
        await Assert.That(model.Cells[counters[0]].Count).IsEqualTo(12900);
        await Assert.That(ReferenceEquals(
            model.Cells[counters[1]].Text, woodBefore)).IsTrue();
        await Assert.That(ReferenceEquals(
            model.Cells[counters[0]].Text, steelBefore)).IsFalse();
    }

    [Test]
    public async Task RefreshKeepsIconCountInSyncWithCounter()
    {
        var input = Input(Group(1, "Steel"));
        var model = ReadoutLayoutEngine.Build(input);
        input.Counts = StaticResources.Counts(("Steel", 60), ("WoodLog", 20500));
        await Assert.That(ReadoutLayoutEngine.TryRefreshCounts(input, model)).IsTrue();
        int iconIndex = model.SlotHits[0].CellIndex;
        await Assert.That(model.Cells[iconIndex].Count).IsEqualTo(60);
        await Assert.That(model.Cells[iconIndex + 1].Count).IsEqualTo(60);
    }

    [Test]
    public async Task SlotVisibilityChangeRefusesRefresh()
    {
        // "~WoodLog" hides at zero: dropping wood to 0 changes which slots
        // exist, which is a structural change the refresh must reject.
        var input = Input(Group(1, "Steel", "~WoodLog"));
        var model = ReadoutLayoutEngine.Build(input);
        await Assert.That(model.SlotHits.Count).IsEqualTo(2);
        input.Counts = StaticResources.Counts(("Steel", 12786), ("WoodLog", 0));
        await Assert.That(ReadoutLayoutEngine.TryRefreshCounts(input, model)).IsFalse();
    }

    [Test]
    public async Task EmptyBandGroupRefreshesInPlace()
    {
        // A group holding its identification band (no visible slots) must
        // stay refreshable as long as it stays empty.
        var input = Input(Group(1, "~Cloth"), Group(2, "Steel"));
        var model = ReadoutLayoutEngine.Build(input);
        input.Counts = StaticResources.Counts(("Steel", 20500), ("WoodLog", 75));
        await Assert.That(ReadoutLayoutEngine.TryRefreshCounts(input, model)).IsTrue();
        int iconIndex = model.SlotHits[0].CellIndex;
        await Assert.That(model.Cells[iconIndex + 1].Text).IsEqualTo("20.5k");
    }
}
