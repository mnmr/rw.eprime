using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

/// Regression for clipped counter numbers: RimWorld substitutes the Small
/// font when Tiny text is unavailable, so the layout must expand cells to
/// the measured font instead of clipping numbers in tiny-sized boxes.
/// The icon must keep its fixed size and stay centered above the counter.
public class LayoutEngineMetricsTests
{
    private static ReadoutGroup Group(int id, params string[][] tiers)
    {
        var group = new ReadoutGroup { Id = id, Name = "G" + id };
        foreach (var tier in tiers) group.Tiers.Add(tier.ToList());
        return group;
    }

    private static LayoutInput Input(CellMetrics metrics, params ReadoutGroup[] groups) => new()
    {
        Groups = groups.ToList(),
        Counts = StaticResources.Counts(("Steel", 120), ("WoodLog", 75)),
        Thresholds = new Dictionary<string, ThresholdSpec>(),
        Catalog = StaticResources.Catalog(),
        Width = 140f,
        Metrics = metrics,
    };

    private static List<RenderCell> CellsOf(RenderModel model, CellKind kind) =>
        model.Cells.Where(c => c.Kind == kind).ToList();

    [Test]
    public async Task DefaultMetricsReproduceBaselineConstants()
    {
        var metrics = default(CellMetrics);
        await Assert.That(metrics.CellW).IsEqualTo(LayoutMetrics.CellW);
        await Assert.That(metrics.CounterRowH).IsEqualTo(LayoutMetrics.CounterRowH);
        await Assert.That(metrics.LabelRowH).IsEqualTo(LayoutMetrics.LabelRowH);
        await Assert.That(metrics.RowPairH).IsEqualTo(LayoutMetrics.RowPairH);
    }

    [Test]
    public async Task MetricsBelowBaselineClampUpToBaseline()
    {
        // Cells never shrink below the icon-fitting minimum, even if a
        // measured font is narrower than the classic tiny geometry.
        var metrics = new CellMetrics(20f, 10f);
        await Assert.That(metrics.CellW).IsEqualTo(LayoutMetrics.CellW);
        await Assert.That(metrics.CounterRowH).IsEqualTo(LayoutMetrics.CounterRowH);
        await Assert.That(metrics.LabelRowH).IsEqualTo(LayoutMetrics.LabelRowH);
        await Assert.That(metrics.RowPairH).IsEqualTo(LayoutMetrics.RowPairH);
    }

    [Test]
    public async Task WiderCellExpandsCounterAndKeepsIconCenteredAtSameSize()
    {
        var metrics = new CellMetrics(48f, LayoutMetrics.CounterRowH);
        var model = ReadoutLayoutEngine.Build(
            Input(metrics, Group(1, new[] { "Steel", "WoodLog" })));
        var icons = CellsOf(model, CellKind.Icon);
        var counters = CellsOf(model, CellKind.Counter);

        float insetX = LayoutMetrics.StripeW + LayoutMetrics.GroupPadX;

        // Second column starts one widened cell over; the counter spans it.
        float cellX = insetX + LayoutMetrics.MarkerColW + 48f;
        await Assert.That(counters[1].Rect.X).IsEqualTo(cellX);
        await Assert.That(counters[1].Rect.W).IsEqualTo(48f);

        // Icon keeps its fixed size and centers inside the wider cell —
        // the extra width becomes symmetric padding on either side.
        await Assert.That(icons[1].Rect.W).IsEqualTo(LayoutMetrics.IconSize);
        await Assert.That(icons[1].Rect.X)
            .IsEqualTo(cellX + (48f - LayoutMetrics.IconSize) / 2f);
        float iconCenter = icons[1].Rect.X + icons[1].Rect.W / 2f;
        float counterCenter = counters[1].Rect.X + counters[1].Rect.W / 2f;
        await Assert.That(counterCenter).IsEqualTo(iconCenter);
    }

    [Test]
    public async Task TallerCounterRowGrowsRowPairAndContainerHeight()
    {
        var metrics = new CellMetrics(LayoutMetrics.CellW, 22f);
        var model = ReadoutLayoutEngine.Build(Input(metrics, Group(1, new[] { "Steel" })));
        var icon = CellsOf(model, CellKind.Icon)[0];
        var counter = CellsOf(model, CellKind.Counter)[0];

        // Counter box: same anchor under the icon row, but tall enough for
        // the substituted font's 22px line.
        await Assert.That(counter.Rect.Y)
            .IsEqualTo(icon.Rect.Y + LayoutMetrics.IconRowH - LayoutMetrics.CounterOverlap);
        await Assert.That(counter.Rect.H).IsEqualTo(22f);

        float expectedRowPair =
            LayoutMetrics.IconRowH + 22f - LayoutMetrics.CounterOverlap;
        float expectedContainerH = 2f * LayoutMetrics.GroupPadY + expectedRowPair;
        var back = CellsOf(model, CellKind.GroupBack)[0];
        await Assert.That(back.Rect.H).IsEqualTo(expectedContainerH);
        await Assert.That(model.TotalHeight).IsEqualTo(expectedContainerH);
    }

    [Test]
    public async Task ContainerAndTotalWidthScaleWithCellW()
    {
        // Three widened cells push the container past the 140 panel width,
        // so TotalWidth must follow the container.
        var metrics = new CellMetrics(48f, LayoutMetrics.CounterRowH);
        var model = ReadoutLayoutEngine.Build(
            Input(metrics, Group(1, new[] { "Steel", "WoodLog", "Silver" })));
        float expectedW = LayoutMetrics.StripeW + LayoutMetrics.GroupPadX
            + LayoutMetrics.MarkerColW + 3f * 48f + LayoutMetrics.GroupPadX;
        var back = CellsOf(model, CellKind.GroupBack)[0];
        await Assert.That(back.Rect.W).IsEqualTo(expectedW);
        await Assert.That(expectedW > 140f).IsTrue();
        await Assert.That(model.TotalWidth).IsEqualTo(expectedW);
    }

    /// Groupless search input whose counted defs all match the query.
    private static LayoutInput SearchInput(CellMetrics metrics, int matchCount, float width)
    {
        var defs = new string[matchCount];
        for (int i = 0; i < matchCount; i++) defs[i] = $"Match{i:D2}";
        var counts = new Dictionary<string, int>();
        foreach (var d in defs) counts[d] = 1;
        return new LayoutInput
        {
            Groups = new List<ReadoutGroup>(),
            Counts = counts,
            Thresholds = new Dictionary<string, ThresholdSpec>(),
            Catalog = StaticResources.CatalogWith(defs),
            Width = width,
            SearchText = "match",
            Metrics = metrics,
        };
    }

    [Test]
    public async Task ResultsGridColumnsRowsAndLabelsUseMetrics()
    {
        // Width 140 fits 3 default columns but only 2 at CellW 48.
        var metrics = new CellMetrics(48f, 22f);
        var model = ReadoutLayoutEngine.Build(SearchInput(metrics, 4, 140f));
        var icons = CellsOf(model, CellKind.Icon);
        await Assert.That(icons.Select(c => c.Rect.X).Distinct().Count()).IsEqualTo(2);
        await Assert.That(icons.Select(c => c.Rect.Y).Distinct().Count()).IsEqualTo(2);

        // Rows advance by the metric row pair, label rows by the metric
        // label height.
        var labels = CellsOf(model, CellKind.Label);
        await Assert.That(labels[0].Rect.H).IsEqualTo(metrics.LabelRowH);
        float rowStride = icons.Select(c => c.Rect.Y).Distinct().OrderBy(y => y).ToList()[1]
            - icons.Select(c => c.Rect.Y).Distinct().OrderBy(y => y).ToList()[0];
        await Assert.That(rowStride).IsEqualTo(metrics.RowPairH);
    }

    private static LayoutInput EditorInput(CellMetrics metrics, ReadoutGroup group) => new()
    {
        Groups = new List<ReadoutGroup> { group },
        EditorMode = true,
        Counts = StaticResources.Counts(("Steel", 120), ("WoodLog", 75)),
        Thresholds = new Dictionary<string, ThresholdSpec>(),
        Catalog = StaticResources.Catalog(),
        Width = 400f,
        Metrics = metrics,
    };

    [Test]
    public async Task EditorModeCellsAndEmptySlotUseMetrics()
    {
        var metrics = new CellMetrics(48f, 22f);
        var group = Group(1, new[] { "Steel" });
        var model = ReadoutLayoutEngine.Build(EditorInput(metrics, group));

        var counters = CellsOf(model, CellKind.Counter);
        await Assert.That(counters[0].Rect.W).IsEqualTo(48f);
        await Assert.That(counters[0].Rect.H).IsEqualTo(22f);

        // Trailing empty slot occupies the SECOND widened column, icon-sized
        // and centered like a real icon cell.
        var empty = CellsOf(model, CellKind.EmptySlot)[0];
        float insetX = LayoutMetrics.StripeW + LayoutMetrics.GroupPadX;
        float secondCellX = insetX + LayoutMetrics.MarkerColW + 48f;
        await Assert.That(empty.Rect.X)
            .IsEqualTo(secondCellX + (48f - LayoutMetrics.IconSize) / 2f);
        await Assert.That(empty.Rect.W).IsEqualTo(LayoutMetrics.IconSize);

        // Container width: two columns (token + empty slot) at the wider cell.
        float expectedW = LayoutMetrics.StripeW + LayoutMetrics.GroupPadX
            + LayoutMetrics.MarkerColW + 2f * 48f + LayoutMetrics.GroupPadX;
        var back = CellsOf(model, CellKind.GroupBack)[0];
        await Assert.That(back.Rect.W).IsEqualTo(expectedW);
    }
}
