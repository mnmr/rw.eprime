using WorkRoles.Core.Signals;

namespace WorkRoles.Core.Tests.Signals;

public class SignalSnapshotTests
{
    [Test]
    public async Task SortsOnceAndSeparatesGlobalFromExactSkillSignals()
    {
        var minor = Make(SignalType.Passive, SignalSourceKind.Passion, "Ludeon.RimWorld", "Minor", "Shooting");
        var precision = Make(SignalType.Active, SignalSourceKind.Expertise, "vanillaexpanded.skills", "Precision", "Shooting");
        var craftingGene = Make(SignalType.Active, SignalSourceKind.Gene, "example.mod", "CraftingGene", "Crafting");
        var fastLearner = Make(SignalType.Active, SignalSourceKind.Trait, "Ludeon.RimWorld", "FastLearner", null!);

        Signal[] signals = [fastLearner, craftingGene, precision, minor];
        var snapshot = new SignalSnapshot(signals);

        await Assert.That(string.Join("|", snapshot.All.Select(x => x.Source.DefName))).IsEqualTo("Minor|Precision|CraftingGene|FastLearner");
        await Assert.That(string.Join("|", snapshot.Global.Select(x => x.Source.DefName))).IsEqualTo("FastLearner");
        await Assert.That(string.Join("|", snapshot.ForSkill("Shooting").Select(x => x.Source.DefName))).IsEqualTo("Minor|Precision");
        await Assert.That(string.Join("|", snapshot.ForSkill("Crafting").Select(x => x.Source.DefName))).IsEqualTo("CraftingGene");
        await Assert.That(snapshot.ForSkill("Shooting").Contains(fastLearner)).IsFalse();
    }

    [Test]
    public async Task LookupIsOrdinalAndUnknownKeysShareAnImmutableEmptyList()
    {
        var shooting = Make(SignalType.Active, SignalSourceKind.Trait, "Ludeon.RimWorld", "Brawler", "Shooting", "shooting");
        Signal[] signals = [shooting];
        var snapshot = new SignalSnapshot(signals);

        var wrongCase = snapshot.ForSkill("shooting");
        var unknown = snapshot.ForSkill("Cooking");
        var missing = snapshot.ForSkill(null!);
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
        var brawler = Make(SignalType.Active, SignalSourceKind.Trait, "Ludeon.RimWorld", "Brawler", "Melee", "melee");
        var nimble = Make(SignalType.Active, SignalSourceKind.Trait, "Ludeon.RimWorld", "Nimble", "Melee");
        List<Signal> input = [nimble, brawler];

        var snapshot = new SignalSnapshot(input);
        input.Clear();

        await Assert.That(snapshot.All.Count).IsEqualTo(2);
        await Assert.That(string.Join("|", snapshot.ForSkill("Melee").Select(x => x.Source.DefName))).IsEqualTo("Brawler|Nimble");
        await Assert.That(() => ((IList<Signal>)snapshot.All).Add(nimble)).Throws<NotSupportedException>();
        await Assert.That(() => ((IList<Signal>)snapshot.ForSkill("Melee")).Clear()).Throws<NotSupportedException>();
    }

    [Test]
    public async Task SnapshotRejectsNullInput()
    {
        await Assert.That(() => new SignalSnapshot(null!)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task SnapshotRejectsNullSignals()
    {
        var valid = Make(SignalType.Passive, SignalSourceKind.Passion, "Ludeon.RimWorld", "Minor", "Shooting");
        Signal[] signals = [valid, null!];

        await Assert.That(() => new SignalSnapshot(signals)).Throws<ArgumentException>();
    }

    [Test]
    public async Task EmptySnapshotIsStableForEveryView()
    {
        await Assert.That(SignalSnapshot.Empty.All.Count).IsEqualTo(0);
        await Assert.That(SignalSnapshot.Empty.Global.Count).IsEqualTo(0);
        await Assert.That(SignalSnapshot.Empty.ForSkill("Shooting").Count).IsEqualTo(0);
    }

    [Test]
    public async Task BuilderExpandsOtherSkillEffectsIntoTargetedSpillovers()
    {
        Signal critical = SignalFactory.Instantiate(PassionSignalDefinitions.All.Single(x => x.Source.DefName == "VSE_Critical"), "Crafting");
        Signal[] signals = [critical];
        string[] skills = ["Shooting", "Crafting", "Cooking"];

        SignalSnapshot snapshot = SignalSnapshotBuilder.Build(signals, skills, crossSkillEffectsEnabled: true);

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
    public async Task BuilderRetargetingPreservesEffectSemanticsAndConditionOrder()
    {
        List<SignalCondition> conditions = [new("setting:enabled", "Setting is enabled"), new("state:ready", "Pawn is ready")];
        var effect = new SignalEffect(
            SignalEffectKind.LearningRate,
            SignalOperation.Multiply,
            1.75f,
            SignalValueUnit.Factor,
            "OtherSkills",
            conditions,
            SignalScaleKind.ExpertiseLevel,
            currentScale: 4f,
            scaleMultiplier: 0.25f,
            alreadyReflected: true
        );
        Signal source = MakeActiveSignal("Crafting", effect);
        Signal[] signals = [source];
        string[] skills = ["Crafting", "Cooking"];

        SignalEffect targeted = SignalSnapshotBuilder.Build(signals, skills, crossSkillEffectsEnabled: true).ForSkill("Cooking").Single().Effects.Single();

        await Assert.That(targeted.Kind).IsEqualTo(SignalEffectKind.LearningRate);
        await Assert.That(targeted.Operation).IsEqualTo(SignalOperation.Multiply);
        await Assert.That(targeted.Magnitude).IsEqualTo(1.75f);
        await Assert.That(targeted.Unit).IsEqualTo(SignalValueUnit.Factor);
        await Assert.That(targeted.TargetDefName).IsEqualTo("Cooking");
        await Assert.That(targeted.ScaleKind).IsEqualTo(SignalScaleKind.ExpertiseLevel);
        await Assert.That(targeted.CurrentScale).IsEqualTo(4f);
        await Assert.That(targeted.ScaleMultiplier).IsEqualTo(0.25f);
        await Assert.That(targeted.ResolvedMagnitude).IsEqualTo(1.75f);
        await Assert.That(targeted.AlreadyReflected).IsTrue();
        await Assert.That(string.Join(";", targeted.Conditions.Select(x => x.Key + "|" + x.Description))).IsEqualTo("setting:enabled|Setting is enabled;state:ready|Pawn is ready");
    }

    [Test]
    public async Task BuilderRetargetingOwnsReadOnlyDefensiveConditionCopy()
    {
        List<SignalCondition> sourceConditions = [new("setting:enabled", "Setting is enabled")];
        var effect = new SignalEffect(SignalEffectKind.LearningRate, SignalOperation.Multiply, 1.5f, SignalValueUnit.Factor, "OtherSkills", sourceConditions);
        Signal source = MakeActiveSignal("Crafting", effect);
        Signal[] signals = [source];
        string[] skills = ["Crafting", "Cooking"];

        SignalEffect targeted = SignalSnapshotBuilder.Build(signals, skills, crossSkillEffectsEnabled: true).ForSkill("Cooking").Single().Effects.Single();
        sourceConditions.Clear();

        await Assert.That(ReferenceEquals(targeted.Conditions, effect.Conditions)).IsFalse();
        await Assert.That(string.Join(";", targeted.Conditions.Select(x => x.Key))).IsEqualTo("setting:enabled");
        await Assert.That(targeted.Conditions is IList<SignalCondition> list && list.IsReadOnly).IsTrue();
        await Assert.That(() => ((IList<SignalCondition>)targeted.Conditions).Clear()).Throws<NotSupportedException>();
    }

    [Test]
    public async Task DisabledCrossSkillSettingOnlyTargetsPersistentBadPassions()
    {
        Signal critical = SignalFactory.Instantiate(PassionSignalDefinitions.All.Single(x => x.Source.DefName == "VSE_Critical"), "Crafting");
        Signal apathy = SignalFactory.Instantiate(PassionSignalDefinitions.All.Single(x => x.Source.DefName == "VSE_Apathy"), "Cooking");
        Signal transientApathy = SignalFactory.Instantiate(PassionSignalDefinitions.All.Single(x => x.Source.DefName == "AS_MoodyPassion_Apathy"), "Shooting");
        Signal[] signals = [critical, apathy, transientApathy];
        string[] skills = ["Shooting", "Crafting", "Cooking"];

        SignalSnapshot snapshot = SignalSnapshotBuilder.Build(signals, skills, crossSkillEffectsEnabled: false);

        await Assert.That(snapshot.ForSkill("Cooking").Count(x => x.Relation == SignalRelation.Spillover)).IsEqualTo(1);
        await Assert.That(snapshot.ForSkill("Shooting").Count(x => x.Relation == SignalRelation.Spillover)).IsEqualTo(0);
    }

    [Test]
    public async Task DisabledCrossSkillSettingCanTargetExplicitStableNoPassionSkills()
    {
        Signal critical = SignalFactory.Instantiate(PassionSignalDefinitions.All.Single(x => x.Source.DefName == "VSE_Critical"), "Crafting");
        Signal[] signals = [critical];
        string[] skills = ["Crafting", "Mining", "Shooting"];
        string[] persistentlyBadSkills = ["Mining"];

        SignalSnapshot snapshot = SignalSnapshotBuilder.Build(signals, skills, crossSkillEffectsEnabled: false, persistentlyBadSkillDefNames: persistentlyBadSkills);

        await Assert.That(snapshot.ForSkill("Mining").Single().Relation).IsEqualTo(SignalRelation.Spillover);
        await Assert.That(snapshot.ForSkill("Shooting").Count).IsEqualTo(0);
    }

    [Test]
    public async Task BuilderKeepsGlobalSignalsOutOfSkillGroups()
    {
        Signal violenceDisabled = SignalFactory.Instantiate(VanillaSignalDefinitions.All.Single(x => x.Source.DefName == "ViolenceDisabled"));
        Signal[] signals = [violenceDisabled];
        string[] skills = ["Shooting", "Melee"];

        SignalSnapshot snapshot = SignalSnapshotBuilder.Build(signals, skills, crossSkillEffectsEnabled: true);

        await Assert.That(snapshot.Global).IsEquivalentTo([violenceDisabled]);
        await Assert.That(snapshot.ForSkill("Shooting").Count).IsEqualTo(0);
        await Assert.That(snapshot.ForSkill("Melee").Count).IsEqualTo(0);
    }

    [Test]
    public async Task WorkTypeSignalsAreIndexedSeparatelyFromSkillsAndGlobals()
    {
        var hatedCooking = new Signal(
            SignalType.Active,
            new SignalSource(SignalSourceKind.WorkAversion, "HatedWork", "void.MoreThanCapable"),
            skillDefName: null!,
            effects: [],
            new SignalUi("hated cooking", null!, null!, null!, null!, "More Than Capable"),
            workTypeDefName: "Cooking"
        );

        Signal[] signals = [hatedCooking];
        var snapshot = new SignalSnapshot(signals);

        await Assert.That(snapshot.ForWorkType("Cooking").Single()).IsEqualTo(hatedCooking);
        await Assert.That(snapshot.ForWorkType("Crafting").Count).IsEqualTo(0);
        await Assert.That(snapshot.ForSkill("Cooking").Count).IsEqualTo(0);
        await Assert.That(snapshot.Global.Count).IsEqualTo(0);
    }

    private static Signal Make(SignalType type, SignalSourceKind kind, string packageId, string defName, string? skillDefName, string? discriminator = null) =>
        new(type, new SignalSource(kind, defName, packageId, effectDiscriminator: discriminator!), skillDefName!, [], new SignalUi(defName, null!, null!, null!, null!, packageId));

    private static Signal MakeActiveSignal(string skillDefName, SignalEffect effect) =>
        new(SignalType.Active, new SignalSource(SignalSourceKind.Passion, "Conditional", "example.mod"), skillDefName, [effect], new SignalUi("Conditional", null!, null!, null!, null!, "Example Mod"));
}
