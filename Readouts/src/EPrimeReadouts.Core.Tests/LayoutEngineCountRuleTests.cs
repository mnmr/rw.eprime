using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

/// Per-token count-rule overrides replace the global storage-only /
/// hide-forbidden basis for every slot showing the token — counts, threshold
/// bands, hide-when-zero visibility and the count-refresh fast path — while
/// search results keep the global basis (the search section's own filters).
public class LayoutEngineCountRuleTests
{
    private static ReadoutGroup Group(params string[] tokens)
    {
        var group = new ReadoutGroup { Id = 1, Name = "G1" };
        group.Tiers.Add(tokens.ToList());
        return group;
    }

    // Steel: 150 map-wide, 110 stored, 140 unforbidden, 100 stored-and-
    // unforbidden. Every basis differs from the raw count (120) so a fallback
    // to Counts or to the wrong basis cannot pass these tests by accident.
    private static Dictionary<string, SearchCount> SteelBreakdown() =>
        new() { ["Steel"] = new SearchCount(150, 110, 140, 100) };

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

    private static Dictionary<string, CountRule> Rule(
        string token, BasisOverride storageOnly, BasisOverride hideForbidden) =>
        new() { [token] = new CountRule(storageOnly, hideForbidden) };

    private static RenderCell SlotCounter(RenderModel model) =>
        model.Cells.First(c => c.Kind == CellKind.Counter && c.Token != null);

    [Test]
    public async Task RuleForcesMapWideCountWhileGlobalIsStorageOnly()
    {
        var input = Input(Group("Steel"), SteelBreakdown(), storageOnly: true);
        input.CountRules = Rule("Steel",
            BasisOverride.ForceOff, BasisOverride.Inherit);
        var model = ReadoutLayoutEngine.Build(input);
        await Assert.That(SlotCounter(model).Count).IsEqualTo(150);
    }

    [Test]
    public async Task RuleForcesStoredCountWhileGlobalIsMapWide()
    {
        var input = Input(Group("Steel"), SteelBreakdown());
        input.CountRules = Rule("Steel",
            BasisOverride.ForceOn, BasisOverride.Inherit);
        var model = ReadoutLayoutEngine.Build(input);
        await Assert.That(SlotCounter(model).Count).IsEqualTo(110);
    }

    [Test]
    public async Task RuleForcesForbiddenStacksVisibleWhileGlobalHidesThem()
    {
        var input = Input(Group("Steel"), SteelBreakdown(), hideForbidden: true);
        input.CountRules = Rule("Steel",
            BasisOverride.Inherit, BasisOverride.ForceOff);
        var model = ReadoutLayoutEngine.Build(input);
        await Assert.That(SlotCounter(model).Count).IsEqualTo(150);
    }

    [Test]
    public async Task RuleHidesForbiddenStacksWhileGlobalShowsThem()
    {
        var input = Input(Group("Steel"), SteelBreakdown());
        input.CountRules = Rule("Steel",
            BasisOverride.Inherit, BasisOverride.ForceOn);
        var model = ReadoutLayoutEngine.Build(input);
        await Assert.That(SlotCounter(model).Count).IsEqualTo(140);
    }

    [Test]
    public async Task TokenWithoutARuleKeepsTheGlobalBasis()
    {
        // Two slots; only Steel has a rule. Cow meat stays on the global
        // storage-only basis while Steel is forced map-wide.
        var input = Input(Group("Steel", "Meat_Cow"), new Dictionary<string, SearchCount>
        {
            ["Steel"] = new SearchCount(150, 110, 140, 100),
            ["Meat_Cow"] = new SearchCount(50, 30, 50, 30),
        }, storageOnly: true);
        input.CountRules = Rule("Steel",
            BasisOverride.ForceOff, BasisOverride.Inherit);
        var model = ReadoutLayoutEngine.Build(input);
        var counters = model.Cells
            .Where(c => c.Kind == CellKind.Counter).ToList();
        await Assert.That(counters[0].Count).IsEqualTo(150);
        await Assert.That(counters[1].Count).IsEqualTo(30);
    }

    [Test]
    public async Task PoolRuleAppliesToEveryMemberOfThePoolSlot()
    {
        // Pool #1 = raw meats. Global is map-wide; the pool's rule forces the
        // stored basis, so the sum is 30 + 10 instead of 50 + 25.
        var input = Input(Group("#1"), new Dictionary<string, SearchCount>
        {
            ["Meat_Cow"] = new SearchCount(50, 30, 50, 30),
            ["Meat_Chicken"] = new SearchCount(25, 10, 25, 10),
        });
        input.Pools = StaticResources.MeatPool();
        input.CountRules = Rule("#1",
            BasisOverride.ForceOn, BasisOverride.Inherit);
        var model = ReadoutLayoutEngine.Build(input);
        await Assert.That(SlotCounter(model).Count).IsEqualTo(40);
    }

    [Test]
    public async Task MemberRuleDoesNotLeakIntoAPoolSlot()
    {
        // A rule keyed by a member defName must not affect the pool token's
        // slot — rules are token-keyed, and the pool is its own token.
        var input = Input(Group("#1"), new Dictionary<string, SearchCount>
        {
            ["Meat_Cow"] = new SearchCount(50, 30, 50, 30),
            ["Meat_Chicken"] = new SearchCount(25, 10, 25, 10),
        });
        input.Pools = StaticResources.MeatPool();
        input.CountRules = Rule("Meat_Cow",
            BasisOverride.ForceOn, BasisOverride.Inherit);
        var model = ReadoutLayoutEngine.Build(input);
        await Assert.That(SlotCounter(model).Count).IsEqualTo(75);
    }

    [Test]
    public async Task RuleKeyIsTheCanonicalTokenOfAHideWhenZeroSlot()
    {
        var input = Input(Group("~Steel"), SteelBreakdown(), storageOnly: true);
        input.CountRules = Rule("Steel",
            BasisOverride.ForceOff, BasisOverride.Inherit);
        var model = ReadoutLayoutEngine.Build(input);
        await Assert.That(SlotCounter(model).Count).IsEqualTo(150);
    }

    [Test]
    public async Task ThresholdBandUsesTheRuleResolvedCount()
    {
        // Map-wide 150 is healthy; the rule narrows Steel to its stored 40,
        // which is below Low=50.
        var input = Input(Group("Steel"), new Dictionary<string, SearchCount>
        {
            ["Steel"] = new SearchCount(150, 40, 150, 40),
        });
        input.Thresholds["Steel"] = new ThresholdSpec(50, 10);
        input.CountRules = Rule("Steel",
            BasisOverride.ForceOn, BasisOverride.Inherit);
        var model = ReadoutLayoutEngine.Build(input);
        await Assert.That(SlotCounter(model).Band).IsEqualTo(Band.Low);
    }

    [Test]
    public async Task HideWhenZeroSlotDisappearsWhenRuleNarrowedCountIsZero()
    {
        // All of Steel is forbidden. The global basis shows forbidden items
        // (count 120), but the rule hides them, so the ~slot disappears.
        var input = Input(Group("~Steel"), new Dictionary<string, SearchCount>
        {
            ["Steel"] = new SearchCount(120, 120, 0, 0),
        });
        input.CountRules = Rule("Steel",
            BasisOverride.Inherit, BasisOverride.ForceOn);
        var model = ReadoutLayoutEngine.Build(input);
        await Assert.That(model.Cells.Any(c => c.Kind == CellKind.Icon)).IsFalse();
    }

    [Test]
    public async Task SearchResultsKeepTheGlobalBasis()
    {
        // Searching for steel: the results row uses the global map-wide basis
        // (150) while the group slot obeys its stored-only rule (110).
        var input = Input(Group("Steel"), SteelBreakdown());
        input.SearchText = "steel";
        input.CountRules = Rule("Steel",
            BasisOverride.ForceOn, BasisOverride.Inherit);
        var model = ReadoutLayoutEngine.Build(input);
        RenderCell resultCounter = model.Cells.First(
            c => c.Kind == CellKind.Counter && c.Token == null);
        await Assert.That(resultCounter.Count).IsEqualTo(150);
        await Assert.That(SlotCounter(model).Count).IsEqualTo(110);
    }

    [Test]
    public async Task CountRefreshFastPathHonorsRules()
    {
        var input = Input(Group("Steel"), SteelBreakdown(), storageOnly: true);
        input.CountRules = Rule("Steel",
            BasisOverride.ForceOff, BasisOverride.Inherit);
        var model = ReadoutLayoutEngine.Build(input);
        await Assert.That(SlotCounter(model).Count).IsEqualTo(150);

        input.SearchCounts = new Dictionary<string, SearchCount>
        {
            ["Steel"] = new SearchCount(200, 110, 200, 110),
        };
        await Assert.That(
            ReadoutLayoutEngine.TryRefreshCounts(input, model)).IsTrue();
        await Assert.That(SlotCounter(model).Count).IsEqualTo(200);
        await Assert.That(SlotCounter(model).Text).IsEqualTo("200");
    }
}
