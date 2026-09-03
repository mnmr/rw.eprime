using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

public class ItemPickerFilterTests
{
    private static List<ResourceTreeNode> Tree()
    {
        var root = new ResourceTreeNode { Id = "Items", Label = "items", Poolable = true };
        root.DefNames.AddRange(new[]
        {
            "Steel", "ModComponent", "ResourceOnly", "ModShelf", "Unstorable",
        });
        return new List<ResourceTreeNode> { root };
    }

    private static FakeResourceCatalog Catalog() => new FakeResourceCatalog()
        .WithItem("Steel", "steel", isResource: true, isStorable: true, ItemSourceIds.Vanilla)
        .WithItem("ModComponent", "advanced component", isResource: true, isStorable: true, "author.mod")
        .WithItem("ResourceOnly", "abstract resource", isResource: true, isStorable: false, "author.mod")
        .WithItem("ModShelf", "advanced shelf", isResource: false, isStorable: true, "author.mod")
        .WithItem("Unstorable", "advanced ghost", isResource: false, isStorable: false, "author.mod");

    private static string DefNames(List<TreeRow> rows) =>
        string.Join(",", rows.Where(row => !row.IsCategory).Select(row => row.DefName));

    [Test]
    public async Task ResourceAndAllStorableModesUseTheApprovedSetRelationship()
    {
        var expanded = new HashSet<string> { "Items" };
        var resources = ResourceTreeFlattener.Flatten(
            Tree(), expanded, new ItemTreeFilter("", ItemPickerType.Resources, ItemSourceIds.All), Catalog());
        var allStorable = ResourceTreeFlattener.Flatten(
            Tree(), expanded, new ItemTreeFilter("", ItemPickerType.AllStorableItems, ItemSourceIds.All), Catalog());

        await Assert.That(DefNames(resources)).IsEqualTo("Steel,ModComponent,ResourceOnly");
        await Assert.That(DefNames(allStorable)).IsEqualTo("Steel,ModComponent,ResourceOnly,ModShelf");
    }

    [Test]
    public async Task TextTypeAndSourceFiltersAreConjunctiveAndExposeMatchingCategoryMembers()
    {
        var rows = ResourceTreeFlattener.Flatten(
            Tree(),
            new HashSet<string>(),
            new ItemTreeFilter("advanced", ItemPickerType.AllStorableItems, "author.mod"),
            Catalog());

        await Assert.That(DefNames(rows)).IsEqualTo("ModComponent,ModShelf");
        await Assert.That(rows[0].Expanded).IsTrue();
        await Assert.That(string.Join(",", rows[0].MatchingDefNames)).IsEqualTo("ModComponent,ModShelf");
    }

    [Test]
    public async Task QueryMatchingACategoryLabelShowsEveryEligibleItemInsideIt()
    {
        var root = new ResourceTreeNode { Id = "Manufactured", Label = "manufactured", Poolable = true };
        root.DefNames.AddRange(new[] { "Steel", "ModShelf", "Unstorable" });
        var child = new ResourceTreeNode { Id = "Components", Label = "components" };
        child.DefNames.Add("ModComponent");
        root.Children.Add(child);
        var other = new ResourceTreeNode { Id = "Other", Label = "other" };
        other.DefNames.Add("ResourceOnly");
        var tree = new List<ResourceTreeNode> { root, other };

        var rows = ResourceTreeFlattener.Flatten(
            tree,
            new HashSet<string>(),
            new ItemTreeFilter("manufact", ItemPickerType.AllStorableItems, ItemSourceIds.All),
            Catalog());

        await Assert.That(string.Join(",", rows.Select(row => row.IsCategory ? row.Id : row.DefName)))
            .IsEqualTo("Manufactured,Components,ModComponent,Steel,ModShelf");
        await Assert.That(string.Join(",", rows[0].MatchingDefNames)).IsEqualTo("ModComponent,Steel,ModShelf");

        var resourcesFromMod = ResourceTreeFlattener.Flatten(
            tree,
            new HashSet<string>(),
            new ItemTreeFilter("manufact", ItemPickerType.Resources, "author.mod"),
            Catalog());
        await Assert.That(DefNames(resourcesFromMod)).IsEqualTo("ModComponent");
    }

    [Test]
    public async Task EmptyFilteredResultDoesNotChangeSavedExpansionState()
    {
        var expanded = new HashSet<string> { "Items" };
        var rows = ResourceTreeFlattener.Flatten(
            Tree(), expanded, new ItemTreeFilter("missing", ItemPickerType.Resources, ItemSourceIds.All), Catalog());

        await Assert.That(rows).IsEmpty();
        await Assert.That(expanded.SetEquals(new[] { "Items" })).IsTrue();
    }

    [Test]
    public async Task SourceChoicesKeepFixedOptionsAndSortContributingModsStably()
    {
        var choices = ItemSourceChoices.Build(
            new[]
            {
                new ItemSourceOption("z.mod", "Zed Content"),
                new ItemSourceOption("a.mod", "Alpha Content"),
                new ItemSourceOption("A.MOD", "Duplicate Alpha"),
                new ItemSourceOption(ItemSourceIds.Vanilla, "Core"),
            },
            "All",
            "Vanilla");

        await Assert.That(string.Join(",", choices.Select(choice => choice.Id)))
            .IsEqualTo(",__vanilla__,a.mod,z.mod");
        await Assert.That(string.Join(",", choices.Select(choice => choice.Label)))
            .IsEqualTo("All,Vanilla,Alpha Content,Zed Content");
    }
}
