using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

public class PoolAssignmentTreeTests
{
    [Test]
    public async Task CustomSeedEquivalentNameRemainsVerbatimAfterExportAndImport()
    {
        var original = new ReadoutModel();
        original.CreatePool(1, "Meals");
        original.CreateGroup(1, "Food");
        string xml = ReadoutsXml.Export(original.Pools, original.Groups);
        bool parsed = ReadoutsXml.TryImport(xml, out var pools, out var groups, out _);
        await Assert.That(parsed).IsTrue();
        var imported = new ReadoutModel();
        int poolId = 10, groupId = 20;
        imported.ApplyImport(pools!, groups!, () => poolId++, () => groupId++);
        static string TranslateSeed(string name) => name == "Meals" ? "Repas" : name;
        var rows = PoolAssignmentTree.Build(PoolSnapshot.Build(imported.Pools, StaticResources.Catalog()),
            true, new ItemTreeFilter("", ItemPickerType.Resources, ItemSourceIds.All), "Pools", TranslateSeed);
        await Assert.That(rows[1].Label).IsEqualTo("Meals");
        await Assert.That(imported.Groups[0].Name).IsEqualTo("Food");
    }

    private static PoolSnapshot Pools() => PoolSnapshot.Build(
        new List<ResourcePool>
        {
            new ResourcePool
            {
                Id = 2,
                Name = "Components",
                IconDefName = "ComponentIndustrial",
                Members = new List<string> { "ComponentIndustrial" },
            },
            new ResourcePool
            {
                Id = 7,
                Name = "Metals",
                SeedName = "Metals",
                IconDefName = "Steel",
                Members = new List<string> { "Steel", "Plasteel" },
            },
        },
        StaticResources.Catalog());

    [Test]
    public async Task LocalizedSearchUsesDisplayNamesAndPreservesSavedPoolIdentities()
    {
        var pools = Pools();
        static string Display(string value) => value == "Metals" ? "Métaux" : value;
        var rows = PoolAssignmentTree.Build(pools, expanded: false,
            new ItemTreeFilter("métaux", ItemPickerType.Resources, ItemSourceIds.All),
            "Réserves", Display);

        await Assert.That(string.Join(",", rows.Select(row => row.Label)))
            .IsEqualTo("Réserves,Métaux");
        await Assert.That(rows[1].Token).IsEqualTo("#7");
        await Assert.That(rows[1].IconDefName).IsEqualTo("Steel");
        await Assert.That(pools.EntryAt(1).Name).IsEqualTo("Metals");
        var all = PoolAssignmentTree.Build(pools, expanded: true,
            new ItemTreeFilter("", ItemPickerType.Resources, ItemSourceIds.All),
            "Réserves", Display);
        await Assert.That(all[1].Label).IsEqualTo("Components");
    }

    [Test]
    public async Task EmptySnapshotProducesNoSyntheticRows()
    {
        var empty = PoolSnapshot.Build(
            new List<ResourcePool>(), StaticResources.Catalog());

        var rows = PoolAssignmentTree.Build(
            empty, expanded: true,
            new ItemTreeFilter("", ItemPickerType.Resources, ItemSourceIds.All),
            rootLabel: "Resource Pools");

        await Assert.That(rows).IsEmpty();
    }

    [Test]
    public async Task CollapsedTreePublishesOnlyItsRoot()
    {
        var rows = PoolAssignmentTree.Build(
            Pools(), expanded: false,
            new ItemTreeFilter("", ItemPickerType.Resources, ItemSourceIds.All),
            rootLabel: "Resource Pools");

        await Assert.That(rows.Length).IsEqualTo(1);
        await Assert.That(rows[0].IsRoot).IsTrue();
        await Assert.That(rows[0].Label).IsEqualTo("Resource Pools");
        await Assert.That(rows[0].Expanded).IsFalse();
    }

    [Test]
    public async Task ExpandedTreePreservesSnapshotOrderAndStableTokens()
    {
        var rows = PoolAssignmentTree.Build(
            Pools(), expanded: true,
            new ItemTreeFilter("", ItemPickerType.Resources, ItemSourceIds.All),
            rootLabel: "Resource Pools");

        await Assert.That(string.Join(",", rows.Select(row => row.Label)))
            .IsEqualTo("Resource Pools,Components,Metals");
        await Assert.That(string.Join(",", rows.Skip(1).Select(row => row.Token)))
            .IsEqualTo("#2,#7");
        await Assert.That(rows[1].IconDefName).IsEqualTo("ComponentIndustrial");
        await Assert.That(rows[2].IconDefName).IsEqualTo("Steel");
    }

    [Test]
    public async Task TextSearchFiltersPoolNamesAndForcesExpansion()
    {
        var rows = PoolAssignmentTree.Build(
            Pools(), expanded: false,
            new ItemTreeFilter("metal", ItemPickerType.Resources, ItemSourceIds.All),
            rootLabel: "Resource Pools");

        await Assert.That(string.Join(",", rows.Select(row => row.Label)))
            .IsEqualTo("Resource Pools,Metals");
        await Assert.That(rows[0].Expanded).IsTrue();
    }

    [Test]
    public async Task NonmatchingTextSearchOmitsTheBranch()
    {
        var rows = PoolAssignmentTree.Build(
            Pools(), expanded: true,
            new ItemTreeFilter("textiles", ItemPickerType.Resources, ItemSourceIds.All),
            rootLabel: "Resource Pools");

        await Assert.That(rows).IsEmpty();
    }

    [Test]
    public async Task ItemTypeAndSourceDoNotHidePoolRows()
    {
        var resourceRows = PoolAssignmentTree.Build(
            Pools(), expanded: true,
            new ItemTreeFilter("", ItemPickerType.Resources, "one.mod"),
            rootLabel: "Resource Pools");
        var storableRows = PoolAssignmentTree.Build(
            Pools(), expanded: true,
            new ItemTreeFilter("", ItemPickerType.AllStorableItems, "other.mod"),
            rootLabel: "Resource Pools");

        await Assert.That(string.Join(",", resourceRows.Select(row => row.Token)))
            .IsEqualTo(string.Join(",", storableRows.Select(row => row.Token)));
        await Assert.That(resourceRows.Length).IsEqualTo(3);
    }
}
