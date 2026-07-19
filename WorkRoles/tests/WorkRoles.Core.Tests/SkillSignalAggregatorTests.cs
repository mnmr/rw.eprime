using WorkRoles.Core;
using WorkRoles.Core.Signals;

namespace WorkRoles.Core.Tests;

public class SkillSignalAggregatorTests
{
    [Test]
    public async Task EveryKnownSkillTargetedActiveDefinitionHasAnExplicitClassification()
    {
        SignalClassificationCatalog policies = SignalClassificationCatalog.Default;
        SignalDefinition[] definitions = SignalCatalog.Default.All.Where(x =>
            x.Type == SignalType.Active
            && (x.DerivesSkillFromSource || x.SkillDefName != null)).ToArray();

        foreach (SignalDefinition definition in definitions)
        {
            Signal signal = SignalFactory.Instantiate(definition,
                definition.DerivesSkillFromSource ? "Shooting" : null);
            if (!policies.TryClassify(signal, out _))
                throw new Exception("Missing classification: " + definition.IdentityForTest());
        }

        await Assert.That(definitions.Length).IsEqualTo(89);
    }

    [Test]
    public async Task ApprovedPrimaryMappingsAreExact()
    {
        var expected = new[]
        {
            Mapping(SignalSourceKind.Passion, "Minor", SignalBucket.Strong),
            Mapping(SignalSourceKind.Passion, "Major", SignalBucket.Great),
            Mapping(SignalSourceKind.Passion, "VSE_Apathy", SignalBucket.Awful),
            Mapping(SignalSourceKind.Passion, "VSE_Natural", SignalBucket.Strong),
            Mapping(SignalSourceKind.Passion, "VSE_Critical", SignalBucket.Exceptional),
            Mapping(SignalSourceKind.Passion, "AS_DedicatedPassion", SignalBucket.Strong),
            Mapping(SignalSourceKind.Passion, "AS_DuncePassion", SignalBucket.Poor),
            Mapping(SignalSourceKind.Passion, "AS_ForbiddenPassion", SignalBucket.Great),
            Mapping(SignalSourceKind.Passion, "AS_FrozenPassion", SignalBucket.Poor),
            Mapping(SignalSourceKind.Passion, "AS_LikeMindedPassion", SignalBucket.Neutral),
            Mapping(SignalSourceKind.Passion, "AS_ObsessivePassion", SignalBucket.Strong),
            Mapping(SignalSourceKind.Passion, "AS_SynergisticPassion", SignalBucket.Strong),
            Mapping(SignalSourceKind.Passion, "AS_TraumaticPassion", SignalBucket.Poor),
            Mapping(SignalSourceKind.Gene, "MeleeDamage_Strong", SignalBucket.Strong),
            Mapping(SignalSourceKind.Gene, "MeleeDamage_Weak", SignalBucket.Poor),
            Mapping(SignalSourceKind.Gene, "Nearsighted", SignalBucket.Poor),
            Mapping(SignalSourceKind.Trait, "Brawler", SignalBucket.Strong, degree: 0,
                discriminator: "melee"),
            Mapping(SignalSourceKind.Trait, "Brawler", SignalBucket.Poor, degree: 0,
                discriminator: "shooting"),
            Mapping(SignalSourceKind.Trait, "Nimble", SignalBucket.Strong, degree: 0),
            Mapping(SignalSourceKind.Trait, "ShootingAccuracy", SignalBucket.Neutral, degree: 1),
            Mapping(SignalSourceKind.Trait, "ShootingAccuracy", SignalBucket.Neutral, degree: -1),
            Mapping(SignalSourceKind.Trait, "Occultist", SignalBucket.Strong, degree: 0),
            Mapping(SignalSourceKind.Trait, "TorturedArtist", SignalBucket.Neutral, degree: 0),
        };

        foreach (ExpectedMapping mapping in expected)
        {
            SignalDefinition definition = SignalCatalog.Default.All.Single(x =>
                x.Source.Kind == mapping.Kind
                && x.Source.DefName == mapping.DefName
                && x.Degree == mapping.Degree
                && x.Source.EffectDiscriminator == mapping.Discriminator);
            Signal signal = SignalFactory.Instantiate(definition,
                definition.DerivesSkillFromSource ? "Shooting" : null);
            bool classified = SignalClassificationCatalog.Default.TryClassify(signal, out var bucket);
            if (!classified || bucket != mapping.Bucket)
                throw new Exception($"{mapping.Kind}/{mapping.DefName}: expected {mapping.Bucket}, got {bucket}");
        }

        // Completeness: every skill-targeted active non-expertise definition
        // must have a row above, so new primaries cannot ship unmapped.
        int primaries = SignalCatalog.Default.All.Count(x =>
            x.Type == SignalType.Active
            && (x.DerivesSkillFromSource || x.SkillDefName != null)
            && x.Source.Kind != SignalSourceKind.Expertise);
        await Assert.That(expected.Length).IsEqualTo(primaries);
    }

    [Test]
    public async Task EveryExpertiseIsExceptionalRegardlessOfLevel()
    {
        foreach (SignalDefinition definition in ExpertiseSignalDefinitions.All)
        {
            Signal signal = SignalFactory.Instantiate(definition, currentScale: 0f);
            bool classified = SignalClassificationCatalog.Default.TryClassify(signal, out var bucket);
            await Assert.That(classified && bucket == SignalBucket.Exceptional).IsTrue()
                .Because(definition.Source.DefName + " must classify Exceptional at any level");
        }
    }

    [Test]
    public async Task ApprovedSpilloverMappingsAreExact()
    {
        var expected = new Dictionary<string, SignalBucket>
        {
            ["VSE_Critical"] = SignalBucket.Poor,
            ["AS_ObsessivePassion"] = SignalBucket.Poor,
            ["AS_SynergisticPassion"] = SignalBucket.Strong,
        };

        foreach (var pair in expected)
        {
            Signal primary = SignalFactory.Instantiate(
                PassionSignalDefinitions.All.Single(x => x.Source.DefName == pair.Key),
                "Crafting");
            var spillover = new Signal(primary.Type, primary.Source, "Cooking", primary.Effects,
                primary.Ui, "Crafting", SignalRelation.Spillover);
            bool classified = SignalClassificationCatalog.Default.TryClassify(spillover, out var bucket);
            await Assert.That(classified && bucket == pair.Value).IsTrue()
                .Because(pair.Key + " spillover was " + bucket);
        }
    }

    [Test]
    public async Task EnabledSkillsStartNeutralAndPassiveOrGlobalSignalsDoNotContribute()
    {
        Signal passive = SignalFactory.Instantiate(
            VanillaSignalDefinitions.All.Single(x => x.Source.DefName == "AptitudeStrong"),
            "Cooking", "AptitudeStrong_Cooking");
        Signal global = SignalFactory.Instantiate(
            VanillaSignalDefinitions.All.Single(x => x.Source.DefName == "FastLearner"));

        SkillBucketSnapshot result = SkillSignalAggregator.Aggregate(
            new[] { "Cooking", "Shooting" }, new SignalSnapshot(new[] { passive, global }));

        await Assert.That(result.All.Count).IsEqualTo(2);
        await Assert.That(result.ForSkill("Cooking").Bucket).IsEqualTo(SignalBucket.Neutral);
        await Assert.That(result.ForSkill("Cooking").Contributions.Count).IsEqualTo(0);
        await Assert.That(result.ForSkill("Shooting").Bucket).IsEqualTo(SignalBucket.Neutral);
    }

    [Test]
    public async Task ContributionsAddAndTwoUnopposedPoorsBecomeAwful()
    {
        Signal nearSighted = Known(SignalSourceKind.Gene, "Nearsighted");
        Signal brawlerShooting = Known(SignalSourceKind.Trait, "Brawler", 0, "shooting");
        Signal minor = Known(SignalSourceKind.Passion, "Minor", runtimeSkill: "Shooting");

        SkillBucketSignal twoPoor = SkillSignalAggregator.Aggregate(
            new[] { "Shooting" }, new SignalSnapshot(new[] { nearSighted, brawlerShooting }))
            .ForSkill("Shooting");
        SkillBucketSignal offset = SkillSignalAggregator.Aggregate(
            new[] { "Shooting" }, new SignalSnapshot(new[] { nearSighted, brawlerShooting, minor }))
            .ForSkill("Shooting");

        await Assert.That(twoPoor.Bucket).IsEqualTo(SignalBucket.Awful);
        await Assert.That(twoPoor.Contributions.Count).IsEqualTo(2);
        await Assert.That(offset.Bucket).IsEqualTo(SignalBucket.Poor);
    }

    [Test]
    public async Task ExplicitAwfulIsAHardVetoAgainstAnyBenefits()
    {
        Signal apathy = Known(SignalSourceKind.Passion, "VSE_Apathy", runtimeSkill: "Shooting");
        Signal expertise = SignalFactory.Instantiate(
            ExpertiseSignalDefinitions.All.First(x => x.SkillDefName == "Shooting"),
            currentScale: 20f);

        SkillBucketSignal result = SkillSignalAggregator.Aggregate(
            new[] { "Shooting" }, new SignalSnapshot(new[] { apathy, expertise }))
            .ForSkill("Shooting");

        await Assert.That(result.Bucket).IsEqualTo(SignalBucket.Awful);
        await Assert.That(result.Contributions.Any(x => x.IsHardVeto)).IsTrue();
    }

    [Test]
    public async Task UnknownActiveSignalsRemainNeutralWithUnclassifiedProvenance()
    {
        var unknown = new Signal(
            SignalType.Active,
            new SignalSource(SignalSourceKind.Trait, "FutureTrait", "future.mod", degree: 0),
            "Cooking",
            new[] { new SignalEffect(SignalEffectKind.WorkSpeed, SignalOperation.Multiply,
                2f, SignalValueUnit.Factor, "CookingSpeed") },
            new SignalUi("future trait", null, null, null, null, "Future Mod"));

        SkillBucketSignal result = SkillSignalAggregator.Aggregate(
            new[] { "Cooking" }, new SignalSnapshot(new[] { unknown })).ForSkill("Cooking");

        await Assert.That(result.Bucket).IsEqualTo(SignalBucket.Neutral);
        await Assert.That(result.Contributions.Count).IsEqualTo(1);
        await Assert.That(result.Contributions[0].IsClassified).IsFalse();
    }

    [Test]
    public async Task PawnSnapshotCachesSignalsAndTheirAggregatedSkillBucketsTogether()
    {
        Signal brawlerMelee = Known(
            SignalSourceKind.Trait, "Brawler", 0, "melee");
        var signals = new SignalSnapshot(new[] { brawlerMelee });

        PawnSignalSnapshot snapshot = PawnSignalSnapshot.Create(
            new[] { "Cooking", "Melee" }, signals);

        await Assert.That(ReferenceEquals(snapshot.Signals, signals)).IsTrue();
        await Assert.That(snapshot.SkillBuckets.All.Count).IsEqualTo(2);
        await Assert.That(snapshot.SkillBuckets.ForSkill("Cooking").Bucket)
            .IsEqualTo(SignalBucket.Neutral);
        await Assert.That(snapshot.SkillBuckets.ForSkill("Melee").Bucket)
            .IsEqualTo(SignalBucket.Strong);
        await Assert.That(ReferenceEquals(
            snapshot.SkillBuckets.ForSkill("Melee").Contributions[0].Signal,
            brawlerMelee)).IsTrue();
    }

    private static Signal Known(
        SignalSourceKind kind,
        string defName,
        int? degree = null,
        string discriminator = null,
        string runtimeSkill = null)
    {
        SignalDefinition definition = SignalCatalog.Default.All.Single(x =>
            x.Source.Kind == kind && x.Source.DefName == defName
            && x.Degree == degree && x.Source.EffectDiscriminator == discriminator);
        return SignalFactory.Instantiate(definition,
            definition.DerivesSkillFromSource ? runtimeSkill : null);
    }

    private static ExpectedMapping Mapping(
        SignalSourceKind kind,
        string defName,
        SignalBucket bucket,
        int? degree = null,
        string discriminator = null) =>
        new(kind, defName, degree, discriminator, bucket);

    private sealed record ExpectedMapping(
        SignalSourceKind Kind,
        string DefName,
        int? Degree,
        string Discriminator,
        SignalBucket Bucket);
}

internal static class SignalDefinitionTestIdentity
{
    internal static string IdentityForTest(this SignalDefinition definition) => string.Join("/", new[]
    {
        definition.Source.Kind.ToString(), definition.Source.PackageId,
        definition.Source.DefName, definition.Degree?.ToString() ?? "",
        definition.Source.EffectDiscriminator ?? "",
    });
}
