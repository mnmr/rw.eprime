using WorkRoles.Core.Signals;

namespace WorkRoles.Core.Tests;

public class SignalCatalogContractTests
{
    [Test]
    public async Task DefaultCatalogueHasTheApprovedCompleteShape()
    {
        var all = SignalCatalog.Default.All;

        await Assert.That(all.Count).IsEqualTo(157);
        await Assert.That(PassionSignalDefinitions.All.Count).IsEqualTo(56);
        await Assert.That(ExpertiseSignalDefinitions.All.Count).IsEqualTo(66);
        await Assert.That(VanillaSignalDefinitions.All.Count).IsEqualTo(35);

        await Assert.That(all.Count(x => x.Source.Kind == SignalSourceKind.Passion
            && x.Source.PackageId == "sarg.alphaskills" && x.Type == SignalType.Active)).IsEqualTo(8);
        await Assert.That(all.Count(x => x.Source.Kind == SignalSourceKind.Passion
            && x.Source.PackageId == "sarg.alphaskills" && x.Type == SignalType.Passive)).IsEqualTo(43);
        await Assert.That(all.Count(x => x.Source.Kind == SignalSourceKind.Expertise
            && x.Source.PackageId == "vanillaexpanded.skills")).IsEqualTo(47);
        await Assert.That(all.Count(x => x.Source.Kind == SignalSourceKind.Expertise
            && x.Source.PackageId == "sarg.alphaskills")).IsEqualTo(19);
        await Assert.That(all.Count(x => x.Source.Kind == SignalSourceKind.Gene
            && x.Source.DefName.StartsWith("Aptitude", StringComparison.Ordinal))).IsEqualTo(4);
        await Assert.That(all.Count(x => x.Source.Kind == SignalSourceKind.Hediff
            && x.Source.DefName == "Inhumanized")).IsEqualTo(3);
    }

    [Test]
    public async Task EveryRecordHasAUniqueStableSourceIdentity()
    {
        var identities = SignalCatalog.Default.All.Select(x => string.Join("\u001f", new[]
        {
            x.Source.Kind.ToString(),
            x.Source.PackageId,
            x.Source.DefName,
            x.Degree?.ToString() ?? "",
            x.Source.EffectDiscriminator ?? "",
        })).ToArray();

        await Assert.That(identities.Distinct(StringComparer.Ordinal).Count()).IsEqualTo(identities.Length);
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
