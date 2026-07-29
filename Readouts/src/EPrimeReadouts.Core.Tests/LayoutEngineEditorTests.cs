using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

/// Tests for ReadoutLayoutEngine in editor mode (LayoutInput.EditorMode = true).
/// Semantics: one tier at a time — only the current tier's items (depth = 1-based
/// current tier number) plus ONE trailing EmptySlot. No Separator cells.
/// Non-editor tests in LayoutEngineTests must pass unchanged.
public class LayoutEngineEditorTests
{
    private static ReadoutGroup Group(int id, params string[][] tiers)
    {
        var group = new ReadoutGroup { Id = id, Name = "G" + id };
        foreach (var tier in tiers) group.Tiers.Add(tier.ToList());
        return group;
    }

    private static LayoutInput EditorInput(params ReadoutGroup[] groups) => new()
    {
        Groups = groups.ToList(),
        Counts = StaticResources.Counts(("Steel", 10), ("WoodLog", 5), ("Silver", 0), ("Cloth", 0)),
        Thresholds = new Dictionary<string, ThresholdSpec>(),
        Catalog = StaticResources.Catalog(),
        Width = 300f,
        EditorMode = true,
    };

    private static List<RenderCell> CellsOf(RenderModel model, CellKind kind) =>
        model.Cells.Where(c => c.Kind == kind).ToList();

    // -----------------------------------------------------------------------
    // Empty group, depth 1 (default): one EmptySlot(0,0) + one Lit triangle

    [Test]
    public async Task EmptyGroup_OneEmptySlot_TierZeroSlotZero()
    {
        // Group with no tiers: DepthOf=TierCount=0 → ClampDepth([],0)=1; t=0
        var model = ReadoutLayoutEngine.Build(EditorInput(Group(1)));

        var empties = CellsOf(model, CellKind.EmptySlot);
        await Assert.That(empties.Count).IsEqualTo(1);
        await Assert.That(empties[0].Tier).IsEqualTo(0);
        await Assert.That(empties[0].Slot).IsEqualTo(0);
    }

    [Test]
    public async Task EmptyGroup_NoIconCells()
    {
        var model = ReadoutLayoutEngine.Build(EditorInput(Group(1)));
        await Assert.That(CellsOf(model, CellKind.Icon).Count).IsEqualTo(0);
    }

    [Test]
    public async Task EmptyGroup_OneLitTriangle()
    {
        // markerCount = min(3, max(tiers.Count=0, depth=1)) = 1
        // Markers.Compute(1, 1, ...) → [Lit, Absent, Absent]
        var model = ReadoutLayoutEngine.Build(EditorInput(Group(1)));
        var triangles = CellsOf(model, CellKind.Triangle);
        await Assert.That(triangles.Count).IsEqualTo(1);
        await Assert.That(triangles[0].Triangle).IsEqualTo(TriangleState.Lit);
    }

    [Test]
    public async Task EmptyGroup_ContainerAndMarkerHitPresent()
    {
        var model = ReadoutLayoutEngine.Build(EditorInput(Group(1)));
        await Assert.That(CellsOf(model, CellKind.GroupBack).Count).IsEqualTo(1);
        await Assert.That(model.MarkerHits.Count).IsEqualTo(1);
        await Assert.That(model.MarkerHits[0].GroupId).IsEqualTo(1);
        await Assert.That(model.TotalHeight > 0f).IsTrue();
    }

    // -----------------------------------------------------------------------
    // tiers=[Steel,WoodLog],[Silver] depth 1 → only tier 0 visible

    [Test]
    public async Task TwoTiersDepth1_OnlyTier0Visible_IconsAndEmptySlot()
    {
        var input = EditorInput(Group(1, new[] { "Steel", "WoodLog" }, new[] { "Silver" }));
        input.DepthOf = _ => 1;
        var model = ReadoutLayoutEngine.Build(input);

        // Icons: Steel, WoodLog — no Silver
        var icons = CellsOf(model, CellKind.Icon);
        await Assert.That(icons.Count).IsEqualTo(2);
        await Assert.That(icons[0].DefName).IsEqualTo("Steel");
        await Assert.That(icons[0].Tier).IsEqualTo(0);
        await Assert.That(icons[0].Slot).IsEqualTo(0);
        await Assert.That(icons[1].DefName).IsEqualTo("WoodLog");
        await Assert.That(icons[1].Tier).IsEqualTo(0);
        await Assert.That(icons[1].Slot).IsEqualTo(1);

        // EmptySlot: Tier=0, Slot=2 (after the two tokens)
        var empties = CellsOf(model, CellKind.EmptySlot);
        await Assert.That(empties.Count).IsEqualTo(1);
        await Assert.That(empties[0].Tier).IsEqualTo(0);
        await Assert.That(empties[0].Slot).IsEqualTo(2);
    }

    [Test]
    public async Task TwoTiersDepth1_NoSilverCells()
    {
        var input = EditorInput(Group(1, new[] { "Steel", "WoodLog" }, new[] { "Silver" }));
        input.DepthOf = _ => 1;
        var model = ReadoutLayoutEngine.Build(input);

        // Silver must not appear (it is in tier 1, not the current tier 0)
        var icons = CellsOf(model, CellKind.Icon);
        await Assert.That(icons.Any(c => c.DefName == "Silver")).IsFalse();
    }

    [Test]
    public async Task TwoTiersDepth1_Markers_LitDimAbsent()
    {
        // markerCount = min(3, max(2, 1)) = 2; Markers.Compute(2, 1) → [Lit, Dim, Absent]
        var input = EditorInput(Group(1, new[] { "Steel", "WoodLog" }, new[] { "Silver" }));
        input.DepthOf = _ => 1;
        var model = ReadoutLayoutEngine.Build(input);

        var triangles = CellsOf(model, CellKind.Triangle);
        await Assert.That(triangles.Count).IsEqualTo(2);
        await Assert.That(triangles[0].Triangle).IsEqualTo(TriangleState.Lit);
        await Assert.That(triangles[1].Triangle).IsEqualTo(TriangleState.Dim);
    }

    // -----------------------------------------------------------------------
    // Same group, depth 2 → only tier 1 (Silver) visible

    [Test]
    public async Task TwoTiersDepth2_OnlyTier1Visible()
    {
        var input = EditorInput(Group(1, new[] { "Steel", "WoodLog" }, new[] { "Silver" }));
        input.DepthOf = _ => 2;
        var model = ReadoutLayoutEngine.Build(input);

        // Icon: Silver only
        var icons = CellsOf(model, CellKind.Icon);
        await Assert.That(icons.Count).IsEqualTo(1);
        await Assert.That(icons[0].DefName).IsEqualTo("Silver");
        await Assert.That(icons[0].Tier).IsEqualTo(1);
        await Assert.That(icons[0].Slot).IsEqualTo(0);

        // EmptySlot: Tier=1, Slot=1
        var empties = CellsOf(model, CellKind.EmptySlot);
        await Assert.That(empties.Count).IsEqualTo(1);
        await Assert.That(empties[0].Tier).IsEqualTo(1);
        await Assert.That(empties[0].Slot).IsEqualTo(1);

        // No Steel or WoodLog
        await Assert.That(icons.Any(c => c.DefName == "Steel")).IsFalse();
        await Assert.That(icons.Any(c => c.DefName == "WoodLog")).IsFalse();
    }

    [Test]
    public async Task TwoTiersDepth2_Markers_LitLitAbsent()
    {
        // markerCount = min(3, max(2, 2)) = 2; Markers.Compute(2, 2) → [Lit, Lit, Absent]
        var input = EditorInput(Group(1, new[] { "Steel", "WoodLog" }, new[] { "Silver" }));
        input.DepthOf = _ => 2;
        var model = ReadoutLayoutEngine.Build(input);

        var triangles = CellsOf(model, CellKind.Triangle);
        await Assert.That(triangles.Count).IsEqualTo(2);
        await Assert.That(triangles[0].Triangle).IsEqualTo(TriangleState.Lit);
        await Assert.That(triangles[1].Triangle).IsEqualTo(TriangleState.Lit);
    }

    // -----------------------------------------------------------------------
    // tiers=[A] depth 2 → empty next tier: only EmptySlot(1,0)

    [Test]
    public async Task OneTierDepth2_EmptyNextTier_OnlyEmptySlot()
    {
        // tiers.Count=1, depth=2 → ClampDepth valid (MaxDepth=2); t=1 >= tiers.Count → tokenCount=0
        var input = EditorInput(Group(1, new[] { "Steel" }));
        input.DepthOf = _ => 2;
        var model = ReadoutLayoutEngine.Build(input);

        await Assert.That(CellsOf(model, CellKind.Icon).Count).IsEqualTo(0);

        var empties = CellsOf(model, CellKind.EmptySlot);
        await Assert.That(empties.Count).IsEqualTo(1);
        await Assert.That(empties[0].Tier).IsEqualTo(1);
        await Assert.That(empties[0].Slot).IsEqualTo(0);
    }

    [Test]
    public async Task OneTierDepth2_Markers_LitLitAbsent()
    {
        // markerCount = min(3, max(1, 2)) = 2; Markers.Compute(2, 2) → [Lit, Lit, Absent]
        var input = EditorInput(Group(1, new[] { "Steel" }));
        input.DepthOf = _ => 2;
        var model = ReadoutLayoutEngine.Build(input);

        var triangles = CellsOf(model, CellKind.Triangle);
        await Assert.That(triangles.Count).IsEqualTo(2);
        await Assert.That(triangles[0].Triangle).IsEqualTo(TriangleState.Lit);
        await Assert.That(triangles[1].Triangle).IsEqualTo(TriangleState.Lit);
    }

    // -----------------------------------------------------------------------
    // Zero-count / hide-flagged tokens: included in editor mode

    [Test]
    public async Task EditorMode_ZeroCountHideFlaggedTokenIsIncluded()
    {
        // ~Cloth: hide-when-zero, count=0 → hidden in normal mode, shown in editor mode
        var input = EditorInput(Group(1, new[] { "~Cloth" }));
        var model = ReadoutLayoutEngine.Build(input);

        var icons = CellsOf(model, CellKind.Icon);
        await Assert.That(icons.Count).IsEqualTo(1);
        await Assert.That(icons[0].DefName).IsEqualTo("Cloth");
    }

    [Test]
    public async Task NonEditor_ZeroCountHideFlaggedTokenIsExcluded()
    {
        // Same token in non-editor mode disappears (contrast)
        var normalInput = new LayoutInput
        {
            Groups = new List<ReadoutGroup> { Group(1, new[] { "~Cloth" }) },
            Counts = StaticResources.Counts(("Cloth", 0)),
            Thresholds = new Dictionary<string, ThresholdSpec>(),
            Catalog = StaticResources.Catalog(),
            Width = 140f,
        };
        var model = ReadoutLayoutEngine.Build(normalInput);
        await Assert.That(model.Cells.Count).IsEqualTo(0);
    }

    // -----------------------------------------------------------------------
    // Counter cells present with correct text

    [Test]
    public async Task EditorMode_CounterCells_PresentWithLiveCount()
    {
        // Steel count=10, WoodLog count=5 at depth 1
        var input = EditorInput(Group(1, new[] { "Steel", "WoodLog" }, new[] { "Silver" }));
        input.DepthOf = _ => 1;
        var model = ReadoutLayoutEngine.Build(input);

        var counters = CellsOf(model, CellKind.Counter);
        await Assert.That(counters.Count).IsEqualTo(2);
        await Assert.That(counters[0].Text).IsEqualTo("10");  // Steel
        await Assert.That(counters[1].Text).IsEqualTo("5");   // WoodLog
    }

    [Test]
    public async Task EditorMode_CounterCell_TierAndSlotMatchIcon()
    {
        var input = EditorInput(Group(1, new[] { "Steel", "WoodLog" }, new[] { "Silver" }));
        input.DepthOf = _ => 1;
        var model = ReadoutLayoutEngine.Build(input);

        var counters = CellsOf(model, CellKind.Counter);
        await Assert.That(counters[0].Tier).IsEqualTo(0);
        await Assert.That(counters[0].Slot).IsEqualTo(0);
        await Assert.That(counters[1].Tier).IsEqualTo(0);
        await Assert.That(counters[1].Slot).IsEqualTo(1);
    }

    // -----------------------------------------------------------------------
    // Container width: marker col + (tokenCount+1)*CellW + insets, no separator

    [Test]
    public async Task EditorMode_ContainerWidth_OneTierNoSeparator()
    {
        // tiers=[Steel,WoodLog],[Silver], depth=1 → tier0 has 2 tokens + 1 empty = 3 cols
        var input = EditorInput(Group(1, new[] { "Steel", "WoodLog" }, new[] { "Silver" }));
        input.DepthOf = _ => 1;
        var model = ReadoutLayoutEngine.Build(input);

        float expectedW = LayoutMetrics.StripeW + LayoutMetrics.GroupPadX
            + LayoutMetrics.MarkerColW + 3f * LayoutMetrics.CellW + LayoutMetrics.GroupPadX;
        var back = CellsOf(model, CellKind.GroupBack)[0];
        await Assert.That(back.Rect.W).IsEqualTo(expectedW);
    }

    // -----------------------------------------------------------------------
    // MarkerHit present in editor mode

    [Test]
    public async Task EditorMode_MarkerHitPresent()
    {
        var model = ReadoutLayoutEngine.Build(EditorInput(Group(1)));
        await Assert.That(model.MarkerHits.Count).IsEqualTo(1);
        await Assert.That(model.MarkerHits[0].GroupId).IsEqualTo(1);
    }

    // -----------------------------------------------------------------------
    // Empty-slot suppression at cap (MaxSlotsPerTier = 8)

    private static ReadoutGroup FullTierGroup(int id)
    {
        // Build a group whose first tier has exactly MaxSlotsPerTier tokens
        string[] tokens = Enumerable.Range(1, TierOps.MaxSlotsPerTier).Select(i => "Def" + i).ToArray();
        return Group(id, tokens);
    }

    private static LayoutInput EditorInputWithCatalog(ReadoutGroup group)
    {
        // Build a catalog that recognises all DefN tokens
        var catalogEntries = Enumerable.Range(1, TierOps.MaxSlotsPerTier)
            .Select(i => "Def" + i).ToArray();
        var counts = catalogEntries.Select(d => (d, 1)).ToArray();
        return new LayoutInput
        {
            Groups = new List<ReadoutGroup> { group },
            Counts = StaticResources.Counts(counts),
            Thresholds = new Dictionary<string, ThresholdSpec>(),
            Catalog = StaticResources.CatalogWith(catalogEntries),
            Width = 600f,
            EditorMode = true,
        };
    }

    [Test]
    public async Task FullTierHasNoEmptySlot()
    {
        // A tier with 8 tokens must produce 8 Icon cells and zero EmptySlot cells.
        var group = FullTierGroup(1);
        var input = EditorInputWithCatalog(group);
        var model = ReadoutLayoutEngine.Build(input);

        var icons = CellsOf(model, CellKind.Icon);
        await Assert.That(icons.Count).IsEqualTo(TierOps.MaxSlotsPerTier);

        var empties = CellsOf(model, CellKind.EmptySlot);
        await Assert.That(empties.Count).IsEqualTo(0);
    }

    [Test]
    public async Task SevenTokensStillGetEmptySlot()
    {
        // A tier with 7 tokens (one below cap) must still produce the trailing EmptySlot.
        int count = TierOps.MaxSlotsPerTier - 1;
        string[] tokens = Enumerable.Range(1, count).Select(i => "Def" + i).ToArray();
        var group = Group(1, tokens);
        var catalogEntries = tokens;
        var inputCounts = catalogEntries.Select(d => (d, 1)).ToArray();
        var input = new LayoutInput
        {
            Groups = new List<ReadoutGroup> { group },
            Counts = StaticResources.Counts(inputCounts),
            Thresholds = new Dictionary<string, ThresholdSpec>(),
            Catalog = StaticResources.CatalogWith(catalogEntries),
            Width = 600f,
            EditorMode = true,
        };
        var model = ReadoutLayoutEngine.Build(input);

        var icons = CellsOf(model, CellKind.Icon);
        await Assert.That(icons.Count).IsEqualTo(count);

        var empties = CellsOf(model, CellKind.EmptySlot);
        await Assert.That(empties.Count).IsEqualTo(1);
        await Assert.That(empties[0].Slot).IsEqualTo(count);
    }
}
