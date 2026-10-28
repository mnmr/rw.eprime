using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

public class SetupDocumentTests
{
    private static Func<int> Ids(int first) { int next = first; return () => next++; }

    [Test]
    public async Task DormantEntriesFollowRenamedObjectsWhenTheirOldNamesAreReused()
    {
        const string xml = "<Readouts><Pools><Pool Name='Metals'><Member>Steel</Member><Member MayRequire='mod'>ModMetal</Member></Pool></Pools><Groups><Group Name='Industry'><Tier><Slot>pool:Metals</Slot><Slot MayRequire='mod'>ModMetal</Slot></Tier></Group></Groups></Readouts>";
        SetupDocument.TryRead(xml, out var full, out _);
        SetupDocument.TryRead(xml, out var available, out _, _ => false, s => s != "ModMetal");
        var model = new ReadoutModel();
        available!.TryApply(model, SetupSections.Model, Ids(1), Ids(10), out _, out _);
        var baseline = SetupDocument.Capture(model);
        model.RenamePool(1, "Materials");
        model.RenameGroup(10, "Production");
        model.CreatePool(2, "Metals").Members.Add("WoodLog");
        model.CreateGroup(11, "Industry");
        model.SetTiers(11, new() { new() { "WoodLog" } });
        var edited = SetupDocument.Capture(model);
        edited.RetainUnavailableFrom(full!, baseline);
        await Assert.That(edited.Pools.Single(p => p.Name == "Materials").Members.Contains("ModMetal")).IsTrue();
        await Assert.That(edited.Pools.Single(p => p.Name == "Metals").Members.Contains("ModMetal")).IsFalse();
        await Assert.That(TierOps.Contains(edited.Groups.Single(g => g.Name == "Production").Tiers, "ModMetal")).IsTrue();
        await Assert.That(TierOps.Contains(edited.Groups.Single(g => g.Name == "Industry").Tiers, "ModMetal")).IsFalse();
        model.DeletePool(1, out _);
        model.Groups.RemoveAll(g => g.Id == 10);
        var deleted = SetupDocument.Capture(model);
        deleted.RetainUnavailableFrom(full!, baseline);
        await Assert.That(deleted.ToXml().Contains("ModMetal")).IsFalse();
    }

    [Test]
    public async Task RenamingAnAvailablePoolPreservesItsUnavailableGroupReferences()
    {
        const string xml = "<Readouts><Pools><Pool Name='Metals'><Member>Steel</Member><Member MayRequire='mod'>ModMetal</Member></Pool></Pools><Groups><Group Name='Hidden' MayRequire='mod'><Tier><Slot>pool:Metals</Slot><Slot MayRequire='mod'>ModMetal</Slot></Tier></Group></Groups></Readouts>";
        SetupDocument.TryRead(xml, out var full, out _);
        SetupDocument.TryRead(xml, out var available, out _, _ => false, s => s != "ModMetal");
        var model = new ReadoutModel();
        available!.TryApply(model, SetupSections.Model, Ids(1), Ids(10), out _, out _);
        var baseline = SetupDocument.Capture(model);
        model.RenamePool(1, "Materials");
        var edited = SetupDocument.Capture(model);
        edited.RetainUnavailableFrom(full!, baseline);
        await Assert.That(edited.Groups[0].Tiers[0][0]).IsEqualTo("pool:Materials");
        SetupDocument.TryRead(edited.ToXml(), out var restored, out _);
        await Assert.That(restored!.Pools[0].Members.Contains("ModMetal")).IsTrue();
    }

    [Test]
    public async Task PoolAliasesCannotSupplyTwoRulesForTheSameResource()
    {
        const string xml = "<Readouts><Pools><Pool Name='Metals'/></Pools><Thresholds><Threshold Token='pool:Metals' Low='3' Critical='1'/><Threshold Token='pool:metals' Low='4' Critical='2'/></Thresholds></Readouts>";
        await Assert.That(SetupDocument.TryRead(xml, out var setup, out _)).IsFalse();
        await Assert.That(setup).IsNull();
    }

    [Test]
    public async Task UnrelatedEditsPreserveDormantTierAndGroupPositions()
    {
        const string xml = "<Readouts><Groups><Group Name='Mod group' MayRequire='mod'><Tier><Slot MayRequire='mod'>ModMetal</Slot></Tier></Group><Group Name='Mixed'><Tier><Slot MayRequire='mod'>ModMetal</Slot></Tier><Tier><Slot>Steel</Slot></Tier></Group></Groups></Readouts>";
        SetupDocument.TryRead(xml, out var full, out _);
        SetupDocument.TryRead(xml, out var available, out _, _ => false, s => s != "ModMetal");
        var model = new ReadoutModel();
        available!.TryApply(model, SetupSections.Model, Ids(1), Ids(10), out _, out _);
        var baseline = SetupDocument.Capture(model);
        model.SetThreshold("Steel", 100, 30);
        var edited = SetupDocument.Capture(model);
        edited.RetainUnavailableFrom(full!, baseline);
        SetupDocument.TryRead(edited.ToXml(), out var restored, out _, _ => true, _ => true);
        await Assert.That(string.Join(",", restored!.Groups.Select(g => g.Name))).IsEqualTo("Mod group,Mixed");
        await Assert.That(restored.Groups[1].Tiers.Count).IsEqualTo(2);
        await Assert.That(restored.Groups[1].Tiers[0][0]).IsEqualTo("ModMetal");
        await Assert.That(restored.Groups[1].Tiers[1][0]).IsEqualTo("Steel");
    }

    [Test]
    public async Task LegacyDuplicateGroupNamesRemainImportableAndCanBeExportedAgain()
    {
        const string legacy = "<Readouts><Groups><Group Name='Food'><Tier><Slot>Steel</Slot></Tier></Group><Group Name='food'><Tier><Slot>WoodLog</Slot></Tier></Group><Group Name=' '/></Groups></Readouts>";
        var model = new ReadoutModel();
        SetupDocument.TryRead(legacy, out var setup, out _);
        setup!.TryApply(model, SetupSections.Model, Ids(1), Ids(10), out _, out _);
        await Assert.That(model.CanUseGroupName(" ")).IsFalse();
        await Assert.That(model.Groups.Select(g => g.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count()).IsEqualTo(3);
        await Assert.That(model.Groups.All(g => !string.IsNullOrWhiteSpace(g.Name))).IsTrue();
        var again = SetupDocument.Capture(model);
        await Assert.That(again.TryApply(model, SetupSections.Model, Ids(30), Ids(40), out var change, out _)).IsTrue();
        await Assert.That(change).IsEqualTo(ReadoutChange.None);
    }

    [Test]
    public async Task LegacyOptionsAreAbsentAndMalformedNewOptionsCannotPartiallyImport()
    {
        SetupDocument.TryRead("<Readouts><Groups/><Pools/></Readouts>", out var legacy, out _);
        await Assert.That(legacy!.Present & (SetupSections.Thresholds | SetupSections.CountRules | SetupSections.PlayerSettings)).IsEqualTo(SetupSections.None);
        const string bad = "<Readouts><Groups/><Pools/><PlayerSettings><Option Name='reserveForBills' Value='oops'/></PlayerSettings></Readouts>";
        await Assert.That(SetupDocument.TryRead(bad, out var rejected, out _)).IsFalse();
        await Assert.That(rejected).IsNull();
    }

    [Test]
    public async Task DormantModEntriesSurviveEditsToTheirAvailablePool()
    {
        const string original = "<Readouts><Pools><Pool Name='Metals' Icon='ModMetal'><Member>Steel</Member><Member MayRequire='mod'>ModMetal</Member></Pool></Pools><Groups><Group Name='Industry'><Tier><Slot>pool:Metals</Slot><Slot MayRequire='mod'>ModMetal</Slot></Tier></Group></Groups><Thresholds><Threshold Token='ModMetal' Low='50' Critical='10' MayRequire='mod'/></Thresholds></Readouts>";
        SetupDocument.TryRead(original, out var full, out _);
        SetupDocument.TryRead(original, out var available, out _, _ => false, s => s != "ModMetal");
        var model = new ReadoutModel();
        available!.TryApply(model, SetupSections.Model, Ids(1), Ids(10), out _, out _);
        var before = SetupDocument.Capture(model);
        model.Pools[0].Members.Add("Plasteel");
        var edited = SetupDocument.Capture(model);
        edited.RetainUnavailableFrom(full!, before);
        string retained = edited.ToXml(s => s == "ModMetal" ? "mod" : null);
        SetupDocument.TryRead(retained, out var restored, out _, _ => true, _ => true);
        await Assert.That(string.Join(",", restored!.Pools[0].Members)).IsEqualTo("Steel,Plasteel,ModMetal");
        await Assert.That(restored.Pools[0].IconDefName).IsEqualTo("ModMetal");
        await Assert.That(string.Join(",", restored.Groups[0].Tiers[0])).IsEqualTo("pool:Metals,ModMetal");
        await Assert.That(restored.Thresholds["ModMetal"].Low).IsEqualTo(50);
        // A deliberate deletion of a visible pool must not resurrect it or its dormant members.
        model.DeletePool(1, out _);
        var deleted = SetupDocument.Capture(model);
        deleted.RetainUnavailableFrom(full!, before);
        await Assert.That(deleted.Pools.Count).IsEqualTo(0);
    }

    [Test]
    public async Task CompleteSetupSurvivesTransferIntoADifferentSave()
    {
        var source = new ReadoutModel();
        source.CreatePool(41, "Metals").Members.AddRange(new[] { "Steel", "ModMetal" });
        source.CreateGroup(72, "Industry");
        source.SetTiers(72, new() { new() { "~#41", "WoodLog" } });
        source.SetThreshold("#41", 300, 80);
        source.SetThreshold("WoodLog", 100, 20);
        source.SetCountRule("#41", new(BasisOverride.ForceOff, BasisOverride.ForceOn));
        var player = new PlayerSetup { ProfileId = "local-profile" };
        player.Options["panelWidth"] = "180";
        player.Options["reserveForBills"] = "True";
        player.EnabledGroups["Industry"] = false;
        string xml = SetupDocument.Capture(source, player).ToXml();
        await Assert.That(SetupDocument.TryRead(xml, out var setup, out _)).IsTrue();
        var target = new ReadoutModel();
        await Assert.That(setup!.TryApply(target, SetupSections.All, Ids(10), Ids(20), out _, out _)).IsTrue();
        await Assert.That(target.Thresholds.TryGetValue("#10", out var threshold)).IsTrue();
        await Assert.That(threshold.Low).IsEqualTo(300);
        await Assert.That(threshold.Critical).IsEqualTo(80);
        await Assert.That(target.Thresholds["WoodLog"].Low).IsEqualTo(100);
        await Assert.That(target.CountRules["#10"].StorageOnly).IsEqualTo(BasisOverride.ForceOff);
        await Assert.That(string.Join(",", target.Groups[0].Tiers[0])).IsEqualTo("~#10,WoodLog");
        await Assert.That(setup.Player!.Options["panelWidth"]).IsEqualTo("180");
        await Assert.That(setup.Player.EnabledGroups["Industry"]).IsFalse();
    }

    [Test]
    public async Task PartialAndLegacyImportsPreserveUnselectedConfigurationAndRejectBrokenReferences()
    {
        var target = new ReadoutModel();
        target.CreatePool(7, "Metals").Members.Add("Steel");
        target.CreateGroup(8, "Industry");
        target.SetTiers(8, new() { new() { "#7" } });
        target.SetThreshold("#7", 400, 100);
        target.SetCountRule("Steel", new(BasisOverride.ForceOn, BasisOverride.Inherit));
        const string legacy = "<Readouts><Pools/><Groups><Group Name='Other'><Tier><Slot>WoodLog</Slot></Tier></Group></Groups></Readouts>";
        SetupDocument.TryRead(legacy, out var setup, out _);
        await Assert.That(setup!.TryApply(target, SetupSections.Groups, Ids(30), Ids(40), out var change, out _)).IsTrue();
        await Assert.That(target.Pools.Count).IsEqualTo(1);
        await Assert.That(target.Thresholds["#7"].Low).IsEqualTo(400);
        await Assert.That(target.CountRules["Steel"].StorageOnly).IsEqualTo(BasisOverride.ForceOn);
        await Assert.That(change).IsEqualTo(ReadoutChange.Groups);
        // Removing Metals would strand the retained threshold. Reject the whole operation.
        await Assert.That(setup.TryApply(target, SetupSections.Pools, Ids(50), Ids(60), out change, out _)).IsFalse();
        await Assert.That(target.PoolById(7)!.Name).IsEqualTo("Metals");
        await Assert.That(change).IsEqualTo(ReadoutChange.None);
    }

    [Test]
    public async Task ReimportingAnIdenticalSetupPreservesIdsReferencesAndRevisions()
    {
        var target = new ReadoutModel();
        var pool = target.CreatePool(7, "Metals"); pool.Members.Add("Steel");
        var group = target.CreateGroup(8, "Industry");
        target.SetTiers(8, new() { new() { "#7" } });
        var setup = SetupDocument.Capture(target);
        int allocations = 0;
        await Assert.That(setup.TryApply(target, SetupSections.Model,
            () => { allocations++; return 30; }, () => { allocations++; return 40; }, out var change, out _)).IsTrue();
        await Assert.That(change).IsEqualTo(ReadoutChange.None);
        await Assert.That(allocations).IsEqualTo(0);
        await Assert.That(ReferenceEquals(target.PoolById(7), pool)).IsTrue();
        await Assert.That(ReferenceEquals(target.GroupById(8), group)).IsTrue();
    }
}
