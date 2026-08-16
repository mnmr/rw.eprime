namespace WorkRoles.Core.Tests.Locations;

public class RoleLocationValidityTests
{
    private static readonly HashSet<string> Live = new() { "4", "9" };

    [Test]
    public async Task RoleWithoutEntriesIsInvalidRegardlessOfLocations()
    {
        await Assert.That(RoleLocationValidity.IsInvalid(entryCount: 0, [], Live)).IsTrue();
    }

    [Test]
    public async Task UnrestrictedRoleWithEntriesIsValid()
    {
        await Assert.That(RoleLocationValidity.IsInvalid(entryCount: 1, [], Live)).IsFalse();
    }

    [Test]
    [Arguments(LocationRules.Settlements)]
    [Arguments(LocationRules.Caravans)]
    public async Task GenericLocationRuleNeverBecomesStale(string locationRule)
    {
        HashSet<string> noLiveLocations = [];

        await Assert.That(RoleLocationValidity.IsInvalid(1, [locationRule], noLiveLocations)).IsFalse();
    }

    [Test]
    public async Task NamedRulesRemainValidWhenOneLocationIsLive()
    {
        await Assert.That(RoleLocationValidity.IsInvalid(1, ["settlement:4", "ship:gone"], Live)).IsFalse();
    }

    [Test]
    public async Task NamedRulesAreInvalidWhenEveryLocationIsGone()
    {
        await Assert.That(RoleLocationValidity.IsInvalid(1, ["settlement:gone", "ship:missing"], Live)).IsTrue();
    }

    [Test]
    public async Task UnknownTokensAreStaleWithoutARecognizedLiveId()
    {
        await Assert.That(RoleLocationValidity.IsInvalid(1, ["garbage"], Live)).IsTrue();
    }

    [Test]
    public async Task NowhereRestrictionIsIntentionalRatherThanStale()
    {
        HashSet<string> noLiveLocations = [];

        await Assert.That(RoleLocationValidity.IsInvalid(1, ["nowhere"], noLiveLocations)).IsFalse();

        await Assert.That(LocationRules.Matches(["nowhere"], new PawnPlace { LocationId = "4", IsSettlement = true })).IsFalse();
    }
}
