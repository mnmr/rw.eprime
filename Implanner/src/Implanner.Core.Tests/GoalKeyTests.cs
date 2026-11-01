using System.Collections.Generic;
using Implanner.Core;

namespace Implanner.Core.Tests;

/// The goal-key wire format is a save-file contract: reservations
/// and owned bills persist these keys, so composing and parsing must stay a
/// single round-tripping implementation for every consumer. Identity is
/// natural — owning plan plus implant kind — and the slot is a part
/// address, the same part on every body that has it.
public class GoalKeyTests
{
    static readonly PartAddress LeftLeg = new("Leg", 0);
    static readonly PartAddress RightLeg = new("Leg", 1);

    [Test]
    public async Task ComposeAndParseRoundTrip()
    {
        string key = GoalKeys.ImplantSlot(41, "BionicLeg", RightLeg);

        await Assert.That(key).IsEqualTo("p41:BionicLeg:Leg#1");
        await Assert.That(GoalKeys.TryParseImplantSlot(key,
            out int planId, out string defName, out PartAddress slot)).IsTrue();
        await Assert.That(planId).IsEqualTo(41);
        await Assert.That(defName).IsEqualTo("BionicLeg");
        await Assert.That(slot).IsEqualTo(RightLeg);
    }

    /// Android Tiers gives each side its own part kind; both sides are
    /// first occurrences of different kinds and stay distinct keys.
    [Test]
    public async Task SidesThatAreSeparatePartKindsStayDistinct()
    {
        string left = GoalKeys.ImplantSlot(2, "ALReceptor", new PartAddress("LeftSightSensor", 0));
        string right = GoalKeys.ImplantSlot(2, "ALReceptor", new PartAddress("RightSightSensor", 0));
        var goals = new[]
        {
            new ImplantGoal(2, "ALReceptor", new[] { new PartAddress("RightSightSensor", 0) }),
        };

        await Assert.That(left).IsNotEqualTo(right);
        await Assert.That(GoalKeys.Contains(goals, right)).IsTrue();
        await Assert.That(GoalKeys.Contains(goals, left)).IsFalse();
    }

    [Test]
    public async Task MalformedKeysAreRejected()
    {
        await Assert.That(GoalKeys.TryParseImplantSlot("x1:A:P#0", out _, out _, out _)).IsFalse();
        await Assert.That(GoalKeys.TryParseImplantSlot("p:A:P#0", out _, out _, out _)).IsFalse();
        await Assert.That(GoalKeys.TryParseImplantSlot("p1:A", out _, out _, out _)).IsFalse();
        await Assert.That(GoalKeys.TryParseImplantSlot("p1:A:", out _, out _, out _)).IsFalse();
        await Assert.That(GoalKeys.TryParseImplantSlot("p1::P#0", out _, out _, out _)).IsFalse();
        await Assert.That(GoalKeys.TryParseImplantSlot("pA:B:P#0", out _, out _, out _)).IsFalse();
        await Assert.That(GoalKeys.TryParseImplantSlot("p1:A:P#x", out _, out _, out _)).IsFalse();
        await Assert.That(GoalKeys.TryParseImplantSlot("", out _, out _, out _)).IsFalse();
        // Retired formats are not current keys.
        await Assert.That(GoalKeys.TryParseImplantSlot("p1:A:0", out _, out _, out _)).IsFalse();
        await Assert.That(GoalKeys.TryParseImplantSlot("i41:3", out _, out _, out _)).IsFalse();
    }

    /// Keys persisted before part addresses carried an ordinal into the
    /// implant's former slot list; loading maps it through game data. An
    /// unmappable slot (out of range, or content missing) drops the entry,
    /// and current keys pass through untouched.
    [Test]
    public async Task LegacyOrdinalKeysMigrateThroughTheSlotMapper()
    {
        IReadOnlyList<PartAddress> Map(string def, int ordinal) =>
            def == "BionicLeg" && ordinal < 2
                ? new[] { new PartAddress("Leg", ordinal) }
                : System.Array.Empty<PartAddress>();
        var legacyGoals = new Dictionary<int, LegacyGoalRef>
        {
            [41] = new LegacyGoalRef(7, "BionicLeg"),
        };

        await Assert.That(GoalKeys.MigrateLegacy("p3:BionicLeg:1", null, Map))
            .IsEqualTo("p3:BionicLeg:Leg#1");
        await Assert.That(GoalKeys.MigrateLegacy("i41:0", legacyGoals, Map))
            .IsEqualTo("p7:BionicLeg:Leg#0");
        await Assert.That(GoalKeys.MigrateLegacy("p3:BionicLeg:5", null, Map)).IsNull();
        await Assert.That(GoalKeys.MigrateLegacy("p3:Gone:0", null, Map)).IsNull();
        await Assert.That(GoalKeys.MigrateLegacy("i99:0", legacyGoals, Map)).IsNull();
        string current = "p3:BionicLeg:Leg#1";
        await Assert.That(ReferenceEquals(
            GoalKeys.MigrateLegacy(current, legacyGoals, Map), current)).IsTrue();
        await Assert.That(GoalKeys.MigrateLegacy("r4:BionicLeg", legacyGoals, Map))
            .IsEqualTo("r4:BionicLeg");
    }

    [Test]
    public async Task ContainsRequiresTheExactSelectedSlot()
    {
        var goals = new[] { new ImplantGoal(5, "BionicLeg", new[] { LeftLeg, new PartAddress("Leg", 2) }) };

        await Assert.That(GoalKeys.Contains(goals, "p5:BionicLeg:Leg#0")).IsTrue();
        await Assert.That(GoalKeys.Contains(goals, "p5:BionicLeg:Leg#2")).IsTrue();
        // Leg#1 is not selected; plan 6 and other kinds do not exist.
        await Assert.That(GoalKeys.Contains(goals, "p5:BionicLeg:Leg#1")).IsFalse();
        await Assert.That(GoalKeys.Contains(goals, "p6:BionicLeg:Leg#0")).IsFalse();
        await Assert.That(GoalKeys.Contains(goals, "p5:BionicArm:Leg#0")).IsFalse();
    }

    [Test]
    public async Task ResolveFindsTheGoalEvenForUnselectedSlots()
    {
        var goals = new[] { new ImplantGoal(5, "BionicLeg", new[] { LeftLeg }) };

        // Resolution is by identity (plan, kind): consumers like recipe
        // selection need the goal for any parsed slot, selected or not.
        await Assert.That(GoalKeys.TryResolveImplantSlot(goals, "p5:BionicLeg:Leg#7",
            out ImplantGoal goal, out PartAddress slot)).IsTrue();
        await Assert.That(goal.ImplantDefName).IsEqualTo("BionicLeg");
        await Assert.That(slot).IsEqualTo(new PartAddress("Leg", 7));
        await Assert.That(GoalKeys.TryResolveImplantSlot(goals, "p9:BionicLeg:Leg#0",
            out _, out _)).IsFalse();
    }
}
