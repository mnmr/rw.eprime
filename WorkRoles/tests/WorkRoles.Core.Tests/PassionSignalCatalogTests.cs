using WorkRoles.Core.Signals;

namespace WorkRoles.Core.Tests;

public class PassionSignalCatalogTests
{
    private static readonly string[] ActiveAlpha =
    {
        "AS_DedicatedPassion",
        "AS_DuncePassion",
        "AS_ForbiddenPassion",
        "AS_FrozenPassion",
        "AS_LikeMindedPassion",
        "AS_ObsessivePassion",
        "AS_SynergisticPassion",
        "AS_TraumaticPassion",
    };

    private static readonly string[] PassiveAlpha =
    {
        "AS_BlindPassion_Elevated",
        "AS_BlindPassion_Elevated_Active",
        "AS_BlindPassion_Sublime",
        "AS_BlindPassion_Sublime_Active",
        "AS_CompetitivePassion",
        "AS_CompetitivePassion_Active",
        "AS_DrunkenPassion",
        "AS_DrunkenPassion_Active",
        "AS_IdeologicalPassion",
        "AS_IdeologicalPassion_Active",
        "AS_IntimatePassion",
        "AS_IntimatePassion_Active",
        "AS_MoodyPassion",
        "AS_MoodyPassion_Apathy",
        "AS_MoodyPassion_Greater",
        "AS_MoodyPassion_Major",
        "AS_MoodyPassion_NoPassion",
        "AS_NightPassion",
        "AS_NightPassion_Active",
        "AS_NomadicPassion",
        "AS_NomadicPassion_Active",
        "AS_NudistPassion",
        "AS_NudistPassion_Active",
        "AS_PainDrivenPassion",
        "AS_PainDrivenPassion_Active",
        "AS_PsychicPassion",
        "AS_PsychicPassion_Critical",
        "AS_PsychicPassion_Major",
        "AS_PsychicPassion_Minor",
        "AS_PsychicPassion_Nullified",
        "AS_RainyDayPassion",
        "AS_RainyDayPassion_Active",
        "AS_StonedPassion",
        "AS_StonedPassion_Active",
        "AS_TranshumanistPassion",
        "AS_TranshumanistPassion_Active",
        "AS_VengefulPassion",
        "AS_VengefulPassion_Active",
    };

    [Test]
    public async Task VanillaAndVseMembershipIsExact()
    {
        var definitions = PassionSignalDefinitions.All;
        var vanilla = definitions.Where(x => x.Source.PackageId == "Ludeon.RimWorld")
            .Select(x => x.Source.DefName).OrderBy(x => x).ToArray();
        var vse = definitions.Where(x => x.Source.PackageId == "vanillaexpanded.skills")
            .Select(x => x.Source.DefName).OrderBy(x => x).ToArray();

        await Assert.That(vanilla).IsEquivalentTo(new[] { "Major", "Minor" });
        await Assert.That(vse).IsEquivalentTo(new[] { "VSE_Apathy", "VSE_Critical", "VSE_Natural" });
        await Assert.That(definitions.Any(x => x.Source.DefName == "None")).IsFalse();
        await Assert.That(definitions.Where(x => x.Source.PackageId != "sarg.alphaskills")
            .All(x => x.Type == SignalType.Active)).IsTrue();
    }

    [Test]
    public async Task AlphaMembershipIsExactlyEightActiveAndThirtyEightPersistentPassive()
    {
        var alpha = PassionSignalDefinitions.All
            .Where(x => x.Source.PackageId == "sarg.alphaskills").ToArray();
        var active = alpha.Where(x => x.Type == SignalType.Active)
            .Select(x => x.Source.DefName).OrderBy(x => x).ToArray();
        var passive = alpha.Where(x => x.Type == SignalType.Passive)
            .Select(x => x.Source.DefName).OrderBy(x => x).ToArray();

        await Assert.That(alpha.Length).IsEqualTo(46);
        await Assert.That(active).IsEquivalentTo(ActiveAlpha);
        await Assert.That(passive).IsEquivalentTo(PassiveAlpha);
    }

    [Test]
    public async Task MixedPassionsRetainExactNativeEffectsWithoutBucketInterpretation()
    {
        AssertFactor("VSE_Apathy", SignalEffectKind.LearningRate, 0.25f);
        AssertFactor("VSE_Apathy", SignalEffectKind.SkillDecay, 1.25f);
        AssertFactor("VSE_Critical", SignalEffectKind.LearningRate, 3f);
        AssertFactor("VSE_Critical", SignalEffectKind.SkillDecay, 0f);
        AssertFactor("AS_FrozenPassion", SignalEffectKind.LearningRate, 0f);
        AssertFactor("AS_FrozenPassion", SignalEffectKind.SkillDecay, 0f);
        AssertFactor("AS_SynergisticPassion", SignalEffectKind.LearningRate, 1f);
        AssertFactor("AS_SynergisticPassion", SignalEffectKind.SkillDecay, 0.9f);

        var dunce = One("AS_DuncePassion");
        await Assert.That(dunce.Type).IsEqualTo(SignalType.Active);
        await Assert.That(dunce.FallbackUi.AuthorTier).IsEqualTo("Minor");
        await Assert.That(dunce.Effects.Any(x => x.Kind == SignalEffectKind.WorkPreference
            && x.Operation == SignalOperation.Descriptive)).IsTrue();
    }

    [Test]
    public async Task CriticalSettingOnlyConditionsItsCrossSkillEffect()
    {
        SignalDefinition critical = One("VSE_Critical");
        SignalEffect ownLearning = critical.Effects.Single(x =>
            x.Kind == SignalEffectKind.LearningRate && x.TargetDefName == "CurrentSkill");
        SignalEffect spillover = critical.Effects.Single(x => x.TargetDefName == "OtherSkills");

        await Assert.That(ownLearning.Conditions.Count).IsEqualTo(0);
        await Assert.That(spillover.Conditions.Select(x => x.Key))
            .IsEquivalentTo(new[] { "setting:vse-critical-affects-other-skills" });
    }

    [Test]
    public async Task EveryPassionDerivesItsSkillAndCarriesSourceAttribution()
    {
        // Count is pinned once, in SignalCatalogContractTests.
        await Assert.That(PassionSignalDefinitions.All.All(x => x.DerivesSkillFromSource)).IsTrue();
        await Assert.That(PassionSignalDefinitions.All.All(x =>
            !string.IsNullOrWhiteSpace(x.Source.PackageId)
            && !string.IsNullOrWhiteSpace(x.FallbackUi.SourceDisplayName))).IsTrue();

        await Assert.That(One("Minor").FallbackUi.SourceDisplayName).IsEqualTo("RimWorld");
        await Assert.That(One("VSE_Critical").FallbackUi.SourceDisplayName)
            .IsEqualTo("Vanilla Skills Expanded");
        await Assert.That(One("AS_DedicatedPassion").FallbackUi.SourceDisplayName)
            .IsEqualTo("Alpha Skills");
    }

    [Test]
    public async Task DisappearingTransientIdentitiesAreExplicitlyExcluded()
    {
        var excluded = new[]
        {
            "AS_YouthPassion",
            "AS_SanguinePassion",
            "AS_SanguinePassion_Active",
            "AS_ToxicPassion",
            "AS_ToxicPassion_Active",
        };

        await Assert.That(PassionSignalDefinitions.All.Any(x =>
            excluded.Contains(x.Source.DefName, StringComparer.Ordinal))).IsFalse();
        foreach (string defName in excluded)
            await Assert.That(PassionSignalDefinitions.IsExcludedTransientIdentity(
                "sarg.alphaskills", defName)).IsTrue();

        await Assert.That(PassionSignalDefinitions.IsExcludedTransientIdentity(
            "SARG.ALPHASKILLS", "AS_YouthPassion")).IsTrue();
        await Assert.That(PassionSignalDefinitions.IsExcludedTransientIdentity(
            "sarg.alphaskills", "AS_NightPassion")).IsFalse();
        await Assert.That(PassionSignalDefinitions.IsExcludedTransientIdentity(
            "another.mod", "AS_YouthPassion")).IsFalse();
    }

    [Test]
    public async Task VanillaPassionsUseTheActualRimWorldIconPaths()
    {
        await Assert.That(One("Minor").FallbackUi.IconKey).IsEqualTo("UI/Icons/PassionMinor");
        await Assert.That(One("Major").FallbackUi.IconKey).IsEqualTo("UI/Icons/PassionMajor");
    }

    private static SignalDefinition One(string defName) =>
        PassionSignalDefinitions.All.Single(x => x.Source.DefName == defName);

    private static void AssertFactor(string defName, SignalEffectKind kind, float expected)
    {
        var effect = One(defName).Effects.Single(x => x.Kind == kind && x.TargetDefName != "OtherSkills");
        if (!effect.Magnitude.HasValue || Math.Abs(effect.Magnitude.Value - expected) > 0.0001f)
            throw new Exception($"{defName} {kind}: expected {expected}, got {effect.Magnitude}");
    }
}
