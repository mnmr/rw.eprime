using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

/// Curve math checked against the game's real StatDef curves (RimWorld 1.6.4871).
public class JobSkillMathTests
{
    private static readonly float[] ConstructSuccess =
    {
        0.75f, 0.8f, 0.85f, 0.875f, 0.9f, 0.925f, 0.95f, 0.975f, 1f, 1.01f,
        1.02f, 1.03f, 1.04f, 1.05f, 1.06f, 1.07f, 1.08f, 1.09f, 1.1f, 1.12f, 1.13f,
    };

    private static readonly float[] SurgerySuccess =
    {
        0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f, 0.7f, 0.75f, 0.8f, 0.85f,
        0.9f, 0.92f, 0.94f, 0.96f, 0.98f, 1f, 1.02f, 1.04f, 1.06f, 1.08f, 1.1f,
    };

    private static readonly float[] FoodPoison =
    {
        0.05f, 0.04f, 0.03f, 0.02f, 0.015f, 0.01f, 0.005f, 0.0025f, 0.0015f, 0.001f,
        0.001f, 0.001f, 0.001f, 0.001f, 0.001f, 0.001f, 0.001f, 0.001f, 0.001f, 0.001f,
    };

    [Test]
    public async Task ConstructionReachesFullSuccessAtEight()
        => await Assert.That(JobSkillMath.LevelReaching(ConstructSuccess, 1f)).IsEqualTo(8);

    [Test]
    public async Task SurgeryReachesFullSuccessAtFifteen()
        => await Assert.That(JobSkillMath.LevelReaching(SurgerySuccess, 1f)).IsEqualTo(15);

    [Test]
    public async Task UnreachableTargetIsMinusOne()
        => await Assert.That(JobSkillMath.LevelReaching(FoodPoison, 1f)).IsEqualTo(-1);

    [Test]
    public async Task FoodPoisonBottomsOutAtNine()
        => await Assert.That(JobSkillMath.LevelOfMinimum(FoodPoison)).IsEqualTo(9);

    [Test]
    public async Task FlatCurveMinimumIsLevelZero()
        => await Assert.That(JobSkillMath.LevelOfMinimum(new[] { 1f, 1f, 1f })).IsEqualTo(0);

    [Test]
    public async Task EmptyCurveYieldsNoMilestones()
    {
        await Assert.That(JobSkillMath.RisingMilestones(new float[0], new[] { 0.5f })).IsEmpty();
        await Assert.That(JobSkillMath.FallingMilestones(new float[0], new[] { 0.5f })).IsEmpty();
    }

    [Test]
    public async Task UnsortedTargetsYieldTheSameMilestonesAsSorted()
    {
        var sorted = JobSkillMath.RisingMilestones(SurgerySuccess, new[] { 0.5f, 0.75f, 1f });
        var unsorted = JobSkillMath.RisingMilestones(SurgerySuccess, new[] { 1f, 0.5f, 0.75f });
        await Assert.That(unsorted).IsEquivalentTo(sorted);
    }

    private static readonly float[] StandardTargets = { 0.5f, 0.75f, 0.9f, 1f };

    [Test]
    public async Task ConstructionMilestones()
    {
        // Baseline 75% covers the 0.5/0.75 targets; 90% at 4, 100% at 8.
        var milestones = JobSkillMath.RisingMilestones(ConstructSuccess, StandardTargets);
        await Assert.That(milestones).IsEquivalentTo(new[] { (0, 0.75f), (4, 0.9f), (8, 1f) });
    }

    [Test]
    public async Task SurgeryMilestones()
    {
        var milestones = JobSkillMath.RisingMilestones(SurgerySuccess, StandardTargets);
        await Assert.That(milestones)
            .IsEquivalentTo(new[] { (0, 0.1f), (4, 0.5f), (7, 0.75f), (10, 0.9f), (15, 1f) });
    }

    [Test]
    public async Task FoodPoisonMilestones()
    {
        // Baseline 5%, half at 3 (2%), a tenth at 6 (0.5%), bottom at 9 (0.1%).
        var milestones = JobSkillMath.FallingMilestones(FoodPoison, new[] { 0.5f, 0.1f });
        await Assert.That(milestones)
            .IsEquivalentTo(new[] { (0, 0.05f), (3, 0.02f), (6, 0.005f), (9, 0.001f) });
    }
}
