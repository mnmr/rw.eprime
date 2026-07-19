using WorkRoles.Core.Signals;

namespace WorkRoles.Core.Tests;

public class SignalPresentationPolicyTests
{
    [Test]
    public async Task ForSkillKeepsSkillAndGlobalSignalsSeparate()
    {
        var shooting = MakeSignal("Minor", "Ludeon.RimWorld", "Shooting", "UI/Icons/PassionMinor");
        var global = MakeSignal("FastLearner", "Ludeon.RimWorld", null, null,
            kind: SignalSourceKind.Trait);
        var melee = MakeSignal("Brawler", "Ludeon.RimWorld", "Melee", null,
            kind: SignalSourceKind.Trait);

        var view = SignalPresentationPolicy.ForSkill(
            new SignalSnapshot(new[] { melee, global, shooting }), "Shooting");

        await Assert.That(view.SkillSignals).IsEquivalentTo(new[] { shooting });
        await Assert.That(view.GlobalSignals).IsEquivalentTo(new[] { global });
        await Assert.That(view.IconCandidates).IsEquivalentTo(new[] { shooting });
        await Assert.That(view.HasTooltip).IsTrue();
        await Assert.That(view.HasGlobalSignals).IsTrue();
    }

    [Test]
    public async Task EmptyGlobalInputOmitsTheGlobalGroup()
    {
        var shooting = MakeSignal("Minor", "Ludeon.RimWorld", "Shooting", "UI/Icons/PassionMinor");

        var view = SignalPresentationPolicy.ForSkill(
            new SignalSnapshot(new[] { shooting }), "Shooting");

        await Assert.That(view.GlobalSignals.Count).IsEqualTo(0);
        await Assert.That(view.HasGlobalSignals).IsFalse();
        await Assert.That(view.HasTooltip).IsTrue();
    }

    [Test]
    public async Task IconCandidatesRequireAuthoredIconsAndOfficialMissesAreAuditable()
    {
        var officialMissing = MakeSignal("Brawler", "Ludeon.RimWorld", "Shooting", null,
            kind: SignalSourceKind.Trait);
        var dlcMissing = MakeSignal("Inhumanized", "ludeon.rimworld.anomaly", "Shooting", "",
            kind: SignalSourceKind.Hediff);
        var modMissing = MakeSignal("VSE_Test", "vanillaexpanded.skills", "Shooting", null,
            kind: SignalSourceKind.Expertise);
        var modIcon = MakeSignal("AS_Test", "sarg.alphaskills", "Shooting", "Passions/AS_Test");

        var view = SignalPresentationPolicy.ForSkill(
            new SignalSnapshot(new[] { modMissing, officialMissing, modIcon, dlcMissing }), "Shooting");

        await Assert.That(view.IconCandidates).IsEquivalentTo(new[] { modIcon });
        await Assert.That(view.OfficialMissingIcons)
            .IsEquivalentTo(new[] { officialMissing, dlcMissing });
        await Assert.That(view.SkillSignals.Count).IsEqualTo(4);
    }

    [Test]
    public async Task PassionTierUsesTheStrongestAuthoredPassionTier()
    {
        var minor = MakeSignal("Minor", "Ludeon.RimWorld", "Shooting", "minor", "Minor");
        var major = MakeSignal("Major", "Ludeon.RimWorld", "Shooting", "major", "Major");
        var trait = MakeSignal("Brawler", "Ludeon.RimWorld", "Shooting", null, "Major",
            SignalSourceKind.Trait);

        var forward = SignalPresentationPolicy.ForSkill(
            new SignalSnapshot(new[] { minor, trait, major }), "Shooting");
        var reverse = SignalPresentationPolicy.ForSkill(
            new SignalSnapshot(new[] { major, trait, minor }), "Shooting");

        await Assert.That(forward.PassionTier).IsEqualTo(SignalPassionTier.Major);
        await Assert.That(reverse.PassionTier).IsEqualTo(SignalPassionTier.Major);
    }

    [Test]
    public async Task NoSignalsMeansNoTooltipAndNoPassionColour()
    {
        var view = SignalPresentationPolicy.ForSkill(SignalSnapshot.Empty, "Shooting");

        await Assert.That(view.HasTooltip).IsFalse();
        await Assert.That(view.PassionTier).IsEqualTo(SignalPassionTier.None);
        await Assert.That(view.IconCandidates.Count).IsEqualTo(0);
        await Assert.That(view.ActiveSignals.Count).IsEqualTo(0);
        await Assert.That(view.PassiveSignals.Count).IsEqualTo(0);
    }

    [Test]
    public async Task SpilloversRemainTooltipFactsButNeverBecomeDecoratorsOrPassionColour()
    {
        var spillover = new Signal(
            SignalType.Active,
            new SignalSource(SignalSourceKind.Passion, "VSE_Critical", "vanillaexpanded.skills"),
            "Cooking",
            new[]
            {
                new SignalEffect(SignalEffectKind.LearningRate, SignalOperation.Multiply,
                    0.25f, SignalValueUnit.Factor, "Cooking"),
            },
            new SignalUi("critical", null, "Passions/PassionCritical", "Major", "Major",
                "Vanilla Skills Expanded"),
            originSkillDefName: "Crafting",
            relation: SignalRelation.Spillover);

        SkillSignalView view = SignalPresentationPolicy.ForSkill(
            new SignalSnapshot(new[] { spillover }), "Cooking");

        await Assert.That(view.SkillSignals).IsEquivalentTo(new[] { spillover });
        await Assert.That(view.IconCandidates.Count).IsEqualTo(0);
        await Assert.That(view.OfficialMissingIcons.Count).IsEqualTo(0);
        await Assert.That(view.PassionTier).IsEqualTo(SignalPassionTier.None);
        await Assert.That(view.HasTooltip).IsTrue();
    }

    [Test]
    public async Task ActivePassiveSplitPreservesOrderWithinEachSource()
    {
        var activeSkill1 = MakeSignal("A1", "Ludeon.RimWorld", "Shooting", "icon1", type: SignalType.Active);
        var passiveSkill = MakeSignal("P1", "Ludeon.RimWorld", "Shooting", "icon2", type: SignalType.Passive);
        var activeSkill2 = MakeSignal("A2", "Ludeon.RimWorld", "Shooting", "icon3", type: SignalType.Active);
        var activeGlobal = MakeSignal("AG1", "Ludeon.RimWorld", null, "icon4", type: SignalType.Active);
        var passiveGlobal = MakeSignal("PG1", "Ludeon.RimWorld", null, "icon5", type: SignalType.Passive);

        var view = SignalPresentationPolicy.ForSkill(
            new SignalSnapshot(new[] { activeSkill1, passiveSkill, activeSkill2, activeGlobal, passiveGlobal }),
            "Shooting");

        // string.Join pins the order: skill signals precede globals, snapshot
        // order preserved within each source (IsEquivalentTo ignores order).
        await Assert.That(string.Join(",", view.ActiveSignals.Select(x => x.Source.DefName)))
            .IsEqualTo("A1,A2,AG1");
        await Assert.That(string.Join(",", view.PassiveSignals.Select(x => x.Source.DefName)))
            .IsEqualTo("P1,PG1");
    }

    [Test]
    public async Task SpilloversIncludedInActivePassiveLists()
    {
        var spillover = new Signal(
            SignalType.Active,
            new SignalSource(SignalSourceKind.Passion, "VSE_Critical", "vanillaexpanded.skills"),
            "Cooking",
            new[]
            {
                new SignalEffect(SignalEffectKind.LearningRate, SignalOperation.Multiply,
                    0.25f, SignalValueUnit.Factor, "Cooking"),
            },
            new SignalUi("critical", null, "Passions/PassionCritical", "Major", "Major",
                "Vanilla Skills Expanded"),
            originSkillDefName: "Crafting",
            relation: SignalRelation.Spillover);
        var activeGlobal = MakeSignal("AG1", "Ludeon.RimWorld", null, "icon1", type: SignalType.Active);

        var view = SignalPresentationPolicy.ForSkill(
            new SignalSnapshot(new[] { spillover, activeGlobal }),
            "Cooking");

        await Assert.That(string.Join(",", view.ActiveSignals.Select(x => x.Source.DefName)))
            .IsEqualTo("VSE_Critical,AG1");
        await Assert.That(view.PassiveSignals.Count).IsEqualTo(0);
    }

    private static Signal MakeSignal(
        string defName,
        string packageId,
        string skill,
        string icon,
        string tier = null,
        SignalSourceKind kind = SignalSourceKind.Passion,
        SignalType type = SignalType.Active) =>
        new Signal(
            type,
            new SignalSource(kind, defName, packageId),
            skill,
            Array.Empty<SignalEffect>(),
            new SignalUi(defName, null, icon, tier, tier, packageId));
}
