using WorkRoles.Core.Signals;
using WorkRoles.Core.Recs;
using SignalSource = WorkRoles.Core.Signals.SignalSource;

namespace WorkRoles.Core.Tests.UI;

public class SkillPresentationDependencyTests
{
    [Test]
    public async Task HunterTuningDoesNotRebuildSkillsButPromotionAndCaptionInputsRemainIndependent()
    {
        int builds = 0;
        var owner = new object();
        var facts = new object();
        var pawn = new object();
        var cache = new PawnPresentationCache<object, object>(
            (_, _) => { builds++; return new object(); },
            ReferenceEquals, ReferenceEqualityComparer.Instance);
        PawnPresentationStamp Stamp(RecommendationsTuningOptions options) =>
            new(owner, facts, options.SkillPromotion, 0, 0, 0);
        var defaults = RecommendationsTuningOptions.Default;
        object first = cache.Get(pawn, Stamp(defaults));
        var hunter = defaults.With(RecommendationTuningOption.HunterFirstTierMaximum, 5);
        await Assert.That(ReferenceEquals(first, cache.Get(pawn, Stamp(hunter)))).IsTrue();
        await Assert.That(builds).IsEqualTo(1);
        await Assert.That(defaults.SkillCaption.Equals(hunter.SkillCaption)).IsTrue();

        var champion = hunter.With(RecommendationTuningOption.ChampionSkillDivisor, 3);
        await Assert.That(ReferenceEquals(first, cache.Get(pawn, Stamp(champion)))).IsTrue();
        await Assert.That(builds).IsEqualTo(1);
        await Assert.That(defaults.SkillCaption.Equals(champion.SkillCaption)).IsFalse();

        var promotion = champion.With(RecommendationTuningOption.OptionalTargetStrongLevel, 11);
        await Assert.That(ReferenceEquals(first, cache.Get(pawn, Stamp(promotion)))).IsFalse();
        await Assert.That(builds).IsEqualTo(2);
        await Assert.That(champion.SkillCaption.Equals(promotion.SkillCaption)).IsTrue();
        cache.Get(pawn, Stamp(promotion.With(RecommendationTuningOption.OptionalTargetStrongLevel, 11)));
        await Assert.That(builds).IsEqualTo(2);
    }

    // Rendered pixels cannot show that a semantically equal refresh discarded
    // reusable projections. This protects the per-skill cache equality boundary.
    [Test]
    public async Task RecapturingOneSkillPreservesTheOtherSkillsSignalProjection()
    {
        static Signal Passion(string skill, string tier) => new(
            SignalType.Active,
            new SignalSource(SignalSourceKind.Passion, tier, "Ludeon.RimWorld"),
            skill, Array.Empty<SignalEffect>(),
            new SignalUi(tier, "[PAWN_nameDef] enjoys this work", "Passion/" + tier,
                tier, null, "RimWorld"));
        var first = new SignalSnapshot(new[] { Passion("Crafting", "Major"), Passion("Mining", "Minor") });
        var second = new SignalSnapshot(new[] { Passion("Crafting", "Major"), Passion("Mining", "Major") });
        SkillSignalView crafting = SignalPresentationPolicy.ForSkill(first, "Crafting");
        SkillSignalView recapturedCrafting = SignalPresentationPolicy.ForSkill(second, "Crafting");
        await Assert.That(crafting.Matches(second, "Crafting")).IsTrue();
        await Assert.That(SignalPresentationPolicy.ForSkill(first, "Mining")
            .Matches(second, "Mining")).IsFalse();
        await Assert.That(EqualityComparer<SkillSignalView>.Default.Equals(
            crafting, recapturedCrafting)).IsTrue();
        await Assert.That(EqualityComparer<SkillSignalView>.Default.Equals(
            SignalPresentationPolicy.ForSkill(first, "Mining"),
            SignalPresentationPolicy.ForSkill(second, "Mining"))).IsFalse();

        var changedDescription = new SignalSnapshot(new[] {
            new Signal(SignalType.Active,
                new SignalSource(SignalSourceKind.Passion, "Major", "Ludeon.RimWorld"),
                "Crafting", Array.Empty<SignalEffect>(),
                new SignalUi("Major", "Different grammar", "Passion/Major", "Major", null, "RimWorld")) });
        await Assert.That(EqualityComparer<SkillSignalView>.Default.Equals(
            crafting, SignalPresentationPolicy.ForSkill(changedDescription, "Crafting"))).IsFalse();
    }
}
