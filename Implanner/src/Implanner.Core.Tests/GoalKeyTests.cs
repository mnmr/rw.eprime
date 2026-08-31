using Implanner.Core;

namespace Implanner.Core.Tests;

/// The goal-key wire format is a save-file contract: latches, reservations
/// and owned bills persist these keys, so composing and parsing must stay a
/// single round-tripping implementation for every consumer.
public class GoalKeyTests
{
    [Test]
    public async Task ComposeAndParseRoundTrip()
    {
        string key = GoalKeys.ImplantSlot(41, 3);

        await Assert.That(key).IsEqualTo("i41:3");
        await Assert.That(GoalKeys.TryParseImplantSlot(key,
            out int goalId, out int ordinal)).IsTrue();
        await Assert.That(goalId).IsEqualTo(41);
        await Assert.That(ordinal).IsEqualTo(3);
    }

    [Test]
    public async Task MalformedKeysAreRejected()
    {
        await Assert.That(GoalKeys.TryParseImplantSlot("x1:0", out _, out _)).IsFalse();
        await Assert.That(GoalKeys.TryParseImplantSlot("i:0", out _, out _)).IsFalse();
        await Assert.That(GoalKeys.TryParseImplantSlot("i1", out _, out _)).IsFalse();
        await Assert.That(GoalKeys.TryParseImplantSlot("i1:", out _, out _)).IsFalse();
        await Assert.That(GoalKeys.TryParseImplantSlot("iA:0", out _, out _)).IsFalse();
        await Assert.That(GoalKeys.TryParseImplantSlot("", out _, out _)).IsFalse();
    }

    [Test]
    public async Task ContainsRequiresTheExactSelectedSlot()
    {
        var goals = new[] { new ImplantGoal(5, "BionicLeg", new[] { 0, 2 }) };

        await Assert.That(GoalKeys.Contains(goals, "i5:0")).IsTrue();
        await Assert.That(GoalKeys.Contains(goals, "i5:2")).IsTrue();
        // Ordinal 1 is not selected, and goal 6 does not exist.
        await Assert.That(GoalKeys.Contains(goals, "i5:1")).IsFalse();
        await Assert.That(GoalKeys.Contains(goals, "i6:0")).IsFalse();
    }

    [Test]
    public async Task ResolveFindsTheGoalEvenForUnselectedOrdinals()
    {
        var goals = new[] { new ImplantGoal(5, "BionicLeg", new[] { 0 }) };

        // Resolution is by goal id: consumers like recipe selection need the
        // goal for any parsed ordinal, selected or not.
        await Assert.That(GoalKeys.TryResolveImplantSlot(goals, "i5:7",
            out ImplantGoal goal, out int ordinal)).IsTrue();
        await Assert.That(goal.ImplantDefName).IsEqualTo("BionicLeg");
        await Assert.That(ordinal).IsEqualTo(7);
        await Assert.That(GoalKeys.TryResolveImplantSlot(goals, "i9:0",
            out _, out _)).IsFalse();
    }
}
