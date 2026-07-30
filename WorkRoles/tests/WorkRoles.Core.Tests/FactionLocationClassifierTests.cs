using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

public class FactionLocationClassifierTests
{
    [Test]
    public async Task SettlementBelongsOnlyToItsOwningPlayerFaction()
    {
        var ownerView = FactionLocationClassifier.Classify(
            "17", ownedByFaction: true, spawnedViaGravship: false,
            parentCanBePlayerHome: true, parentIsSettlement: true,
            hasGravEngine: false);
        var otherView = FactionLocationClassifier.Classify(
            "17", ownedByFaction: false, spawnedViaGravship: false,
            parentCanBePlayerHome: true, parentIsSettlement: true,
            hasGravEngine: false);

        await Assert.That(ownerView.IsSettlement).IsTrue();
        await Assert.That(otherView.IsSettlement).IsFalse();
        await Assert.That(otherView.IsShip).IsFalse();
    }

    [Test]
    public async Task GravshipBelongsOnlyToItsOwningPlayerFaction()
    {
        var ownerView = FactionLocationClassifier.Classify(
            "23", ownedByFaction: true, spawnedViaGravship: true,
            parentCanBePlayerHome: true, parentIsSettlement: false,
            hasGravEngine: true);
        var otherView = FactionLocationClassifier.Classify(
            "23", ownedByFaction: false, spawnedViaGravship: true,
            parentCanBePlayerHome: true, parentIsSettlement: false,
            hasGravEngine: true);

        await Assert.That(ownerView.IsShip).IsTrue();
        await Assert.That(otherView.IsShip).IsFalse();
    }

    [Test]
    public async Task GravshipParkedAtSettlementClassifiesAsSettlement()
    {
        var place = FactionLocationClassifier.Classify(
            "29", ownedByFaction: true, spawnedViaGravship: true,
            parentCanBePlayerHome: true, parentIsSettlement: true,
            hasGravEngine: true);

        await Assert.That(place.IsSettlement).IsTrue();
        await Assert.That(place.IsShip).IsFalse();
    }
}
