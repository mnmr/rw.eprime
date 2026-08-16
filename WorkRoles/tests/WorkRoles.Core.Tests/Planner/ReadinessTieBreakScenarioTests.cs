using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests.Planner;

/// Content-gate readiness is the final same-role tie-break: between
/// otherwise equally ranked candidates, the pawn able to execute more of the
/// role's gate-bearing contents is assigned. Eligibility stays level-free.
public class ReadinessTieBreakScenarioTests
{
    [Test]
    public async Task EquallyRankedCandidatesBreakTiesOnGateReadiness()
    {
        RoleView drugMaker = RecsTestBed.Role(1, "Crafting");
        RecsTestBed.AddGate(drugMaker, "Intellectual", 4);
        RecsTestBed.Require(drugMaker, 1);

        // Neutral signals keep both below the surplus floor, so only the
        // single demanded pick is handed out and the tie-break decides it.
        PawnView belowGate = RecsTestBed.Pawn();
        belowGate.SkillLevels["Crafting"] = 9;
        belowGate.SignalBuckets["Crafting"] = SignalBucket.Neutral;
        belowGate.SkillLevels["Intellectual"] = 2;
        belowGate.SignalBuckets["Intellectual"] = SignalBucket.Neutral;
        PawnView meetsGate = RecsTestBed.Pawn();
        meetsGate.SkillLevels["Crafting"] = 9;
        meetsGate.SignalBuckets["Crafting"] = SignalBucket.Neutral;
        meetsGate.SkillLevels["Intellectual"] = 5;
        meetsGate.SignalBuckets["Intellectual"] = SignalBucket.Neutral;
        ColonyView colony = RecsTestBed.Colony([drugMaker], belowGate, meetsGate);

        RecommendationPlan plan = RecommendationPlan.Build(colony);
        HashSet<int> first = [.. Enumerable.Range(0, plan.RoleCountAt(0)).Select(index => plan.RoleAt(0, index))];
        HashSet<int> second = [.. Enumerable.Range(0, plan.RoleCountAt(1)).Select(index => plan.RoleAt(1, index))];

        await Assert.That(second.Contains(drugMaker.Id)).IsTrue();
        await Assert.That(first.Contains(drugMaker.Id)).IsFalse();
    }

    [Test]
    public async Task PawnBelowEveryContentGateRemainsEligible()
    {
        // Level-free eligibility: the only candidate is below the sole gated
        // content's minimum yet still receives the demanded role.
        RoleView drugMaker = RecsTestBed.Role(1, "Crafting");
        RecsTestBed.AddGate(drugMaker, "Intellectual", 8);
        RecsTestBed.Require(drugMaker, 1);

        PawnView pawn = RecsTestBed.Pawn();
        pawn.SkillLevels["Crafting"] = 9;
        pawn.SignalBuckets["Crafting"] = SignalBucket.Strong;
        pawn.SkillLevels["Intellectual"] = 2;
        pawn.SignalBuckets["Intellectual"] = SignalBucket.Neutral;
        ColonyView colony = RecsTestBed.Colony([drugMaker], pawn);

        RecommendationPlan plan = RecommendationPlan.Build(colony);
        HashSet<int> assignments = [.. Enumerable.Range(0, plan.RoleCountAt(0)).Select(index => plan.RoleAt(0, index))];

        await Assert.That(assignments.Contains(drugMaker.Id)).IsTrue();
    }
}
