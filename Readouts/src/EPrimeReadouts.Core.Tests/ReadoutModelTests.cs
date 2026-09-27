using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

public class ReadoutModelTests
{
    private static ReadoutModel ModelWithGroups(params string[] names)
    {
        var model = new ReadoutModel();
        for (int i = 0; i < names.Length; i++) model.CreateGroup(i + 1, names[i]);
        return model;
    }

    private static string DisplayNames(ReadoutModel model) =>
        string.Join(",", model.InDisplayOrder().Select(g => g.Name));

    [Test]
    public async Task CreateAssignsSequentialOrderIndexes()
    {
        var model = ModelWithGroups("Food", "Drugs");
        await Assert.That(model.GroupById(1)!.OrderIndex).IsEqualTo(0);
        await Assert.That(model.GroupById(2)!.OrderIndex).IsEqualTo(1);
    }

    [Test]
    public async Task InDisplayOrderSortsByOrderIndex()
    {
        var model = ModelWithGroups("Food", "Drugs", "Wealth");
        model.ReorderGroup(3, -2);
        await Assert.That(DisplayNames(model)).IsEqualTo("Wealth,Drugs,Food");
    }

    [Test]
    public async Task ReorderSwapsWithNeighbor()
    {
        var model = ModelWithGroups("Food", "Drugs");
        await Assert.That(model.ReorderGroup(2, -1)).IsTrue();
        await Assert.That(DisplayNames(model)).IsEqualTo("Drugs,Food");
    }

    [Test]
    public async Task ReorderPastEndsRefused()
    {
        var model = ModelWithGroups("Food");
        await Assert.That(model.ReorderGroup(1, -1)).IsFalse();
        await Assert.That(model.ReorderGroup(1, 1)).IsFalse();
    }

    [Test]
    public async Task DeleteRemovesGroup()
    {
        var model = ModelWithGroups("Food");
        await Assert.That(model.DeleteGroup(1)).IsTrue();
        await Assert.That(model.Groups.Count).IsEqualTo(0);
    }

    [Test]
    public async Task RenameChangesName()
    {
        var model = ModelWithGroups("Food");
        await Assert.That(model.RenameGroup(1, "Meals")).IsTrue();
        await Assert.That(model.GroupById(1)!.Name).IsEqualTo("Meals");
        await Assert.That(model.RenameGroup(99, "X")).IsFalse();
    }

    // --- Unique group names (a name identifies exactly one group) ---

    [Test]
    public async Task GroupNamesAreUniqueIgnoringCaseAndSurroundingSpace()
    {
        var model = ModelWithGroups("Food", "Drugs");

        await Assert.That(model.CanUseGroupName(" food ")).IsFalse();
        await Assert.That(model.CanUseGroupName("   ")).IsFalse();
        await Assert.That(model.CanUseGroupName("Meals")).IsTrue();
        // A group may keep (or re-case) its own name.
        await Assert.That(model.CanUseGroupName("FOOD", exceptGroupId: 1)).IsTrue();

        await Assert.That(model.RenameGroup(2, "FOOD")).IsFalse();
        await Assert.That(model.RenameGroup(2, "  ")).IsFalse();
        await Assert.That(model.RenameGroup(1, " Meals ")).IsTrue();
        await Assert.That(DisplayNames(model)).IsEqualTo("Meals,Drugs");
        await Assert.That(model.GroupByName("meals")!.Id).IsEqualTo(1);

        await Assert.That(() => model.CreateGroup(3, "drugs"))
            .Throws<InvalidOperationException>();
        await Assert.That(model.Groups.Count).IsEqualTo(2);
    }

    [Test]
    public async Task LoadMakesDuplicateAndBlankLegacyGroupNamesUnique()
    {
        // Saves from before names were unique can hold duplicates and blanks.
        // Load renames later duplicates in display order, deterministically,
        // and never takes a name another group already has.
        var model = new ReadoutModel();
        model.Groups.Add(new ReadoutGroup { Id = 7, Name = "food", OrderIndex = 1 });
        model.Groups.Add(new ReadoutGroup { Id = 3, Name = "Food", OrderIndex = 0 });
        model.Groups.Add(new ReadoutGroup { Id = 5, Name = "  ", OrderIndex = 2 });
        model.Groups.Add(new ReadoutGroup { Id = 9, Name = "Food (2)", OrderIndex = 3 });

        model.CleanupMissing(t => true, m => true);

        await Assert.That(DisplayNames(model)).IsEqualTo("Food,food (3),Group,Food (2)");
    }

    [Test]
    public async Task ImportMakesDuplicateGroupNamesUnique()
    {
        // Files exported before names were unique still import.
        const string xml = "<Readouts><Groups>"
            + "<Group Name=\"Food\"/><Group Name=\"FOOD\"/><Group Name=\"\"/>"
            + "</Groups></Readouts>";
        await Assert.That(ReadoutsXml.TryImport(xml, out var pools, out var groups, out _)).IsTrue();
        var model = ModelWithGroups("Old");
        int next = 10;
        model.ApplyImport(pools, groups, () => next++, () => next++);

        await Assert.That(DisplayNames(model)).IsEqualTo("Food,FOOD (2),Group");
    }

    [Test]
    public async Task SetTiersRefusesMoreThanMaxTiers()
    {
        var model = ModelWithGroups("Food");
        var four = new List<List<string>>
        {
            new() { "A" }, new() { "B" }, new() { "C" }, new() { "D" },
        };
        await Assert.That(model.SetTiers(1, four)).IsFalse();
        await Assert.That(model.GroupById(1)!.TierCount).IsEqualTo(0);
    }

    [Test]
    public async Task SetTiersCompacts()
    {
        var model = ModelWithGroups("Food");
        var tiers = new List<List<string>> { new(), new() { "Steel" } };
        await Assert.That(model.SetTiers(1, tiers)).IsTrue();
        await Assert.That(model.GroupById(1)!.TierCount).IsEqualTo(1);
    }

    [Test]
    public async Task CleanupPurgesTiersAndThresholds()
    {
        var model = ModelWithGroups("Food");
        model.SetTiers(1, new List<List<string>> { new() { "Gone", "Steel" } });
        model.SetThreshold("Gone", 10, 2);
        model.SetThreshold("Steel", 100, 20);
        model.CleanupMissing(d => d == "Steel", m => true);
        await Assert.That(string.Join(",", model.GroupById(1)!.Tiers[0])).IsEqualTo("Steel");
        await Assert.That(model.Thresholds.ContainsKey("Gone")).IsFalse();
        await Assert.That(model.Thresholds.ContainsKey("Steel")).IsTrue();
    }

    // --- MoveGroupTo ---

    [Test]
    public async Task MoveGroupTo_MiddleToFront()
    {
        // Groups: Food(0), Drugs(1), Metals(2)
        var model = ModelWithGroups("Food", "Drugs", "Metals");
        bool result = model.MoveGroupTo(2, 0); // move Drugs (id=2) to front
        await Assert.That(result).IsTrue();
        await Assert.That(DisplayNames(model)).IsEqualTo("Drugs,Food,Metals");
    }

    [Test]
    public async Task MoveGroupTo_FrontToEnd_IndexClampedPastEnd()
    {
        // Groups: Food(0), Drugs(1), Metals(2) — move Food to index 99 → clamped to 2 (last)
        var model = ModelWithGroups("Food", "Drugs", "Metals");
        bool result = model.MoveGroupTo(1, 99);
        await Assert.That(result).IsTrue();
        await Assert.That(DisplayNames(model)).IsEqualTo("Drugs,Metals,Food");
    }

    [Test]
    public async Task MoveGroupTo_UnknownIdReturnsFalse()
    {
        var model = ModelWithGroups("Food", "Drugs");
        await Assert.That(model.MoveGroupTo(999, 0)).IsFalse();
    }

    [Test]
    public async Task MoveGroupTo_NormalizesOrderIndexes()
    {
        // After move, all OrderIndexes should be exactly 0..n-1 in display order
        var model = ModelWithGroups("Food", "Drugs", "Metals");
        model.MoveGroupTo(3, 0); // move Metals (id=3) to front
        var inOrder = model.InDisplayOrder();
        await Assert.That(string.Join(",", inOrder.Select(g => g.Name))).IsEqualTo("Metals,Food,Drugs");
        // Verify OrderIndexes are exactly 0, 1, 2
        await Assert.That(inOrder[0].OrderIndex).IsEqualTo(0);
        await Assert.That(inOrder[1].OrderIndex).IsEqualTo(1);
        await Assert.That(inOrder[2].OrderIndex).IsEqualTo(2);
    }
}
