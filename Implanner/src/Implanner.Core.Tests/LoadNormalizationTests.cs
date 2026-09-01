using System.Collections.Generic;
using Implanner.Core;

namespace Implanner.Core.Tests;

/// Plan ids are identity: goal keys, assignments, and base links embed
/// them, so a save whose id counter was lost (older builds did not persist
/// it) must not reissue an existing id, and a duplicated id must be
/// repaired deterministically. Legacy goal keys migrate onto the FINAL ids.
public class LoadNormalizationTests
{
    static Plan LoadedPlan(int id, string name, int basePlanId, params ImplantGoal[] goals)
        => new Plan(id, name, basePlanId, new List<ImplantGoal>(goals));

    [Test]
    public async Task LoadClampsThePlanCounterAboveExistingIds()
    {
        var model = new PlannerModel();
        model.AddLoadedPlan(LoadedPlan(7, "Full", 0,
            new ImplantGoal(7, "BionicLeg", new[] { 0, 1 })));
        int nextPlanId = 1;   // pre-counter save default

        model.NormalizeLoadedIds(ref nextPlanId);

        await Assert.That(nextPlanId).IsEqualTo(8);
    }

    /// Three plans share id 3; the first occurrence keeps it and every
    /// reference to the duplicated id (an assignment, another plan's base
    /// link, and a duplicate's OWN base link) resolves to that one
    /// deterministic owner. Re-idded plans take fresh ids above the whole
    /// loaded set and their goals are restamped.
    [Test]
    public async Task DuplicatedIdsResolveToTheFirstOccurrence()
    {
        var model = new PlannerModel();
        model.AddLoadedPlan(LoadedPlan(3, "A", 0,
            new ImplantGoal(3, "BionicEye", new[] { 0 })));
        model.AddLoadedPlan(LoadedPlan(3, "B", basePlanId: 3,
            new ImplantGoal(3, "BionicArm", new[] { 0 })));
        model.AddLoadedPlan(LoadedPlan(3, "C", 0,
            new ImplantGoal(3, "BionicLeg", new[] { 0, 1 })));
        model.AddLoadedPlan(LoadedPlan(5, "D", basePlanId: 3));
        model.AddLoadedAssignment(1, 3);
        int nextPlanId = 1;

        model.NormalizeLoadedIds(ref nextPlanId);

        Plan a = model.Plans[0], b = model.Plans[1], c = model.Plans[2], d = model.Plans[3];
        await Assert.That(a.Id).IsEqualTo(3);
        await Assert.That(b.Id).IsEqualTo(6);
        await Assert.That(c.Id).IsEqualTo(7);
        await Assert.That(d.Id).IsEqualTo(5);
        await Assert.That(nextPlanId).IsEqualTo(8);
        await Assert.That(c.Implants[0].PlanId).IsEqualTo(7);

        await Assert.That(model.AssignedPlan(1)).IsSameReferenceAs(a);
        await Assert.That(model.PlanById(d.BasePlanId)).IsSameReferenceAs(a);
        // B extended the duplicated id: it now inherits A's eye, not itself.
        var effectiveB = model.EffectiveImplants(b);
        await Assert.That(effectiveB.Count).IsEqualTo(2);
        await Assert.That(effectiveB[0].ImplantDefName).IsEqualTo("BionicArm");
        await Assert.That(effectiveB[1].ImplantDefName).IsEqualTo("BionicEye");
        await Assert.That(effectiveB[1].PlanId).IsEqualTo(3);
    }

    /// Pre-natural-key saves persist per-goal ids and "i{goalId}:{ordinal}"
    /// reservation/bill keys. The legacy map must be built AFTER
    /// NormalizeLoadedIds: a goal whose plan was re-idded migrates onto the
    /// new plan id, so the migrated key still resolves inside that plan.
    /// Natural keys pass through untouched; an unmapped goal id is dropped.
    [Test]
    public async Task LegacyKeysMigrateOntoFinalPlanIds()
    {
        var model = new PlannerModel();
        model.AddLoadedPlan(LoadedPlan(3, "First", 0,
            new ImplantGoal(3, "BionicEye", new[] { 0 })));
        model.AddLoadedPlan(LoadedPlan(3, "Second", 0,
            new ImplantGoal(3, "BionicLeg", new[] { 0, 1 })));
        int nextPlanId = 4;
        model.NormalizeLoadedIds(ref nextPlanId);
        // Legacy goal 17 was the second plan's leg goal (registered by list
        // position while loading; ids read only after normalization).
        var legacy = new Dictionary<int, LegacyGoalRef>
        {
            { 17, new LegacyGoalRef(model.Plans[1].Id, "BionicLeg") },
        };

        string? migrated = GoalKeys.MigrateLegacy("i17:1", legacy);
        string natural = "p3:BionicEye:0";

        await Assert.That(model.Plans[1].Id).IsEqualTo(4);
        await Assert.That(migrated).IsEqualTo("p4:BionicLeg:1");
        await Assert.That(GoalKeys.Contains(model.Plans[1].Implants, migrated!)).IsTrue();
        await Assert.That(GoalKeys.MigrateLegacy(natural, legacy)).IsSameReferenceAs(natural);
        await Assert.That(GoalKeys.MigrateLegacy("i99:0", legacy)).IsNull();
        await Assert.That(GoalKeys.MigrateLegacy("i17:1", null)).IsNull();
    }
}
