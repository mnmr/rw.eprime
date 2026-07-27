using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

/// Tests for category pool token rendering in the layout engine.
public class LayoutEnginePoolTests
{
    private static ReadoutGroup Group(int id, params string[] tokens)
    {
        var group = new ReadoutGroup { Id = id, Name = "G" + id };
        group.Tiers.Add(tokens.ToList());
        return group;
    }

    private static LayoutInput Input(ReadoutGroup group, Dictionary<string, int> counts = null,
        Dictionary<string, ThresholdSpec> thresholds = null) => new()
    {
        Groups = new List<ReadoutGroup> { group },
        Counts = counts ?? new Dictionary<string, int>(),
        Thresholds = thresholds ?? new Dictionary<string, ThresholdSpec>(),
        Catalog = StaticResources.Catalog(),
        Width = 140f,
    };

    private static List<RenderCell> CellsOf(RenderModel model, CellKind kind) =>
        model.Cells.Where(c => c.Kind == kind).ToList();

    [Test]
    public async Task PoolSumAndFirstMemberIcon()
    {
        // @MeatRaw has members Meat_Cow=5, Meat_Chicken=7 → sum 12
        // Icon DefName must be first member "Meat_Cow", Token must be "@MeatRaw"
        var counts = StaticResources.Counts(("Meat_Cow", 5), ("Meat_Chicken", 7));
        var model = ReadoutLayoutEngine.Build(Input(Group(1, "@MeatRaw"), counts));

        var icons = CellsOf(model, CellKind.Icon);
        await Assert.That(icons.Count).IsEqualTo(1);
        await Assert.That(icons[0].DefName).IsEqualTo("Meat_Cow");
        await Assert.That(icons[0].Token).IsEqualTo("@MeatRaw");

        var counters = CellsOf(model, CellKind.Counter);
        await Assert.That(counters.Count).IsEqualTo(1);
        await Assert.That(counters[0].Text).IsEqualTo("12");
        await Assert.That(counters[0].Token).IsEqualTo("@MeatRaw");
        await Assert.That(counters[0].DefName).IsEqualTo("Meat_Cow");
    }

    [Test]
    public async Task PoolVisibleAtZeroSumByDefault()
    {
        // No counts → pool shows with "0" (show-when-zero default)
        var model = ReadoutLayoutEngine.Build(Input(Group(1, "@MeatRaw")));
        var counters = CellsOf(model, CellKind.Counter);
        await Assert.That(counters.Count).IsEqualTo(1);
        await Assert.That(counters[0].Text).IsEqualTo("0");
    }

    [Test]
    public async Task HideFlaggedPoolHiddenAtZeroSum()
    {
        // ~@MeatRaw with no counts → hidden
        var model = ReadoutLayoutEngine.Build(Input(Group(1, "~@MeatRaw")));
        await Assert.That(model.Cells.Count).IsEqualTo(0);
        await Assert.That(model.TotalHeight).IsEqualTo(0f);
    }

    [Test]
    public async Task ThresholdOnPoolSum()
    {
        // Thresholds["@MeatRaw"] = (20, 10); sum 12 → Band.Low
        var counts = StaticResources.Counts(("Meat_Cow", 5), ("Meat_Chicken", 7));
        var thresholds = new Dictionary<string, ThresholdSpec>
        {
            ["@MeatRaw"] = new ThresholdSpec(20, 10),
        };
        var model = ReadoutLayoutEngine.Build(Input(Group(1, "@MeatRaw"), counts, thresholds));
        var counters = CellsOf(model, CellKind.Counter);
        await Assert.That(counters.Count).IsEqualTo(1);
        await Assert.That(counters[0].Band).IsEqualTo(Band.Low);
    }

    [Test]
    public async Task GroupRendersWhenOnlySlotIsZeroCountPlainToken()
    {
        // "Cloth" (show-when-zero default) → group still renders
        var model = ReadoutLayoutEngine.Build(Input(Group(1, "Cloth")));
        var icons = CellsOf(model, CellKind.Icon);
        await Assert.That(icons.Count).IsEqualTo(1);
        await Assert.That(icons[0].DefName).IsEqualTo("Cloth");
        await Assert.That(model.MarkerHits.Count).IsEqualTo(1);
    }

    [Test]
    public async Task UnknownCategoryTokenSkippedEntirely()
    {
        // "@Nope" has no members in the catalog → skipped, no cells
        var model = ReadoutLayoutEngine.Build(Input(Group(1, "@Nope")));
        await Assert.That(model.Cells.Count).IsEqualTo(0);
        await Assert.That(model.MarkerHits.Count).IsEqualTo(0);
        await Assert.That(model.TotalHeight).IsEqualTo(0f);
    }
}
