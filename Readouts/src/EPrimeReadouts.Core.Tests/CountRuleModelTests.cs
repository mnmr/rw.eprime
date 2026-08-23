using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

/// Authoritative count-rule state: set/clear normalization and no-op
/// detection, pool-deletion purge, load-time cleanup, and overwrite-import —
/// mirroring how thresholds behave for the same canonical-token keyspace.
public class CountRuleModelTests
{
    private static readonly CountRule StoredOnly =
        new CountRule(BasisOverride.ForceOn, BasisOverride.Inherit);

    [Test]
    public async Task SetStoresARuleAndReportsChange()
    {
        var model = new ReadoutModel();
        await Assert.That(model.SetCountRule("Steel", StoredOnly)).IsTrue();
        await Assert.That(model.CountRules["Steel"]).IsEqualTo(StoredOnly);
    }

    [Test]
    public async Task SettingTheSameRuleReportsNoChange()
    {
        var model = new ReadoutModel();
        model.SetCountRule("Steel", StoredOnly);
        await Assert.That(model.SetCountRule("Steel", StoredOnly)).IsFalse();
    }

    [Test]
    public async Task FullyInheritRuleRemovesTheEntry()
    {
        var model = new ReadoutModel();
        model.SetCountRule("Steel", StoredOnly);
        await Assert.That(model.SetCountRule("Steel", default)).IsTrue();
        await Assert.That(model.CountRules.ContainsKey("Steel")).IsFalse();
    }

    [Test]
    public async Task FullyInheritRuleOnAnAbsentTokenReportsNoChange()
    {
        var model = new ReadoutModel();
        await Assert.That(model.SetCountRule("Steel", default)).IsFalse();
    }

    [Test]
    public async Task DeletingAPoolPurgesItsRuleAndReportsTheDomain()
    {
        var model = new ReadoutModel();
        model.CreatePool(5, "Meats");
        model.SetCountRule("#5", StoredOnly);
        model.SetCountRule("Steel", StoredOnly);

        await Assert.That(model.DeletePool(5, out ReadoutChange change)).IsTrue();
        await Assert.That((change & ReadoutChange.CountRules) != 0).IsTrue();
        await Assert.That(model.CountRules.ContainsKey("#5")).IsFalse();
        await Assert.That(model.CountRules.ContainsKey("Steel")).IsTrue();
    }

    [Test]
    public async Task DeletingAPoolWithoutARuleDoesNotReportTheDomain()
    {
        var model = new ReadoutModel();
        model.CreatePool(5, "Meats");
        await Assert.That(model.DeletePool(5, out ReadoutChange change)).IsTrue();
        await Assert.That((change & ReadoutChange.CountRules) != 0).IsFalse();
    }

    [Test]
    public async Task CleanupDropsRulesWhoseTokenNoLongerResolves()
    {
        var model = new ReadoutModel();
        model.SetCountRule("Steel", StoredOnly);
        model.SetCountRule("RemovedModThing", StoredOnly);

        model.CleanupMissing(
            token => token == "Steel",
            member => true);

        await Assert.That(model.CountRules.ContainsKey("Steel")).IsTrue();
        await Assert.That(model.CountRules.ContainsKey("RemovedModThing")).IsFalse();
    }

    [Test]
    public async Task OverwriteImportClearsRulesAndReportsTheDomain()
    {
        var model = new ReadoutModel();
        model.SetCountRule("Steel", StoredOnly);

        int nextId = 1;
        ReadoutChange change = model.ApplyImport(
            new List<ResourcePool>(),
            new List<ReadoutGroup>(),
            () => nextId++,
            () => nextId++);

        await Assert.That((change & ReadoutChange.CountRules) != 0).IsTrue();
        await Assert.That(model.CountRules.Count).IsEqualTo(0);
    }

    [Test]
    public async Task CountRuleChangeBumpsOnlyItsRevisionDomain()
    {
        var revisions = new ReadoutRevisions();
        revisions.Bump(ReadoutChange.CountRules);

        await Assert.That(revisions.Version).IsEqualTo(1);
        await Assert.That(revisions.CountRules).IsEqualTo(1);
        await Assert.That(revisions.Groups).IsEqualTo(0);
        await Assert.That(revisions.Pools).IsEqualTo(0);
        await Assert.That(revisions.Thresholds).IsEqualTo(0);
    }

    [Test]
    public async Task ThresholdChangeDoesNotBumpTheCountRuleDomain()
    {
        var revisions = new ReadoutRevisions();
        revisions.Bump(ReadoutChange.Thresholds);
        await Assert.That(revisions.CountRules).IsEqualTo(0);
    }
}
