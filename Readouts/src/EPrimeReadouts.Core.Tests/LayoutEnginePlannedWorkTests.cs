using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

/// Planned-work debt narrows every displayed count exactly like the storage and
/// forbidden options do: group slots, pool sums, visibility and threshold bands.
public class LayoutEnginePlannedWorkTests
{
    private static ReadoutGroup Group(params string[] tokens)
    {
        var group = new ReadoutGroup { Id = 1, Name = "G1" };
        group.Tiers.Add(tokens.ToList());
        return group;
    }

    private static LayoutInput Input(
        ReadoutGroup group,
        Dictionary<string, PlannedWorkDebt>? debts = null,
        bool allowNegative = false)
    {
        return new LayoutInput
        {
            Groups = new List<ReadoutGroup> { group },
            Counts = StaticResources.Counts(
                ("Steel", 120), ("Meat_Cow", 30), ("Meat_Chicken", 10)),
            SearchCounts = new Dictionary<string, SearchCount>
            {
                ["Steel"] = new SearchCount(120, 120, 120, 120),
                ["Meat_Cow"] = new SearchCount(30, 30, 30, 30),
                ["Meat_Chicken"] = new SearchCount(10, 10, 10, 10),
            },
            Debts = debts,
            AllowNegativeCounts = allowNegative,
            Thresholds = new Dictionary<string, ThresholdSpec>(),
            Catalog = StaticResources.Catalog(),
            Width = 140f,
        };
    }

    private static RenderCell Counter(RenderModel model) =>
        model.Cells.First(c => c.Kind == CellKind.Counter);

    private static Dictionary<string, PlannedWorkDebt> Debt(
        string defName, int bills = 0, int buildables = 0) =>
        new() { [defName] = new PlannedWorkDebt(bills, buildables) };

    [Test]
    public async Task NoDebtMapLeavesCountsUnchanged()
    {
        var model = ReadoutLayoutEngine.Build(Input(Group("Steel")));
        await Assert.That(Counter(model).Count).IsEqualTo(120);
    }

    [Test]
    public async Task BillDebtReducesTheSlotCount()
    {
        var model = ReadoutLayoutEngine.Build(
            Input(Group("Steel"), Debt("Steel", bills: 45)));
        await Assert.That(Counter(model).Count).IsEqualTo(75);
        await Assert.That(Counter(model).Text).IsEqualTo("75");
    }

    [Test]
    public async Task BillAndBuildableDebtBothCount()
    {
        var model = ReadoutLayoutEngine.Build(
            Input(Group("Steel"), Debt("Steel", bills: 45, buildables: 30)));
        await Assert.That(Counter(model).Count).IsEqualTo(45);
    }

    [Test]
    public async Task DebtOnAnUnrelatedDefLeavesTheSlotAlone()
    {
        var model = ReadoutLayoutEngine.Build(
            Input(Group("Steel"), Debt("WoodLog", bills: 45)));
        await Assert.That(Counter(model).Count).IsEqualTo(120);
    }

    [Test]
    public async Task OverrunClampsToZeroByDefault()
    {
        var model = ReadoutLayoutEngine.Build(
            Input(Group("Steel"), Debt("Steel", bills: 400)));
        await Assert.That(Counter(model).Count).IsEqualTo(0);
    }

    [Test]
    public async Task OverrunShowsNegativeWhenEnabled()
    {
        var model = ReadoutLayoutEngine.Build(
            Input(Group("Steel"), Debt("Steel", bills: 400), allowNegative: true));
        await Assert.That(Counter(model).Count).IsEqualTo(-280);
        await Assert.That(Counter(model).Text).IsEqualTo("-280");
    }

    [Test]
    public async Task ANegativeCountUsesTheCriticalTintWithoutAThreshold()
    {
        var model = ReadoutLayoutEngine.Build(
            Input(Group("Steel"), Debt("Steel", bills: 400), allowNegative: true));
        await Assert.That(Counter(model).Band).IsEqualTo(Band.Critical);
    }

    [Test]
    public async Task ANegativeSlotStaysVisible()
    {
        // Overrun is exactly the state a player needs to see; the slot's
        // hide-when-zero mark ("~") must not swallow it.
        var model = ReadoutLayoutEngine.Build(
            Input(Group("~Steel"), Debt("Steel", bills: 400), allowNegative: true));
        await Assert.That(model.Cells.Any(c => c.Kind == CellKind.Icon)).IsTrue();
    }

    [Test]
    public async Task AZeroedOutSlotStillHidesWhenNegativesAreOff()
    {
        var model = ReadoutLayoutEngine.Build(
            Input(Group("~Steel"), Debt("Steel", bills: 400)));
        await Assert.That(model.Cells.Any(c => c.Kind == CellKind.Icon)).IsFalse();
    }

    [Test]
    public async Task PoolSumsSubtractEachMemberDebt()
    {
        var input = Input(Group("#1"), new Dictionary<string, PlannedWorkDebt>
        {
            ["Meat_Cow"] = new PlannedWorkDebt(12, 0),
            ["Meat_Chicken"] = new PlannedWorkDebt(0, 3),
        });
        input.Pools = StaticResources.MeatPool();
        var model = ReadoutLayoutEngine.Build(input);
        await Assert.That(Counter(model).Count).IsEqualTo(25);
    }

    [Test]
    public async Task ThresholdBandReflectsTheDebtedCount()
    {
        var input = Input(Group("Steel"), Debt("Steel", buildables: 90));
        input.Thresholds["Steel"] = new ThresholdSpec(50, 10);
        var model = ReadoutLayoutEngine.Build(input);
        await Assert.That(Counter(model).Band).IsEqualTo(Band.Low);
    }

    [Test]
    public async Task ANegativeCountLandsInTheCriticalBand()
    {
        var input = Input(Group("Steel"), Debt("Steel", bills: 400),
            allowNegative: true);
        input.Thresholds["Steel"] = new ThresholdSpec(50, 10);
        var model = ReadoutLayoutEngine.Build(input);
        await Assert.That(Counter(model).Band).IsEqualTo(Band.Critical);
    }

    [Test]
    public async Task SearchResultsSubtractDebtToo()
    {
        var input = Input(Group("Steel"), Debt("Steel", bills: 45));
        input.SearchText = "steel";
        var model = ReadoutLayoutEngine.Build(input);
        RenderCell result = model.Cells.Last(c => c.Kind == CellKind.Counter);
        await Assert.That(result.Count).IsEqualTo(75);
    }

    [Test]
    public async Task ANegativeSearchResultUsesTheCriticalTintWithoutAThreshold()
    {
        var input = Input(Group("Steel"), Debt("Steel", bills: 400),
            allowNegative: true);
        input.SearchText = "steel";
        var model = ReadoutLayoutEngine.Build(input);
        RenderCell result = model.Cells.Last(c => c.Kind == CellKind.Counter);
        await Assert.That(result.Band).IsEqualTo(Band.Critical);
    }

    [Test]
    public async Task ANegativeEditorCountUsesTheCriticalTintWithoutAThreshold()
    {
        var input = Input(Group("Steel"), Debt("Steel", bills: 400),
            allowNegative: true);
        input.EditorMode = true;
        var model = ReadoutLayoutEngine.Build(input);
        await Assert.That(Counter(model).Band).IsEqualTo(Band.Critical);
    }
}
