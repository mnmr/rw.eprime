using System;
using System.Collections.Generic;
using Implanner.Core;

namespace Implanner.Core.Tests;

/// The delivery-latch model: a goal delivered once never re-enters the
/// pipeline; later loss shows as regression, and an explicit re-enlist
/// clears exactly the unsatisfied latched goals.
public class DeliveryLatchTests
{
    [Test]
    public async Task LatchedSlotLostLaterShowsRegressedNotMissing()
    {
        var plan = new Plan(1, "Test");
        plan.Implants.Add(new ImplantGoal(1, "BionicLeg", new[] { 0 }));
        var latched = new HashSet<string>(StringComparer.Ordinal)
        {
            GoalKeys.ImplantSlot(1, 0),
        };
        var contexts = new[]
        {
            new ImplantContext(new[] { "Leg/Left" }, 1.25f),
        };

        var result = PlanEvaluator.Evaluate(plan.Implants,
            Array.Empty<InstalledImplant>(),
            contexts, away: false, latched);

        await Assert.That(result.Implants[0].Regressed).IsEqualTo(1);
        await Assert.That(result.Implants[0].Missing).IsEqualTo(0);
        await Assert.That(result.State).IsEqualTo(PawnPlanState.Regressed);
    }

    [Test]
    public async Task ActionableWorkOutranksRegressionInPawnState()
    {
        var plan = new Plan(1, "Test");
        plan.Implants.Add(new ImplantGoal(1, "BionicLeg", new[] { 0 }));
        plan.Implants.Add(new ImplantGoal(2, "BionicArm", new[] { 0 }));
        var latched = new HashSet<string>(StringComparer.Ordinal)
        {
            GoalKeys.ImplantSlot(1, 0),
        };
        var contexts = new[]
        {
            new ImplantContext(new[] { "Leg/Left" }, 1.25f),
            new ImplantContext(new[] { "Shoulder/Left" }, 1.25f),
        };

        var result = PlanEvaluator.Evaluate(plan.Implants,
            Array.Empty<InstalledImplant>(),
            contexts, away: false, latched);

        await Assert.That(result.State).IsEqualTo(PawnPlanState.Active);
    }

    [Test]
    public async Task LatchedImplantSlotRegressesIndividually()
    {
        var plan = new Plan(1, "Test");
        plan.Implants.Add(new ImplantGoal(1, "BionicLeg", new[] { 0, 1 }));
        var latched = new HashSet<string>(StringComparer.Ordinal)
        {
            GoalKeys.ImplantSlot(1, 0),
        };
        var contexts = new[]
        {
            new ImplantContext(new[] { "Leg/Left", "Leg/Right" }, 1.25f),
        };

        var result = PlanEvaluator.Evaluate(plan.Implants,
            Array.Empty<InstalledImplant>(),
            contexts, away: false, latched);

        await Assert.That(result.Implants[0].Regressed).IsEqualTo(1);
        await Assert.That(result.Implants[0].Missing).IsEqualTo(1);
    }

    [Test]
    public async Task LatchIsIdempotentAndReEnlistClearsExactKeys()
    {
        var model = new PlannerModel();
        int next = 1;
        var plan = model.CreatePlan("Test", () => next++)!;
        model.AssignPlan(7, plan.Id);

        await Assert.That(model.Latch(7, "i1:0")).IsEqualTo(PlannerChange.Latches);
        await Assert.That(model.Latch(7, "i1:0")).IsEqualTo(PlannerChange.None);
        model.Latch(7, "i2:0");

        // Re-enlist returns only the named (unsatisfied) goals to the
        // pipeline; still-satisfied latches stay latched.
        await Assert.That(model.ReEnlist(7, new[] { "i1:0" })).IsEqualTo(PlannerChange.Latches);
        await Assert.That(model.IsLatched(7, "i1:0")).IsFalse();
        await Assert.That(model.IsLatched(7, "i2:0")).IsTrue();
        await Assert.That(model.ReEnlist(7, new[] { "i1:0" })).IsEqualTo(PlannerChange.None);
    }

    [Test]
    public async Task PruneLatchesDropsKeysOfRemovedGoals()
    {
        var model = new PlannerModel();
        int next = 1;
        var plan = model.CreatePlan("Test", () => next++)!;
        model.AssignPlan(7, plan.Id);
        model.Latch(7, "i1:0");
        model.Latch(7, "i2:0");

        // Goal 1 was removed from the plan; only goal 2 slot 0 remains.
        var goals = new[] { new ImplantGoal(2, "BionicLeg", new[] { 0 }) };
        var change = model.PruneLatches(7, goals);

        await Assert.That(change).IsEqualTo(PlannerChange.Latches);
        await Assert.That(model.IsLatched(7, "i1:0")).IsFalse();
        await Assert.That(model.IsLatched(7, "i2:0")).IsTrue();
        await Assert.That(model.PruneLatches(7, goals)).IsEqualTo(PlannerChange.None);
    }
}
