namespace WorkRoles.Core.Tests.Persistence;

public class LocationTokenMigrationTests
{
    [Test]
    public async Task LegacyShipTokensCollapseOntoTheSoleShipAndStaleLocationsAreRemoved()
    {
        HashSet<string> liveSettlementTokens = ["settlement:4"];
        var normalized = LocationTokenMigration.Normalize(
            ["ship:old-map-17", "settlement:4", "ship:old-map-29", "settlement:gone", LocationRules.Caravans, "garbage", "settlement:4"],
            stableShipToken: "ship:GravEngine42",
            liveSettlementTokens
        );

        await Assert.That(string.Join("|", normalized)).IsEqualTo("ship:GravEngine42|settlement:4|caravans");
    }

    [Test]
    public async Task CleanupKeepsRestrictedRolesDisabledWhenNothingSurvives()
    {
        HashSet<string> noLiveSettlementTokens = [];
        var normalized = LocationTokenMigration.Normalize(["settlement:gone", "garbage"], stableShipToken: null!, noLiveSettlementTokens);

        await Assert.That(string.Join("|", normalized)).IsEqualTo("nowhere");
    }

    [Test]
    public async Task CleanupKeepsUnrestrictedRolesUnrestricted()
    {
        HashSet<string> noLiveSettlementTokens = [];
        var normalized = LocationTokenMigration.Normalize([], stableShipToken: null!, noLiveSettlementTokens);

        await Assert.That(normalized).IsEmpty();
    }

    [Test]
    public async Task SelectingARealLocationRemovesTheMigrationFallback()
    {
        List<string> tokens = [LocationRules.Nowhere];

        bool changed = LocationTokenSelection.Toggle(tokens, LocationRules.Settlements);

        await Assert.That(changed).IsTrue();
        await Assert.That(string.Join("|", tokens)).IsEqualTo("settlements");
    }
}
