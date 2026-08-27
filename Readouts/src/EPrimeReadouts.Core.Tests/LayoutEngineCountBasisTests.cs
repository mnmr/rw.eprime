using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

/// The storage-only and hide-forbidden options narrow the displayed count
/// everywhere, not just in search results: group slots, pool sums, slot
/// visibility and threshold bands all use the narrowed basis.
public class LayoutEngineCountBasisTests
{
    private static ReadoutGroup Group(params string[] tokens)
    {
        var group = new ReadoutGroup { Id = 1, Name = "G1" };
        group.Tiers.Add(tokens.ToList());
        return group;
    }

    private static LayoutInput Input(
        ReadoutGroup group,
        Dictionary<string, SearchCount>? searchCounts,
        bool storageOnly = false, bool hideForbidden = false)
    {
        return new LayoutInput
        {
            Groups = new List<ReadoutGroup> { group },
            Counts = StaticResources.Counts(
                ("Steel", 120), ("Meat_Cow", 30), ("Meat_Chicken", 10)),
            SearchCounts = searchCounts,
            SearchStorageOnly = storageOnly,
            SearchHideForbidden = hideForbidden,
            Thresholds = new Dictionary<string, ThresholdSpec>(),
            Catalog = StaticResources.Catalog(),
            Width = 140f,
        };
    }

    private static RenderCell Counter(RenderModel model) =>
        model.Cells.First(c => c.Kind == CellKind.Counter);

    // Steel: 150 on the map, 110 stored, 140 unforbidden map-wide, 100
    // stored-and-unforbidden. Every basis differs from the raw count (120)
    // so a fallback to Counts cannot pass these tests by accident.
    private static Dictionary<string, SearchCount> SteelBreakdown() =>
        new() { ["Steel"] = new SearchCount(150, 110, 140, 100) };

    [Test]
    public async Task GroupSlotShowsMapWideCountWhenStorageOnlyOff()
    {
        var model = ReadoutLayoutEngine.Build(
            Input(Group("Steel"), SteelBreakdown()));
        await Assert.That(Counter(model).Count).IsEqualTo(150);
        await Assert.That(Counter(model).Text).IsEqualTo("150");
    }

    [Test]
    public async Task GroupSlotShowsStoredCountWhenStorageOnlyOn()
    {
        var model = ReadoutLayoutEngine.Build(
            Input(Group("Steel"), SteelBreakdown(), storageOnly: true));
        await Assert.That(Counter(model).Count).IsEqualTo(110);
    }

    [Test]
    public async Task GroupSlotDropsForbiddenStacksWhenHideForbiddenOn()
    {
        var model = ReadoutLayoutEngine.Build(
            Input(Group("Steel"), SteelBreakdown(), hideForbidden: true));
        await Assert.That(Counter(model).Count).IsEqualTo(140);
    }

    [Test]
    public async Task StorageOnlyAndHideForbiddenComposeOnStoredUnforbidden()
    {
        var model = ReadoutLayoutEngine.Build(
            Input(Group("Steel"), SteelBreakdown(),
                storageOnly: true, hideForbidden: true));
        await Assert.That(Counter(model).Count).IsEqualTo(100);
    }

    [Test]
    public async Task PoolSlotSumsNarrowedMemberCounts()
    {
        var input = Input(Group("#1"), new Dictionary<string, SearchCount>
        {
            // Cow: 50 map-wide, 30 stored; Chicken: 25 map-wide, 10 stored.
            ["Meat_Cow"] = new SearchCount(50, 30, 50, 30),
            ["Meat_Chicken"] = new SearchCount(25, 10, 25, 10),
        });
        input.Pools = StaticResources.MeatPool();
        var model = ReadoutLayoutEngine.Build(input);
        await Assert.That(Counter(model).Count).IsEqualTo(75);
    }

    [Test]
    public async Task HideWhenZeroSlotDisappearsWhenNarrowedCountIsZero()
    {
        // All of Steel is forbidden: raw count 120, narrowed count 0.
        var input = Input(Group("~Steel"), new Dictionary<string, SearchCount>
        {
            ["Steel"] = new SearchCount(120, 120, 0, 0),
        }, hideForbidden: true);
        var model = ReadoutLayoutEngine.Build(input);
        await Assert.That(model.Cells.Any(c => c.Kind == CellKind.Icon)).IsFalse();
    }

    [Test]
    public async Task ThresholdBandReflectsNarrowedCount()
    {
        // Raw 120 is healthy; the narrowed stored count 40 is below Low=50.
        var input = Input(Group("Steel"), SteelBreakdown(), storageOnly: true);
        input.SearchCounts = new Dictionary<string, SearchCount>
        {
            ["Steel"] = new SearchCount(150, 40, 150, 40),
        };
        input.Thresholds["Steel"] = new ThresholdSpec(50, 10);
        var model = ReadoutLayoutEngine.Build(input);
        await Assert.That(Counter(model).Band).IsEqualTo(Band.Low);
    }

    [Test]
    public async Task NullSearchCountsFallsBackToRawCounts()
    {
        var model = ReadoutLayoutEngine.Build(
            Input(Group("Steel"), null, storageOnly: true, hideForbidden: true));
        await Assert.That(Counter(model).Count).IsEqualTo(120);
    }
}
