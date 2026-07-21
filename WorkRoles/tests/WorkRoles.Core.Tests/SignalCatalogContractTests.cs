using WorkRoles.Core.Signals;

namespace WorkRoles.Core.Tests;

public class SignalCatalogContractTests
{
    [Test]
    public async Task DefaultCatalogueIsExactlyTheAuditedDefinitionUnion()
    {
        var expected = PassionSignalDefinitions.All
            .Concat(ExpertiseSignalDefinitions.All)
            .Concat(VanillaSignalDefinitions.All)
            .Select(definition => definition.IdentityForTest())
            .ToArray();
        var actual = SignalCatalog.Default.All
            .Select(definition => definition.IdentityForTest())
            .ToArray();

        await Assert.That(actual).IsEquivalentTo(expected);
    }

    [Test]
    public async Task EveryActiveRecordHasStructuredEffectsAndStaticRecordsAreNotMarkedTransient()
    {
        var all = SignalCatalog.Default.All;
        var alphaPassions = all.Where(x => x.Source.Kind == SignalSourceKind.Passion
            && x.Source.PackageId == "sarg.alphaskills").ToArray();

        await Assert.That(all.Where(x => x.Type == SignalType.Active).All(x => x.Effects.Count > 0)).IsTrue();
        await Assert.That(alphaPassions.Where(x => x.Type == SignalType.Active).All(x => !x.IsTransient)).IsTrue();
        await Assert.That(alphaPassions.Where(x => x.Type == SignalType.Passive).All(x => x.IsTransient)).IsTrue();
        await Assert.That(all.Where(x => x.Source.Kind != SignalSourceKind.Passion).All(x => !x.IsTransient)).IsTrue();
    }

    [Test]
    public async Task EverySourceCarriesCanonicalPackageIdsAndHumanReadableAttribution()
    {
        var all = SignalCatalog.Default.All;

        await Assert.That(all.All(x => !string.IsNullOrWhiteSpace(x.Source.PackageId))).IsTrue();
        await Assert.That(all.All(x => !string.IsNullOrWhiteSpace(x.FallbackUi.SourceDisplayName))).IsTrue();
        await Assert.That(all.SelectMany(x => x.Source.RequiredPackageIds)
            .All(x => !string.IsNullOrWhiteSpace(x) && x == x.Trim() && !x.Contains(' '))).IsTrue();

        AssertAttribution(SignalSourceKind.Passion, "Minor", "Ludeon.RimWorld", "RimWorld");
        AssertAttribution(SignalSourceKind.Passion, "VSE_Critical", "vanillaexpanded.skills", "Vanilla Skills Expanded");
        AssertAttribution(SignalSourceKind.Passion, "AS_DedicatedPassion", "sarg.alphaskills", "Alpha Skills");
        AssertAttribution(SignalSourceKind.Gene, "AptitudeStrong", "Ludeon.RimWorld.Biotech", "Biotech");
        AssertAttribution(SignalSourceKind.Trait, "PerfectMemory", "Ludeon.RimWorld.Anomaly", "Anomaly");
        AssertAttribution(SignalSourceKind.Expertise, "Precision", "vanillaexpanded.skills", "Vanilla Skills Expanded");
        AssertAttribution(SignalSourceKind.Expertise, "AS_Blasting", "sarg.alphaskills", "Alpha Skills");
    }

    private static void AssertAttribution(
        SignalSourceKind kind,
        string defName,
        string packageId,
        string sourceDisplayName)
    {
        var definition = SignalCatalog.Default.All.First(x =>
            x.Source.Kind == kind && x.Source.DefName == defName);
        if (definition.Source.PackageId != packageId
            || definition.FallbackUi.SourceDisplayName != sourceDisplayName)
        {
            throw new Exception($"{kind}/{defName}: expected {packageId} / {sourceDisplayName}, got "
                + $"{definition.Source.PackageId} / {definition.FallbackUi.SourceDisplayName}");
        }
    }
}
