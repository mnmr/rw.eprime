using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

public class ReadoutModelChangeDetectionTests
{
    [Test]
    public async Task RenamingGroupToCurrentNameReportsNoChange()
    {
        var model = new ReadoutModel();
        model.CreateGroup(1, "Food");

        await Assert.That(model.RenameGroup(1, "Food")).IsFalse();
    }

    [Test]
    public async Task ReapplyingGroupTiersReportsNoChange()
    {
        var model = new ReadoutModel();
        model.CreateGroup(1, "Food");
        var tiers = new List<List<string>> { new() { "Steel" } };
        model.SetTiers(1, tiers);

        await Assert.That(model.SetTiers(1, tiers)).IsFalse();
    }

    [Test]
    public async Task MovingGroupToItsCurrentPositionReportsNoChange()
    {
        var model = new ReadoutModel();
        model.CreateGroup(1, "Food");
        model.CreateGroup(2, "Medicine");

        await Assert.That(model.MoveGroupTo(1, 0)).IsFalse();
    }

    [Test]
    public async Task ReapplyingThresholdReportsNoChange()
    {
        var model = new ReadoutModel();
        model.SetThreshold("Steel", 100, 20);

        await Assert.That(model.SetThreshold("Steel", 100, 20)).IsFalse();
    }

    [Test]
    public async Task RenamingPoolToCurrentNameReportsNoChange()
    {
        var model = new ReadoutModel();
        model.CreatePool(1, "Metals");

        await Assert.That(model.RenamePool(1, "Metals")).IsFalse();
    }

    [Test]
    public async Task ReapplyingPoolMembersReportsNoChange()
    {
        var model = new ReadoutModel();
        model.CreatePool(1, "Metals");
        var members = new List<string> { "Steel", "Plasteel" };
        model.SetPoolMembers(1, members);

        await Assert.That(model.SetPoolMembers(1, members)).IsFalse();
    }

    [Test]
    public async Task ReapplyingPoolIconReportsNoChange()
    {
        var model = new ReadoutModel();
        model.CreatePool(1, "Metals");
        model.SetPoolIcon(1, "Steel");

        await Assert.That(model.SetPoolIcon(1, "Steel")).IsFalse();
    }

    [Test]
    public async Task ApplyingEmptyIconToPoolWithoutExplicitIconReportsNoChange()
    {
        var model = new ReadoutModel();
        model.CreatePool(1, "Metals");

        await Assert.That(model.SetPoolIcon(1, "")).IsFalse();
    }

    [Test]
    public async Task ApplyingNullIconToPoolWithEmptyIconReportsNoChange()
    {
        var model = new ReadoutModel();
        var pool = model.CreatePool(1, "Metals");
        pool.IconDefName = "";

        await Assert.That(model.SetPoolIcon(1, null)).IsFalse();
    }

    [Test]
    public async Task ClearingExplicitPoolIconCanonicalizesNoIconToNull()
    {
        var model = new ReadoutModel();
        model.CreatePool(1, "Metals");
        model.SetPoolIcon(1, "Steel");

        await Assert.That(model.SetPoolIcon(1, "")).IsTrue();
        await Assert.That(model.PoolById(1)!.IconDefName).IsNull();
    }

    [Test]
    public async Task DeletingUnusedPoolReportsOnlyPoolChange()
    {
        var model = new ReadoutModel();
        model.CreatePool(1, "Metals");

        bool deleted = model.DeletePool(1, out var change);

        await Assert.That(deleted).IsTrue();
        await Assert.That(change).IsEqualTo(ReadoutChange.Pools);
    }

    [Test]
    public async Task DeletingReferencedPoolReportsEveryChangedDomain()
    {
        var model = new ReadoutModel();
        model.CreatePool(1, "Metals");
        model.CreateGroup(1, "Materials");
        model.SetTiers(1, new List<List<string>> { new() { "#1" } });
        model.SetThreshold("#1", 100, 20);

        bool deleted = model.DeletePool(1, out var change);

        await Assert.That(deleted).IsTrue();
        await Assert.That(change).IsEqualTo(ReadoutChange.All);
    }
}
