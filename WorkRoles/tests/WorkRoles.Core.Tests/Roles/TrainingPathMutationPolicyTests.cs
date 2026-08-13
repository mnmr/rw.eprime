using System.Reflection;

namespace WorkRoles.Core.Tests.Roles;

public class TrainingPathMutationPolicyTests
{
    [Test]
    public async Task RecommendationOrderTreatsNullAndEmptyAsEqual()
    {
        Type policy = typeof(ScopeEngine).Assembly.GetType("WorkRoles.Core.TrainingPathMutationPolicy");
        MethodInfo equal = policy?.GetMethod("IntSequenceEqual", BindingFlags.Public | BindingFlags.Static);
        int[] empty = [];

        bool result = equal != null && (bool)equal.Invoke(null, [null, empty]);

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task RecommendationOrderTreatsIdenticalValuesAsEqual()
    {
        Type policy = typeof(ScopeEngine).Assembly.GetType("WorkRoles.Core.TrainingPathMutationPolicy");
        MethodInfo equal = policy?.GetMethod("IntSequenceEqual", BindingFlags.Public | BindingFlags.Static);
        int[] first = [4, 2];
        int[] second = [4, 2];

        bool result = equal != null && (bool)equal.Invoke(null, [first, second]);

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task RecommendationOrderTreatsReorderedValuesAsDifferent()
    {
        Type policy = typeof(ScopeEngine).Assembly.GetType("WorkRoles.Core.TrainingPathMutationPolicy");
        MethodInfo equal = policy?.GetMethod("IntSequenceEqual", BindingFlags.Public | BindingFlags.Static);
        int[] first = [4, 2];
        int[] second = [2, 4];

        bool result = equal != null && (bool)equal.Invoke(null, [first, second]);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task TrainingColorIgnoresRgbaWhenDisabled()
    {
        MethodInfo equal = typeof(TrainingPathMutationPolicy).GetMethod("ColorEqual", BindingFlags.Public | BindingFlags.Static);

        bool result = equal != null && (bool)equal.Invoke(null, [false, 1f, 1f, 1f, 1f, false, 0f, 0f, 0f, 0f]);

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task TrainingColorTreatsIdenticalEnabledRgbaAsEqual()
    {
        MethodInfo equal = typeof(TrainingPathMutationPolicy).GetMethod("ColorEqual", BindingFlags.Public | BindingFlags.Static);

        bool result = equal != null && (bool)equal.Invoke(null, [true, 0.2f, 0.3f, 0.4f, 0.5f, true, 0.2f, 0.3f, 0.4f, 0.5f]);

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task TrainingColorDetectsChangedAlphaWhenEnabled()
    {
        MethodInfo equal = typeof(TrainingPathMutationPolicy).GetMethod("ColorEqual", BindingFlags.Public | BindingFlags.Static);

        bool result = equal != null && (bool)equal.Invoke(null, [true, 0.2f, 0.3f, 0.4f, 0.5f, true, 0.2f, 0.3f, 0.4f, 0.6f]);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task TrainingBandsTreatIdenticalSequencesAsEqual()
    {
        MethodInfo equal = typeof(TrainingPathMutationPolicy).GetMethod("BandsEqual", BindingFlags.Public | BindingFlags.Static);
        object[] bands = [new int[] { 7, 9 }, new int[] { 0, 8 }, new int[] { 8, 20 }, new int[] { 7, 9 }, new int[] { 0, 8 }, new int[] { 8, 20 }];

        bool result = equal != null && (bool)equal.Invoke(null, bands);

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task TrainingBandsDetectChangedMaximums()
    {
        MethodInfo equal = typeof(TrainingPathMutationPolicy).GetMethod("BandsEqual", BindingFlags.Public | BindingFlags.Static);
        object[] bands = [new int[] { 7, 9 }, new int[] { 0, 8 }, new int[] { 8, 20 }, new int[] { 7, 9 }, new int[] { 0, 8 }, new int[] { 9, 20 }];

        bool result = equal != null && (bool)equal.Invoke(null, bands);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task TrainingBandsTreatEmptyAndNullSequencesAsEqual()
    {
        MethodInfo equal = typeof(TrainingPathMutationPolicy).GetMethod("BandsEqual", BindingFlags.Public | BindingFlags.Static);
        int[] empty = [];
        object[] bands = [empty, empty, empty, null, null, null];

        bool result = equal != null && (bool)equal.Invoke(null, bands);

        await Assert.That(result).IsTrue();
    }
}
