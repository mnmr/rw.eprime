using System.Reflection;
using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

public class TrainingPathMutationPolicyTests
{
    [Test]
    public async Task RecommendationOrderEqualityNormalizesNullToEmptyAndPreservesOrder()
    {
        Type policy = typeof(ScopeEngine).Assembly.GetType(
            "WorkRoles.Core.TrainingPathMutationPolicy");
        MethodInfo equal = policy?.GetMethod("IntSequenceEqual",
            BindingFlags.Public | BindingFlags.Static);

        bool nullEqualsEmpty = equal != null && (bool)equal.Invoke(
            null, new object[] { null, Array.Empty<int>() });
        bool valuesEqual = equal != null && (bool)equal.Invoke(
            null, new object[] { new[] { 4, 2 }, new[] { 4, 2 } });
        bool reorderedEqual = equal != null && (bool)equal.Invoke(
            null, new object[] { new[] { 4, 2 }, new[] { 2, 4 } });

        await Assert.That(nullEqualsEmpty).IsTrue();
        await Assert.That(valuesEqual).IsTrue();
        await Assert.That(reorderedEqual).IsFalse();
    }

    [Test]
    public async Task TrainingColorEqualityIgnoresRgbaWhenDisabled()
    {
        MethodInfo equal = typeof(TrainingPathMutationPolicy).GetMethod(
            "ColorEqual", BindingFlags.Public | BindingFlags.Static);

        bool disabledEqual = equal != null && (bool)equal.Invoke(null,
            new object[] { false, 1f, 1f, 1f, 1f, false, 0f, 0f, 0f, 0f });
        bool enabledEqual = equal != null && (bool)equal.Invoke(null,
            new object[] { true, 0.2f, 0.3f, 0.4f, 0.5f,
                true, 0.2f, 0.3f, 0.4f, 0.5f });
        bool changedAlphaEqual = equal != null && (bool)equal.Invoke(null,
            new object[] { true, 0.2f, 0.3f, 0.4f, 0.5f,
                true, 0.2f, 0.3f, 0.4f, 0.6f });

        await Assert.That(disabledEqual).IsTrue();
        await Assert.That(enabledEqual).IsTrue();
        await Assert.That(changedAlphaEqual).IsFalse();
    }

    [Test]
    public async Task BandEqualityComparesAllThreeNormalizedSequences()
    {
        MethodInfo equal = typeof(TrainingPathMutationPolicy).GetMethod(
            "BandsEqual", BindingFlags.Public | BindingFlags.Static);
        object[] same =
        {
            new[] { 7, 9 }, new[] { 0, 8 }, new[] { 8, 20 },
            new[] { 7, 9 }, new[] { 0, 8 }, new[] { 8, 20 },
        };
        object[] changedMaximum =
        {
            new[] { 7, 9 }, new[] { 0, 8 }, new[] { 8, 20 },
            new[] { 7, 9 }, new[] { 0, 8 }, new[] { 9, 20 },
        };
        object[] empty =
        {
            Array.Empty<int>(), Array.Empty<int>(), Array.Empty<int>(),
            null, null, null,
        };

        bool sameEqual = equal != null && (bool)equal.Invoke(null, same);
        bool changedEqual = equal != null
            && (bool)equal.Invoke(null, changedMaximum);
        bool emptyEqual = equal != null && (bool)equal.Invoke(null, empty);

        await Assert.That(sameEqual).IsTrue();
        await Assert.That(changedEqual).IsFalse();
        await Assert.That(emptyEqual).IsTrue();
    }
}
