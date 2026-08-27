using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

/// Collapsed render mode: a DepthOf of 0 collapses a group HORIZONTALLY —
/// the band keeps its normal single-row height but renders zero slots, so
/// only the stripe, backing and all-dim triangles remain at the zero-slot
/// container width. Band heights never vary with depth, which is what makes
/// per-band hover expansion stable. Editor mode never collapses.
public class LayoutEngineCollapsedTests
{
    private static ReadoutGroup Group(int id, params string[] tokens)
    {
        var group = new ReadoutGroup { Id = id, Name = "G" + id };
        group.Tiers.Add(tokens.ToList());
        return group;
    }

    private static LayoutInput Input(params ReadoutGroup[] groups) => new()
    {
        Groups = groups.ToList(),
        Counts = StaticResources.Counts(("Steel", 120), ("WoodLog", 75)),
        Thresholds = new Dictionary<string, ThresholdSpec>(),
        Catalog = StaticResources.Catalog(),
        Width = 140f,
        DepthOf = g => 0,
    };

    private static List<RenderCell> CellsOf(RenderModel model, CellKind kind) =>
        model.Cells.Where(c => c.Kind == kind).ToList();

    private static float ZeroSlotW => LayoutMetrics.StripeW + LayoutMetrics.GroupPadX
        + LayoutMetrics.MarkerColW + LayoutMetrics.GroupPadX;

    [Test]
    public async Task CollapsedGroupIsAZeroSlotBandAtNormalRowHeight()
    {
        var input = Input(Group(1, "Steel"));
        var model = ReadoutLayoutEngine.Build(input);
        var backs = CellsOf(model, CellKind.GroupBack);
        await Assert.That(backs.Count).IsEqualTo(1);
        await Assert.That(backs[0].Rect.W).IsEqualTo(ZeroSlotW);
        await Assert.That(backs[0].Rect.H)
            .IsEqualTo(2f * LayoutMetrics.GroupPadY + input.Metrics.RowPairH);
        await Assert.That(CellsOf(model, CellKind.Icon).Count).IsEqualTo(0);
        await Assert.That(CellsOf(model, CellKind.Counter).Count).IsEqualTo(0);
        await Assert.That(model.SlotHits.Count).IsEqualTo(0);
        await Assert.That(model.MarkerHits.Count).IsEqualTo(0);
        await Assert.That(model.TotalHeight).IsEqualTo(backs[0].Rect.H);
    }

    [Test]
    public async Task CollapsedAndExpandedBandsHaveIdenticalHeights()
    {
        // Height never varies with depth — hover expansion must not shift
        // the bands below the hovered one.
        var collapsed = ReadoutLayoutEngine.Build(Input(Group(1, "Steel")));
        var expandedInput = Input(Group(1, "Steel"));
        expandedInput.DepthOf = g => 1;
        var expanded = ReadoutLayoutEngine.Build(expandedInput);
        var collapsedBack = collapsed.Cells.First(c => c.Kind == CellKind.GroupBack);
        var expandedBack = expanded.Cells.First(c => c.Kind == CellKind.GroupBack);
        await Assert.That(collapsedBack.Rect.H).IsEqualTo(expandedBack.Rect.H);
        await Assert.That(collapsedBack.Rect.W < expandedBack.Rect.W).IsTrue();
    }

    [Test]
    public async Task CollapsedGroupShowsAllDimTrianglesForItsTiers()
    {
        var group = Group(1, "Steel");
        group.Tiers.Add(new List<string> { "WoodLog" });
        var model = ReadoutLayoutEngine.Build(Input(group));
        var triangles = CellsOf(model, CellKind.Triangle);
        await Assert.That(triangles.Count).IsEqualTo(2);
        await Assert.That(triangles.All(t => t.Triangle == TriangleState.Dim)).IsTrue();
    }

    [Test]
    public async Task CollapsedBandRendersEvenWhenExpandedGroupWouldBeHidden()
    {
        // "~Cloth" at zero count renders no slots, so this group vanishes
        // when expanded — but collapsed mode must still show its band.
        var model = ReadoutLayoutEngine.Build(Input(Group(1, "~Cloth")));
        await Assert.That(CellsOf(model, CellKind.GroupBack).Count).IsEqualTo(1);
    }

    [Test]
    public async Task PerGroupDepthMixesCollapsedAndExpandedBands()
    {
        var input = Input(Group(1, "Steel"), Group(2, "WoodLog"));
        input.DepthOf = g => g.Id == 1 ? 0 : 1;
        var model = ReadoutLayoutEngine.Build(input);
        var backs = CellsOf(model, CellKind.GroupBack);
        await Assert.That(backs.Count).IsEqualTo(2);
        await Assert.That(backs[0].Rect.H).IsEqualTo(backs[1].Rect.H);
        await Assert.That(backs[0].Rect.W).IsEqualTo(ZeroSlotW);
        await Assert.That(backs[1].Rect.Y)
            .IsEqualTo(backs[0].Rect.H + LayoutMetrics.GroupGap);
        // Only the expanded group's slot is clickable.
        await Assert.That(model.SlotHits.Count).IsEqualTo(1);
        await Assert.That(model.SlotHits[0].Token).IsEqualTo("WoodLog");
    }

    [Test]
    public async Task GroupBackCellsCarryTheirGroupId()
    {
        // Per-band hover resolves the hovered group from the GroupBack under
        // the pointer, so every band must carry its group id; the search
        // results container uses -1.
        var input = Input(Group(7, "Steel"), Group(9, "WoodLog"));
        input.DepthOf = g => g.Id == 7 ? 0 : 1;
        input.SearchText = "steel";
        var model = ReadoutLayoutEngine.Build(input);
        var backs = CellsOf(model, CellKind.GroupBack);
        await Assert.That(backs.Count).IsEqualTo(3);
        // Results container first, then the two group bands in order.
        await Assert.That(backs[0].GroupId).IsEqualTo(-1);
        await Assert.That(backs[1].GroupId).IsEqualTo(7);
        await Assert.That(backs[2].GroupId).IsEqualTo(9);
    }

    [Test]
    public async Task GroupColorIndexIsStableWhenEarlierGroupRendersNothing()
    {
        // Group 1 renders no slots ("~Cloth" at zero count) and keeps only
        // its identification band at index 0; group 2 must still get color
        // index 1, not shift into slot 0.
        var input = Input(Group(1, "~Cloth"), Group(2, "Steel"));
        input.DepthOf = g => 1;
        var model = ReadoutLayoutEngine.Build(input);
        var backs = model.Cells.Where(c => c.Kind == CellKind.GroupBack).ToList();
        await Assert.That(backs.Count).IsEqualTo(2);
        await Assert.That(backs.Select(c => c.GroupIndex))
            .IsEquivalentTo(new[] { 0, 1 });
        // Only the group with content renders slots.
        await Assert.That(model.SlotHits.Count).IsEqualTo(1);
        await Assert.That(model.SlotHits[0].Token).IsEqualTo("Steel");
    }

    [Test]
    public async Task GroupColorIndexAgreesBetweenCollapsedAndExpandedStates()
    {
        // Same groups, hover flips depth 0 <-> 1: each group's color index
        // must not change between the two states.
        var collapsed = ReadoutLayoutEngine.Build(Input(Group(1, "~Cloth"), Group(2, "Steel")));
        var expandedInput = Input(Group(1, "~Cloth"), Group(2, "Steel"));
        expandedInput.DepthOf = g => 1;
        var expanded = ReadoutLayoutEngine.Build(expandedInput);
        var collapsedBacks = collapsed.Cells
            .Where(c => c.Kind == CellKind.GroupBack).ToList();
        var expandedBacks = expanded.Cells
            .Where(c => c.Kind == CellKind.GroupBack).ToList();
        // Both states render both groups (the empty group keeps its thin
        // identification band when expanded) at unchanged color indices.
        await Assert.That(collapsedBacks.Select(c => c.GroupIndex))
            .IsEquivalentTo(new[] { 0, 1 });
        await Assert.That(expandedBacks.Select(c => c.GroupIndex))
            .IsEquivalentTo(new[] { 0, 1 });
    }

    [Test]
    public async Task EditorModeIgnoresCollapsedDepth()
    {
        var input = Input(Group(1, "Steel"));
        input.EditorMode = true;
        var model = ReadoutLayoutEngine.Build(input);
        // The editor band still renders its tier row (slots present).
        await Assert.That(CellsOf(model, CellKind.Icon).Count).IsEqualTo(1);
    }
}
