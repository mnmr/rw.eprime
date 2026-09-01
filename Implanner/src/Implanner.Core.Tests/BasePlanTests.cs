using System.Collections.Generic;
using Implanner.Core;

namespace Implanner.Core.Tests;

/// Plan extension: a plan's effective goals are its own plus the base
/// chain's, own selections override overlapping slots, goal keys stay
/// stable, and losing the base plan detaches cleanly.
public class BasePlanTests
{
    int nextPlan = 1;

    int TakePlanId() => nextPlan++;

    [Test]
    public async Task EffectiveGoalsMergeOwnAndInheritedWithOwnOverriding()
    {
        var model = new PlannerModel();
        var basePlan = model.CreatePlan("Base", TakePlanId)!;
        model.SetImplantSlot(basePlan.Id, "BionicLeg", 0, true);
        model.SetImplantSlot(basePlan.Id, "BionicLeg", 1, true);
        model.SetImplantSlot(basePlan.Id, "BionicEye", 0, true);
        var derived = model.CreatePlan("Derived", TakePlanId, basePlan.Id)!;
        // Re-include the left leg (ordinal 0) as an own goal.
        model.SetImplantSlot(derived.Id, "BionicLeg", 0, true);
        model.SetImplantSlot(derived.Id, "BionicArm", 0, true);

        IReadOnlyList<ImplantGoal> effective = model.EffectiveImplants(derived);

        // Own goals first (leg ordinal 0, arm), then the base leg goal with
        // the overridden slot removed, then the untouched base eye goal.
        await Assert.That(effective.Count).IsEqualTo(4);
        await Assert.That(effective[0].ImplantDefName).IsEqualTo("BionicLeg");
        await Assert.That(effective[0].SlotOrdinals).IsEquivalentTo(new[] { 0 });
        await Assert.That(effective[1].ImplantDefName).IsEqualTo("BionicArm");
        // The inherited leg goal keeps its base plan identity but loses
        // ordinal 0.
        await Assert.That(effective[2].PlanId).IsEqualTo(basePlan.Id);
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
        model.SetImplantSlot(basePlan.Id, "BionicEye", 0, true);
        var derived = model.CreatePlan("Derived", TakePlanId, basePlan.Id)!;
        model.SetImplantSlot(derived.Id, "BionicEye", 0, true);

        IReadOnlyList<ImplantGoal> effective = model.EffectiveImplants(derived);

        await Assert.That(effective.Count).IsEqualTo(1);
        await Assert.That(effective[0].PlanId).IsEqualTo(derived.Id);
    }

    [Test]
    public async Task ChainsInheritThroughIntermediatePlans()
    {
        var model = new PlannerModel();
        var grandBase = model.CreatePlan("A", TakePlanId)!;
        model.SetImplantSlot(grandBase.Id, "BionicEye", 0, true);
        var middle = model.CreatePlan("B", TakePlanId, grandBase.Id)!;
        model.SetImplantSlot(middle.Id, "BionicArm", 0, true);
        var leaf = model.CreatePlan("C", TakePlanId, middle.Id)!;

        IReadOnlyList<ImplantGoal> effective = model.EffectiveImplants(leaf);

        await Assert.That(effective.Count).IsEqualTo(2);
        await Assert.That(effective[0].ImplantDefName).IsEqualTo("BionicArm");
        await Assert.That(effective[1].ImplantDefName).IsEqualTo("BionicEye");
    }

    /// Same-record stand-in: both stomach kinds target ordinal 0.
    static bool StomachResolver(ImplantGoal a, int ordA, ImplantGoal b, int ordB) =>
        (a.ImplantDefName == "NuclearStomach" || a.ImplantDefName == "DetoxifierStomach")
        && (b.ImplantDefName == "NuclearStomach" || b.ImplantDefName == "DetoxifierStomach");

    /// Two inherited goals conflict across base levels: the nearer base's
    /// choice is accepted first, so the grand-base's stomach is suppressed
    /// while its unrelated eye still inherits. The leaf itself picks
    /// neither.
    [Test]
    public async Task NearerBaseWinsAnInheritedVersusInheritedConflict()
    {
        var model = new PlannerModel();
        model.SetSlotConflictResolver(StomachResolver);
        var grandBase = model.CreatePlan("A", TakePlanId)!;
        model.SetImplantSlot(grandBase.Id, "NuclearStomach", 0, true);
        model.SetImplantSlot(grandBase.Id, "BionicEye", 0, true);
        var middle = model.CreatePlan("B", TakePlanId, grandBase.Id)!;
        model.SetImplantSlot(middle.Id, "DetoxifierStomach", 0, true);
        var leaf = model.CreatePlan("C", TakePlanId, middle.Id)!;
        model.SetImplantSlot(leaf.Id, "BionicArm", 0, true);

        IReadOnlyList<ImplantGoal> effective = model.EffectiveImplants(leaf);

        await Assert.That(effective.Count).IsEqualTo(3);
        await Assert.That(effective[0].ImplantDefName).IsEqualTo("BionicArm");
        await Assert.That(effective[1].ImplantDefName).IsEqualTo("DetoxifierStomach");
        await Assert.That(effective[1].PlanId).IsEqualTo(middle.Id);
        await Assert.That(effective[2].ImplantDefName).IsEqualTo("BionicEye");
        await Assert.That(effective[2].PlanId).IsEqualTo(grandBase.Id);
    }

    /// Base links are set only at creation, so a cycle can only come from
    /// corrupted save data. The chain walk must still terminate and yield
    /// each plan's own goals plus every other plan on the loop once.
    [Test]
    public async Task ACorruptedBaseCycleTerminates()
    {
        var model = new PlannerModel();
        model.AddLoadedPlan(new Plan(1, "X", basePlanId: 2, new List<ImplantGoal>
        {
            new ImplantGoal(1, "BionicEye", new[] { 0 }),
        }));
        model.AddLoadedPlan(new Plan(2, "Y", basePlanId: 1, new List<ImplantGoal>
        {
            new ImplantGoal(2, "BionicArm", new[] { 0 }),
        }));

        IReadOnlyList<ImplantGoal> x = model.EffectiveImplants(model.Plans[0]);
        IReadOnlyList<ImplantGoal> y = model.EffectiveImplants(model.Plans[1]);

        await Assert.That(x.Count).IsEqualTo(2);
        await Assert.That(x[0].ImplantDefName).IsEqualTo("BionicEye");
        await Assert.That(x[1].ImplantDefName).IsEqualTo("BionicArm");
        await Assert.That(y.Count).IsEqualTo(2);
        await Assert.That(y[0].ImplantDefName).IsEqualTo("BionicArm");
        await Assert.That(y[1].ImplantDefName).IsEqualTo("BionicEye");
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
        model.SetImplantSlot(basePlan.Id, "BionicEye", 0, true);
        var derived = model.CreatePlan("Derived", TakePlanId, basePlan.Id)!;
        model.SetImplantSlot(derived.Id, "BionicArm", 0, true);

        model.DeletePlan(basePlan.Id);

        await Assert.That(derived.BasePlanId).IsEqualTo(0);
        IReadOnlyList<ImplantGoal> effective = model.EffectiveImplants(derived);
        await Assert.That(effective.Count).IsEqualTo(1);
        await Assert.That(effective[0].ImplantDefName).IsEqualTo("BionicArm");
    }

    [Test]
    public async Task CleanupDetachesPlansWhoseLoadedBaseIsMissing()
    {
        var model = new PlannerModel();
        var plan = new Plan(1, "Loaded", basePlanId: 999, new List<ImplantGoal>());
        model.AddLoadedPlan(plan);

        var change = model.CleanupMissing(pawnExists: _ => true);

        await Assert.That((change & PlannerChange.Plans) != 0).IsTrue();
        await Assert.That(plan.BasePlanId).IsEqualTo(0);
    }
}
