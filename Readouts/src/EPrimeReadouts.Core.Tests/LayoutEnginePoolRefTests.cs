using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

/// Tests for first-class pool reference (#poolId) token rendering in the layout engine.
public class LayoutEnginePoolRefTests
{
    private const int PoolId = 1;

    private static ReadoutGroup Group(int id, params string[] tokens)
    {
        var group = new ReadoutGroup { Id = id, Name = "G" + id };
        group.Tiers.Add(tokens.ToList());
        return group;
    }

    private static LayoutInput Input(ReadoutGroup group, PoolSnapshot? pools = null,
        Dictionary<string, int>? counts = null,
        Dictionary<string, ThresholdSpec>? thresholds = null) => new()
    {
        Groups = new List<ReadoutGroup> { group },
        Counts = counts ?? new Dictionary<string, int>(),
        Thresholds = thresholds ?? new Dictionary<string, ThresholdSpec>(),
        Catalog = StaticResources.Catalog(),
        Pools = pools,
        Width = 140f,
    };

    private static LayoutInput EditorInput(ReadoutGroup group, PoolSnapshot? pools = null,
        Dictionary<string, int>? counts = null) => new()
    {
        Groups = new List<ReadoutGroup> { group },
        Counts = counts ?? new Dictionary<string, int>(),
        Thresholds = new Dictionary<string, ThresholdSpec>(),
        Catalog = StaticResources.Catalog(),
        Pools = pools,
        Width = 300f,
        EditorMode = true,
    };

    private static List<RenderCell> CellsOf(RenderModel model, CellKind kind) =>
        model.Cells.Where(c => c.Kind == kind).ToList();

    // -----------------------------------------------------------------------
    // Normal mode: #poolId token rendering

    [Test]
    public async Task PoolRef_IconIsSnapshotIcon_SummedCounter()
    {
        // Pool: Meat_Cow=5, Meat_Chicken=7 → sum 12; icon = Meat_Cow (first member)
        var pools = StaticResources.MeatPool(PoolId);
        var counts = StaticResources.Counts(("Meat_Cow", 5), ("Meat_Chicken", 7));
        string token = SlotToken.PoolToken(PoolId); // "#1"
        var model = ReadoutLayoutEngine.Build(Input(Group(1, token), pools, counts));

        var icons = CellsOf(model, CellKind.Icon);
        await Assert.That(icons.Count).IsEqualTo(1);
        await Assert.That(icons[0].DefName).IsEqualTo("Meat_Cow");
        await Assert.That(icons[0].Token).IsEqualTo(token);

        var counters = CellsOf(model, CellKind.Counter);
        await Assert.That(counters.Count).IsEqualTo(1);
        await Assert.That(counters[0].Text).IsEqualTo("12");
        await Assert.That(counters[0].DefName).IsEqualTo("Meat_Cow");
    }

    [Test]
    public async Task PoolRef_ReusesTheImmutableSnapshotMemberList()
    {
        PoolSnapshot pools = StaticResources.MeatPool(PoolId);
        pools.TryGet(PoolId, out IReadOnlyList<string>? members,
            out _, out _);

        RenderModel model = ReadoutLayoutEngine.Build(Input(
            Group(1, SlotToken.PoolToken(PoolId)), pools));

        await Assert.That(ReferenceEquals(
            model.SlotHits[0].Members, members)).IsTrue();
    }

    [Test]
    public async Task PoolRef_ExplicitIconDefName_UsedAsIcon()
    {
        // Pool with explicit icon = Meat_Chicken
        var pools = StaticResources.MeatPoolWithIcon(PoolId);
        var counts = StaticResources.Counts(("Meat_Cow", 5), ("Meat_Chicken", 7));
        string token = SlotToken.PoolToken(PoolId);
        var model = ReadoutLayoutEngine.Build(Input(Group(1, token), pools, counts));

        var icons = CellsOf(model, CellKind.Icon);
        await Assert.That(icons.Count).IsEqualTo(1);
        await Assert.That(icons[0].DefName).IsEqualTo("Meat_Chicken");
    }

    [Test]
    public async Task PoolRef_UnknownPool_Skipped_NormalMode()
    {
        // No pools snapshot → #99 is unknown → no cells
        string token = SlotToken.PoolToken(99);
        var model = ReadoutLayoutEngine.Build(Input(Group(1, token)));

        await Assert.That(model.Cells.Count).IsEqualTo(0);
        await Assert.That(model.TotalHeight).IsEqualTo(0f);
    }

    [Test]
    public async Task PoolRef_NullSnapshot_Treated_As_Empty_NormalMode()
    {
        // Pools = null → #1 unknown → skipped
        string token = SlotToken.PoolToken(PoolId);
        var model = ReadoutLayoutEngine.Build(Input(Group(1, token), pools: null));

        await Assert.That(model.Cells.Count).IsEqualTo(0);
        await Assert.That(model.TotalHeight).IsEqualTo(0f);
    }

    [Test]
    public async Task PoolRef_ZeroMemberPool_Skipped_NormalMode()
    {
        // Pool exists but has no members (empty category) → skipped in normal mode
        var pool = new ResourcePool { Id = PoolId, Name = "Empty", Members = new List<string>() };
        var pools = PoolSnapshot.Build(new List<ResourcePool> { pool }, StaticResources.Catalog());
        string token = SlotToken.PoolToken(PoolId);
        var model = ReadoutLayoutEngine.Build(Input(Group(1, token), pools));

        await Assert.That(model.Cells.Count).IsEqualTo(0);
        await Assert.That(model.TotalHeight).IsEqualTo(0f);
    }

    [Test]
    public async Task PoolRef_VisibleAtZeroSumByDefault()
    {
        // No counts → pool shows with "0" (show-when-zero default for plain token)
        var pools = StaticResources.MeatPool(PoolId);
        string token = SlotToken.PoolToken(PoolId);
        var model = ReadoutLayoutEngine.Build(Input(Group(1, token), pools));

        var counters = CellsOf(model, CellKind.Counter);
        await Assert.That(counters.Count).IsEqualTo(1);
        await Assert.That(counters[0].Text).IsEqualTo("0");
    }

    [Test]
    public async Task PoolRef_HideFlagged_HiddenAtZeroSum()
    {
        // ~#1 with no counts → hidden
        var pools = StaticResources.MeatPool(PoolId);
        string token = "~" + SlotToken.PoolToken(PoolId); // "~#1"
        var model = ReadoutLayoutEngine.Build(Input(Group(1, token), pools));

        await Assert.That(model.Cells.Count).IsEqualTo(0);
        await Assert.That(model.TotalHeight).IsEqualTo(0f);
    }

    [Test]
    public async Task PoolRef_ThresholdOnPoolSum()
    {
        // Threshold["#1"] = (20,10); sum 12 → Band.Low
        var pools = StaticResources.MeatPool(PoolId);
        var counts = StaticResources.Counts(("Meat_Cow", 5), ("Meat_Chicken", 7));
        string token = SlotToken.PoolToken(PoolId);
        var thresholds = new Dictionary<string, ThresholdSpec>
        {
            [token] = new ThresholdSpec(20, 10),
        };
        var model = ReadoutLayoutEngine.Build(Input(Group(1, token), pools, counts, thresholds));

        var counters = CellsOf(model, CellKind.Counter);
        await Assert.That(counters.Count).IsEqualTo(1);
        await Assert.That(counters[0].Band).IsEqualTo(Band.Low);
    }

    // -----------------------------------------------------------------------
    // Search highlight: pool name matches

    [Test]
    public async Task PoolRef_HighlightMatchesPoolName()
    {
        // Pool name = "Meats"; search "meat" → highlight cell emitted
        var pools = StaticResources.MeatPool(PoolId);
        var counts = StaticResources.Counts(("Meat_Cow", 5), ("Meat_Chicken", 7));
        string token = SlotToken.PoolToken(PoolId);
        var input = Input(Group(1, token), pools, counts);
        input.SearchText = "meat";
        var model = ReadoutLayoutEngine.Build(input);

        var highlights = CellsOf(model, CellKind.Highlight);
        await Assert.That(highlights.Count).IsEqualTo(1);
        await Assert.That(highlights[0].Token).IsEqualTo(token);
    }

    [Test]
    public async Task PoolRef_HighlightDoesNotMatchMemberLabel()
    {
        // Searching for "cow" (a member label) should NOT highlight the pool slot
        // (pool highlight matches pool NAME, not member labels)
        var pools = StaticResources.MeatPool(PoolId);
        var counts = StaticResources.Counts(("Meat_Cow", 5), ("Meat_Chicken", 7));
        string token = SlotToken.PoolToken(PoolId);
        var input = Input(Group(1, token), pools, counts);
        input.SearchText = "cow";
        var model = ReadoutLayoutEngine.Build(input);

        var highlights = CellsOf(model, CellKind.Highlight);
        // No highlight: pool name "Meats" does not match "cow"
        await Assert.That(highlights.Count).IsEqualTo(0);
    }

    // -----------------------------------------------------------------------
    // Editor mode: #poolId tokens

    [Test]
    public async Task PoolRef_EditorMode_UnknownPool_CellEmitted()
    {
        // In editor mode, unknown pool token is kept editable (cell with Token present)
        string token = SlotToken.PoolToken(99);
        var model = ReadoutLayoutEngine.Build(EditorInput(Group(1, token), pools: null));

        // Should have an Icon cell with the token set (no def, but slot is present)
        var icons = CellsOf(model, CellKind.Icon);
        await Assert.That(icons.Count).IsEqualTo(1);
        await Assert.That(icons[0].Token).IsEqualTo(token);
        await Assert.That(icons[0].DefName).IsNull();
    }

    [Test]
    public async Task PoolRef_EditorMode_ZeroMemberPool_CellEmitted()
    {
        // Pool with no resolved members: still emit slot in editor mode
        var pool = new ResourcePool { Id = PoolId, Name = "Empty", Members = new List<string>() };
        var pools = PoolSnapshot.Build(new List<ResourcePool> { pool }, StaticResources.Catalog());
        string token = SlotToken.PoolToken(PoolId);
        var model = ReadoutLayoutEngine.Build(EditorInput(Group(1, token), pools));

        var icons = CellsOf(model, CellKind.Icon);
        await Assert.That(icons.Count).IsEqualTo(1);
        await Assert.That(icons[0].Token).IsEqualTo(token);
        await Assert.That(icons[0].Tier).IsEqualTo(0);
        await Assert.That(icons[0].Slot).IsEqualTo(0);
    }

    [Test]
    public async Task PoolRef_EditorMode_KnownPool_IconAndCounter()
    {
        // Known pool in editor mode: icon = snapshot icon, counter = sum
        var pools = StaticResources.MeatPool(PoolId);
        var counts = StaticResources.Counts(("Meat_Cow", 3), ("Meat_Chicken", 4));
        string token = SlotToken.PoolToken(PoolId);
        var model = ReadoutLayoutEngine.Build(EditorInput(Group(1, token), pools, counts));

        var icons = CellsOf(model, CellKind.Icon);
        await Assert.That(icons.Count).IsEqualTo(1);
        await Assert.That(icons[0].DefName).IsEqualTo("Meat_Cow");
        await Assert.That(icons[0].Token).IsEqualTo(token);

        var counters = CellsOf(model, CellKind.Counter);
        await Assert.That(counters.Count).IsEqualTo(1);
        await Assert.That(counters[0].Text).IsEqualTo("7");
    }

    [Test]
    public async Task PoolRef_EditorMode_ZeroHideFlagged_TokenIncluded()
    {
        // ~#1 in editor mode: included even though hide-flagged and zero count
        var pools = StaticResources.MeatPool(PoolId);
        string token = "~" + SlotToken.PoolToken(PoolId);
        var model = ReadoutLayoutEngine.Build(EditorInput(Group(1, token), pools));

        var icons = CellsOf(model, CellKind.Icon);
        await Assert.That(icons.Count).IsEqualTo(1);
        await Assert.That(icons[0].Token).IsEqualTo(token);
    }

    [Test]
    public async Task PoolRef_EditorMode_SlotIndexCorrect()
    {
        // pool-ref in tier 0 slot 0, EmptySlot at slot 1
        var pools = StaticResources.MeatPool(PoolId);
        string token = SlotToken.PoolToken(PoolId);
        var model = ReadoutLayoutEngine.Build(EditorInput(Group(1, token), pools));

        var icons = CellsOf(model, CellKind.Icon);
        await Assert.That(icons[0].Tier).IsEqualTo(0);
        await Assert.That(icons[0].Slot).IsEqualTo(0);

        var empties = CellsOf(model, CellKind.EmptySlot);
        await Assert.That(empties.Count).IsEqualTo(1);
        await Assert.That(empties[0].Tier).IsEqualTo(0);
        await Assert.That(empties[0].Slot).IsEqualTo(1);
    }
}
