namespace WorkRoles.Core.Tests.Locations;

public class FactionLocationClassifierTests
{
    [Test]
    public async Task PlayerOwnedSettlementIsClassifiedAsASettlement()
    {
        var location = FactionLocationClassifier.Classify("17", ownedByFaction: true, spawnedViaGravship: false, parentCanBePlayerHome: true, parentIsSettlement: true, hasGravEngine: false);

        await Assert.That(location.IsSettlement).IsTrue();
        await Assert.That(location.IsShip).IsFalse();
    }

    [Test]
    public async Task ForeignSettlementIsNotClassifiedAsAPlayerLocation()
    {
        var location = FactionLocationClassifier.Classify("17", ownedByFaction: false, spawnedViaGravship: false, parentCanBePlayerHome: true, parentIsSettlement: true, hasGravEngine: false);

        await Assert.That(location.IsSettlement).IsFalse();
        await Assert.That(location.IsShip).IsFalse();
    }

    [Test]
    public async Task PlayerOwnedGravshipUsesTheEngineIdentity()
    {
        var location = FactionLocationClassifier.Classify(
            mapLocationId: "23",
            shipLocationId: "GravEngine42",
            ownedByFaction: true,
            spawnedViaGravship: true,
            parentCanBePlayerHome: true,
            parentIsSettlement: false,
            hasGravEngine: true
        );

        await Assert.That(location.IsShip).IsTrue();
        await Assert.That(location.LocationId).IsEqualTo("GravEngine42");
    }

    [Test]
    public async Task ForeignGravshipIsNotClassifiedAsAPlayerShip()
    {
        var location = FactionLocationClassifier.Classify(
            mapLocationId: "23",
            shipLocationId: "GravEngine42",
            ownedByFaction: false,
            spawnedViaGravship: true,
            parentCanBePlayerHome: true,
            parentIsSettlement: false,
            hasGravEngine: true
        );

        await Assert.That(location.IsShip).IsFalse();
    }

    [Test]
    public async Task GravshipParkedAtSettlementClassifiesAsSettlement()
    {
        var place = FactionLocationClassifier.Classify(
            mapLocationId: "29",
            shipLocationId: "GravEngine42",
            ownedByFaction: true,
            spawnedViaGravship: true,
            parentCanBePlayerHome: true,
            parentIsSettlement: true,
            hasGravEngine: true
        );

        await Assert.That(place.IsSettlement).IsTrue();
        await Assert.That(place.IsShip).IsFalse();
        await Assert.That(place.LocationId).IsEqualTo("29");
    }
}
