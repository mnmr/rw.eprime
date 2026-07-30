using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

public class RoleLocationValidityTests
{
    private static readonly HashSet<string> Live = new() { "4", "9" };

    [Test]
    public async Task RoleWithoutEntriesIsInvalidRegardlessOfLocations()
    {
        await Assert.That(RoleLocationValidity.IsInvalid(
            entryCount: 0, Array.Empty<string>(), Live)).IsTrue();
    }

    [Test]
    public async Task UnrestrictedRoleWithEntriesIsValid()
    {
        await Assert.That(RoleLocationValidity.IsInvalid(
            entryCount: 1, Array.Empty<string>(), Live)).IsFalse();
    }

    [Test]
    public async Task GenericSettlementOrCaravanRuleNeverBecomesStale()
    {
        await Assert.That(RoleLocationValidity.IsInvalid(
            1, new[] { LocationRules.Settlements }, new HashSet<string>())).IsFalse();
        await Assert.That(RoleLocationValidity.IsInvalid(
            1, new[] { LocationRules.Caravans }, new HashSet<string>())).IsFalse();
    }

    [Test]
    public async Task NamedRulesAreInvalidOnlyWhenEveryLocationIsGone()
    {
        await Assert.That(RoleLocationValidity.IsInvalid(
            1, new[] { "settlement:4", "ship:gone" }, Live)).IsFalse();
        await Assert.That(RoleLocationValidity.IsInvalid(
            1, new[] { "settlement:gone", "ship:missing" }, Live)).IsTrue();
    }

    [Test]
    public async Task UnknownTokensAreStaleWithoutARecognizedLiveId()
    {
        await Assert.That(RoleLocationValidity.IsInvalid(
            1, new[] { "garbage" }, Live)).IsTrue();
    }
}
