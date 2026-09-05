using System.Globalization;
using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

/// With vertical tier layout, an expanded group keeps one band and grows it
/// downward: every visible tier gets its own icon+counter row, rows sit a
/// small gap apart so a counter reads as belonging to the icon above it,
/// and the marker stack centers on the taller band. Cycling and hovering
/// produce the same geometry; only the marker tint tells them apart.
public class LayoutEngineVerticalTiersTests
{
    private const float RowPairH = 41f;   // 27 icon + 16 counter - 2 overlap
    private const float RowGap = LayoutMetrics.TierRowGap;

    private static ReadoutGroup Group()
    {
        var group = new ReadoutGroup { Id = 1, Name = "G1" };
        group.Tiers.Add(new List<string> { "Steel", "WoodLog" });
        // Hide-when-zero tokens so an empty tier can drop out of the layout.
        group.Tiers.Add(new List<string> { "~Gold", "~Silver" });
        group.Tiers.Add(new List<string> { "Cloth", "MealSimple", "MealFine" });
        return group;
    }

    private static Dictionary<string, int> Counts(int goldCount = 40, int silverCount = 900) =>
        StaticResources.Counts(
            ("Steel", 120), ("WoodLog", 75), ("Gold", goldCount),
            ("Silver", silverCount), ("Cloth", 30), ("MealSimple", 12),
            ("MealFine", 4));

    private static LayoutInput Input(int depth, int? configured, bool vertical,
        int goldCount = 40, int silverCount = 900)
    {
        return new LayoutInput
        {
            Groups = new List<ReadoutGroup> { Group() },
            Counts = Counts(goldCount, silverCount),
            Thresholds = new Dictionary<string, ThresholdSpec>(),
            Catalog = StaticResources.Catalog(),
            Width = 140f,
            DepthOf = g => depth,
            ConfiguredDepthOf = configured.HasValue ? g => configured.Value : null,
            VerticalTiers = vertical,
        };
    }

    /// "Token@x,y" per slot hit in emission order, plus the band size.
    private static string Dump(RenderModel model)
    {
        var parts = new List<string>();
        foreach (SlotHit hit in model.SlotHits)
            parts.Add(hit.Token + "@" + N(hit.Rect.X) + "," + N(hit.Rect.Y));
        RenderBand band = model.Bands[0];
        parts.Add("band " + N(band.Rect.W) + "x" + N(band.Rect.H));
        return string.Join(" ", parts);
    }

    private static string N(float value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);

    [Test]
    public async Task VerticalLayoutGivesEachVisibleTierItsOwnRowInsideOneBand()
    {
        var model = ReadoutLayoutEngine.Build(
            Input(depth: 3, configured: null, vertical: true));

        float row1 = RowPairH + RowGap;
        float row2 = 2f * (RowPairH + RowGap);
        // Columns start at x = 3 stripe + 4 pad + 11 marker = 18; width is
        // the widest row (3 slots): 18 + 3*34 + 4 = 124.
        await Assert.That(Dump(model)).IsEqualTo(
            "Steel@18,0 WoodLog@52,0 "
            + "~Gold@18," + N(row1) + " ~Silver@52," + N(row1) + " "
            + "Cloth@18," + N(row2) + " MealSimple@52," + N(row2)
            + " MealFine@86," + N(row2) + " "
            + "band 124x" + N(3f * RowPairH + 2f * RowGap));
        await Assert.That(model.Bands.Count).IsEqualTo(1);
        await Assert.That(model.TotalHeight).IsEqualTo(3f * RowPairH + 2f * RowGap);

        // The gap between a counter and the icon below it exceeds the
        // counter's own distance from its icon (which overlaps by 2).
        RenderCell steelCounter = model.Cells[model.SlotHits[0].CellIndex + 1];
        RenderCell goldIcon = model.Cells[model.SlotHits[2].CellIndex];
        float counterToNextIcon = goldIcon.Rect.Y - (steelCounter.Rect.Y + steelCounter.Rect.H);
        await Assert.That(counterToNextIcon).IsEqualTo(RowGap);
        await Assert.That(counterToNextIcon).IsGreaterThan(0f);

        // The marker stack stays centered on the first row, exactly where
        // the single-row band keeps it, so growing the band never moves it;
        // the marker rail spans the whole band so every row cycles depth.
        float bandH = 3f * RowPairH + 2f * RowGap;
        var triangles = model.Cells.Where(c => c.Kind == CellKind.Triangle).ToArray();
        await Assert.That(triangles.Length).IsEqualTo(3);
        await Assert.That(triangles[0].Rect.Y)
            .IsEqualTo((RowPairH - LayoutMetrics.MarkerStackH) / 2f);
        var collapsed = ReadoutLayoutEngine.Build(
            Input(depth: 1, configured: null, vertical: true));
        RenderCell collapsedTop = collapsed.Cells.First(c => c.Kind == CellKind.Triangle);
        await Assert.That(triangles[0].Rect.Y).IsEqualTo(collapsedTop.Rect.Y);
        await Assert.That(model.MarkerHits[0].Rect.H).IsEqualTo(bandH);
    }

    [Test]
    public async Task HorizontalLayoutKeepsEveryVisibleTierOnTheSingleRow()
    {
        var model = ReadoutLayoutEngine.Build(
            Input(depth: 3, configured: null, vertical: false));

        await Assert.That(Dump(model)).IsEqualTo(
            "Steel@18,0 WoodLog@52,0 ~Gold@86,0 ~Silver@120,0 Cloth@154,0 "
            + "MealSimple@188,0 MealFine@222,0 band 260x" + N(RowPairH));
    }

    [Test]
    public async Task HoverExpansionMatchesCycledExpansionExceptForMarkerTint()
    {
        var cycled = ReadoutLayoutEngine.Build(
            Input(depth: 3, configured: null, vertical: true));
        var hovered = ReadoutLayoutEngine.Build(
            Input(depth: 3, configured: 1, vertical: true));

        await Assert.That(Dump(hovered)).IsEqualTo(Dump(cycled));
        await Assert.That(hovered.Cells.Count).IsEqualTo(cycled.Cells.Count);
        for (int i = 0; i < cycled.Cells.Count; i++)
        {
            await Assert.That(hovered.Cells[i].Rect).IsEqualTo(cycled.Cells[i].Rect);
            await Assert.That(hovered.Cells[i].Kind).IsEqualTo(cycled.Cells[i].Kind);
        }
        var tints = hovered.Cells.Where(c => c.Kind == CellKind.Triangle)
            .Select(c => c.Triangle).ToArray();
        await Assert.That(tints).IsEquivalentTo(new[]
        {
            TriangleState.Lit, TriangleState.HoverLit, TriangleState.HoverLit,
        });
    }

    [Test]
    public async Task DepthOneAndTiersWithNothingToShowAddNoRows()
    {
        var single = ReadoutLayoutEngine.Build(
            Input(depth: 1, configured: null, vertical: true));
        await Assert.That(Dump(single)).IsEqualTo(
            "Steel@18,0 WoodLog@52,0 band 90x" + N(RowPairH));

        // Tier 2 has nothing visible (both counts zero): tier 3 moves up
        // into the second row and the band shrinks to two rows.
        var skipped = ReadoutLayoutEngine.Build(Input(
            depth: 3, configured: null, vertical: true, goldCount: 0, silverCount: 0));
        float row1 = RowPairH + RowGap;
        await Assert.That(Dump(skipped)).IsEqualTo(
            "Steel@18,0 WoodLog@52,0 "
            + "Cloth@18," + N(row1) + " MealSimple@52," + N(row1)
            + " MealFine@86," + N(row1) + " "
            + "band 124x" + N(2f * RowPairH + RowGap));
    }

    [Test]
    public async Task CountRefreshKeepsWorkingOnAVerticalModel()
    {
        var input = Input(depth: 3, configured: null, vertical: true);
        var model = ReadoutLayoutEngine.Build(input);
        input.Counts = Counts(silverCount: 950);

        await Assert.That(ReadoutLayoutEngine.TryRefreshCounts(input, model)).IsTrue();
        RenderCell silverCounter = model.Cells[model.SlotHits[3].CellIndex + 1];
        await Assert.That(silverCounter.Text).IsEqualTo("950");
    }
}
