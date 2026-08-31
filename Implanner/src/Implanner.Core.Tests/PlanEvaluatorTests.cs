using System;
using System.Collections.Generic;
using Implanner.Core;

namespace Implanner.Core.Tests;

/// Behavioral coverage for implant matching and progress: exact-first
/// matching with superior substitutes, per-slot selection, anatomy blocking,
/// and aggregate pawn state.
public class PlanEvaluatorTests
{
    static readonly IReadOnlyList<InstalledImplant> NoImplants = Array.Empty<InstalledImplant>();

    static Plan ImplantPlan(params ImplantGoal[] goals)
    {
        var plan = new Plan(1, "Test");
        plan.Implants.AddRange(goals);
        return plan;
    }

    static PlanEvaluation Evaluate(
        Plan plan,
        IReadOnlyList<InstalledImplant>? implants = null,
        IReadOnlyList<ImplantContext>? implantContexts = null,
        bool away = false)
    {
        return PlanEvaluator.Evaluate(
            plan.Implants,
            implants ?? NoImplants,
            implantContexts ?? Array.Empty<ImplantContext>(),
            away);
    }

    static readonly int[] BothSlots = { 0, 1 };
    static readonly int[] FirstSlot = { 0 };

    [Test]
    public async Task ExactImplantMatchesBeforeSuperiorSubstitute()
    {
        // Pawn has an archotech arm (left) and a bionic arm (right). The
        // bionic goal on the right slot matches exactly and the archotech
        // goal consumes the archotech arm, not the reverse.
        var plan = ImplantPlan(
            new ImplantGoal(1, "BionicArm", new[] { 1 }),
            new ImplantGoal(2, "ArchotechArm", FirstSlot));
        var installed = new[]
        {
            new InstalledImplant("ArchotechArm", "Shoulder/Left", 1.5f),
            new InstalledImplant("BionicArm", "Shoulder/Right", 1.25f),
        };
        var arms = new[] { "Shoulder/Left", "Shoulder/Right" };
        var contexts = new[]
        {
            new ImplantContext(arms, 1.25f),
            new ImplantContext(arms, 1.5f),
        };

        var result = Evaluate(plan, installed, contexts);

        await Assert.That(result.Implants[0].IsComplete).IsTrue();
        await Assert.That(result.Implants[1].IsComplete).IsTrue();
        await Assert.That(result.State).IsEqualTo(PawnPlanState.Complete);
    }

    [Test]
    public async Task OneSuperiorImplantCannotSatisfyTwoGoals()
    {
        // Both goals select the left shoulder; the single archotech arm there
        // can substitute for only one of them.
        var plan = ImplantPlan(
            new ImplantGoal(1, "BionicArm", FirstSlot),
            new ImplantGoal(2, "PowerClaw", FirstSlot));
        var installed = new[]
        {
            new InstalledImplant("ArchotechArm", "Shoulder/Left", 1.5f),
        };
        var arms = new[] { "Shoulder/Left", "Shoulder/Right" };
        var contexts = new[]
        {
            new ImplantContext(arms, 1.25f),
            new ImplantContext(arms, 1.1f),
        };

        var result = Evaluate(plan, installed, contexts);

        int satisfied = result.Implants[0].Satisfied + result.Implants[1].Satisfied;
        await Assert.That(satisfied).IsEqualTo(1);
    }

    [Test]
    public async Task SubstituteRequiresSelectedSlotAndSufficientEfficiency()
    {
        var plan = ImplantPlan(new ImplantGoal(1, "BionicEye", BothSlots));
        var installed = new[]
        {
            // Superior efficiency but a different slot: must not count.
            new InstalledImplant("ArchotechArm", "Shoulder/Left", 1.5f),
            // Selected slot but inferior efficiency: must not count.
            new InstalledImplant("SimpleEye", "Eye/Left", 0.9f),
            // Selected slot, superior efficiency: counts.
            new InstalledImplant("ArchotechEye", "Eye/Right", 1.6f),
        };
        var contexts = new[]
        {
            new ImplantContext(new[] { "Eye/Left", "Eye/Right" }, 1.3f),
        };

        var result = Evaluate(plan, installed, contexts);

        await Assert.That(result.Implants[0].Satisfied).IsEqualTo(1);
        await Assert.That(result.Implants[0].Missing).IsEqualTo(1);
        await Assert.That(result.SatisfiedUnits).IsEqualTo(1);
        await Assert.That(result.TotalUnits).IsEqualTo(2);
        await Assert.That(result.State).IsEqualTo(PawnPlanState.Active);
    }

    [Test]
    public async Task ImplantInUnselectedSlotDoesNotSatisfyTheGoal()
    {
        // The goal selects only the left leg; a bionic in the right leg does
        // not count.
        var plan = ImplantPlan(new ImplantGoal(1, "BionicLeg", FirstSlot));
        var installed = new[]
        {
            new InstalledImplant("BionicLeg", "Leg/Right", 1.25f),
        };
        var contexts = new[]
        {
            new ImplantContext(new[] { "Leg/Left", "Leg/Right" }, 1.25f),
        };

        var result = Evaluate(plan, installed, contexts);

        await Assert.That(result.Implants[0].Satisfied).IsEqualTo(0);
        await Assert.That(result.Implants[0].Missing).IsEqualTo(1);
    }

    [Test]
    public async Task SlotBeyondApplicableAnatomyIsBlocked()
    {
        var plan = ImplantPlan(new ImplantGoal(1, "BionicArm", BothSlots));
        var contexts = new[]
        {
            new ImplantContext(new[] { "Shoulder/Left" }, 1.25f),
        };

        var result = Evaluate(plan, implantContexts: contexts);

        await Assert.That(result.Implants[0].Blocked).IsEqualTo(1);
        await Assert.That(result.Implants[0].Missing).IsEqualTo(1);
        await Assert.That(result.Implants[0].Blocker).IsEqualTo(GoalBlocker.Anatomy);
    }

    [Test]
    public async Task SatisfiedSlotsPublishTheirGoalKeys()
    {
        var plan = ImplantPlan(new ImplantGoal(3, "BionicLeg", BothSlots));
        var installed = new[]
        {
            new InstalledImplant("BionicLeg", "Leg/Left", 1.25f),
        };
        var contexts = new[]
        {
            new ImplantContext(new[] { "Leg/Left", "Leg/Right" }, 1.25f),
        };

        var result = Evaluate(plan, installed, contexts);

        await Assert.That(result.SatisfiedGoalKeys).IsEquivalentTo(new[] { "i3:0" });
    }

    // Same-slot exclusivity stand-ins for the game-side conflict facts:
    // brain-style implants coexist, artificial parts occupy their slot.
    static readonly Func<string, string, bool> Coexisting = static (_, _) => false;
    static readonly Func<string, string, bool> Occupying = static (_, _) => true;

    [Test]
    public async Task CoexistingImplantDoesNotSatisfyASecondSamePartGoal()
    {
        // Two different brain implants are planned on the same slot; they
        // coexist, so installing the first must leave the second goal
        // missing — automation still has to produce, reserve, and schedule
        // it. (Both kinds carry the default 1.0 efficiency, so an
        // efficiency-floor check alone cannot tell them apart.)
        var goals = new[]
        {
            new ImplantGoal(1, "Neurocalculator", FirstSlot),
            new ImplantGoal(2, "CircadianAssistant", FirstSlot),
        };
        var installed = new[]
        {
            new InstalledImplant("Neurocalculator", "Brain", 1f),
        };
        var contexts = new[]
        {
            new ImplantContext(new[] { "Brain" }, 1f),
            new ImplantContext(new[] { "Brain" }, 1f),
        };

        var result = PlanEvaluator.Evaluate(goals, installed, contexts,
            away: false, latchedKeys: null, sameSlotExclusive: Coexisting);
        await Assert.That(result.Implants[0].IsComplete).IsTrue();
        await Assert.That(result.Implants[1].Missing).IsEqualTo(1);
        await Assert.That(result.State).IsEqualTo(PawnPlanState.Active);
        await Assert.That(result.SatisfiedGoalKeys).IsEquivalentTo(new[] { "i1:0" });

        var missing = PlanEvaluator.MissingImplantSlotKeys(goals, installed,
            contexts, latchedKeys: null, sameSlotExclusive: Coexisting);
        await Assert.That(missing).IsEquivalentTo(new[] { "i2:0" });
    }

    [Test]
    public async Task OccupyingSuperiorImplantSatisfiesInsteadOfBeingReplaced()
    {
        // The player manually installed an archotech leg where the plan
        // wants a bionic leg. The archotech part occupies the slot and is
        // at least as efficient, so the goal is satisfied — automation must
        // never schedule a replacement of the better part.
        var goals = new[] { new ImplantGoal(1, "BionicLeg", FirstSlot) };
        var installed = new[]
        {
            new InstalledImplant("ArchotechLeg", "Leg/Left", 1.5f),
        };
        var contexts = new[]
        {
            new ImplantContext(new[] { "Leg/Left" }, 1.25f),
        };

        var result = PlanEvaluator.Evaluate(goals, installed, contexts,
            away: false, latchedKeys: null, sameSlotExclusive: Occupying);
        await Assert.That(result.Implants[0].IsComplete).IsTrue();

        var missing = PlanEvaluator.MissingImplantSlotKeys(goals, installed,
            contexts, latchedKeys: null, sameSlotExclusive: Occupying);
        await Assert.That(missing).IsEmpty();
    }

    [Test]
    public async Task InferiorOccupantIsStillReplaced()
    {
        // A peg leg occupies the slot but sits far below the bionic goal's
        // efficiency floor: the slot stays missing so surgery replaces it.
        var goals = new[] { new ImplantGoal(1, "BionicLeg", FirstSlot) };
        var installed = new[]
        {
            new InstalledImplant("PegLeg", "Leg/Left", 0.6f),
        };
        var contexts = new[]
        {
            new ImplantContext(new[] { "Leg/Left" }, 1.25f),
        };

        var missing = PlanEvaluator.MissingImplantSlotKeys(goals, installed,
            contexts, latchedKeys: null, sameSlotExclusive: Occupying);
        await Assert.That(missing).IsEquivalentTo(new[] { "i1:0" });
    }

    [Test]
    public async Task AwayOverridesEvaluationState()
    {
        var plan = ImplantPlan(new ImplantGoal(1, "BionicLeg", FirstSlot));
        var contexts = new[]
        {
            new ImplantContext(new[] { "Leg/Left" }, 1.25f),
        };

        var result = Evaluate(plan, implantContexts: contexts, away: true);

        await Assert.That(result.State).IsEqualTo(PawnPlanState.Away);
    }

    [Test]
    public async Task EmptyPlanIsCompleteWithFullProgress()
    {
        var plan = new Plan(1, "Empty");

        var result = Evaluate(plan);

        await Assert.That(result.State).IsEqualTo(PawnPlanState.Complete);
        await Assert.That(result.Progress).IsEqualTo(1f);
    }
}
