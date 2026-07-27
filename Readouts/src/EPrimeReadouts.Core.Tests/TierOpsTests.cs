using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

public class TierOpsTests
{
    private static List<List<string>> Tiers(params string[][] tiers) =>
        tiers.Select(t => t.ToList()).ToList();

    [Test]
    public async Task AddAppendsToExistingTier()
    {
        var tiers = Tiers(new[] { "Steel" });
        await Assert.That(TierOps.Add(tiers, "WoodLog", 0, -1)).IsTrue();
        await Assert.That(string.Join(",", tiers[0])).IsEqualTo("Steel,WoodLog");
    }

    [Test]
    public async Task AddAtSlotInserts()
    {
        var tiers = Tiers(new[] { "Steel", "WoodLog" });
        await Assert.That(TierOps.Add(tiers, "Silver", 0, 1)).IsTrue();
        await Assert.That(string.Join(",", tiers[0])).IsEqualTo("Steel,Silver,WoodLog");
    }

    [Test]
    public async Task AddCreatesNewTierAtTierCount()
    {
        var tiers = Tiers(new[] { "Steel" });
        await Assert.That(TierOps.Add(tiers, "Silver", 1, 0)).IsTrue();
        await Assert.That(tiers.Count).IsEqualTo(2);
        await Assert.That(string.Join(",", tiers[1])).IsEqualTo("Silver");
    }

    [Test]
    public async Task AddRefusesFourthTier()
    {
        var tiers = Tiers(new[] { "A" }, new[] { "B" }, new[] { "C" });
        await Assert.That(TierOps.Add(tiers, "D", 3, 0)).IsFalse();
        await Assert.That(tiers.Count).IsEqualTo(3);
    }

    [Test]
    public async Task AddDedupesAcrossTiers()
    {
        var tiers = Tiers(new[] { "Steel" }, new[] { "Silver" });
        await Assert.That(TierOps.Add(tiers, "Steel", 1, 0)).IsFalse();
        await Assert.That(string.Join(",", tiers[1])).IsEqualTo("Silver");
    }

    [Test]
    public async Task RemoveCompactsEmptiedTier()
    {
        var tiers = Tiers(new[] { "Steel" }, new[] { "Silver" });
        await Assert.That(TierOps.Remove(tiers, "Steel")).IsTrue();
        await Assert.That(tiers.Count).IsEqualTo(1);
        await Assert.That(string.Join(",", tiers[0])).IsEqualTo("Silver");
    }

    [Test]
    public async Task MoveWithinTierReorders()
    {
        var tiers = Tiers(new[] { "A", "B", "C" });
        await Assert.That(TierOps.Move(tiers, 0, 0, 0, 2)).IsTrue();
        await Assert.That(string.Join(",", tiers[0])).IsEqualTo("B,A,C");
    }

    [Test]
    public async Task MoveWithinTierToEndAppends()
    {
        var tiers = Tiers(new[] { "A", "B", "C" });
        await Assert.That(TierOps.Move(tiers, 0, 0, 0, 3)).IsTrue();
        await Assert.That(string.Join(",", tiers[0])).IsEqualTo("B,C,A");
    }

    [Test]
    public async Task MoveBetweenTiersCompactsEmptiedSource()
    {
        var tiers = Tiers(new[] { "A" }, new[] { "B" });
        await Assert.That(TierOps.Move(tiers, 0, 0, 1, 1)).IsTrue();
        await Assert.That(tiers.Count).IsEqualTo(1);
        await Assert.That(string.Join(",", tiers[0])).IsEqualTo("B,A");
    }

    [Test]
    public async Task MoveToNewTierCreatesIt()
    {
        var tiers = Tiers(new[] { "A", "B" });
        await Assert.That(TierOps.Move(tiers, 0, 0, 1, 0)).IsTrue();
        await Assert.That(tiers.Count).IsEqualTo(2);
        await Assert.That(string.Join(",", tiers[0])).IsEqualTo("B");
        await Assert.That(string.Join(",", tiers[1])).IsEqualTo("A");
    }

    [Test]
    public async Task MoveToNewTierRefusedAtMaxTiers()
    {
        var tiers = Tiers(new[] { "A", "X" }, new[] { "B" }, new[] { "C" });
        await Assert.That(TierOps.Move(tiers, 0, 0, 3, 0)).IsFalse();
    }

    [Test]
    public async Task CleanupPurgesMissingAndCompacts()
    {
        var tiers = Tiers(new[] { "Gone", "Steel" }, new[] { "AlsoGone" });
        TierOps.Cleanup(tiers, d => d == "Steel");
        await Assert.That(tiers.Count).IsEqualTo(1);
        await Assert.That(string.Join(",", tiers[0])).IsEqualTo("Steel");
    }

    [Test]
    public async Task CloneIsDeep()
    {
        var tiers = Tiers(new[] { "Steel" });
        var copy = TierOps.Clone(tiers);
        copy[0].Add("WoodLog");
        await Assert.That(tiers[0].Count).IsEqualTo(1);
    }

    [Test]
    public async Task AddRefusesCanonicalDuplicate()
    {
        // "~Steel" refused when "Steel" is already present
        var tiers1 = Tiers(new[] { "Steel" });
        await Assert.That(TierOps.Add(tiers1, "~Steel", 0, -1)).IsFalse();
        await Assert.That(tiers1[0].Count).IsEqualTo(1);

        // "Steel" refused when "~Steel" is already present
        var tiers2 = Tiers(new[] { "~Steel" });
        await Assert.That(TierOps.Add(tiers2, "Steel", 0, -1)).IsFalse();
        await Assert.That(tiers2[0].Count).IsEqualTo(1);
    }

    [Test]
    public async Task RemoveMatchesCanonically()
    {
        // Remove("Steel") should remove the stored "~Steel" token
        var tiers = Tiers(new[] { "~Steel" });
        await Assert.That(TierOps.Remove(tiers, "Steel")).IsTrue();
        await Assert.That(tiers.Count).IsEqualTo(0);
    }
}
