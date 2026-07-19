using WorkRoles.Core.Signals;

namespace WorkRoles.Core.Tests;

public class SignalDefinitionTests
{
    [Test]
    public async Task ActiveDefinitionRequiresAnEffect()
    {
        await Assert.That(() => Definition("Brawler", "melee", effects: Array.Empty<SignalEffect>()))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task CatalogRejectsDuplicateFullIdentityButAllowsSeveralEffectsPerSource()
    {
        var melee = Definition("Brawler", "melee");
        var shooting = Definition("Brawler", "shooting", skill: "Shooting");

        await Assert.That(() => new SignalCatalog(new[] { melee, melee }))
            .Throws<ArgumentException>();

        var catalog = new SignalCatalog(new[] { melee, shooting });
        var found = catalog.Find(SignalSourceKind.Trait, "Brawler", 0);
        await Assert.That(found.Count).IsEqualTo(2);
        await Assert.That(found.Select(x => x.Source.EffectDiscriminator))
            .IsEquivalentTo(new[] { "melee", "shooting" });
    }

    [Test]
    public async Task CatalogTreatsPackageIdAsPartOfFullSourceIdentity()
    {
        var first = Definition("SharedDef", "same");
        var second = new SignalDefinition(
            first.Type,
            new SignalSource(first.Source.Kind, first.Source.DefName, "example.other",
                effectDiscriminator: first.Source.EffectDiscriminator),
            first.Degree,
            first.SkillDefName,
            first.DerivesSkillFromSource,
            first.Effects,
            new SignalUi("shared", null, null, null, null, "Other mod"));

        var catalog = new SignalCatalog(new[] { first, second });

        await Assert.That(catalog.Find(SignalSourceKind.Trait, "SharedDef", 0).Count).IsEqualTo(2);
    }

    [Test]
    public async Task FactoryKeepsSemanticIdentityWhileApplyingLocalizedUi()
    {
        var definition = Definition("Brawler", "melee");
        var signal = SignalFactory.Instantiate(
            definition,
            runtimeSkillDefName: "Melee",
            ui: new SignalUiOverride(
                label: "Nahkämpfer",
                description: "Lokalisierte Beschreibung",
                sourceDisplayName: "RimWorld Core"));

        await Assert.That(signal.Source.DefName).IsEqualTo("Brawler");
        await Assert.That(signal.Source.PackageId).IsEqualTo("Ludeon.RimWorld");
        await Assert.That(signal.SkillDefName).IsEqualTo("Melee");
        await Assert.That(signal.Ui.Label).IsEqualTo("Nahkämpfer");
        await Assert.That(signal.Ui.SourceDisplayName).IsEqualTo("RimWorld Core");
        await Assert.That(signal.Type).IsEqualTo(SignalType.Active);
    }

    [Test]
    public async Task PassiveSkillLevelRoutingMatchesOnlyTheDeclaredAptitudeSkill()
    {
        var definition = new SignalDefinition(
            SignalType.Passive,
            new SignalSource(SignalSourceKind.Trait, "FutureAptitude", "example.mod"),
            degree: 0,
            skillDefName: "Crafting",
            derivesSkillFromSource: false,
            effects: new[]
            {
                new SignalEffect(SignalEffectKind.SkillLevel, SignalOperation.Add, 4f,
                    SignalValueUnit.SkillLevels, "Crafting", alreadyReflected: true),
            },
            fallbackUi: new SignalUi("aptitude", null, null, null, null, "Example mod"));

        await Assert.That(definition.IsPassiveSkillLevelFor("Crafting")).IsTrue();
        await Assert.That(definition.IsPassiveSkillLevelFor("Shooting")).IsFalse();
        await Assert.That(Definition("Brawler", "melee").IsPassiveSkillLevelFor("Melee")).IsFalse();
    }

    [Test]
    public async Task FactoryPreservesTemplateWhenGeneratedSourceGetsActualDefName()
    {
        var definition = new SignalDefinition(
            SignalType.Passive,
            new SignalSource(
                SignalSourceKind.Gene,
                "AptitudeStrong",
                "Ludeon.RimWorld.Biotech"),
            degree: null,
            skillDefName: null,
            derivesSkillFromSource: true,
            effects: new[]
            {
                new SignalEffect(
                    SignalEffectKind.SkillLevel,
                    SignalOperation.Add,
                    4f,
                    SignalValueUnit.SkillLevels,
                    alreadyReflected: true),
            },
            fallbackUi: new SignalUi("strong", null, null, null, null, "Biotech"));

        var signal = SignalFactory.Instantiate(
            definition,
            runtimeSkillDefName: "Shooting",
            actualSourceDefName: "AptitudeStrong_Shooting");

        await Assert.That(signal.Source.DefName).IsEqualTo("AptitudeStrong_Shooting");
        await Assert.That(signal.Source.TemplateId).IsEqualTo("AptitudeStrong");
        await Assert.That(signal.SkillDefName).IsEqualTo("Shooting");
    }

    [Test]
    public async Task FactoryRejectsSkillThatConflictsWithFixedDefinition()
    {
        var definition = Definition("Brawler", "melee");
        await Assert.That(() => SignalFactory.Instantiate(definition, runtimeSkillDefName: "Shooting"))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task FactoryResolvesExpertiseLevelAndGlobalMultiplier()
    {
        var definition = new SignalDefinition(
            SignalType.Active,
            new SignalSource(SignalSourceKind.Expertise, "Foreman", "vanillaexpanded.skills"),
            degree: null,
            skillDefName: "Construction",
            derivesSkillFromSource: false,
            effects: new[]
            {
                new SignalEffect(
                    SignalEffectKind.WorkSpeed,
                    SignalOperation.Add,
                    0.05f,
                    SignalValueUnit.StatValue,
                    "ConstructionSpeed",
                    scaleKind: SignalScaleKind.ExpertiseLevel),
            },
            fallbackUi: new SignalUi("Building", null, null, null, null, "Vanilla Skills Expanded"));

        await Assert.That(definition.Effects[0].ResolvedMagnitude.HasValue).IsFalse();

        var signal = SignalFactory.Instantiate(
            definition,
            runtimeSkillDefName: "Construction",
            currentScale: 12f,
            scaleMultiplier: 1.5f);

        await Assert.That(signal.Effects[0].CurrentScale).IsEqualTo(12f);
        await Assert.That(signal.Effects[0].ScaleMultiplier).IsEqualTo(1.5f);
        await Assert.That(Math.Abs(signal.Effects[0].ResolvedMagnitude.Value - 0.9f) < 0.0001f).IsTrue();
    }

    [Test]
    public async Task ComparerUsesTheDocumentedOrdinalOrder()
    {
        var ui = new SignalUi("x", null, null, null, null, "RimWorld");
        Signal Make(SignalType type, SignalSourceKind kind, string package, string def, string discriminator, string skill) =>
            new(type,
                new SignalSource(kind, def, package, effectDiscriminator: discriminator),
                skill,
                Array.Empty<SignalEffect>(),
                ui);

        var values = new List<Signal>
        {
            Make(SignalType.Active, SignalSourceKind.Gene, "b", "A", null, null),
            Make(SignalType.Passive, SignalSourceKind.Trait, "a", "A", null, null),
            Make(SignalType.Passive, SignalSourceKind.Passion, "z", "Z", null, null),
            Make(SignalType.Passive, SignalSourceKind.Passion, "a", "B", null, null),
            Make(SignalType.Passive, SignalSourceKind.Passion, "a", "A", "two", "Shooting"),
            Make(SignalType.Passive, SignalSourceKind.Passion, "a", "A", null, null),
        };

        values.Sort(SignalComparer.Instance);

        var ordered = string.Join("|", values.Select(x =>
            $"{x.Type}:{x.Source.Kind}:{x.Source.PackageId}:{x.Source.DefName}:{x.Source.EffectDiscriminator}:{x.SkillDefName}"));
        await Assert.That(ordered).IsEqualTo(
            "Passive:Passion:a:A::|" +
            "Passive:Passion:a:A:two:Shooting|" +
            "Passive:Passion:a:B::|" +
            "Passive:Passion:z:Z::|" +
            "Passive:Trait:a:A::|" +
            "Active:Gene:b:A::");
    }

    [Test]
    public async Task DefaultCatalogCombinesEveryAuditedDefinition()
    {
        await Assert.That(SignalCatalog.Default.All.Count).IsEqualTo(152);
        await Assert.That(SignalCatalog.Default.Find(SignalSourceKind.Passion, "AS_DedicatedPassion").Count)
            .IsEqualTo(1);
        await Assert.That(SignalCatalog.Default.Find(SignalSourceKind.Expertise, "Precision").Count)
            .IsEqualTo(1);
        await Assert.That(SignalCatalog.Default.Find(SignalSourceKind.Trait, "Brawler", 0).Count)
            .IsEqualTo(2);
    }

    private static SignalDefinition Definition(
        string defName,
        string discriminator,
        string skill = "Melee",
        IReadOnlyList<SignalEffect> effects = null) =>
        new(
            SignalType.Active,
            new SignalSource(
                SignalSourceKind.Trait,
                defName,
                "Ludeon.RimWorld",
                effectDiscriminator: discriminator),
            degree: 0,
            skillDefName: skill,
            derivesSkillFromSource: false,
            effects: effects ?? new[]
            {
                new SignalEffect(
                    SignalEffectKind.HitChance,
                    SignalOperation.Add,
                    4f,
                    SignalValueUnit.StatValue,
                    "MeleeHitChance"),
            },
            fallbackUi: new SignalUi("brawler", "Fights up close.", null, null, null, "RimWorld"));
}
