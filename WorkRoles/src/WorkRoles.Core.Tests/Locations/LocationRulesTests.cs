using RimShared.Common;

namespace WorkRoles.Core.Tests.Locations;

public class LocationRulesTests
{
    private static readonly PawnPlace AtSettlement = new() { LocationId = "4", IsSettlement = true };
    private static readonly PawnPlace AtShip = new() { LocationId = "9", IsShip = true };
    private static readonly PawnPlace InCaravan = new() { LocationId = null! };

    [Test]
    public async Task PawnPlaceIsAValueTypeForHotRuleChecks()
    {
        await Assert.That(typeof(PawnPlace).IsValueType).IsTrue();
    }

    [Test]
    public async Task NullTokensMatchEverywhere()
    {
        await Assert.That(LocationRules.Matches(null!, InCaravan)).IsTrue();
    }

    [Test]
    public async Task EmptyTokensMatchEverywhere()
    {
        await Assert.That(LocationRules.Matches([], AtSettlement)).IsTrue();
    }

    [Test]
    [Arguments("4", true, false, true)]
    [Arguments("9", false, true, false)]
    [Arguments(null, false, false, false)]
    [Arguments("12", false, false, false)]
    public async Task SettlementsTokenMatchesSettlementOnly(string? locationId, bool isSettlement, bool isShip, bool expected)
    {
        var place = new PawnPlace
        {
            LocationId = locationId,
            IsSettlement = isSettlement,
            IsShip = isShip,
        };

        await Assert.That(LocationRules.Matches([LocationRules.Settlements], place)).IsEqualTo(expected);
    }

    [Test]
    [Arguments("4", true, false, false)]
    [Arguments("9", false, true, false)]
    [Arguments(null, false, false, true)]
    [Arguments("12", false, false, true)]
    public async Task CaravansTokenMatchesOnlyNotHomeLocations(string? locationId, bool isSettlement, bool isShip, bool expected)
    {
        var place = new PawnPlace
        {
            LocationId = locationId,
            IsSettlement = isSettlement,
            IsShip = isShip,
        };

        await Assert.That(LocationRules.Matches([LocationRules.Caravans], place)).IsEqualTo(expected);
    }

    [Test]
    [Arguments("settlement:4", "4", true, false, true)]
    [Arguments("settlement:5", "4", true, false, false)]
    [Arguments("ship:9", "9", false, true, true)]
    [Arguments("ship:9", "4", true, false, false)]
    [Arguments("settlement:gone", "4", true, false, false)]
    [Arguments("garbage", "4", true, false, false)]
    public async Task SpecificTokenMatchesOnlyItsLocationId(string token, string locationId, bool isSettlement, bool isShip, bool expected)
    {
        var place = new PawnPlace
        {
            LocationId = locationId,
            IsSettlement = isSettlement,
            IsShip = isShip,
        };

        await Assert.That(LocationRules.Matches([token], place)).IsEqualTo(expected);
    }

    [Test]
    public async Task SpecificTokenMatchingDoesNotAllocate()
    {
        string[] settlementToken = ["settlement:4"];
        string[] shipToken = ["ship:9"];

        // Warm the JIT and any framework-owned static state before measuring.
        LocationRules.Matches(settlementToken, AtSettlement);
        LocationRules.Matches(shipToken, AtShip);

        bool matched = true;
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10_000; i++)
        {
            matched &= LocationRules.Matches(settlementToken, AtSettlement);
            matched &= LocationRules.Matches(shipToken, AtShip);
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        await Assert.That(matched).IsTrue();
        await Assert.That(allocated).IsEqualTo(0L);
    }

    [Test]
    public async Task AnyMatchingTokenPasses()
    {
        await Assert.That(LocationRules.Matches(["settlement:5", LocationRules.Caravans], InCaravan)).IsTrue();
    }

    [Test]
    public async Task SeveralNonmatchingTokensFail()
    {
        await Assert.That(LocationRules.Matches(["settlement:5", LocationRules.Caravans], AtSettlement)).IsFalse();
    }
}
