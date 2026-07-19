using WorkRoles.Core;
using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests;

public class RecommendationExplanationTests
{
    [Test]
    public async Task TrainingAssignmentExplainsItsTargetRole()
    {
        RoleView medic = RecsTestBed.Role(1, "Doctor", "Medic");
        RoleView doctor = RecsTestBed.Role(2, "Doctor", "Doctor");
        doctor.HolderMode = RoleHolderMode.Custom;
        doctor.MinHolders = 1;
        doctor.TrainingWaivers = 1;
        PawnView pawn = RecsTestBed.Pawn();
        pawn.SkillLevels["Medicine"] = 8;
        ColonyView colony = RecsTestBed.Colony(
            new List<RoleView> { medic, doctor }, pawn);
        colony.Paths.Add(RecsTestBed.Path(1,
            (medic.Id, 5, 15), (doctor.Id, 15, 21)));

        RoleRecommendationExplanation explanation =
            RecsEngine.Run(colony)[0].Explanations[medic.Id];

        await Assert.That(explanation.Decision).IsEqualTo(RecommendationDecision.Training);
        await Assert.That(explanation.RelatedRoleId).IsEqualTo(doctor.Id);
    }

    [Test]
    public async Task CandidateRejectedByMaximumExplainsTheConfiguredCap()
    {
        RoleView role = RecsTestBed.Role(1, "Crafting");
        role.HolderMode = RoleHolderMode.Custom;
        role.MaxHolders = 1;
        PawnView lower = RecsTestBed.Pawn();
        lower.SkillLevels["Crafting"] = 5;
        lower.SignalBuckets["Crafting"] = SignalBucket.Strong;
        lower.Existing.Add(new AssignmentView { RoleId = role.Id, Enabled = true });
        PawnView higher = RecsTestBed.Pawn();
        higher.SkillLevels["Crafting"] = 12;
        higher.SignalBuckets["Crafting"] = SignalBucket.Strong;

        RoleRecommendationExplanation explanation = RecsEngine.Run(
            RecsTestBed.Colony(new List<RoleView> { role }, lower, higher))[0]
            .Explanations[role.Id];

        await Assert.That(explanation.Decision)
            .IsEqualTo(RecommendationDecision.ConfiguredMaximumReached);
        await Assert.That(explanation.ConfiguredMaximum).IsEqualTo(1);
    }

    [Test]
    public async Task ExplanationUsesMaximumAppliedByHolderLimitPolicy()
    {
        RoleView role = RecsTestBed.Role(1, "Crafting");
        role.HolderMode = RoleHolderMode.Custom;
        role.MaxHolders = 5;
        PawnView rejected = RecsTestBed.Pawn();
        rejected.SkillLevels["Crafting"] = 5;
        rejected.SignalBuckets["Crafting"] = SignalBucket.Strong;
        rejected.Existing.Add(new AssignmentView { RoleId = role.Id, Enabled = true });
        PawnView selected = RecsTestBed.Pawn();
        selected.SkillLevels["Crafting"] = 12;
        selected.SignalBuckets["Crafting"] = SignalBucket.Strong;

        List<PawnResult> results = RecsEngine.Run(
            RecsTestBed.Colony(new List<RoleView> { role }, rejected, selected),
            new RecRule[]
            {
                new SignalCandidatesRule(),
                new HolderLimitRule(new FixedMaximumPolicy(1)),
                new OrderingRule(),
            });
        RoleRecommendationExplanation explanation = results[0].Explanations[role.Id];

        await Assert.That(explanation.Decision)
            .IsEqualTo(RecommendationDecision.ConfiguredMaximumReached);
        await Assert.That(explanation.ConfiguredMaximum).IsEqualTo(1);
    }


    [Test]
    public async Task StrongSignalExplainsQualificationAndCurrentCoverageFacts()
    {
        RoleView role = RecsTestBed.Role(1, "Crafting");
        role.MinHolders = 1;
        role.MaxHolders = 3;
        PawnView pawn = RecsTestBed.Pawn();
        pawn.SkillLevels["Crafting"] = 2;
        pawn.SignalBuckets["Crafting"] = SignalBucket.Strong;

        PawnResult result = RecsEngine.Run(
            RecsTestBed.Colony(new List<RoleView> { role }, pawn))[0];
        RoleRecommendationExplanation explanation = result.Explanations[role.Id];

        await Assert.That(explanation.Recommended).IsTrue();
        await Assert.That(explanation.Decision)
            .IsEqualTo(RecommendationDecision.SignalQualified);
        await Assert.That(explanation.RequiredHolders).IsEqualTo(1);
        await Assert.That(explanation.RecommendedHolders).IsEqualTo(1);
        await Assert.That(explanation.ConfiguredMaximum).IsEqualTo(3);
        await Assert.That(explanation.RequiredSkills).IsEquivalentTo(new[] { "Crafting" });
        await Assert.That(explanation.SignalSkillDefName).IsEqualTo("Crafting");
        await Assert.That(explanation.SignalBucket).IsEqualTo(SignalBucket.Strong);
    }

    [Test]
    public async Task RemovedExistingRoleExplainsThatRequiredCoverageWasFilled()
    {
        RoleView role = RecsTestBed.Role(1, "Crafting");
        role.MinHolders = 1;
        role.MaxHolders = 3;
        PawnView chosen = RecsTestBed.Pawn();
        chosen.SkillLevels["Crafting"] = 2;
        chosen.SignalBuckets["Crafting"] = SignalBucket.Strong;
        PawnView removed = RecsTestBed.Pawn();
        removed.SkillLevels["Crafting"] = 10;
        removed.Existing.Add(new AssignmentView { RoleId = role.Id, Enabled = true });

        PawnResult result = RecsEngine.Run(
            RecsTestBed.Colony(new List<RoleView> { role }, chosen, removed))[1];
        RoleRecommendationExplanation explanation = result.Explanations[role.Id];

        await Assert.That(explanation.Recommended).IsFalse();
        await Assert.That(explanation.Decision)
            .IsEqualTo(RecommendationDecision.RequiredCoverageFilled);
        await Assert.That(explanation.RequiredHolders).IsEqualTo(1);
        await Assert.That(explanation.RecommendedHolders).IsEqualTo(1);
        await Assert.That(explanation.SignalBucket).IsEqualTo(SignalBucket.Neutral);
    }

    [Test]
    public async Task CoverageExplanationsExposeSelectedAndRejectedCandidateRankings()
    {
        RoleView role = RecsTestBed.Role(1, "Crafting");
        role.MinHolders = 2;
        PawnView first = RecsTestBed.Pawn(); first.SkillLevels["Crafting"] = 9;
        PawnView second = RecsTestBed.Pawn(); second.SkillLevels["Crafting"] = 7;
        PawnView rejected = RecsTestBed.Pawn(); rejected.SkillLevels["Crafting"] = 3;
        rejected.Existing.Add(new AssignmentView { RoleId = role.Id, Enabled = true });

        List<PawnResult> results = RecsEngine.Run(
            RecsTestBed.Colony(new List<RoleView> { role }, first, second, rejected));
        RoleRecommendationExplanation selected = results[1].Explanations[role.Id];
        RoleRecommendationExplanation notSelected = results[2].Explanations[role.Id];

        await Assert.That(selected.Decision)
            .IsEqualTo(RecommendationDecision.CoverageDrafted);
        await Assert.That(selected.CandidateRank).IsEqualTo(2);
        await Assert.That(selected.CandidatePoolSize).IsEqualTo(3);
        await Assert.That(selected.CandidateSkillDefName).IsEqualTo("Crafting");
        await Assert.That(selected.CandidateSkillLevel).IsEqualTo(7);
        await Assert.That(selected.CoverageOpenSlots).IsEqualTo(2);

        await Assert.That(notSelected.Decision)
            .IsEqualTo(RecommendationDecision.RequiredCoverageFilled);
        await Assert.That(notSelected.CandidateRank).IsEqualTo(3);
        await Assert.That(notSelected.CandidatePoolSize).IsEqualTo(3);
        await Assert.That(notSelected.CandidateSkillLevel).IsEqualTo(3);
        await Assert.That(notSelected.CoverageOpenSlots).IsEqualTo(2);
    }

    [Test]
    public async Task AwfulSignalIsTheRemovalReasonEvenWhenCoverageRemainsOpen()
    {
        RoleView role = RecsTestBed.Role(1, "Crafting");
        role.MinHolders = 2;
        PawnView chosen = RecsTestBed.Pawn();
        chosen.SkillLevels["Crafting"] = 2;
        chosen.SignalBuckets["Crafting"] = SignalBucket.Strong;
        PawnView removed = RecsTestBed.Pawn();
        removed.SkillLevels["Crafting"] = 20;
        removed.SignalBuckets["Crafting"] = SignalBucket.Awful;
        removed.Existing.Add(new AssignmentView { RoleId = role.Id, Enabled = true });

        PawnResult result = RecsEngine.Run(
            RecsTestBed.Colony(new List<RoleView> { role }, chosen, removed))[1];

        await Assert.That(result.Explanations[role.Id].Decision)
            .IsEqualTo(RecommendationDecision.AwfulSignal);
    }

    [Test]
    public async Task RemovedSignalCandidateExplainsTrainingBandRejection()
    {
        RoleView role = RecsTestBed.Role(1, "Crafting");
        PawnView removed = RecsTestBed.Pawn();
        removed.SkillLevels["Crafting"] = 2;
        removed.SignalBuckets["Crafting"] = SignalBucket.Strong;
        removed.Existing.Add(new AssignmentView { RoleId = role.Id, Enabled = true });
        PawnView higher = RecsTestBed.Pawn();
        higher.SkillLevels["Crafting"] = 10;
        ColonyView colony = RecsTestBed.Colony(
            new List<RoleView> { role }, removed, higher);
        colony.Paths.Add(RecsTestBed.Path(1, (role.Id, 8, 21)));

        PawnResult result = RecsEngine.Run(colony)[0];

        await Assert.That(result.Explanations[role.Id].Decision)
            .IsEqualTo(RecommendationDecision.OutsideTrainingBand);
    }

    [Test]
    public async Task TrainingBandIsRankingContextNotARejectionForNeutralDraftCandidates()
    {
        RoleView role = RecsTestBed.Role(1, "Crafting");
        role.MinHolders = 1;
        PawnView removed = RecsTestBed.Pawn();
        removed.SkillLevels["Crafting"] = 2;
        removed.Existing.Add(new AssignmentView { RoleId = role.Id, Enabled = true });
        PawnView chosen = RecsTestBed.Pawn();
        chosen.SkillLevels["Crafting"] = 10;
        ColonyView colony = RecsTestBed.Colony(
            new List<RoleView> { role }, removed, chosen);
        colony.Paths.Add(RecsTestBed.Path(1, (role.Id, 8, 21)));

        PawnResult result = RecsEngine.Run(colony)[0];

        await Assert.That(result.Explanations[role.Id].Decision)
            .IsEqualTo(RecommendationDecision.RequiredCoverageFilled);
    }

    [Test]
    public async Task RemovedPartExplainsWhichRecommendedRoleCoversIt()
    {
        RoleView whole = RecsTestBed.Role(1, "Crafting", "MakeA", "MakeB");
        RoleView part = RecsTestBed.Role(2, "Crafting", "MakeA");
        PawnView pawn = RecsTestBed.Pawn();
        pawn.SkillLevels["Crafting"] = 8;
        pawn.SignalBuckets["Crafting"] = SignalBucket.Strong;
        pawn.Existing.Add(new AssignmentView { RoleId = part.Id, Enabled = true });

        PawnResult result = RecsEngine.Run(
            RecsTestBed.Colony(new List<RoleView> { whole, part }, pawn))[0];
        RoleRecommendationExplanation explanation = result.Explanations[part.Id];

        await Assert.That(explanation.Decision)
            .IsEqualTo(RecommendationDecision.CoveredByRecommendedRole);
        await Assert.That(explanation.RelatedRoleId).IsEqualTo(whole.Id);
    }

    private sealed class FixedMaximumPolicy : ITrainingDemandPolicy
    {
        private readonly int maximum;

        internal FixedMaximumPolicy(int maximum) => this.maximum = maximum;

        public int Minimum(int baseMinimum, int inboundAssignments) => baseMinimum;
        public int Maximum(int baseMaximum, int inboundAssignments) => maximum;
    }
}
