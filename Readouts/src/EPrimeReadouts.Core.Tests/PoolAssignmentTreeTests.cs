using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

public class PoolAssignmentTreeTests
{
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
                IconDefName = "Steel",
                Members = new List<string> { "Steel", "Plasteel" },
            },
        },
        StaticResources.Catalog());

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
