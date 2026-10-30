using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests.SampleColony;

public class TuningResetRegressionTests
{
    [Test]
    [Arguments("1.6", "WR_RecTuneHunterSection")]
    [Arguments("1.7", "WR_RecTuneHunterSection")]
    [Arguments("1.6", "WR_RecTuneOptionalSection")]
    [Arguments("1.7", "WR_RecTuneOptionalSection")]
    public async Task ResetRestoresCoupledDefaultsAndCapturedColonyOutcomes(string version, string section)
    {
        var capture = new CapturedColony(version);
        RecommendationsTuningOptions lowered = capture.Tuning;
        RecommendationTuningOption[] thresholds = section == "WR_RecTuneHunterSection"
            ? [RecommendationTuningOption.HunterFirstTierMaximum, RecommendationTuningOption.HunterSecondTierMaximum,
               RecommendationTuningOption.HunterThirdTierMaximum, RecommendationTuningOption.HunterFourthTierMaximum]
            : [RecommendationTuningOption.OptionalTargetStrongLevel, RecommendationTuningOption.OptionalTargetGreatLevel];
        foreach (var option in thresholds) lowered = lowered.With(option, 0);
        await Assert.That(thresholds.All(option => lowered.Get(option) == 0)).IsTrue();
        RecommendationsTuningOptions reset = lowered.ResetSection(section);
        foreach (var descriptor in RecommendationsTuningOptions.Descriptors)
            await Assert.That(reset.Get(descriptor.Option)).IsEqualTo(descriptor.SectionLabelKey == section
                ? descriptor.DefaultValue : capture.Tuning.Get(descriptor.Option));

        RecommendationPlan baseline = RecommendationPlan.Build(capture.Colony, capture.Tuning);
        RecommendationPlan changed = RecommendationPlan.Build(capture.Colony, lowered);
        RecommendationPlan restored = RecommendationPlan.Build(capture.Colony, reset);
        bool changedOutcome = false;
        for (int pawn = 0; pawn < baseline.PawnCount; pawn++)
        {
            string expected = Outcome(baseline, pawn);
            changedOutcome |= Outcome(changed, pawn) != expected;
            await Assert.That(Outcome(restored, pawn)).IsEqualTo(expected);
        }
        await Assert.That(changedOutcome).IsTrue().Because("the realistic colony must exercise the lowered thresholds");
        await Assert.That(ReferenceEquals(reset, reset.ResetSection(section))).IsTrue();
    }

    [Test]
    public async Task ResetPreservesOtherSectionsAndNoOpIdentity()
    {
        var options = RecommendationsTuningOptions.Default
            .With(RecommendationTuningOption.ChampionSkillDivisor, 3)
            .With(RecommendationTuningOption.OptionalTargetStrongLevel, 0)
            .With(RecommendationTuningOption.OptionalTargetGreatLevel, 0);
        var reset = options.ResetSection("WR_RecTuneOptionalSection");
        await Assert.That(reset.Get(RecommendationTuningOption.ChampionSkillDivisor)).IsEqualTo(3);
        await Assert.That(reset.Get(RecommendationTuningOption.OptionalTargetStrongLevel)).IsEqualTo(10);
        await Assert.That(reset.Get(RecommendationTuningOption.OptionalTargetGreatLevel)).IsEqualTo(15);
        await Assert.That(ReferenceEquals(reset, reset.ResetSection("WR_RecTuneOptionalSection"))).IsTrue();
        await Assert.That(ReferenceEquals(reset, reset.ResetSection("unknown"))).IsTrue();
        await Assert.That(ReferenceEquals(reset.ResetSection("WR_RecTuneChampionSection"), RecommendationsTuningOptions.Default)).IsTrue();
    }

    private static string Outcome(RecommendationPlan plan, int pawn) =>
        string.Join(",", TrainingEligibilityRegressionTests.Roles(plan, pawn)) + ":"
        + string.Join(",", TrainingEligibilityRegressionTests.ActivePaths(plan, pawn));
}
