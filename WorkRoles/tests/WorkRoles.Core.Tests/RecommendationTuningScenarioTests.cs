using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests;

public class RecommendationTuningScenarioTests
{
    [Test]
    public async Task ChampionMultipliersChangeThePublishedMinimumHolder()
    {
        RoleView crafter = RecsTestBed.Role(1, "Crafting");
        crafter.HolderMode = RoleHolderMode.Custom;
        crafter.RequiredTotal = 1;

        PawnView greatTen = RecsTestBed.Pawn();
        greatTen.SkillLevels["Crafting"] = 10;
        greatTen.SignalBuckets["Crafting"] = SignalBucket.Great;
        PawnView neutralTwenty = RecsTestBed.Pawn();
        neutralTwenty.SkillLevels["Crafting"] = 20;
        neutralTwenty.SignalBuckets["Crafting"] = SignalBucket.Neutral;
        ColonyView colony = RecsTestBed.Colony(
            new List<RoleView> { crafter }, greatTen, neutralTwenty);

        RecommendationsTuningOptions noSurplus =
            RecommendationsTuningOptions.Default.With(
                RecommendationTuningOption.SurplusMinimumSignal,
                (int)SignalBucket.Exceptional);
        RecommendationPlan defaults = RecommendationPlan.Build(
            colony, noSurplus);
        RecommendationPlan reducedGreatMultiplier = RecommendationPlan.Build(
            colony,
            noSurplus.With(
                RecommendationTuningOption.ChampionGreatMultiplierHalfUnits,
                1));

        await Assert.That(RoleIds(defaults, 0)).IsEqualTo("1");
        await Assert.That(RoleIds(defaults, 1)).IsEqualTo("");
        await Assert.That(RoleIds(reducedGreatMultiplier, 0)).IsEqualTo("");
        await Assert.That(RoleIds(reducedGreatMultiplier, 1)).IsEqualTo("1");
    }

    [Test]
    public async Task OrderingSignalPointsChangeThePublishedRoleOrder()
    {
        RoleView doctor = RecsTestBed.Role(1, "Doctor");
        doctor.HolderMode = RoleHolderMode.Custom;
        RoleView cook = RecsTestBed.Role(2, "Cooking");
        cook.HolderMode = RoleHolderMode.Custom;

        PawnView pawn = RecsTestBed.Pawn();
        pawn.SkillLevels["Medicine"] = 10;
        pawn.SignalBuckets["Medicine"] = SignalBucket.Strong;
        pawn.SkillLevels["Cooking"] = 10;
        pawn.SignalBuckets["Cooking"] = SignalBucket.Great;
        ColonyView colony = RecsTestBed.Colony(
            new List<RoleView> { doctor, cook }, pawn);

        RecommendationPlan defaults = RecommendationPlan.Build(
            colony, RecommendationsTuningOptions.Default);
        RecommendationPlan reducedGreatPoints = RecommendationPlan.Build(
            colony,
            RecommendationsTuningOptions.Default.With(
                RecommendationTuningOption.OrderingGreatSignalPoints,
                0));

        await Assert.That(RoleIds(defaults, 0)).IsEqualTo("2,1");
        await Assert.That(RoleIds(reducedGreatPoints, 0)).IsEqualTo("1,2");
    }

    [Test]
    public async Task SemanticallyEqualEditPreservesTheOptionsSnapshot()
    {
        RecommendationsTuningOptions defaults =
            RecommendationsTuningOptions.Default;

        RecommendationsTuningOptions normalized = defaults.With(
            RecommendationTuningOption.ChampionSkillDivisor,
            2);

        await Assert.That(normalized).IsSameReferenceAs(defaults);
    }

    private static string RoleIds(RecommendationPlan plan, int pawnIndex)
    {
        var ids = new List<int>();
        for (int index = 0; index < plan.RoleCountAt(pawnIndex); index++)
            ids.Add(plan.RoleAt(pawnIndex, index));
        return string.Join(",", ids);
    }
}
