using Implanner.Core;

namespace Implanner.Core.Tests;

/// Plan ids are identity: goal keys, assignments, and base links embed
/// them, so a save whose id counter was lost (older builds did not persist
/// it) must not reissue an existing id, and a duplicated id must be
/// repaired deterministically.
public class LoadNormalizationTests
{
    [Test]
    public async Task LoadClampsThePlanCounterAboveExistingIds()
    {
        var model = new PlannerModel();
        var plan = new Plan(7, "Full");
        plan.Implants.Add(new ImplantGoal(7, "BionicLeg", new[] { 0, 1 }));
        model.AddLoadedPlan(plan);
        int nextPlanId = 1;   // pre-counter save default

        model.NormalizeLoadedIds(ref nextPlanId);

        await Assert.That(nextPlanId).IsEqualTo(8);
    }

    [Test]
    public async Task DuplicatedPlanIdsAreReassignedWithRestampedGoals()
    {
        var model = new PlannerModel();
        var first = new Plan(1, "Full");
        first.Implants.Add(new ImplantGoal(1, "LearningAssistant", new[] { 0 }));
        var second = new Plan(1, "Copy");
        second.Implants.Add(new ImplantGoal(1, "BionicLeg", new[] { 0, 1 }));
        model.AddLoadedPlan(first);
        model.AddLoadedPlan(second);
        int nextPlanId = 2;

        model.NormalizeLoadedIds(ref nextPlanId);

        await Assert.That(model.Plans[0].Id).IsEqualTo(1);
        await Assert.That(model.Plans[1].Id).IsEqualTo(2);
        await Assert.That(model.Plans[1].Implants[0].PlanId).IsEqualTo(2);
        await Assert.That(nextPlanId).IsEqualTo(3);
    }
}
