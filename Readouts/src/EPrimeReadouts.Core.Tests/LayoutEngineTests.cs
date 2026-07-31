using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

public class LayoutEngineTests
{
    private static ReadoutGroup Group(int id, params string[][] tiers)
    {
        var group = new ReadoutGroup { Id = id, Name = "G" + id };
        foreach (var tier in tiers) group.Tiers.Add(tier.ToList());
        return group;
    }

    private static LayoutInput Input(params ReadoutGroup[] groups) => new()
    {
        Groups = groups.ToList(),
        Counts = StaticResources.Counts(("Steel", 120), ("WoodLog", 75), ("Silver", 900), ("MealSimple", 8)),
        Thresholds = new Dictionary<string, ThresholdSpec>(),
        Catalog = StaticResources.Catalog(),
        Width = 140f,
    };

    private static List<RenderCell> CellsOf(RenderModel model, CellKind kind) =>
        model.Cells.Where(c => c.Kind == kind).ToList();

    [Test]
    public async Task GroupRendersIconAndCounterPerVisibleResource()
    {
        var model = ReadoutLayoutEngine.Build(Input(Group(1, new[] { "Steel", "WoodLog" })));
        await Assert.That(CellsOf(model, CellKind.Icon).Count).IsEqualTo(2);
        await Assert.That(CellsOf(model, CellKind.Counter).Count).IsEqualTo(2);
        await Assert.That(CellsOf(model, CellKind.Counter)[0].Text).IsEqualTo("120");
    }

    /// With the NEW default (show-when-zero), a plain "Cloth" slot renders even at zero.
    /// "~Cloth" (hide-when-zero) is hidden at zero count.
    [Test]
    public async Task ZeroCountHiddenOnlyWhenFlaggedHideWhenZero()
    {
        // ~Cloth is hidden (old hide-at-zero behaviour)
        var inputHide = Input(Group(1, new[] { "Steel", "~Cloth" }));
        var modelHide = ReadoutLayoutEngine.Build(inputHide);
        await Assert.That(CellsOf(modelHide, CellKind.Icon).Count).IsEqualTo(1);

        // Plain "Cloth" (show-when-zero default) IS shown with Text "0"
        var inputShow = Input(Group(1, new[] { "Steel", "Cloth" }));
        var modelShow = ReadoutLayoutEngine.Build(inputShow);
        var counters = CellsOf(modelShow, CellKind.Counter);
        await Assert.That(counters.Count).IsEqualTo(2);
        var clothCounter = counters.FirstOrDefault(c => c.DefName == "Cloth");
        await Assert.That(clothCounter.Text).IsEqualTo("0");
    }

    [Test]
    public async Task ZeroCountWithThresholdIsShownCritical()
    {
        var input = Input(Group(1, new[] { "Cloth" }));
        input.Thresholds["Cloth"] = new ThresholdSpec(50, 10);
        var model = ReadoutLayoutEngine.Build(input);
        var counters = CellsOf(model, CellKind.Counter);
        await Assert.That(counters.Count).IsEqualTo(1);
        await Assert.That(counters[0].Band).IsEqualTo(Band.Critical);
        await Assert.That(counters[0].Text).IsEqualTo("0");
    }

    /// Group disappears only when ALL tokens are ~-flagged and have zero count.
    [Test]
    public async Task EmptyGroupDisappearsEntirely()
    {
        var model = ReadoutLayoutEngine.Build(Input(Group(1, new[] { "~Cloth" })));
        await Assert.That(model.Cells.Count).IsEqualTo(0);
        await Assert.That(model.MarkerHits.Count).IsEqualTo(0);
        await Assert.That(model.TotalHeight).IsEqualTo(0f);
    }

    [Test]
    public async Task DepthLimitsVisibleTiers()
    {
        var group = Group(1, new[] { "Steel" }, new[] { "WoodLog" });
        var input = Input(group);
        input.DepthOf = g => 1;
        var model = ReadoutLayoutEngine.Build(input);
        var icons = CellsOf(model, CellKind.Icon);
        await Assert.That(icons.Count).IsEqualTo(1);
        await Assert.That(icons[0].DefName).IsEqualTo("Steel");
    }

    [Test]
    public async Task MarkersReflectTierCountAndDepth()
    {
        var group = Group(1, new[] { "Steel" }, new[] { "WoodLog" });
        var input = Input(group);
        input.DepthOf = g => 1;
        var model = ReadoutLayoutEngine.Build(input);
        var triangles = CellsOf(model, CellKind.Triangle);
        await Assert.That(triangles.Count).IsEqualTo(2);
        await Assert.That(triangles[0].Triangle).IsEqualTo(TriangleState.Lit);
        await Assert.That(triangles[1].Triangle).IsEqualTo(TriangleState.Dim);
        await Assert.That(model.MarkerHits.Count).IsEqualTo(1);
        await Assert.That(model.MarkerHits[0].GroupId).IsEqualTo(1);
    }

    [Test]
    public async Task GroupsNeverWrapAndContainerGrows()
    {
        // 4 slots at Width 140: groups never wrap, all 4 icons share one row.
        var group = Group(1, new[] { "Steel", "WoodLog", "Silver", "MealSimple" });
        var model = ReadoutLayoutEngine.Build(Input(group));
        var icons = CellsOf(model, CellKind.Icon);
        await Assert.That(icons.Count).IsEqualTo(4);

        // All icons share the same Y (single row)
        float firstY = icons[0].Rect.Y;
        await Assert.That(icons[1].Rect.Y).IsEqualTo(firstY);
        await Assert.That(icons[2].Rect.Y).IsEqualTo(firstY);
        await Assert.That(icons[3].Rect.Y).IsEqualTo(firstY);

        // 4th icon X = insetX + MarkerColW + 3*CellW + centering
        float insetX = LayoutMetrics.StripeW + LayoutMetrics.GroupPadX;
        float expectedIconX = insetX + LayoutMetrics.MarkerColW + 3f * LayoutMetrics.CellW
            + (LayoutMetrics.CellW - LayoutMetrics.IconSize) / 2f;
        await Assert.That(icons[3].Rect.X).IsEqualTo(expectedIconX);

        // GroupBack width = StripeW + GroupPadX + MarkerColW + 4*CellW + GroupPadX  (> 140)
        float expectedContainerW = LayoutMetrics.StripeW + LayoutMetrics.GroupPadX
            + LayoutMetrics.MarkerColW + 4f * LayoutMetrics.CellW + LayoutMetrics.GroupPadX;
        var backs = CellsOf(model, CellKind.GroupBack);
        await Assert.That(backs[0].Rect.W).IsEqualTo(expectedContainerW);
        await Assert.That(expectedContainerW > 140f).IsTrue();

        // TotalWidth equals that container width
        await Assert.That(model.TotalWidth).IsEqualTo(expectedContainerW);

        // TotalHeight = 2*GroupPadY + RowPairH (single row)
        float expectedH = 2f * LayoutMetrics.GroupPadY + LayoutMetrics.RowPairH;
        await Assert.That(model.TotalHeight).IsEqualTo(expectedH);
    }

    [Test]
    public async Task CounterRowOverlapsTheFontPaddingBelowTheIcon()
    {
        var model = ReadoutLayoutEngine.Build(Input(Group(1, new[] { "Steel" })));
        var icon = CellsOf(model, CellKind.Icon)[0];
        var counter = CellsOf(model, CellKind.Counter)[0];
        // The icon row is flush with the 27px icon; the counter box rises 2px
        // into it so the Tiny font's internal top padding provides the visual
        // gap instead of blank canvas.
        await Assert.That(counter.Rect.Y).IsEqualTo(icon.Rect.Y + LayoutMetrics.IconSize - 2f);
        await Assert.That(model.TotalHeight).IsEqualTo(41f);
    }

    [Test]
    public async Task CounterSitsCenteredUnderItsIcon()
    {
        var model = ReadoutLayoutEngine.Build(Input(Group(1, new[] { "Steel" })));
        var icon = CellsOf(model, CellKind.Icon)[0];
        var counter = CellsOf(model, CellKind.Counter)[0];
        float iconCenter = icon.Rect.X + icon.Rect.W / 2f;
        float counterCenter = counter.Rect.X + counter.Rect.W / 2f;
        await Assert.That(counterCenter).IsEqualTo(iconCenter);
        await Assert.That(counter.Rect.Y)
            .IsEqualTo(icon.Rect.Y + LayoutMetrics.IconRowH - LayoutMetrics.CounterOverlap);
    }

    [Test]
    public async Task GroupsAreSeparatedByGap()
    {
        var model = ReadoutLayoutEngine.Build(Input(
            Group(1, new[] { "Steel" }),
            Group(2, new[] { "Silver" })));
        float rowPairH = LayoutMetrics.RowPairH;
        // Each container = 2*GroupPadY + rowPairH. MarkerHit is inset by GroupPadY from container top.
        // MarkerHits[1].Y = container1H + GroupGap + GroupPadY
        //                  = (2*GroupPadY + rowPairH) + GroupGap + GroupPadY
        //                  = 3*GroupPadY + rowPairH + GroupGap
        float container1H = 2f * LayoutMetrics.GroupPadY + rowPairH;
        await Assert.That(model.MarkerHits[1].Rect.Y)
            .IsEqualTo(container1H + LayoutMetrics.GroupGap + LayoutMetrics.GroupPadY);
        // TotalHeight = container1H + GroupGap + container2H = 2*(2*GroupPadY + rowPairH) + GroupGap
        await Assert.That(model.TotalHeight)
            .IsEqualTo(2f * container1H + LayoutMetrics.GroupGap);
    }

    [Test]
    public async Task PlainZeroSlotRendersShowWhenZeroDefault()
    {
        // A group with only a zero-count "Cloth" slot (no ~ flag) must still render
        var model = ReadoutLayoutEngine.Build(Input(Group(1, new[] { "Cloth" })));
        var icons = CellsOf(model, CellKind.Icon);
        await Assert.That(icons.Count).IsEqualTo(1);
        await Assert.That(icons[0].DefName).IsEqualTo("Cloth");
        var counters = CellsOf(model, CellKind.Counter);
        await Assert.That(counters[0].Text).IsEqualTo("0");
    }

    [Test]
    public async Task EachRenderedGroupGetsABackCellFirst()
    {
        // Two groups → two GroupBack cells with GroupIndex 0 and 1,
        // each preceding its content cells, rects spanning computed widths and correct heights.
        var model = ReadoutLayoutEngine.Build(Input(
            Group(1, new[] { "Steel" }),
            Group(2, new[] { "Silver" })));

        var backs = CellsOf(model, CellKind.GroupBack);
        await Assert.That(backs.Count).IsEqualTo(2);
        await Assert.That(backs[0].GroupIndex).IsEqualTo(0);
        await Assert.That(backs[1].GroupIndex).IsEqualTo(1);

        // Each GroupBack rect starts at X=0
        await Assert.That(backs[0].Rect.X).IsEqualTo(0f);
        await Assert.That(backs[1].Rect.X).IsEqualTo(0f);

        // Single-slot container width = StripeW + GroupPadX + MarkerColW + 1*CellW + GroupPadX
        float expectedW = LayoutMetrics.StripeW + LayoutMetrics.GroupPadX
            + LayoutMetrics.MarkerColW + 1f * LayoutMetrics.CellW + LayoutMetrics.GroupPadX;
        await Assert.That(backs[0].Rect.W).IsEqualTo(expectedW);
        await Assert.That(backs[1].Rect.W).IsEqualTo(expectedW);

        // Single-row group container height = 2*GroupPadY + RowPairH
        float rowPairH = LayoutMetrics.RowPairH;
        float containerH = 2f * LayoutMetrics.GroupPadY + rowPairH;
        await Assert.That(backs[0].Rect.H).IsEqualTo(containerH);
        await Assert.That(backs[1].Rect.H).IsEqualTo(containerH);

        // GroupBack[0] must appear BEFORE any Triangle or Icon cells in the list
        int firstBack0 = model.Cells.IndexOf(backs[0]);
        int firstTriangle = model.Cells.FindIndex(c => c.Kind == CellKind.Triangle);
        await Assert.That(firstBack0 < firstTriangle).IsTrue();

        // GroupBack[1] must appear before the second group's Triangle cells
        int firstBack1 = model.Cells.IndexOf(backs[1]);
        int secondTriangle = model.Cells.FindIndex(firstTriangle + 1, c => c.Kind == CellKind.Triangle);
        await Assert.That(firstBack1 < secondTriangle).IsTrue();
    }

    [Test]
    public async Task ResultsSectionGetsNeutralBackCell()
    {
        // Search active → a GroupBack with GroupIndex -1 emitted before the results label.
        var input = Input(Group(1, new[] { "Steel" }));
        input.SearchText = "steel";
        var model = ReadoutLayoutEngine.Build(input);

        var backs = CellsOf(model, CellKind.GroupBack);
        // At least one back cell with GroupIndex = -1 (Results container)
        var resultsBack = backs.FirstOrDefault(b => b.GroupIndex == -1);
        await Assert.That(resultsBack.GroupIndex).IsEqualTo(-1);

        // It must be emitted BEFORE the Label cell
        int backIdx = model.Cells.FindIndex(c => c.Kind == CellKind.GroupBack && c.GroupIndex == -1);
        int labelIdx = model.Cells.FindIndex(c => c.Kind == CellKind.Label);
        await Assert.That(backIdx < labelIdx).IsTrue();

        // Results container rect starts at X=0, Y=0 and spans input.Width (wraps at panel width)
        await Assert.That(resultsBack.Rect.X).IsEqualTo(0f);
        await Assert.That(resultsBack.Rect.W).IsEqualTo(140f);
        await Assert.That(resultsBack.Rect.Y).IsEqualTo(0f);
    }

    [Test]
    public async Task CounterTextUsesCompactFormatAbove10000()
    {
        // A slot with count 12786 must display "12.8k" (not "12786")
        var input = new LayoutInput
        {
            Groups = new List<ReadoutGroup> { Group(1, new[] { "Steel" }) },
            Counts = StaticResources.Counts(("Steel", 12786)),
            Thresholds = new Dictionary<string, ThresholdSpec>(),
            Catalog = StaticResources.Catalog(),
            Width = 140f,
        };
        var model = ReadoutLayoutEngine.Build(input);
        var counters = CellsOf(model, CellKind.Counter);
        await Assert.That(counters.Count).IsEqualTo(1);
        await Assert.That(counters[0].Text).IsEqualTo("12.8k");
        // The RAW count must survive on both cells — tooltips read it from the
        // cell, never by parsing the compact display text.
        await Assert.That(counters[0].Count).IsEqualTo(12786);
        await Assert.That(CellsOf(model, CellKind.Icon)[0].Count).IsEqualTo(12786);
    }

    [Test]
    public async Task TotalWidthCoversWidestContainer()
    {
        // Two groups: 1 slot and 5 slots at Width=140.
        // The 5-slot container is wider than 140, so TotalWidth equals that width.
        var input = new LayoutInput
        {
            Groups = new List<ReadoutGroup>
            {
                Group(1, new[] { "Steel" }),
                Group(2, new[] { "Steel", "WoodLog", "Silver", "MealSimple", "Cloth" }),
            },
            Counts = StaticResources.Counts(("Steel", 1), ("WoodLog", 1), ("Silver", 1), ("MealSimple", 1)),
            Thresholds = new Dictionary<string, ThresholdSpec>(),
            Catalog = StaticResources.Catalog(),
            Width = 140f,
        };
        var model = ReadoutLayoutEngine.Build(input);

        float fiveSlotW = LayoutMetrics.StripeW + LayoutMetrics.GroupPadX
            + LayoutMetrics.MarkerColW + 5f * LayoutMetrics.CellW + LayoutMetrics.GroupPadX;
        await Assert.That(fiveSlotW > 140f).IsTrue();
        await Assert.That(model.TotalWidth).IsEqualTo(fiveSlotW);
    }
}
