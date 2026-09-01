using Implanner.Core;

namespace Implanner.Core.Tests;

/// The goal-key wire format is a save-file contract: reservations
/// and owned bills persist these keys, so composing and parsing must stay a
/// single round-tripping implementation for every consumer. Identity is
/// natural — owning plan plus implant kind — so keys need no allocation.
public class GoalKeyTests
{
    [Test]
    public async Task ComposeAndParseRoundTrip()
    {
        string key = GoalKeys.ImplantSlot(41, "BionicLeg", 3);

        await Assert.That(key).IsEqualTo("p41:BionicLeg:3");
        await Assert.That(GoalKeys.TryParseImplantSlot(key,
            out int planId, out string defName, out int ordinal)).IsTrue();
        await Assert.That(planId).IsEqualTo(41);
        await Assert.That(defName).IsEqualTo("BionicLeg");
        await Assert.That(ordinal).IsEqualTo(3);
    }

    [Test]
    public async Task MalformedKeysAreRejected()
    {
        await Assert.That(GoalKeys.TryParseImplantSlot("x1:A:0", out _, out _, out _)).IsFalse();
        await Assert.That(GoalKeys.TryParseImplantSlot("p:A:0", out _, out _, out _)).IsFalse();
        await Assert.That(GoalKeys.TryParseImplantSlot("p1:A", out _, out _, out _)).IsFalse();
        await Assert.That(GoalKeys.TryParseImplantSlot("p1:A:", out _, out _, out _)).IsFalse();
        await Assert.That(GoalKeys.TryParseImplantSlot("p1::0", out _, out _, out _)).IsFalse();
        await Assert.That(GoalKeys.TryParseImplantSlot("pA:B:0", out _, out _, out _)).IsFalse();
        await Assert.That(GoalKeys.TryParseImplantSlot("p1:A:x", out _, out _, out _)).IsFalse();
        await Assert.That(GoalKeys.TryParseImplantSlot("", out _, out _, out _)).IsFalse();
        // The retired legacy format is not a natural key.
        await Assert.That(GoalKeys.TryParseImplantSlot("i41:3", out _, out _, out _)).IsFalse();
    }

    [Test]
    public async Task LegacyKeysParseOnlyThroughTheLegacyParser()
    {
        await Assert.That(GoalKeys.TryParseLegacyImplantSlot("i41:3",
            out int goalId, out int ordinal)).IsTrue();
        await Assert.That(goalId).IsEqualTo(41);
        await Assert.That(ordinal).IsEqualTo(3);
        await Assert.That(GoalKeys.TryParseLegacyImplantSlot("p41:BionicLeg:3",
            out _, out _)).IsFalse();
    }

    [Test]
    public async Task ContainsRequiresTheExactSelectedSlot()
    {
        var goals = new[] { new ImplantGoal(5, "BionicLeg", new[] { 0, 2 }) };

        await Assert.That(GoalKeys.Contains(goals, "p5:BionicLeg:0")).IsTrue();
        await Assert.That(GoalKeys.Contains(goals, "p5:BionicLeg:2")).IsTrue();
        // Ordinal 1 is not selected; plan 6 and other kinds do not exist.
        await Assert.That(GoalKeys.Contains(goals, "p5:BionicLeg:1")).IsFalse();
        await Assert.That(GoalKeys.Contains(goals, "p6:BionicLeg:0")).IsFalse();
        await Assert.That(GoalKeys.Contains(goals, "p5:BionicArm:0")).IsFalse();
    }

    [Test]
    public async Task ResolveFindsTheGoalEvenForUnselectedOrdinals()
    {
        var goals = new[] { new ImplantGoal(5, "BionicLeg", new[] { 0 }) };

        // Resolution is by identity (plan, kind): consumers like recipe
        // selection need the goal for any parsed ordinal, selected or not.
        await Assert.That(GoalKeys.TryResolveImplantSlot(goals, "p5:BionicLeg:7",
            out ImplantGoal goal, out int ordinal)).IsTrue();
        await Assert.That(goal.ImplantDefName).IsEqualTo("BionicLeg");
        await Assert.That(ordinal).IsEqualTo(7);
        await Assert.That(GoalKeys.TryResolveImplantSlot(goals, "p9:BionicLeg:0",
            out _, out _)).IsFalse();
    }
}
