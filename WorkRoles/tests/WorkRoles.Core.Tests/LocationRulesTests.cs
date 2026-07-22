using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

public class LocationRulesTests
{
    private static readonly PawnPlace AtSettlement = new() { LocationId = "4", IsSettlement = true };
    private static readonly PawnPlace AtShip = new() { LocationId = "9", IsShip = true };
    private static readonly PawnPlace InCaravan = new() { LocationId = null };
    private static readonly PawnPlace OnRaidMap = new() { LocationId = "12" };

    [Test]
    public async Task PawnPlaceIsAValueTypeForHotRuleChecks()
    {
        await Assert.That(typeof(PawnPlace).IsValueType).IsTrue();
    }

    [Test]
    public async Task NoTokensMatchesEverywhere()
    {
        await Assert.That(LocationRules.Matches(null, InCaravan)).IsTrue();
        await Assert.That(LocationRules.Matches(new string[0], AtSettlement)).IsTrue();
    }

    [Test]
    public async Task SettlementsMatchesAnySettlementOnly()
    {
        var tokens = new[] { LocationRules.Settlements };
        await Assert.That(LocationRules.Matches(tokens, AtSettlement)).IsTrue();
        await Assert.That(LocationRules.Matches(tokens, AtShip)).IsFalse();
        await Assert.That(LocationRules.Matches(tokens, InCaravan)).IsFalse();
        await Assert.That(LocationRules.Matches(tokens, OnRaidMap)).IsFalse();
    }

    [Test]
    public async Task CaravansIsTheNotHomeBucket()
    {
        var tokens = new[] { LocationRules.Caravans };
        await Assert.That(LocationRules.Matches(tokens, InCaravan)).IsTrue();
        await Assert.That(LocationRules.Matches(tokens, OnRaidMap)).IsTrue();
        await Assert.That(LocationRules.Matches(tokens, AtSettlement)).IsFalse();
        await Assert.That(LocationRules.Matches(tokens, AtShip)).IsFalse();
    }

    [Test]
    public async Task SpecificTokensMatchById_AndStaleIdsNeverMatch()
    {
        await Assert.That(LocationRules.Matches(new[] { "settlement:4" }, AtSettlement)).IsTrue();
        await Assert.That(LocationRules.Matches(new[] { "settlement:5" }, AtSettlement)).IsFalse();
        await Assert.That(LocationRules.Matches(new[] { "ship:9" }, AtShip)).IsTrue();
        await Assert.That(LocationRules.Matches(new[] { "ship:9" }, AtSettlement)).IsFalse();
        await Assert.That(LocationRules.Matches(new[] { "settlement:gone" }, AtSettlement)).IsFalse();
        await Assert.That(LocationRules.Matches(new[] { "garbage" }, AtSettlement)).IsFalse();
    }

    [Test]
    public async Task SpecificTokenMatchingDoesNotAllocate()
    {
        var settlementToken = new[] { "settlement:4" };
        var shipToken = new[] { "ship:9" };

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
    public async Task AnyTokenMatchingPasses()
    {
        var tokens = new[] { "settlement:5", LocationRules.Caravans };
        await Assert.That(LocationRules.Matches(tokens, InCaravan)).IsTrue();
        await Assert.That(LocationRules.Matches(tokens, AtSettlement)).IsFalse();
    }
}
