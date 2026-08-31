using System.Collections.Generic;
using Implanner.Core;

namespace Implanner.Core.Tests;

/// Plan extension: a plan's effective goals are its own plus the base
/// chain's, own selections override overlapping slots, goal keys stay
/// stable, and losing the base plan detaches cleanly.
public class BasePlanTests
{
    int nextPlan = 1;
    int nextGoal = 1;

    int TakePlanId() => nextPlan++;
    int TakeGoalId() => nextGoal++;

    [Test]
    public async Task EffectiveGoalsMergeOwnAndInheritedWithOwnOverriding()
    {
        var model = new PlannerModel();
        var basePlan = model.CreatePlan("Base", TakePlanId)!;
        model.SetImplantSlot(basePlan.Id, "BionicLeg", 0, true, TakeGoalId);
        model.SetImplantSlot(basePlan.Id, "BionicLeg", 1, true, TakeGoalId);
        model.SetImplantSlot(basePlan.Id, "BionicEye", 0, true, TakeGoalId);
        var derived = model.CreatePlan("Derived", TakePlanId, basePlan.Id)!;
        // Re-include the left leg (ordinal 0) as an own goal.
        model.SetImplantSlot(derived.Id, "BionicLeg", 0, true, TakeGoalId);
        model.SetImplantSlot(derived.Id, "BionicArm", 0, true, TakeGoalId);

        List<ImplantGoal> effective = model.EffectiveImplants(derived);

        // Own goals first (leg ordinal 0, arm), then the base leg goal with
        // the overridden slot removed, then the untouched base eye goal.
        await Assert.That(effective.Count).IsEqualTo(4);
        await Assert.That(effective[0].ImplantDefName).IsEqualTo("BionicLeg");
        await Assert.That(effective[0].SlotOrdinals).IsEquivalentTo(new[] { 0 });
        await Assert.That(effective[1].ImplantDefName).IsEqualTo("BionicArm");
        // The inherited leg goal keeps its base goal id but loses ordinal 0.
        await Assert.That(effective[2].Id).IsEqualTo(basePlan.Implants[0].Id);
        await Assert.That(effective[2].SlotOrdinals).IsEquivalentTo(new[] { 1 });
        await Assert.That(effective[3].ImplantDefName).IsEqualTo("BionicEye");

        // The base plan's own effective set is untouched by the derivation.
        await Assert.That(model.EffectiveImplants(basePlan).Count).IsEqualTo(2);
    }

    [Test]
    public async Task FullyOverriddenBaseGoalDisappears()
    {
        var model = new PlannerModel();
        var basePlan = model.CreatePlan("Base", TakePlanId)!;
        model.SetImplantSlot(basePlan.Id, "BionicEye", 0, true, TakeGoalId);
        var derived = model.CreatePlan("Derived", TakePlanId, basePlan.Id)!;
        model.SetImplantSlot(derived.Id, "BionicEye", 0, true, TakeGoalId);

        List<ImplantGoal> effective = model.EffectiveImplants(derived);

        await Assert.That(effective.Count).IsEqualTo(1);
        await Assert.That(effective[0].Id).IsEqualTo(derived.Implants[0].Id);
    }

    [Test]
    public async Task ChainsInheritThroughIntermediatePlans()
    {
        var model = new PlannerModel();
        var grandBase = model.CreatePlan("A", TakePlanId)!;
        model.SetImplantSlot(grandBase.Id, "BionicEye", 0, true, TakeGoalId);
        var middle = model.CreatePlan("B", TakePlanId, grandBase.Id)!;
        model.SetImplantSlot(middle.Id, "BionicArm", 0, true, TakeGoalId);
        var leaf = model.CreatePlan("C", TakePlanId, middle.Id)!;

        List<ImplantGoal> effective = model.EffectiveImplants(leaf);

        await Assert.That(effective.Count).IsEqualTo(2);
        await Assert.That(effective[0].ImplantDefName).IsEqualTo("BionicArm");
        await Assert.That(effective[1].ImplantDefName).IsEqualTo("BionicEye");
    }

    [Test]
    public async Task CreateWithMissingBaseFallsBackToStandalone()
    {
        var model = new PlannerModel();

        var plan = model.CreatePlan("Orphan", TakePlanId, basePlanId: 999)!;

        await Assert.That(plan.BasePlanId).IsEqualTo(0);
    }

    [Test]
    public async Task DeletingABasePlanDetachesItsChildren()
    {
        var model = new PlannerModel();
        var basePlan = model.CreatePlan("Base", TakePlanId)!;
        model.SetImplantSlot(basePlan.Id, "BionicEye", 0, true, TakeGoalId);
        var derived = model.CreatePlan("Derived", TakePlanId, basePlan.Id)!;
        model.SetImplantSlot(derived.Id, "BionicArm", 0, true, TakeGoalId);

        model.DeletePlan(basePlan.Id);

        await Assert.That(derived.BasePlanId).IsEqualTo(0);
        List<ImplantGoal> effective = model.EffectiveImplants(derived);
        await Assert.That(effective.Count).IsEqualTo(1);
        await Assert.That(effective[0].ImplantDefName).IsEqualTo("BionicArm");
    }

    [Test]
    public async Task CleanupDetachesPlansWhoseLoadedBaseIsMissing()
    {
        var model = new PlannerModel();
        var plan = new Plan(1, "Loaded") { BasePlanId = 999 };
        model.AddLoadedPlan(plan);

        var change = model.CleanupMissing(pawnExists: _ => true);

        await Assert.That((change & PlannerChange.Plans) != 0).IsTrue();
        await Assert.That(plan.BasePlanId).IsEqualTo(0);
    }
}
