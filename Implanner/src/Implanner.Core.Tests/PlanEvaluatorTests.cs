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

    static Plan ImplantPlan(params ImplantGoal[] goals) =>
        new Plan(1, "Test", 0, new List<ImplantGoal>(goals));

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
    public async Task ImpossibleSlotsAreExcludedFromTheTarget()
    {
        // The body has one shoulder; the second selected slot can never be
        // installed, so it neither completes nor counts as missing.
        var plan = ImplantPlan(new ImplantGoal(1, "BionicArm", BothSlots));
        var contexts = new[]
        {
            new ImplantContext(new[] { "Shoulder/Left" }, 1.25f),
        };

        var result = Evaluate(plan, implantContexts: contexts);

        await Assert.That(result.Implants[0].Requested).IsEqualTo(1);
        await Assert.That(result.Implants[0].Missing).IsEqualTo(1);
        await Assert.That(result.TotalUnits).IsEqualTo(1);
        await Assert.That(result.State).IsEqualTo(PawnPlanState.Active);
    }

    [Test]
    public async Task AWhollyImpossiblePlanCountsAsComplete()
    {
        // No applicable anatomy at all: zero target, full progress, and the
        // colonist never shows as having outstanding work.
        var plan = ImplantPlan(new ImplantGoal(1, "BionicArm", FirstSlot));
        var contexts = new[]
        {
            new ImplantContext(System.Array.Empty<string>(), 1.25f),
        };

        var result = Evaluate(plan, implantContexts: contexts);

        await Assert.That(result.Implants[0].Requested).IsEqualTo(0);
        await Assert.That(result.TotalUnits).IsEqualTo(0);
        await Assert.That(result.Progress).IsEqualTo(1f);
        await Assert.That(result.State).IsEqualTo(PawnPlanState.Complete);
    }

    [Test]
    public async Task SatisfiedSlotConsumesItsImplantExactlyOnce()
    {
        // One installed leg covers exactly one of the two selected slots:
        // the other stays demanded.
        var plan = ImplantPlan(new ImplantGoal(3, "BionicLeg", BothSlots));
        var installed = new[]
        {
            new InstalledImplant("BionicLeg", "Leg/Left", 1.25f),
        };
        var contexts = new[]
        {
            new ImplantContext(new[] { "Leg/Left", "Leg/Right" }, 1.25f),
        };

        var missing = PlanEvaluator.MissingImplantSlotKeys(
            plan.Implants, installed, contexts);

        await Assert.That(missing).IsEquivalentTo(new[] { "p3:BionicLeg:1" });
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
            new ImplantGoal(1, "CircadianAssistant", FirstSlot),
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
            away: false, sameSlotExclusive: Coexisting);
        await Assert.That(result.Implants[0].IsComplete).IsTrue();
        await Assert.That(result.Implants[1].Missing).IsEqualTo(1);
        await Assert.That(result.State).IsEqualTo(PawnPlanState.Active);

        var missing = PlanEvaluator.MissingImplantSlotKeys(goals, installed,
            contexts, sameSlotExclusive: Coexisting);
        await Assert.That(missing).IsEquivalentTo(
            new[] { "p1:CircadianAssistant:0" });
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
            away: false, sameSlotExclusive: Occupying);
        await Assert.That(result.Implants[0].IsComplete).IsTrue();

        var missing = PlanEvaluator.MissingImplantSlotKeys(goals, installed,
            contexts, sameSlotExclusive: Occupying);
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
            contexts, sameSlotExclusive: Occupying);
        await Assert.That(missing).IsEquivalentTo(new[] { "p1:BionicLeg:0" });
    }

    /// An inherited goal evaluates exactly like an own goal but keeps its
    /// base plan's identity: a superior occupant satisfies its left leg,
    /// and the missing right leg is keyed by the BASE plan's id.
    [Test]
    public async Task InheritedGoalsSubstituteAndKeyByTheirBasePlan()
    {
        var model = new PlannerModel();
        int next = 1;
        var basePlan = model.CreatePlan("Base", () => next++)!;
        model.SetImplantSlot(basePlan.Id, "BionicLeg", 0, true);
        model.SetImplantSlot(basePlan.Id, "BionicLeg", 1, true);
        var derived = model.CreatePlan("Derived", () => next++, basePlan.Id)!;
        model.SetImplantSlot(derived.Id, "BionicArm", 0, true);
        IReadOnlyList<ImplantGoal> goals = model.EffectiveImplants(derived);
        var installed = new[]
        {
            new InstalledImplant("ArchotechLeg", "Leg/Left", 1.5f),
        };
        var contexts = new[]
        {
            new ImplantContext(new[] { "Shoulder/Left", "Shoulder/Right" }, 1.25f),
            new ImplantContext(new[] { "Leg/Left", "Leg/Right" }, 1.25f),
        };

        var result = PlanEvaluator.Evaluate(goals, installed, contexts,
            away: false, sameSlotExclusive: Occupying);
        var missing = PlanEvaluator.MissingImplantSlotKeys(goals, installed,
            contexts, sameSlotExclusive: Occupying);

        await Assert.That(result.Implants[0].Missing).IsEqualTo(1);
        await Assert.That(result.Implants[1].Satisfied).IsEqualTo(1);
        await Assert.That(result.Implants[1].Missing).IsEqualTo(1);
        await Assert.That(missing).IsEquivalentTo(
            new[] { "p2:BionicArm:0", "p1:BionicLeg:1" });
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
