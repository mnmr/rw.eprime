using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests;

/// The repeat-champion spreading behavior is experimental; per the owner's
/// direction these tests pin the penalty modifiers applied for earlier
/// champion assignments and the zero-penalty escape hatch, not the final
/// colony assignments the penalties produce.
public class RepeatChampionPenaltyTests
{
    [Test]
    public async Task PriorChampionshipsApplyTierScaledScorePenalties()
    {
        RoleView grower = SkilledRole(1, "Plants");
        RoleView priorOverlap = SkilledRole(2, "Plants");
        RoleView priorDistinct = SkilledRole(3, "Mining");
        RoleView priorOccasional = SkilledRole(4, "Medicine");
        priorOccasional.ChampionPenalty = false;
        RoleView priorOccasionalOverlap = SkilledRole(5, "Plants");
        priorOccasionalOverlap.ChampionPenalty = false;
        ColonyView colony = RecsTestBed.Colony(
            new List<RoleView>
            {
                grower, priorOverlap, priorDistinct,
                priorOccasional, priorOccasionalOverlap,
            },
            RecsTestBed.Pawn(), RecsTestBed.Pawn(),
            RecsTestBed.Pawn(), RecsTestBed.Pawn());
        var facts = new EngineContext(colony);
        var formulas = new RecommendationFormulaEngine(
            RecommendationsTuningOptions.Default);

        // 4 colonists, doubled fixed-point: percent defaults 60/40/20
        // become 4/3/1.
        await Assert.That(Penalty(facts, grower, formulas, priorOverlap.Id))
            .IsEqualTo(4);
        await Assert.That(Penalty(facts, grower, formulas, priorDistinct.Id))
            .IsEqualTo(3);
        await Assert.That(Penalty(facts, grower, formulas, priorOccasional.Id))
            .IsEqualTo(1);
        // An occasional prior stays cheap even when its skills overlap.
        await Assert.That(
                Penalty(facts, grower, formulas, priorOccasionalOverlap.Id))
            .IsEqualTo(1);
        // Multiple championships accumulate per prior role.
        await Assert.That(Penalty(
                facts, grower, formulas,
                priorOverlap.Id, priorDistinct.Id, priorOccasional.Id))
            .IsEqualTo(8);
        await Assert.That(RepeatChampionPenalties.PenaltyFor(
                facts, grower, null, formulas))
            .IsEqualTo(0);
    }

    [Test]
    public async Task ZeroedPenaltiesKeepChampionshipsClustered()
    {
        RoleView crafter = RecsTestBed.Role(1, "Crafting");
        RecsTestBed.Require(crafter, 1);
        RoleView doctor = RecsTestBed.Role(2, "Doctor");
        RecsTestBed.Require(doctor, 1);

        // The ace ties the rival on Medicine, so any nonzero repeat penalty
        // from the Crafting championship would hand Doctor to the rival.
        PawnView ace = RecsTestBed.Pawn();
        ace.SkillLevels["Crafting"] = 20;
        ace.SignalBuckets["Crafting"] = SignalBucket.Great;
        ace.SkillLevels["Medicine"] = 14;
        ace.SignalBuckets["Medicine"] = SignalBucket.Great;
        PawnView rival = RecsTestBed.Pawn();
        rival.SkillLevels["Medicine"] = 14;
        rival.SignalBuckets["Medicine"] = SignalBucket.Great;
        ColonyView colony = RecsTestBed.Colony(
            new List<RoleView> { crafter, doctor },
            ace, rival,
            RecsTestBed.Pawn(), RecsTestBed.Pawn(),
            RecsTestBed.Pawn(), RecsTestBed.Pawn());

        RecommendationsTuningOptions zeroed =
            RecommendationsTuningOptions.Default
                .With(
                    RecommendationTuningOption.SurplusMinimumSignal,
                    (int)SignalBucket.Exceptional)
                .With(RecommendationTuningOption.RepeatChampionOverlapPenalty, 0)
                .With(RecommendationTuningOption.RepeatChampionDistinctPenalty, 0)
                .With(
                    RecommendationTuningOption.RepeatChampionOccasionalPenalty,
                    0);
        RecommendationPlan plan = RecommendationPlan.Build(colony, zeroed);

        await Assert.That(Roles(plan, 0)).IsEqualTo("1,2");
        await Assert.That(Roles(plan, 1)).IsEqualTo("");
    }

    private static RoleView SkilledRole(int id, string skill)
    {
        RoleView role = RecsTestBed.Role(id, "Crafting");
        role.Skills.Add(new RoleSkillView
        {
            SkillDefName = skill,
            Primary = true,
        });
        return role;
    }

    private static int Penalty(
        EngineContext facts,
        RoleView role,
        RecommendationFormulaEngine formulas,
        params int[] priorChampionRoleIds)
        => RepeatChampionPenalties.PenaltyFor(
            facts, role, priorChampionRoleIds.ToList(), formulas);

    private static string Roles(RecommendationPlan plan, int pawnIndex)
    {
        var roleIds = new List<int>();
        for (int index = 0; index < plan.RoleCountAt(pawnIndex); index++)
            roleIds.Add(plan.RoleAt(pawnIndex, index));
        roleIds.Sort();
        return string.Join(",", roleIds);
    }
}
