using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

public class ResourceTreeFlattenerTests
{
    private static List<ResourceTreeNode> Tree()
    {
        var food = new ResourceTreeNode { Id = "Foods", Label = "foods" };
        food.DefNames.AddRange(new[] { "MealSimple", "RawRice" });
        var meals = new ResourceTreeNode { Id = "FoodMeals", Label = "meals" };
        meals.DefNames.Add("MealFine");
        food.Children.Add(meals);
        var manufactured = new ResourceTreeNode { Id = "Manufactured", Label = "manufactured" };
        manufactured.DefNames.Add("ComponentIndustrial");
        return new List<ResourceTreeNode> { food, manufactured };
    }

    private static string RowIds(List<TreeRow> rows) =>
        string.Join(",", rows.Select(r => r.IsCategory ? r.Id : r.DefName));

    [Test]
    public async Task CollapsedRootsShowOnlyCategoryRows()
    {
        var rows = ResourceTreeFlattener.Flatten(Tree(), new HashSet<string>(), "", StaticResources.Catalog());
        await Assert.That(RowIds(rows)).IsEqualTo("Foods,Manufactured");
        await Assert.That(rows.All(r => r.IsCategory)).IsTrue();
        await Assert.That(rows[0].Expanded).IsFalse();
    }

    [Test]
    public async Task ExpandedCategoryShowsChildrenThenDefs()
    {
        var rows = ResourceTreeFlattener.Flatten(Tree(), new HashSet<string> { "Foods" }, "", StaticResources.Catalog());
        await Assert.That(RowIds(rows)).IsEqualTo("Foods,FoodMeals,MealSimple,RawRice,Manufactured");
        await Assert.That(rows[1].Indent).IsEqualTo(1);
        await Assert.That(rows[2].Indent).IsEqualTo(1);
    }

    [Test]
    public async Task FilterForceExpandsAndShowsOnlyMatches()
    {
        var rows = ResourceTreeFlattener.Flatten(Tree(), new HashSet<string>(), "meal", StaticResources.Catalog());
        await Assert.That(RowIds(rows)).IsEqualTo("Foods,FoodMeals,MealFine,MealSimple");
        await Assert.That(rows.Any(r => r.Id == "Manufactured")).IsFalse();
        await Assert.That(rows.First(r => r.Id == "Foods").Expanded).IsTrue();
    }

    [Test]
    public async Task ChildExpansionIsIgnoredWhileParentCollapsed()
    {
        var rows = ResourceTreeFlattener.Flatten(
            Tree(), new HashSet<string> { "FoodMeals" }, "", StaticResources.Catalog());
        await Assert.That(RowIds(rows)).IsEqualTo("Foods,Manufactured");
        await Assert.That(rows[0].Expanded).IsFalse();
    }

    [Test]
    public async Task PoolableFlagReachesCategoryRows()
    {
        var root = new ResourceTreeNode { Id = "Root", Label = "root", Poolable = true };
        root.DefNames.Add("Steel");
        var nonPoolable = new ResourceTreeNode { Id = "Child", Label = "child", Poolable = false };
        nonPoolable.DefNames.Add("WoodLog");
        root.Children.Add(nonPoolable);

        var rows = ResourceTreeFlattener.Flatten(
            new List<ResourceTreeNode> { root },
            new HashSet<string> { "Root" }, "", StaticResources.Catalog());

        var rootRow = rows.First(r => r.Id == "Root");
        await Assert.That(rootRow.Poolable).IsTrue();

        var childRow = rows.First(r => r.Id == "Child");
        await Assert.That(childRow.Poolable).IsFalse();
    }
}
