using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

public class LocationTokenMigrationTests
{
    [Test]
    public async Task LegacyShipTokensCollapseOntoTheSoleShipAndStaleLocationsAreRemoved()
    {
        var normalized = LocationTokenMigration.Normalize(
            new[]
            {
                "ship:old-map-17",
                "settlement:4",
                "ship:old-map-29",
                "settlement:gone",
                LocationRules.Caravans,
                "garbage",
                "settlement:4",
            },
            stableShipToken: "ship:GravEngine42",
            liveSettlementTokens: new HashSet<string> { "settlement:4" });

        await Assert.That(string.Join("|", normalized))
            .IsEqualTo("ship:GravEngine42|settlement:4|caravans");
    }

    [Test]
    public async Task CleanupKeepsRestrictedRolesDisabledWhenNothingSurvives()
    {
        var normalized = LocationTokenMigration.Normalize(
            new[] { "settlement:gone", "garbage" },
            stableShipToken: null,
            liveSettlementTokens: new HashSet<string>());
        var unrestricted = LocationTokenMigration.Normalize(
            Array.Empty<string>(),
            stableShipToken: null,
            liveSettlementTokens: new HashSet<string>());

        await Assert.That(string.Join("|", normalized)).IsEqualTo("nowhere");
        await Assert.That(unrestricted).IsEmpty();
    }

    [Test]
    public async Task SelectingARealLocationRemovesTheMigrationFallback()
    {
        var tokens = new List<string> { LocationRules.Nowhere };

        bool changed = LocationTokenSelection.Toggle(
            tokens, LocationRules.Settlements);

        await Assert.That(changed).IsTrue();
        await Assert.That(string.Join("|", tokens)).IsEqualTo("settlements");
    }
}
