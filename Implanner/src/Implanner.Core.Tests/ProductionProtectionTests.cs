using Implanner.Core;

namespace Implanner.Core.Tests;

public class ProductionProtectionTests
{
    // Ingredient acceptance is simulation state: a joining client must retain
    // the host's selected item IDs even before the next stock-sampling boundary.
    [Test]
    public async Task ProtectedIngredientsSurviveRestoreUntilExplicitlyReleased()
    {
        var host = new PlannerModel();
        var revisions = new PlannerRevisions();
        var selected = new HashSet<int> { 42, 73 };
        revisions.Bump(host.ReplaceProductionProtectedItems(selected));
        selected.Clear();
        await Assert.That(host.IsProductionIngredientProtected(42)).IsTrue();
        await Assert.That(host.IsProductionIngredientProtected(73)).IsTrue();
        await Assert.That(host.IsProductionIngredientProtected(99)).IsFalse();
        await Assert.That(revisions.Production).IsEqualTo(0);

        var restored = new PlannerModel();
        foreach (int id in host.ProductionProtectedItems)
            restored.AddLoadedProductionProtectedItem(id);
        await Assert.That(restored.IsProductionIngredientProtected(42)).IsTrue();
        await Assert.That(restored.IsProductionIngredientProtected(73)).IsTrue();

        var published = host.ProductionProtectedItems;
        int stamp = revisions.Version;
        revisions.Bump(host.ReplaceProductionProtectedItems(new[] { 73, 42, 42 }));
        await Assert.That(ReferenceEquals(published, host.ProductionProtectedItems)).IsTrue();
        await Assert.That(revisions.Version).IsEqualTo(stamp);

        var change = host.ReplaceProductionProtectedItems(new[] { 73 });
        await Assert.That(change).IsEqualTo(PlannerChange.ProductionProtection);
        revisions.Bump(change);
        await Assert.That(host.IsProductionIngredientProtected(42)).IsFalse();
        await Assert.That(host.IsProductionIngredientProtected(73)).IsTrue();
        await Assert.That(restored.IsProductionIngredientProtected(42)).IsTrue();
        await Assert.That(revisions.Production).IsEqualTo(0);
    }
}
