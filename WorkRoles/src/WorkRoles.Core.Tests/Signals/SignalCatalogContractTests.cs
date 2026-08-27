using WorkRoles.Core.Signals;

namespace WorkRoles.Core.Tests.Signals;

public class SignalCatalogContractTests
{
    [Test]
    public async Task DefaultCatalogueIsExactlyTheAuditedDefinitionUnion()
    {
        var componentDefinitionIdentities = PassionSignalDefinitions
            .All.Concat(ExpertiseSignalDefinitions.All)
            .Concat(VanillaSignalDefinitions.All)
            .Select(definition => definition.IdentityForTest())
            .ToArray();
        var defaultDefinitionIdentities = SignalCatalog.Default.All.Select(definition => definition.IdentityForTest()).ToArray();

        await Assert.That(defaultDefinitionIdentities).IsEquivalentTo(componentDefinitionIdentities);
    }

    [Test]
    public async Task EveryActiveRecordHasStructuredEffects()
    {
        var all = SignalCatalog.Default.All;

        await Assert.That(all.Where(x => x.Type == SignalType.Active).All(x => x.Effects.Count > 0)).IsTrue();
    }

    [Test]
    public async Task ActiveAlphaSkillsPassionsAreStatic()
    {
        var alphaPassions = SignalCatalog.Default.All.Where(x => x.Source.Kind == SignalSourceKind.Passion && x.Source.PackageId == "sarg.alphaskills");

        await Assert.That(alphaPassions.Where(x => x.Type == SignalType.Active).All(x => !x.IsTransient)).IsTrue();
    }

    [Test]
    public async Task PassiveAlphaSkillsPassionsAreTransient()
    {
        var alphaPassions = SignalCatalog.Default.All.Where(x => x.Source.Kind == SignalSourceKind.Passion && x.Source.PackageId == "sarg.alphaskills");

        await Assert.That(alphaPassions.Where(x => x.Type == SignalType.Passive).All(x => x.IsTransient)).IsTrue();
    }

    [Test]
    public async Task NonPassionSignalsAreStatic()
    {
        var all = SignalCatalog.Default.All;

        await Assert.That(all.Where(x => x.Source.Kind != SignalSourceKind.Passion).All(x => !x.IsTransient)).IsTrue();
    }

    [Test]
    public async Task EverySourceCarriesACanonicalPackageId()
    {
        var all = SignalCatalog.Default.All;

        await Assert.That(all.All(x => !string.IsNullOrWhiteSpace(x.Source.PackageId))).IsTrue();
    }

    [Test]
    public async Task EverySourceCarriesHumanReadableAttribution()
    {
        var all = SignalCatalog.Default.All;

        await Assert.That(all.All(x => !string.IsNullOrWhiteSpace(x.FallbackUi.SourceDisplayName))).IsTrue();
    }

    [Test]
    public async Task RequiredPackageIdsAreCanonical()
    {
        var all = SignalCatalog.Default.All;

        await Assert.That(all.SelectMany(x => x.Source.RequiredPackageIds).All(x => !string.IsNullOrWhiteSpace(x) && x == x.Trim() && !x.Contains(' '))).IsTrue();
    }

    [Test]
    public async Task RepresentativeSourceAttributionMatchesItsPackage()
    {
        string actualAttribution = string.Join(
            "\n",
            [
                Attribution(SignalSourceKind.Passion, "Minor"),
                Attribution(SignalSourceKind.Passion, "VSE_Critical"),
                Attribution(SignalSourceKind.Passion, "AS_DedicatedPassion"),
                Attribution(SignalSourceKind.Gene, "AptitudeStrong"),
                Attribution(SignalSourceKind.Trait, "PerfectMemory"),
                Attribution(SignalSourceKind.Expertise, "Precision"),
                Attribution(SignalSourceKind.Expertise, "AS_Blasting"),
                Attribution(SignalSourceKind.Trait, "Brawler"),
                Attribution(SignalSourceKind.Hediff, "Inhumanized"),
            ]
        );
        const string expectedAttribution = """
Passion/Minor=Ludeon.RimWorld|RimWorld
Passion/VSE_Critical=vanillaexpanded.skills|Vanilla Skills Expanded
Passion/AS_DedicatedPassion=sarg.alphaskills|Alpha Skills
Gene/AptitudeStrong=Ludeon.RimWorld.Biotech|Biotech
Trait/PerfectMemory=Ludeon.RimWorld.Anomaly|Anomaly
Expertise/Precision=vanillaexpanded.skills|Vanilla Skills Expanded
Expertise/AS_Blasting=sarg.alphaskills|Alpha Skills
Trait/Brawler=Ludeon.RimWorld|RimWorld
Hediff/Inhumanized=Ludeon.RimWorld.Anomaly|Anomaly
""";

        await Assert.That(actualAttribution).IsEqualTo(expectedAttribution);
    }

    private static string Attribution(SignalSourceKind kind, string defName)
    {
        SignalDefinition definition = SignalCatalog.Default.All.First(candidate => candidate.Source.Kind == kind && candidate.Source.DefName == defName);
        return $"{kind}/{defName}={definition.Source.PackageId}|{definition.FallbackUi.SourceDisplayName}";
    }
}
