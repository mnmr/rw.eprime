using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

public class SeededNameTests
{
    private static string French(string name) => name == "Meals" ? "Repas" : name == "Food" ? "Nourriture" : name;
    private static string German(string name) => name == "Meals" ? "Mahlzeiten" : name == "Food" ? "Nahrung" : name;

    [Test]
    public async Task SeededNamesTranslateWithoutChangingSavedNamesAndRenamesStayAuthoredAfterRoundTrip()
    {
        const string seed = "<Readouts><Pools><Pool Name='Meals' SeedName='Meals'/></Pools>"
            + "<Groups><Group Name='Food' SeedName='Food'><Tier><Slot>pool:Meals</Slot></Tier></Group></Groups></Readouts>";
        await Assert.That(ReadoutsXml.TryImport(seed, out var pools, out var groups, out _)).IsTrue();
        var model = new ReadoutModel();
        model.ApplyImport(pools!, groups!, () => 11, () => 22);
        string originalXml = ReadoutSnapshot.Capture(model.Pools, model.Groups).ToXml();
        var first = PoolSnapshot.Build(model.Pools, StaticResources.Catalog());
        await Assert.That(SeededName.Resolve(first.EntryAt(0).Name, first.EntryAt(0).SeedName, French)).IsEqualTo("Repas");
        await Assert.That(SeededName.Resolve(model.Groups[0].Name, model.Groups[0].SeedName, German)).IsEqualTo("Nahrung");
        await Assert.That(ReadoutSnapshot.Capture(model.Pools, model.Groups).ToXml()).IsEqualTo(originalXml);
        await Assert.That(model.RenamePool(11, " Meals ")).IsFalse();
        await Assert.That(model.RenameGroup(22, " Food ")).IsFalse();
        await Assert.That(SeededName.Resolve(model.Pools[0].Name, model.Pools[0].SeedName, French)).IsEqualTo("Repas");
        model.RenamePool(11, "My meals");
        model.RenameGroup(22, "My food");
        model.RenamePool(11, "Meals");
        model.RenameGroup(22, "Food");
        string authoredXml = ReadoutSnapshot.Capture(model.Pools, model.Groups).ToXml();
        await Assert.That(ReadoutsXml.TryImport(authoredXml, out pools, out groups, out _)).IsTrue();
        model.ApplyImport(pools!, groups!, () => 33, () => 44);
        var current = PoolSnapshot.Build(model.Pools, StaticResources.Catalog());
        var rows = PoolAssignmentTree.Build(current, true,
            new ItemTreeFilter("", ItemPickerType.Resources, ItemSourceIds.All), "Pools", German);
        await Assert.That(rows[1].Label).IsEqualTo("Meals");
        await Assert.That(SeededName.Resolve(model.Groups[0].Name, model.Groups[0].SeedName, French)).IsEqualTo("Food");
        // Previously published immutable snapshots retain their original provenance.
        await Assert.That(SeededName.Resolve(first.EntryAt(0).Name, first.EntryAt(0).SeedName, German)).IsEqualTo("Mahlzeiten");
    }
}
