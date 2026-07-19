using WorkRoles.Core.Signals;

namespace WorkRoles.Core.Tests;

public class SignalSnapshotTests
{
    [Test]
    public async Task SortsOnceAndSeparatesGlobalFromExactSkillSignals()
    {
        var minor = Make(SignalType.Passive, SignalSourceKind.Passion,
            "Ludeon.RimWorld", "Minor", "Shooting");
        var precision = Make(SignalType.Active, SignalSourceKind.Expertise,
            "vanillaexpanded.skills", "Precision", "Shooting");
        var craftingGene = Make(SignalType.Active, SignalSourceKind.Gene,
            "example.mod", "CraftingGene", "Crafting");
        var fastLearner = Make(SignalType.Active, SignalSourceKind.Trait,
            "Ludeon.RimWorld", "FastLearner", null);

        var snapshot = new SignalSnapshot(new[]
        {
            fastLearner, craftingGene, precision, minor,
        });

        await Assert.That(string.Join("|", snapshot.All.Select(x => x.Source.DefName)))
            .IsEqualTo("Minor|Precision|CraftingGene|FastLearner");
        await Assert.That(string.Join("|", snapshot.Global.Select(x => x.Source.DefName)))
            .IsEqualTo("FastLearner");
        await Assert.That(string.Join("|", snapshot.ForSkill("Shooting")
            .Select(x => x.Source.DefName)))
            .IsEqualTo("Minor|Precision");
        await Assert.That(string.Join("|", snapshot.ForSkill("Crafting")
            .Select(x => x.Source.DefName)))
            .IsEqualTo("CraftingGene");
        await Assert.That(snapshot.ForSkill("Shooting").Contains(fastLearner)).IsFalse();
    }

    [Test]
    public async Task LookupIsOrdinalAndUnknownKeysShareAnImmutableEmptyList()
    {
        var shooting = Make(SignalType.Active, SignalSourceKind.Trait,
            "Ludeon.RimWorld", "Brawler", "Shooting", "shooting");
        var snapshot = new SignalSnapshot(new[] { shooting });

        var wrongCase = snapshot.ForSkill("shooting");
        var unknown = snapshot.ForSkill("Cooking");
        var missing = snapshot.ForSkill(null);
        var blank = snapshot.ForSkill("  ");

        await Assert.That(snapshot.ForSkill("Shooting").Count).IsEqualTo(1);
        await Assert.That(wrongCase.Count).IsEqualTo(0);
        await Assert.That(ReferenceEquals(wrongCase, unknown)).IsTrue();
        await Assert.That(ReferenceEquals(unknown, missing)).IsTrue();
        await Assert.That(ReferenceEquals(missing, blank)).IsTrue();
        await Assert.That(wrongCase is IList<Signal> list && list.IsReadOnly).IsTrue();
    }

    [Test]
    public async Task CopiesInputAndKeepsSeveralSourcesForOneSkill()
    {
        var brawler = Make(SignalType.Active, SignalSourceKind.Trait,
            "Ludeon.RimWorld", "Brawler", "Melee", "melee");
        var nimble = Make(SignalType.Active, SignalSourceKind.Trait,
            "Ludeon.RimWorld", "Nimble", "Melee");
        var input = new List<Signal> { nimble, brawler };

        var snapshot = new SignalSnapshot(input);
        input.Clear();

        await Assert.That(snapshot.All.Count).IsEqualTo(2);
        await Assert.That(string.Join("|", snapshot.ForSkill("Melee")
            .Select(x => x.Source.DefName)))
            .IsEqualTo("Brawler|Nimble");
        await Assert.That(() => ((IList<Signal>)snapshot.All).Add(nimble))
            .Throws<NotSupportedException>();
        await Assert.That(() => ((IList<Signal>)snapshot.ForSkill("Melee")).Clear())
            .Throws<NotSupportedException>();
    }

    [Test]
    public async Task RejectsInvalidInputAndProvidesAStableEmptySnapshot()
    {
        var valid = Make(SignalType.Passive, SignalSourceKind.Passion,
            "Ludeon.RimWorld", "Minor", "Shooting");

        await Assert.That(() => new SignalSnapshot(null)).Throws<ArgumentNullException>();
        await Assert.That(() => new SignalSnapshot(new Signal[] { valid, null }))
            .Throws<ArgumentException>();
        await Assert.That(SignalSnapshot.Empty.All.Count).IsEqualTo(0);
        await Assert.That(SignalSnapshot.Empty.Global.Count).IsEqualTo(0);
        await Assert.That(SignalSnapshot.Empty.ForSkill("Shooting").Count).IsEqualTo(0);
    }

    [Test]
    public async Task BuilderExpandsOtherSkillEffectsIntoTargetedSpillovers()
    {
        Signal critical = SignalFactory.Instantiate(
            PassionSignalDefinitions.All.Single(x => x.Source.DefName == "VSE_Critical"),
            "Crafting");

        SignalSnapshot snapshot = SignalSnapshotBuilder.Build(
            new[] { critical },
            new[] { "Shooting", "Crafting", "Cooking" },
            crossSkillEffectsEnabled: true);

        Signal primary = snapshot.ForSkill("Crafting").Single();
        Signal shooting = snapshot.ForSkill("Shooting").Single();
        Signal cooking = snapshot.ForSkill("Cooking").Single();
        await Assert.That(primary.Relation).IsEqualTo(SignalRelation.Primary);
        await Assert.That(primary.Effects.Any(x => x.TargetDefName == "OtherSkills")).IsFalse();
        await Assert.That(shooting.Relation).IsEqualTo(SignalRelation.Spillover);
        await Assert.That(shooting.OriginSkillDefName).IsEqualTo("Crafting");
        await Assert.That(shooting.Effects.Single().TargetDefName).IsEqualTo("Shooting");
        await Assert.That(cooking.Relation).IsEqualTo(SignalRelation.Spillover);
        await Assert.That(snapshot.All.Count).IsEqualTo(3);
    }

    [Test]
    public async Task DisabledCrossSkillSettingOnlyTargetsPersistentBadPassions()
    {
        Signal critical = SignalFactory.Instantiate(
            PassionSignalDefinitions.All.Single(x => x.Source.DefName == "VSE_Critical"),
            "Crafting");
        Signal apathy = SignalFactory.Instantiate(
            PassionSignalDefinitions.All.Single(x => x.Source.DefName == "VSE_Apathy"),
            "Cooking");
        Signal transientApathy = SignalFactory.Instantiate(
            PassionSignalDefinitions.All.Single(x => x.Source.DefName == "AS_MoodyPassion_Apathy"),
            "Shooting");

        SignalSnapshot snapshot = SignalSnapshotBuilder.Build(
            new[] { critical, apathy, transientApathy },
            new[] { "Shooting", "Crafting", "Cooking" },
            crossSkillEffectsEnabled: false);

        await Assert.That(snapshot.ForSkill("Cooking").Count(x =>
            x.Relation == SignalRelation.Spillover)).IsEqualTo(1);
        await Assert.That(snapshot.ForSkill("Shooting").Count(x =>
            x.Relation == SignalRelation.Spillover)).IsEqualTo(0);
    }

    [Test]
    public async Task DisabledCrossSkillSettingCanTargetExplicitStableNoPassionSkills()
    {
        Signal critical = SignalFactory.Instantiate(
            PassionSignalDefinitions.All.Single(x => x.Source.DefName == "VSE_Critical"),
            "Crafting");

        SignalSnapshot snapshot = SignalSnapshotBuilder.Build(
            new[] { critical },
            new[] { "Crafting", "Mining", "Shooting" },
            crossSkillEffectsEnabled: false,
            persistentlyBadSkillDefNames: new[] { "Mining" });

        await Assert.That(snapshot.ForSkill("Mining").Single().Relation)
            .IsEqualTo(SignalRelation.Spillover);
        await Assert.That(snapshot.ForSkill("Shooting").Count).IsEqualTo(0);
    }

    [Test]
    public async Task BuilderKeepsGlobalSignalsOutOfSkillGroups()
    {
        Signal violenceDisabled = SignalFactory.Instantiate(
            VanillaSignalDefinitions.All.Single(x => x.Source.DefName == "ViolenceDisabled"));

        SignalSnapshot snapshot = SignalSnapshotBuilder.Build(
            new[] { violenceDisabled },
            new[] { "Shooting", "Melee" },
            crossSkillEffectsEnabled: true);

        await Assert.That(snapshot.Global).IsEquivalentTo(new[] { violenceDisabled });
        await Assert.That(snapshot.ForSkill("Shooting").Count).IsEqualTo(0);
        await Assert.That(snapshot.ForSkill("Melee").Count).IsEqualTo(0);
    }

    private static Signal Make(
        SignalType type,
        SignalSourceKind kind,
        string packageId,
        string defName,
        string skillDefName,
        string discriminator = null) =>
        new(
            type,
            new SignalSource(kind, defName, packageId,
                effectDiscriminator: discriminator),
            skillDefName,
            Array.Empty<SignalEffect>(),
            new SignalUi(defName, null, null, null, null, packageId));
}
