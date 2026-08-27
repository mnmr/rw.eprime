using WorkRoles.Core.Signals;

namespace WorkRoles.Core.Tests.Signals;

public class VanillaSignalCatalogTests
{
    [Test]
    [Arguments("AptitudeStrong_Shooting", "AptitudeStrong", "Shooting", true)]
    [Arguments("AptitudeStrong_Melee", "AptitudeStrong", "Shooting", false)]
    [Arguments("AptitudeStrong_Shooting_Extra", "AptitudeStrong", "Shooting", false)]
    [Arguments("aptitudestrong_Shooting", "AptitudeStrong", "Shooting", false)]
    public async Task GeneratedAptitudeIdentityRequiresTheExactTemplateAndSkillSuffix(string generatedDefName, string templateDefName, string skillDefName, bool expected)
    {
        bool result = VanillaSignalDefinitions.IsGeneratedAptitudeIdentity(generatedDefName, templateDefName, skillDefName);

        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task AptitudeCatalogContainsExactlyFourActivePassivePairs()
    {
        SignalDefinition[] definitions = VanillaSignalDefinitions
            .All.Where(definition => definition.Source.Kind == SignalSourceKind.Gene && definition.Source.DefName.StartsWith("Aptitude", StringComparison.Ordinal))
            .ToArray();
        HashSet<string> templates = [.. definitions.Select(definition => definition.Source.DefName)];

        await Assert.That(definitions.Length).IsEqualTo(8);
        await Assert.That(templates.SetEquals(["AptitudeTerrible", "AptitudePoor", "AptitudeStrong", "AptitudeRemarkable"])).IsTrue();
    }

    [Test]
    [Arguments("AptitudeTerrible", -8f)]
    [Arguments("AptitudePoor", -4f)]
    [Arguments("AptitudeStrong", 4f)]
    [Arguments("AptitudeRemarkable", 8f)]
    public async Task AptitudeTemplateShipsAsPassiveInbornAndActiveImplantTwins(string templateDefName, float expectedMagnitude)
    {
        SignalDefinition[] twins = VanillaSignalDefinitions.All.Where(definition => definition.Source.Kind == SignalSourceKind.Gene && definition.Source.DefName == templateDefName).ToArray();

        await Assert.That(twins.Length).IsEqualTo(2);

        SignalDefinition passive = twins.Single(definition => definition.Source.EffectDiscriminator != "implant");
        SignalDefinition implant = twins.Single(definition => definition.Source.EffectDiscriminator == "implant");
        SignalEffect passiveEffect = passive.Effects.Single(effect => effect.Kind == SignalEffectKind.SkillLevel);
        SignalEffect implantEffect = implant.Effects.Single(effect => effect.Kind == SignalEffectKind.SkillLevel);

        await Assert.That(passive.Type).IsEqualTo(SignalType.Passive);
        await Assert.That(implant.Type).IsEqualTo(SignalType.Active);
        await Assert.That(passive.DerivesSkillFromSource).IsTrue();
        await Assert.That(implant.DerivesSkillFromSource).IsTrue();
        await Assert.That(passiveEffect.Magnitude).IsEqualTo(expectedMagnitude);
        await Assert.That(implantEffect.Magnitude).IsEqualTo(expectedMagnitude);
        await Assert.That(passiveEffect.AlreadyReflected).IsTrue();
        await Assert.That(implantEffect.AlreadyReflected).IsTrue();
        await Assert.That(passive.Effects.SequenceEqual(implant.Effects)).IsTrue();
    }

    [Test]
    public async Task TerribleAptitudeDisablesPassion()
    {
        bool disablesPassion = One(SignalSourceKind.Gene, "AptitudeTerrible").Effects.Any(effect => effect.Kind == SignalEffectKind.Passion && effect.Operation == SignalOperation.Disable);

        await Assert.That(disablesPassion).IsTrue();
    }

    [Test]
    public async Task RemarkableAptitudeAddsOnePassionTier()
    {
        bool addsPassion = One(SignalSourceKind.Gene, "AptitudeRemarkable").Effects.Any(effect => effect.Kind == SignalEffectKind.Passion && effect.Magnitude == 1f);

        await Assert.That(addsPassion).IsTrue();
    }

    [Test]
    public async Task AuditedGeneMembershipIsExact()
    {
        var genes = VanillaSignalDefinitions
            .All.Where(x => x.Source.Kind == SignalSourceKind.Gene && !x.Source.DefName.StartsWith("Aptitude", StringComparison.Ordinal))
            .Select(x => x.Source.DefName)
            .OrderBy(x => x)
            .ToArray();

        await Assert.That(genes).IsEquivalentTo(["FireTerror", "Learning_Fast", "Learning_Slow", "MeleeDamage_Strong", "MeleeDamage_Weak", "Nearsighted", "ViolenceDisabled"]);
    }

    [Test]
    public async Task MeleeDamageGenesRetainTheirExactMultipliers()
    {
        await Assert.That(Effect("MeleeDamage_Strong", SignalEffectKind.Damage).Magnitude).IsEqualTo(1.5f);
        await Assert.That(Effect("MeleeDamage_Weak", SignalEffectKind.Damage).Magnitude).IsEqualTo(0.5f);
    }

    [Test]
    public async Task NearsightedGeneRetainsItsMediumAndLongRangePenalties()
    {
        var nearsighted = One(SignalSourceKind.Gene, "Nearsighted");

        await Assert.That(nearsighted.Effects.Count).IsEqualTo(2);
        await Assert.That(nearsighted.Effects.Any(x => x.Magnitude == 0.5f && x.Conditions.Any(c => c.Key == "range:medium"))).IsTrue();
        await Assert.That(nearsighted.Effects.Any(x => x.Magnitude == 0.25f && x.Conditions.Any(c => c.Key == "range:long"))).IsTrue();
    }

    [Test]
    public async Task ViolenceDisabledGeneDisablesTheCapability()
    {
        bool disablesCapability = One(SignalSourceKind.Gene, "ViolenceDisabled").Effects.Any(x => x.Kind == SignalEffectKind.Capability && x.Operation == SignalOperation.Disable);

        await Assert.That(disablesCapability).IsTrue();
    }

    [Test]
    public async Task FireTerrorGeneRetainsItsMoodAndMentalBreakEffects()
    {
        var fire = One(SignalSourceKind.Gene, "FireTerror");

        await Assert.That(fire.Effects.Any(x => x.Kind == SignalEffectKind.Mood && x.Magnitude == -10f)).IsTrue();
        await Assert.That(fire.Effects.Any(x => x.Kind == SignalEffectKind.MentalBreak && x.Magnitude == 0.1f && x.Unit == SignalValueUnit.Days)).IsTrue();
        await Assert.That(fire.Effects.Count).IsEqualTo(2);
    }

    [Test]
    public async Task TraitKeysUseRealDefNamesAndDegrees()
    {
        var keys = VanillaSignalDefinitions
            .All.Where(x => x.Source.Kind == SignalSourceKind.Trait)
            .Select(x => $"{x.Source.DefName}/{x.Degree}/{x.Source.EffectDiscriminator}")
            .OrderBy(x => x)
            .ToArray();
        await Assert
            .That(keys)
            .IsEquivalentTo([
                "Brawler/0/melee",
                "Brawler/0/shooting",
                "FastLearner/0/",
                "Gourmand/0/",
                "GreatMemory/0/",
                "Immunity/-1/",
                "Industriousness/-1/",
                "Industriousness/-2/",
                "Industriousness/1/",
                "Industriousness/2/",
                "Neurotic/1/",
                "Neurotic/2/",
                "Nimble/0/",
                "Occultist/0/",
                "PerfectMemory/0/",
                "Pyromaniac/0/",
                "ShootingAccuracy/-1/",
                "ShootingAccuracy/1/",
                "SlowLearner/0/",
                "TooSmart/0/",
                "TorturedArtist/0/",
            ]);
    }

    [Test]
    public async Task BrawlerShipsActiveMeleeAndShootingEffects()
    {
        var brawler = VanillaSignalDefinitions.All.Where(x => x.Source.DefName == "Brawler").ToArray();

        await Assert.That(brawler.Select(x => x.SkillDefName
            ?? throw new InvalidOperationException("Brawler effect requires a skill."))).IsEquivalentTo(["Melee", "Shooting"]);
        await Assert.That(brawler.All(x => x.Type == SignalType.Active)).IsTrue();
        await Assert.That(brawler.SelectMany(x => x.Effects).Where(x => x.Kind == SignalEffectKind.SkillLevel).All(x => x.AlreadyReflected)).IsTrue();
    }

    [Test]
    [Arguments("Gourmand", 0)]
    [Arguments("Immunity", -1)]
    public async Task PassiveTraitRemainsPassive(string defName, int degree)
    {
        await Assert.That(One(SignalSourceKind.Trait, defName, degree).Type).IsEqualTo(SignalType.Passive);
    }

    [Test]
    [Arguments(2, 0.35f)]
    [Arguments(-2, -0.35f)]
    public async Task IndustriousnessRetainsItsWorkSpeedMagnitude(int degree, float expectedMagnitude)
    {
        await Assert.That(Effect("Industriousness", SignalEffectKind.WorkSpeed, degree).Magnitude).IsEqualTo(expectedMagnitude);
    }

    [Test]
    public async Task PerfectMemoryDisablesSkillDecay()
    {
        await Assert.That(Effect("PerfectMemory", SignalEffectKind.SkillDecay, 0).Operation).IsEqualTo(SignalOperation.Disable);
    }

    [Test]
    public async Task InhumanizedProducesThreePassiveAlreadyReflectedTargets()
    {
        var definitions = VanillaSignalDefinitions.All.Where(x => x.Source.Kind == SignalSourceKind.Hediff && x.Source.DefName == "Inhumanized").ToArray();
        await Assert.That(definitions.Length).IsEqualTo(3);
        await Assert.That(definitions.Select(x => x.SkillDefName
            ?? throw new InvalidOperationException("Inhumanized effect requires a skill."))).IsEquivalentTo(["Animals", "Social", "Artistic"]);
        await Assert.That(definitions.All(x => x.Type == SignalType.Passive && x.Effects.Single().Magnitude == -12f && x.Effects.Single().AlreadyReflected)).IsTrue();
    }

    private static SignalDefinition One(SignalSourceKind kind, string defName, int? degree = null) =>
        VanillaSignalDefinitions.All.First(x => x.Source.Kind == kind && x.Source.DefName == defName && x.Degree == degree);

    private static SignalEffect Effect(string defName, SignalEffectKind kind, int? degree = null) =>
        VanillaSignalDefinitions.All.First(x => x.Source.DefName == defName && x.Degree == degree).Effects.First(x => x.Kind == kind);
}
