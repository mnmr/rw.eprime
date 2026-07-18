using WorkRoles.Core.Signals;

namespace WorkRoles.Core.Tests;

public class SignalModelTests
{
    [Test]
    public async Task SourceAndUiRetainSemanticAndDisplayOriginsSeparately()
    {
        var source = new SignalSource(
            SignalSourceKind.Passion,
            "AS_DedicatedPassion",
            "sarg.alphaskills",
            requiredPackageIds: new[] { "vanillaexpanded.skills" });
        var ui = new SignalUi(
            "dedicated",
            "A steady interest.",
            "Passions/AS_DedicatedPassion",
            "Minor",
            "Minor",
            "Alpha Skills");

        await Assert.That(source.PackageId).IsEqualTo("sarg.alphaskills");
        await Assert.That(source.RequiredPackageIds).IsEquivalentTo(new[] { "vanillaexpanded.skills" });
        await Assert.That(ui.SourceDisplayName).IsEqualTo("Alpha Skills");
    }

    [Test]
    public async Task ConstructorsRejectMissingSemanticIdentityAndUiOrigin()
    {
        await Assert.That(() => new SignalSource(SignalSourceKind.Gene, "Gene", " "))
            .Throws<ArgumentException>();
        await Assert.That(() => new SignalSource(SignalSourceKind.Gene, "", "Ludeon.RimWorld.Biotech"))
            .Throws<ArgumentException>();
        await Assert.That(() => new SignalUi(null, null, null, null, null, ""))
            .Throws<ArgumentException>();
        await Assert.That(() => new SignalEffect(
                SignalEffectKind.StatModifier,
                SignalOperation.Add,
                1f,
                SignalValueUnit.StatValue))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task CollectionsAreCopiedAndExposedReadOnly()
    {
        var dependencies = new List<string> { "vanillaexpanded.skills" };
        var conditions = new List<SignalCondition> { new("night", "At night") };
        var effect = new SignalEffect(
            SignalEffectKind.LearningRate,
            SignalOperation.Multiply,
            2.5f,
            SignalValueUnit.Factor,
            conditions: conditions);
        var effects = new List<SignalEffect> { effect };
        var source = new SignalSource(
            SignalSourceKind.Passion,
            "AS_NightPassion_Active",
            "sarg.alphaskills",
            requiredPackageIds: dependencies);
        var signal = new Signal(
            SignalType.Passive,
            source,
            "Shooting",
            effects,
            new SignalUi("night", null, null, "Major", "Major", "Alpha Skills"));

        dependencies.Add("Ludeon.RimWorld.Royalty");
        conditions.Add(new SignalCondition("outdoors", "Outdoors"));
        effects.Clear();

        await Assert.That(source.RequiredPackageIds.Count).IsEqualTo(1);
        await Assert.That(effect.Conditions.Count).IsEqualTo(1);
        await Assert.That(signal.Effects.Count).IsEqualTo(1);
        await Assert.That(source.RequiredPackageIds is IList<string> list && list.IsReadOnly).IsTrue();
    }

    [Test]
    public async Task ExpertiseScaleResolvesWithoutChangingSourceMagnitude()
    {
        var effect = new SignalEffect(
            SignalEffectKind.WorkSpeed,
            SignalOperation.Add,
            0.05f,
            SignalValueUnit.StatValue,
            "ConstructionSpeed",
            scaleKind: SignalScaleKind.ExpertiseLevel,
            currentScale: 12f,
            scaleMultiplier: 1.5f);

        await Assert.That(effect.Magnitude).IsEqualTo(0.05f);
        await Assert.That(effect.CurrentScale).IsEqualTo(12f);
        await Assert.That(effect.ScaleMultiplier).IsEqualTo(1.5f);
        await Assert.That(effect.ResolvedMagnitude.HasValue).IsTrue();
        await Assert.That(Math.Abs(effect.ResolvedMagnitude.Value - 0.9f) < 0.0001f).IsTrue();
    }

    [Test]
    public async Task DescriptiveEffectHasNoResolvedMagnitude()
    {
        var effect = new SignalEffect(
            SignalEffectKind.WorkPreference,
            SignalOperation.Descriptive,
            null,
            SignalValueUnit.None,
            conditions: new[] { new SignalCondition("ranged-weapon", "When carrying a ranged weapon") });

        await Assert.That(effect.ResolvedMagnitude.HasValue).IsFalse();
    }

    [Test]
    public async Task EquivalentSignalsHaveValueEqualityAndHashCodes()
    {
        Signal Make() => new(
            SignalType.Active,
            new SignalSource(SignalSourceKind.Gene, "Learning_Fast", "Ludeon.RimWorld.Biotech"),
            null,
            new[]
            {
                new SignalEffect(
                    SignalEffectKind.LearningRate,
                    SignalOperation.Add,
                    0.5f,
                    SignalValueUnit.Factor,
                    "GlobalLearningFactor"),
            },
            new SignalUi("quick study", "Learns faster.", "UI/Icons/Genes/Gene_FastLearning", null, null, "Biotech"));

        var first = Make();
        var second = Make();

        await Assert.That(first.Equals(second)).IsTrue();
        await Assert.That(first.GetHashCode()).IsEqualTo(second.GetHashCode());
    }
}
