using WorkRoles.Core.Signals;

namespace WorkRoles.Core.Tests.Signals;

public class SkillSignalAggregatorTests
{
    [Test]
    public async Task EveryKnownSkillTargetedActiveDefinitionHasAnExplicitClassification()
    {
        SignalClassificationCatalog policies = SignalClassificationCatalog.Default;
        SignalDefinition[] definitions = SignalCatalog.Default.All.Where(x => x.Type == SignalType.Active && (x.DerivesSkillFromSource || x.SkillDefName != null)).ToArray();

        foreach (SignalDefinition definition in definitions)
        {
            Signal signal = SignalFactory.Instantiate(definition, definition.DerivesSkillFromSource ? "Shooting" : null);
            if (!policies.TryClassify(signal, out _))
                throw new Exception("Missing classification: " + definition.IdentityForTest());
        }

        await Assert.That(definitions.Length).IsEqualTo(93);
    }

    [Test]
    public async Task ApprovedPrimaryMappingsAreExact()
    {
        ExpectedMapping[] expected =
        [
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
            Mapping(SignalSourceKind.Gene, "AptitudeTerrible", SignalBucket.Awful, discriminator: "implant"),
            Mapping(SignalSourceKind.Gene, "AptitudePoor", SignalBucket.Poor, discriminator: "implant"),
            Mapping(SignalSourceKind.Gene, "AptitudeStrong", SignalBucket.Strong, discriminator: "implant"),
            Mapping(SignalSourceKind.Gene, "AptitudeRemarkable", SignalBucket.Strong, discriminator: "implant"),
            Mapping(SignalSourceKind.Trait, "Brawler", SignalBucket.Strong, degree: 0, discriminator: "melee"),
            Mapping(SignalSourceKind.Trait, "Brawler", SignalBucket.Poor, degree: 0, discriminator: "shooting"),
            Mapping(SignalSourceKind.Trait, "Nimble", SignalBucket.Strong, degree: 0),
            Mapping(SignalSourceKind.Trait, "ShootingAccuracy", SignalBucket.Neutral, degree: 1),
            Mapping(SignalSourceKind.Trait, "ShootingAccuracy", SignalBucket.Neutral, degree: -1),
            Mapping(SignalSourceKind.Trait, "Occultist", SignalBucket.Strong, degree: 0),
            Mapping(SignalSourceKind.Trait, "TorturedArtist", SignalBucket.Neutral, degree: 0),
        ];

        foreach (ExpectedMapping mapping in expected)
        {
            SignalDefinition definition = SignalCatalog.Default.All.Single(candidate => candidate.Source.Kind == mapping.Kind && candidate.Source.DefName == mapping.DefName
                && candidate.Degree == mapping.Degree && candidate.Source.EffectDiscriminator == mapping.Discriminator);
            Signal signal = SignalFactory.Instantiate(definition, definition.DerivesSkillFromSource ? "Shooting" : null);
            bool classified = SignalClassificationCatalog.Default.TryClassify(signal, out SignalBucket bucket);
            if (!classified || bucket != mapping.Bucket)
                throw new Exception($"{mapping.Kind}/{mapping.DefName}: expected {mapping.Bucket}, got {bucket}");
        }

        int primaryDefinitionCount = SignalCatalog.Default.All.Count(definition =>
            definition.Type == SignalType.Active && (definition.DerivesSkillFromSource || definition.SkillDefName != null) && definition.Source.Kind != SignalSourceKind.Expertise
        );
        await Assert.That(expected.Length).IsEqualTo(primaryDefinitionCount);
    }

    [Test]
    public async Task EveryExpertiseIsExceptionalRegardlessOfLevel()
    {
        foreach (SignalDefinition definition in ExpertiseSignalDefinitions.All)
        {
            Signal signal = SignalFactory.Instantiate(definition, currentScale: 0f);
            bool classified = SignalClassificationCatalog.Default.TryClassify(signal, out var bucket);
            await Assert.That(classified && bucket == SignalBucket.Exceptional).IsTrue().Because(definition.Source.DefName + " must classify Exceptional at any level");
        }
    }

    [Test]
    public async Task ApprovedSpilloverMappingsAreExact()
    {
        SpilloverMapping[] expected =
        [
            new("VSE_Critical", SignalBucket.Poor),
            new("AS_ObsessivePassion", SignalBucket.Poor),
            new("AS_SynergisticPassion", SignalBucket.Strong),
        ];

        foreach (SpilloverMapping mapping in expected)
        {
            Signal primary = SignalFactory.Instantiate(PassionSignalDefinitions.All.Single(definition => definition.Source.DefName == mapping.DefName), "Crafting");
            var spillover = new Signal(primary.Type, primary.Source, "Cooking", primary.Effects, primary.Ui, "Crafting", SignalRelation.Spillover);
            bool classified = SignalClassificationCatalog.Default.TryClassify(spillover, out SignalBucket actualBucket);
            await Assert.That(classified && actualBucket == mapping.Bucket).IsTrue().Because(mapping.DefName + " spillover was " + actualBucket);
        }
    }

    [Test]
    public async Task EnabledSkillsStartNeutralAndPassiveOrGlobalSignalsDoNotContribute()
    {
        SignalDefinition passiveDefinition = VanillaSignalDefinitions.All.Single(x => x.Source.DefName == "AptitudeStrong" && x.Type == SignalType.Passive);
        Signal passive = SignalFactory.Instantiate(passiveDefinition, "Cooking", "AptitudeStrong_Cooking");
        Signal global = SignalFactory.Instantiate(VanillaSignalDefinitions.All.Single(x => x.Source.DefName == "FastLearner"));

        string[] skills = ["Cooking", "Shooting"];
        Signal[] signals = [passive, global];
        SkillBucketSnapshot result = SkillSignalAggregator.Aggregate(skills, new SignalSnapshot(signals));

        await Assert.That(result.All.Count).IsEqualTo(2);
        await Assert.That(result.ForSkill("Cooking").Bucket).IsEqualTo(SignalBucket.Neutral);
        await Assert.That(result.ForSkill("Cooking").Contributions.Count).IsEqualTo(0);
        await Assert.That(result.ForSkill("Shooting").Bucket).IsEqualTo(SignalBucket.Neutral);
    }

    [Test]
    public async Task TwoUnopposedPoorContributionsBecomeAwful()
    {
        Signal nearSighted = Known(SignalSourceKind.Gene, "Nearsighted");
        Signal brawlerShooting = Known(SignalSourceKind.Trait, "Brawler", 0, "shooting");
        string[] skills = ["Shooting"];
        Signal[] signals = [nearSighted, brawlerShooting];

        SkillBucketSignal twoPoor = SkillSignalAggregator.Aggregate(skills, new SignalSnapshot(signals)).ForSkill("Shooting");

        await Assert.That(twoPoor.Bucket).IsEqualTo(SignalBucket.Awful);
        await Assert.That(twoPoor.Contributions.Count).IsEqualTo(2);
    }

    [Test]
    public async Task StrongContributionOffsetsTwoPoorContributions()
    {
        Signal nearSighted = Known(SignalSourceKind.Gene, "Nearsighted");
        Signal brawlerShooting = Known(SignalSourceKind.Trait, "Brawler", 0, "shooting");
        Signal minor = Known(SignalSourceKind.Passion, "Minor", runtimeSkill: "Shooting");
        string[] skills = ["Shooting"];
        Signal[] signals = [nearSighted, brawlerShooting, minor];

        SkillBucketSignal offset = SkillSignalAggregator.Aggregate(skills, new SignalSnapshot(signals)).ForSkill("Shooting");

        await Assert.That(offset.Bucket).IsEqualTo(SignalBucket.Poor);
    }

    [Test]
    public async Task ExplicitAwfulIsAHardVetoAgainstAnyBenefits()
    {
        Signal apathy = Known(SignalSourceKind.Passion, "VSE_Apathy", runtimeSkill: "Shooting");
        Signal expertise = SignalFactory.Instantiate(ExpertiseSignalDefinitions.All.First(x => x.SkillDefName == "Shooting"), currentScale: 20f);

        string[] skills = ["Shooting"];
        Signal[] signals = [apathy, expertise];
        SkillBucketSignal result = SkillSignalAggregator.Aggregate(skills, new SignalSnapshot(signals)).ForSkill("Shooting");

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
            [new SignalEffect(SignalEffectKind.WorkSpeed, SignalOperation.Multiply, 2f, SignalValueUnit.Factor, "CookingSpeed")],
            new SignalUi("future trait", null, null, null, null, "Future Mod")
        );
        string[] skills = ["Cooking"];
        Signal[] signals = [unknown];

        SkillBucketSignal result = SkillSignalAggregator.Aggregate(skills, new SignalSnapshot(signals)).ForSkill("Cooking");

        await Assert.That(result.Bucket).IsEqualTo(SignalBucket.Neutral);
        await Assert.That(result.Contributions.Count).IsEqualTo(1);
        await Assert.That(result.Contributions[0].IsClassified).IsFalse();
    }

    [Test]
    public async Task PawnSnapshotCachesSignalsAndTheirAggregatedSkillBucketsTogether()
    {
        Signal brawlerMelee = Known(SignalSourceKind.Trait, "Brawler", 0, "melee");
        Signal[] collectedSignals = [brawlerMelee];
        var signals = new SignalSnapshot(collectedSignals);
        string[] skills = ["Cooking", "Melee"];

        PawnSignalSnapshot snapshot = PawnSignalSnapshot.Create(skills, signals);

        await Assert.That(ReferenceEquals(snapshot.Signals, signals)).IsTrue();
        await Assert.That(snapshot.SkillBuckets.All.Count).IsEqualTo(2);
        await Assert.That(snapshot.SkillBuckets.ForSkill("Cooking").Bucket).IsEqualTo(SignalBucket.Neutral);
        await Assert.That(snapshot.SkillBuckets.ForSkill("Melee").Bucket).IsEqualTo(SignalBucket.Strong);
        await Assert.That(ReferenceEquals(snapshot.SkillBuckets.ForSkill("Melee").Contributions[0].Signal, brawlerMelee)).IsTrue();
    }

    [Test]
    public async Task PawnSnapshotClassifiesWorkAversionAsAnExactAwfulWorkType()
    {
        var hatedCooking = new Signal(
            SignalType.Active,
            new SignalSource(SignalSourceKind.WorkAversion, "HatedWork", "void.MoreThanCapable"),
            skillDefName: null,
            effects: [new SignalEffect(SignalEffectKind.WorkPreference, SignalOperation.Descriptive, null, SignalValueUnit.None, "Cooking")],
            new SignalUi("hated cooking", null, null, null, null, "More Than Capable"),
            workTypeDefName: "Cooking"
        );

        string[] skills = ["Cooking"];
        Signal[] signals = [hatedCooking];
        PawnSignalSnapshot snapshot = PawnSignalSnapshot.Create(skills, new SignalSnapshot(signals));

        WorkTypeBucketSignal cooking = snapshot.WorkTypeBuckets.ForWorkType("Cooking");
        await Assert.That(cooking.Bucket).IsEqualTo(SignalBucket.Awful);
        await Assert.That(cooking.Contributions.Single().IsHardVeto).IsTrue();
        await Assert.That(snapshot.WorkTypeBuckets.ForWorkType("Crafting") == null).IsTrue();
        await Assert.That(snapshot.SkillBuckets.ForSkill("Cooking").Bucket).IsEqualTo(SignalBucket.Neutral);
    }

    [Test]
    public async Task BestFitRanksAggregatedBucketBeforeSkillLevel()
    {
        Signal cookingMinor = Known(SignalSourceKind.Passion, "Minor", runtimeSkill: "Cooking");
        Signal craftingMajor = Known(SignalSourceKind.Passion, "Major", runtimeSkill: "Crafting");
        Signal miningMinor = Known(SignalSourceKind.Passion, "Minor", runtimeSkill: "Mining");
        string[] skills = ["Cooking", "Crafting", "Mining"];
        Signal[] signals = [cookingMinor, craftingMajor, miningMinor];
        SkillBucketSnapshot buckets = SkillSignalAggregator.Aggregate(skills, new SignalSnapshot(signals));

        SkillBucketChoice result = SkillBucketRanking.Best(buckets, [new SkillBucketCandidate("Cooking", 20), new SkillBucketCandidate("Crafting", 2), new SkillBucketCandidate("Mining", 12)]);

        await Assert.That(result.SkillDefName).IsEqualTo("Crafting");
        await Assert.That(result.Bucket).IsEqualTo(SignalBucket.Great);
        await Assert.That(result.SkillLevel).IsEqualTo(2);
    }

    [Test]
    public async Task BestFitUsesSkillLevelWhenBucketsTie()
    {
        Signal cookingMinor = Known(SignalSourceKind.Passion, "Minor", runtimeSkill: "Cooking");
        Signal miningMinor = Known(SignalSourceKind.Passion, "Minor", runtimeSkill: "Mining");
        string[] skills = ["Cooking", "Mining"];
        Signal[] signals = [cookingMinor, miningMinor];
        SkillBucketSnapshot buckets = SkillSignalAggregator.Aggregate(skills, new SignalSnapshot(signals));

        SkillBucketChoice result = SkillBucketRanking.Best(buckets, [new SkillBucketCandidate("Cooking", 5), new SkillBucketCandidate("Mining", 12)]);

        await Assert.That(result.SkillDefName).IsEqualTo("Mining");
        await Assert.That(result.Bucket).IsEqualTo(SignalBucket.Strong);
        await Assert.That(result.SkillLevel).IsEqualTo(12);
    }

    private static Signal Known(SignalSourceKind kind, string defName, int? degree = null, string discriminator = null, string runtimeSkill = null)
    {
        SignalDefinition definition = SignalCatalog.Default.All.Single(x =>
            x.Source.Kind == kind && x.Source.DefName == defName && x.Degree == degree && x.Source.EffectDiscriminator == discriminator
        );
        return SignalFactory.Instantiate(definition, definition.DerivesSkillFromSource ? runtimeSkill : null);
    }

    private static ExpectedMapping Mapping(SignalSourceKind kind, string defName, SignalBucket bucket, int? degree = null, string discriminator = null) =>
        new(kind, defName, degree, discriminator, bucket);

    private sealed record ExpectedMapping(SignalSourceKind Kind, string DefName, int? Degree, string Discriminator, SignalBucket Bucket);

    private sealed record SpilloverMapping(string DefName, SignalBucket Bucket);
}

internal static class SignalDefinitionTestIdentity
{
    internal static string IdentityForTest(this SignalDefinition definition)
    {
        string degree = definition.Degree?.ToString() ?? "";
        string discriminator = definition.Source.EffectDiscriminator ?? "";
        return string.Join("/", [definition.Source.Kind.ToString(), definition.Source.PackageId, definition.Source.DefName, degree, discriminator]);
    }
}
