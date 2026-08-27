using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

/// Every slot in the main readout (group rows and search results) publishes
/// a SlotHit: the clickable cell-column rect plus the member defNames the
/// game layer selects on the map when the slot is clicked. Editor bands are
/// not clickable-for-selection and publish none.
public class LayoutEngineSlotHitTests
{
    private static ReadoutGroup Group(params string[] tokens)
    {
        var group = new ReadoutGroup { Id = 1, Name = "G1" };
        group.Tiers.Add(tokens.ToList());
        return group;
    }

    private static LayoutInput Input(params string[] tokens) => new()
    {
        Groups = new List<ReadoutGroup> { Group(tokens) },
        Counts = StaticResources.Counts(
            ("Steel", 120), ("WoodLog", 75), ("Meat_Cow", 30), ("Meat_Chicken", 10)),
        Thresholds = new Dictionary<string, ThresholdSpec>(),
        Catalog = StaticResources.Catalog(),
        Width = 140f,
    };

    [Test]
    public async Task GroupSlotsEmitOneHitPerVisibleSlotWithMembers()
    {
        var model = ReadoutLayoutEngine.Build(Input("Steel", "WoodLog"));
        await Assert.That(model.SlotHits.Count).IsEqualTo(2);
        await Assert.That(model.SlotHits[0].Token).IsEqualTo("Steel");
        await Assert.That(model.SlotHits[0].Members).IsEquivalentTo(new[] { "Steel" });
        await Assert.That(model.SlotHits[1].Token).IsEqualTo("WoodLog");
    }

    [Test]
    public async Task GroupSlotHitsPointDirectlyAtTheirMatchingIconCells()
    {
        var model = ReadoutLayoutEngine.Build(Input("Steel", "WoodLog"));
        var cellIndexField = typeof(SlotHit).GetField("CellIndex");

        await Assert.That(cellIndexField).IsNotNull();
        for (int i = 0; i < model.SlotHits.Count; i++)
        {
            int cellIndex = (int)cellIndexField!.GetValue(model.SlotHits[i])!;
            RenderCell cell = model.Cells[cellIndex];
            await Assert.That(cell.Kind).IsEqualTo(CellKind.Icon);
            await Assert.That(cell.Token).IsEqualTo(model.SlotHits[i].Token);
        }
    }

    [Test]
    public async Task PoolSlotHitCarriesAllMemberDefNames()
    {
        var input = Input("#1");
        input.Pools = StaticResources.MeatPool();
        var model = ReadoutLayoutEngine.Build(input);
        await Assert.That(model.SlotHits.Count).IsEqualTo(1);
        await Assert.That(model.SlotHits[0].Members)
            .IsEquivalentTo(new[] { "Meat_Cow", "Meat_Chicken" });
    }

    [Test]
    public async Task HitRectSpansTheFullCellColumnOfItsIconAndCounter()
    {
        var input = Input("Steel");
        var model = ReadoutLayoutEngine.Build(input);
        var hit = model.SlotHits[0];
        var icon = model.Cells.First(c => c.Kind == CellKind.Icon);
        var counter = model.Cells.First(c => c.Kind == CellKind.Counter);
        // Column-aligned with the counter (which spans the full CellW), from
        // the icon's top edge to the counter's bottom edge.
        await Assert.That(hit.Rect.X).IsEqualTo(counter.Rect.X);
        await Assert.That(hit.Rect.W).IsEqualTo(input.Metrics.CellW);
        await Assert.That(hit.Rect.Y).IsEqualTo(icon.Rect.Y);
        await Assert.That(hit.Rect.Y + hit.Rect.H)
            .IsEqualTo(counter.Rect.Y + counter.Rect.H);
    }

    [Test]
    public async Task SearchResultSlotsEmitHitsForTheirDef()
    {
        var input = Input("Steel");
        input.SearchText = "wood";
        var model = ReadoutLayoutEngine.Build(input);
        // "wood" matches only WoodLog, shown in the results grid; the Steel
        // group slot below stays clickable too.
        await Assert.That(model.SlotHits.Count).IsEqualTo(2);
        await Assert.That(model.SlotHits[0].Token).IsEqualTo("WoodLog");
        await Assert.That(model.SlotHits[0].Members).IsEquivalentTo(new[] { "WoodLog" });
        await Assert.That(model.SlotHits[1].Token).IsEqualTo("Steel");
    }

    [Test]
    public async Task SearchResultHitPointsDirectlyAtItsIconCell()
    {
        var input = Input("Steel");
        input.SearchText = "wood";
        var model = ReadoutLayoutEngine.Build(input);
        var cellIndexField = typeof(SlotHit).GetField("CellIndex");

        await Assert.That(cellIndexField).IsNotNull();
        int cellIndex = (int)cellIndexField!.GetValue(model.SlotHits[0])!;
        RenderCell cell = model.Cells[cellIndex];
        await Assert.That(cell.Kind).IsEqualTo(CellKind.Icon);
        await Assert.That(cell.DefName).IsEqualTo("WoodLog");
    }

    [Test]
    public async Task EditorModeEmitsNoSlotHits()
    {
        var input = Input("Steel", "WoodLog");
        input.EditorMode = true;
        var model = ReadoutLayoutEngine.Build(input);
        await Assert.That(model.SlotHits.Count).IsEqualTo(0);
    }
}
