using System;
using System.Collections.Generic;
using Implanner.Core;

namespace Implanner.Core.Tests;

/// Implant-combination rules, mirroring the game's surgery workers: one part
/// per slot, replacements clear their subtree, implants never mount on added
/// parts, and incompatible hediff tags (skin glands) exclude each other on
/// the same part. Also covers conflict suppression in plan inheritance: a
/// derived plan's choice wins over a conflicting inherited goal.
public class ImplantConflictTests
{
    static readonly string[] NoTags = Array.Empty<string>();
    static readonly int[] NoAncestors = Array.Empty<int>();

    static PlannedSlotFacts Facts(string defName, bool replacement, int record,
        int[]? ancestors = null, string[]? tags = null, string[]? incompatible = null)
        => new PlannedSlotFacts(defName, replacement, record,
            ancestors ?? NoAncestors, tags ?? NoTags, incompatible ?? NoTags);

    [Test]
    public async Task TwoReplacementsOnTheSameSlotConflict()
    {
        // Only one stomach: nuclear vs detoxifier both replace record 12.
        var nuclear = Facts("NuclearStomach", replacement: true, record: 12);
        var detoxifier = Facts("DetoxifierStomach", replacement: true, record: 12);

        await Assert.That(ImplantConflictRules.Conflicts(nuclear, detoxifier)).IsTrue();
    }

    [Test]
    public async Task ReplacementAndImplantOnTheSameSlotConflict()
    {
        // A knee spike mounts on the leg; a bionic leg replaces it. The game
        // refuses implants on added parts and destroys mounted hediffs when
        // the part is replaced, so the pair can never coexist.
        var bionicLeg = Facts("BionicLeg", replacement: true, record: 20);
        var kneeSpike = Facts("KneeSpike", replacement: false, record: 20);

        await Assert.That(ImplantConflictRules.Conflicts(bionicLeg, kneeSpike)).IsTrue();
        await Assert.That(ImplantConflictRules.Conflicts(kneeSpike, bionicLeg)).IsTrue();
    }

    [Test]
    public async Task ImplantsOnTheSameSlotCoexistWithoutTagClashes()
    {
        // Multiple brain implants co-exist on the brain record.
        var neurocalculator = Facts("Neurocalculator", replacement: false, record: 3);
        var learningAssistant = Facts("LearningAssistant", replacement: false, record: 3);

        await Assert.That(ImplantConflictRules.Conflicts(
            neurocalculator, learningAssistant)).IsFalse();
    }

    [Test]
    public async Task IncompatibleHediffTagsConflictOnTheSameSlot()
    {
        // Skin glands: each recipe declares the SkinGland tag incompatible
        // and each hediff carries it (case-insensitive, like the game).
        var armorskin = Facts("ArmorskinGland", replacement: false, record: 7,
            tags: new[] { "SkinGland" }, incompatible: new[] { "skingland" });
        var fireskin = Facts("FireskinGland", replacement: false, record: 7,
            tags: new[] { "SkinGland" }, incompatible: new[] { "SkinGland" });

        await Assert.That(ImplantConflictRules.Conflicts(armorskin, fireskin)).IsTrue();
    }

    [Test]
    public async Task ReplacementConflictsWithGoalsInItsSubtree()
    {
        // A bionic arm at the shoulder clears the hand under it.
        var bionicArm = Facts("BionicArm", replacement: true, record: 10);
        var handImplant = Facts("HandImplant", replacement: false, record: 14,
            ancestors: new[] { 13, 10, 0 });
        var unrelated = Facts("BionicEye", replacement: true, record: 5,
            ancestors: new[] { 2, 0 });

        await Assert.That(ImplantConflictRules.Conflicts(bionicArm, handImplant)).IsTrue();
        await Assert.That(ImplantConflictRules.Conflicts(handImplant, bionicArm)).IsTrue();
        await Assert.That(ImplantConflictRules.Conflicts(bionicArm, unrelated)).IsFalse();
    }

    /// Same-record slot map for the suppression test: both stomach kinds
    /// target ordinal 0 = record 12.
    static bool StomachResolver(ImplantGoal a, int ordA, ImplantGoal b, int ordB) =>
        (a.ImplantDefName == "NuclearStomach" || a.ImplantDefName == "DetoxifierStomach")
        && (b.ImplantDefName == "NuclearStomach" || b.ImplantDefName == "DetoxifierStomach");

    /// The click IS the choice: selecting a slot that can never coexist
    /// with an own selection deselects the previous pick instead of leaving
    /// a dead goal behind.
    [Test]
    public async Task SelectingAConflictingSlotDeselectsThePreviousOwnPick()
    {
        var model = new PlannerModel { SlotConflictResolver = StomachResolver };
        int nextPlan = 1, nextGoal = 1;
        var plan = model.CreatePlan("Test", () => nextPlan++)!;
        model.SetImplantSlot(plan.Id, "NuclearStomach", 0, true, () => nextGoal++);
        model.SetImplantSlot(plan.Id, "BionicEye", 0, true, () => nextGoal++);

        var change = model.SetImplantSlot(
            plan.Id, "DetoxifierStomach", 0, true, () => nextGoal++);

        await Assert.That(change).IsEqualTo(PlannerChange.Plans);
        await Assert.That(plan.Implants.Count).IsEqualTo(2);
        await Assert.That(plan.Implants[0].ImplantDefName).IsEqualTo("BionicEye");
        await Assert.That(plan.Implants[1].ImplantDefName).IsEqualTo("DetoxifierStomach");
    }

    [Test]
    public async Task DerivedPlanChoiceSuppressesConflictingInheritedGoal()
    {
        var model = new PlannerModel { SlotConflictResolver = StomachResolver };
        int nextPlan = 1, nextGoal = 1;
        var basePlan = model.CreatePlan("Base", () => nextPlan++)!;
        model.SetImplantSlot(basePlan.Id, "NuclearStomach", 0, true, () => nextGoal++);
        model.SetImplantSlot(basePlan.Id, "BionicEye", 0, true, () => nextGoal++);
        var derived = model.CreatePlan("Derived", () => nextPlan++, basePlan.Id)!;
        model.SetImplantSlot(derived.Id, "DetoxifierStomach", 0, true, () => nextGoal++);

        List<ImplantGoal> effective = model.EffectiveImplants(derived);

        // The derived stomach wins; the unrelated eye still inherits.
        await Assert.That(effective.Count).IsEqualTo(2);
        await Assert.That(effective[0].ImplantDefName).IsEqualTo("DetoxifierStomach");
        await Assert.That(effective[1].ImplantDefName).IsEqualTo("BionicEye");

        // The base plan itself is untouched.
        await Assert.That(model.EffectiveImplants(basePlan).Count).IsEqualTo(2);
    }
}
