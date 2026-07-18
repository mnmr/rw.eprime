using WorkRoles.Core.Signals;

namespace WorkRoles.Core.Tests;

public class VanillaSignalCatalogTests
{
    [Test]
    public async Task GeneratedAptitudeIdentityRequiresTheExactTemplateAndSkillSuffix()
    {
        await Assert.That(VanillaSignalDefinitions.IsGeneratedAptitudeIdentity(
            "AptitudeStrong_Shooting", "AptitudeStrong", "Shooting")).IsTrue();
        await Assert.That(VanillaSignalDefinitions.IsGeneratedAptitudeIdentity(
            "AptitudeStrong_Melee", "AptitudeStrong", "Shooting")).IsFalse();
        await Assert.That(VanillaSignalDefinitions.IsGeneratedAptitudeIdentity(
            "AptitudeStrong_Shooting_Extra", "AptitudeStrong", "Shooting")).IsFalse();
        await Assert.That(VanillaSignalDefinitions.IsGeneratedAptitudeIdentity(
            "aptitudestrong_Shooting", "AptitudeStrong", "Shooting")).IsFalse();
    }

    [Test]
    public async Task AptitudeTemplatesArePassiveExactAndAlreadyReflected()
    {
        var expected = new Dictionary<string, float>
        {
            ["AptitudeTerrible"] = -8f,
            ["AptitudePoor"] = -4f,
            ["AptitudeStrong"] = 4f,
            ["AptitudeRemarkable"] = 8f,
        };
        var actual = VanillaSignalDefinitions.All.Where(x =>
            x.Source.Kind == SignalSourceKind.Gene && x.Source.DefName.StartsWith("Aptitude", StringComparison.Ordinal))
            .ToArray();

        await Assert.That(actual.Length).IsEqualTo(4);
        foreach (var definition in actual)
        {
            await Assert.That(definition.Type).IsEqualTo(SignalType.Passive);
            await Assert.That(definition.DerivesSkillFromSource).IsTrue();
            var effect = definition.Effects.Single(x => x.Kind == SignalEffectKind.SkillLevel);
            await Assert.That(effect.Magnitude).IsEqualTo(expected[definition.Source.DefName]);
            await Assert.That(effect.AlreadyReflected).IsTrue();
        }

        await Assert.That(One(SignalSourceKind.Gene, "AptitudeTerrible").Effects
            .Any(x => x.Kind == SignalEffectKind.Passion && x.Operation == SignalOperation.Disable)).IsTrue();
        await Assert.That(One(SignalSourceKind.Gene, "AptitudeRemarkable").Effects
            .Any(x => x.Kind == SignalEffectKind.Passion && x.Magnitude == 1f)).IsTrue();
    }

    [Test]
    public async Task AuditedGeneMembershipAndEffectsAreExact()
    {
        var genes = VanillaSignalDefinitions.All.Where(x =>
                x.Source.Kind == SignalSourceKind.Gene && !x.Source.DefName.StartsWith("Aptitude", StringComparison.Ordinal))
            .Select(x => x.Source.DefName).OrderBy(x => x).ToArray();
        await Assert.That(genes).IsEquivalentTo(new[]
        {
            "FireTerror", "Learning_Fast", "Learning_Slow", "MeleeDamage_Strong",
            "MeleeDamage_Weak", "Nearsighted", "ViolenceDisabled",
        });

        await Assert.That(Effect("MeleeDamage_Strong", SignalEffectKind.Damage).Magnitude).IsEqualTo(1.5f);
        await Assert.That(Effect("MeleeDamage_Weak", SignalEffectKind.Damage).Magnitude).IsEqualTo(0.5f);
        await Assert.That(One(SignalSourceKind.Gene, "Nearsighted").Effects.Count).IsEqualTo(2);
        await Assert.That(One(SignalSourceKind.Gene, "ViolenceDisabled").Effects
            .Any(x => x.Kind == SignalEffectKind.Capability && x.Operation == SignalOperation.Disable)).IsTrue();

        var fire = One(SignalSourceKind.Gene, "FireTerror");
        await Assert.That(fire.Effects.Any(x => x.Kind == SignalEffectKind.Mood && x.Magnitude == -10f)).IsTrue();
        await Assert.That(fire.Effects.Any(x => x.Kind == SignalEffectKind.MentalBreak
            && x.Magnitude == 0.1f && x.Unit == SignalValueUnit.Days)).IsTrue();
        await Assert.That(fire.Effects.Any(x => x.TargetDefName == "Pyromaniac")).IsTrue();
    }

    [Test]
    public async Task TraitKeysUseRealDefNamesAndDegrees()
    {
        var keys = VanillaSignalDefinitions.All.Where(x => x.Source.Kind == SignalSourceKind.Trait)
            .Select(x => $"{x.Source.DefName}/{x.Degree}/{x.Source.EffectDiscriminator}")
            .OrderBy(x => x).ToArray();
        await Assert.That(keys).IsEquivalentTo(new[]
        {
            "Brawler/0/melee", "Brawler/0/shooting", "FastLearner/0/", "Gourmand/0/",
            "GreatMemory/0/", "Immunity/-1/", "Industriousness/-1/", "Industriousness/-2/",
            "Industriousness/1/", "Industriousness/2/", "Neurotic/1/", "Neurotic/2/",
            "Nimble/0/", "Occultist/0/", "PerfectMemory/0/", "Pyromaniac/0/",
            "ShootingAccuracy/-1/", "ShootingAccuracy/1/", "SlowLearner/0/",
            "TooSmart/0/", "TorturedArtist/0/",
        });

        var brawler = VanillaSignalDefinitions.All.Where(x => x.Source.DefName == "Brawler").ToArray();
        await Assert.That(brawler.Select(x => x.SkillDefName)).IsEquivalentTo(new[] { "Melee", "Shooting" });
        await Assert.That(brawler.All(x => x.Type == SignalType.Active)).IsTrue();
        await Assert.That(brawler.SelectMany(x => x.Effects)
            .Where(x => x.Kind == SignalEffectKind.SkillLevel).All(x => x.AlreadyReflected)).IsTrue();

        await Assert.That(One(SignalSourceKind.Trait, "Gourmand", 0).Type).IsEqualTo(SignalType.Passive);
        await Assert.That(One(SignalSourceKind.Trait, "Immunity", -1).Type).IsEqualTo(SignalType.Passive);
        await Assert.That(Effect("Industriousness", SignalEffectKind.WorkSpeed, 2).Magnitude).IsEqualTo(0.35f);
        await Assert.That(Effect("Industriousness", SignalEffectKind.WorkSpeed, -2).Magnitude).IsEqualTo(-0.35f);
        await Assert.That(Effect("PerfectMemory", SignalEffectKind.SkillDecay, 0).Operation)
            .IsEqualTo(SignalOperation.Disable);
    }

    [Test]
    public async Task InhumanizedProducesThreePassiveAlreadyReflectedTargets()
    {
        var definitions = VanillaSignalDefinitions.All.Where(x =>
            x.Source.Kind == SignalSourceKind.Hediff && x.Source.DefName == "Inhumanized").ToArray();
        await Assert.That(definitions.Length).IsEqualTo(3);
        await Assert.That(definitions.Select(x => x.SkillDefName))
            .IsEquivalentTo(new[] { "Animals", "Social", "Artistic" });
        await Assert.That(definitions.All(x => x.Type == SignalType.Passive
            && x.Effects.Single().Magnitude == -12f
            && x.Effects.Single().AlreadyReflected)).IsTrue();
    }

    [Test]
    public async Task SourceAttributionDistinguishesCoreBiotechAndAnomaly()
    {
        await Assert.That(One(SignalSourceKind.Trait, "Brawler", 0).FallbackUi.SourceDisplayName)
            .IsEqualTo("RimWorld");
        await Assert.That(One(SignalSourceKind.Gene, "AptitudeStrong").FallbackUi.SourceDisplayName)
            .IsEqualTo("Biotech");
        await Assert.That(One(SignalSourceKind.Trait, "PerfectMemory", 0).FallbackUi.SourceDisplayName)
            .IsEqualTo("Anomaly");
        await Assert.That(One(SignalSourceKind.Hediff, "Inhumanized").FallbackUi.SourceDisplayName)
            .IsEqualTo("Anomaly");
    }

    private static SignalDefinition One(SignalSourceKind kind, string defName, int? degree = null) =>
        VanillaSignalDefinitions.All.First(x => x.Source.Kind == kind
            && x.Source.DefName == defName && x.Degree == degree);

    private static SignalEffect Effect(string defName, SignalEffectKind kind, int? degree = null) =>
        VanillaSignalDefinitions.All.First(x => x.Source.DefName == defName && x.Degree == degree)
            .Effects.First(x => x.Kind == kind);
}
