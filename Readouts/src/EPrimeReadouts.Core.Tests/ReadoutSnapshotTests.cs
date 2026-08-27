using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

public class ReadoutSnapshotTests
{
    [Test]
    public async Task CaptureDetachesNestedPoolAndGroupCollections()
    {
        var model = new ReadoutModel();
        var pool = model.CreatePool(1, "Meals");
        pool.Members.Add("MealSimple");
        var group = model.CreateGroup(2, "Food");
        group.Tiers.Add(new List<string> { "#1" });

        ReadoutSnapshot snapshot = ReadoutSnapshot.Capture(model.Pools, model.Groups);
        pool.Name = "Changed";
        pool.Members.Add("MealFine");
        group.Name = "Changed";
        group.Tiers[0].Add("Steel");

        await Assert.That(snapshot.Pools[0].Name).IsEqualTo("Meals");
        await Assert.That(snapshot.Pools[0].Members).IsEquivalentTo(new[] { "MealSimple" });
        await Assert.That(snapshot.Groups[0].Name).IsEqualTo("Food");
        await Assert.That(snapshot.Groups[0].Tiers[0]).IsEquivalentTo(new[] { "#1" });
    }

    [Test]
    public async Task PublishedCollectionsRejectConsumerMutation()
    {
        var model = new ReadoutModel();
        model.CreatePool(1, "Meals").Members.Add("MealSimple");
        model.CreateGroup(2, "Food").Tiers.Add(new List<string> { "#1" });
        ReadoutSnapshot snapshot = ReadoutSnapshot.Capture(model.Pools, model.Groups);

        await Assert.That(() => ((IList<string>)snapshot.Pools[0].Members)[0] = "Steel")
            .Throws<NotSupportedException>();
        await Assert.That(() => ((IList<string>)snapshot.Groups[0].Tiers[0])[0] = "Steel")
            .Throws<NotSupportedException>();
    }
}
