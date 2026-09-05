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
        int[]? ancestors = null, string[]? tags = null, string[]? incompatible = null,
        string[]? removeWith = null, bool mountsOnArtificial = false)
        => new PlannedSlotFacts(defName, replacement, record,
            ancestors ?? NoAncestors, tags ?? NoTags, incompatible ?? NoTags,
            removeWith ?? NoTags, mountsOnArtificial);

    /// Bionic modularity modules: their surgery worker does not inherit
    /// vanilla's refusal of artificial parts (they exist to mount ON a
    /// bionic limb), so a module and the bionic arm on the same shoulder
    /// coexist, as does a module under a replacement higher up the limb.
    /// Two modules still exclude each other through their slot tags, and
    /// a plain implant on the same shoulder still loses to the bionic arm.
    [Test]
    public async Task ImplantsWithoutTheArtificialPartRefusalMountOnReplacements()
    {
        var bionicArm = Facts("BionicArm", replacement: true, record: 10);
        var rifleModule = Facts("ChargeRifleModule", replacement: false, record: 10,
            tags: new[] { "ModuleCombat" }, incompatible: new[] { "ModuleCombat" },
            mountsOnArtificial: true);
        var bladeModule = Facts("ElbowBladeModule", replacement: false, record: 10,
            tags: new[] { "ModuleCombat" }, incompatible: new[] { "ModuleCombat" },
            mountsOnArtificial: true);
        var handModule = Facts("HandModule", replacement: false, record: 14,
            ancestors: new[] { 13, 10, 0 }, mountsOnArtificial: true);
        var plainImplant = Facts("ShoulderImplant", replacement: false, record: 10);

        await Assert.That(ImplantConflictRules.Conflicts(bionicArm, rifleModule)).IsFalse();
        await Assert.That(ImplantConflictRules.Conflicts(rifleModule, bionicArm)).IsFalse();
        await Assert.That(ImplantConflictRules.Conflicts(bionicArm, handModule)).IsFalse();
        await Assert.That(ImplantConflictRules.Conflicts(rifleModule, bladeModule)).IsTrue();
        await Assert.That(ImplantConflictRules.Conflicts(bionicArm, plainImplant)).IsTrue();
    }

    /// Mechanite strains (FSF Advanced Bionics Expansion 1.6): every strain
    /// carries a shared tag AND removes that tag on installation, so the
    /// second injection destroys the first wherever it sits — the pair can
    /// never coexist in any surgery order. Immunity mechanites carry no tag
    /// and remove nothing, so they coexist with any strain. Removal tags
    /// are matched exactly, as the game does (Hediff.PostAdd).
    [Test]
    public async Task MutualRemovalTagsConflictAnywhereOnTheBody()
    {
        var armor = Facts("MechanitesArmor", replacement: false, record: 7,
            tags: new[] { "Mechanites" }, removeWith: new[] { "Mechanites" });
        var speed = Facts("MechanitesSpeed", replacement: false, record: 7,
            tags: new[] { "Mechanites" }, removeWith: new[] { "Mechanites" });
        var sensoryOnHead = Facts("MechanitesSensory", replacement: false, record: 2,
            tags: new[] { "Mechanites" }, removeWith: new[] { "Mechanites" });
        var immunity = Facts("MechanitesImmunity", replacement: false, record: 7);
        var moddedCase = Facts("MechanitesModded", replacement: false, record: 7,
            tags: new[] { "mechanites" }, removeWith: new[] { "mechanites" });

        await Assert.That(ImplantConflictRules.Conflicts(armor, speed)).IsTrue();
        await Assert.That(ImplantConflictRules.Conflicts(armor, sensoryOnHead)).IsTrue();
        await Assert.That(ImplantConflictRules.Conflicts(armor, immunity)).IsFalse();
        await Assert.That(ImplantConflictRules.Conflicts(immunity, speed)).IsFalse();
        await Assert.That(ImplantConflictRules.Conflicts(armor, moddedCase)).IsFalse();
    }

    /// One-sided removal leaves a surgery order where both survive (the
    /// remover first, then the tagged implant), so it is not a conflict.
    [Test]
    public async Task OneSidedRemovalIsNotAConflict()
    {
        var purge = Facts("PurgeSerum", replacement: false, record: 7,
            removeWith: new[] { "Mechanites" });
        var strain = Facts("MechanitesSpeed", replacement: false, record: 7,
            tags: new[] { "Mechanites" });

        await Assert.That(ImplantConflictRules.Conflicts(purge, strain)).IsFalse();
        await Assert.That(ImplantConflictRules.Conflicts(strain, purge)).IsFalse();
    }

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

    /// The game checks each recipe's incompatible tags against the OTHER
    /// hediff's tags, so a declaration on one side alone excludes the
    /// pair in both surgery orders.
    [Test]
    public async Task OneSidedTagDeclarationConflictsBothWays()
    {
        var armorskin = Facts("ArmorskinGland", replacement: false, record: 7,
            tags: new[] { "SkinGland" }, incompatible: new[] { "SkinGland" });
        var moddedGland = Facts("ModdedGland", replacement: false, record: 7,
            tags: new[] { "SkinGland" });

        await Assert.That(ImplantConflictRules.Conflicts(armorskin, moddedGland)).IsTrue();
        await Assert.That(ImplantConflictRules.Conflicts(moddedGland, armorskin)).IsTrue();
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
        var model = new PlannerModel();
        model.SetSlotConflictResolver(StomachResolver);
        int nextPlan = 1;
        var plan = model.CreatePlan("Test", () => nextPlan++)!;
        model.SetImplantSlot(plan.Id, "NuclearStomach", 0, true);
        model.SetImplantSlot(plan.Id, "BionicEye", 0, true);

        var change = model.SetImplantSlot(
            plan.Id, "DetoxifierStomach", 0, true);

        await Assert.That(change).IsEqualTo(PlannerChange.Plans);
        await Assert.That(plan.Implants.Count).IsEqualTo(2);
        await Assert.That(plan.Implants[0].ImplantDefName).IsEqualTo("BionicEye");
        await Assert.That(plan.Implants[1].ImplantDefName).IsEqualTo("DetoxifierStomach");
    }

    /// Per-leg stand-in: a bionic leg replaces the leg a knee spike mounts
    /// on, so the two kinds conflict only at the same ordinal.
    static bool LegResolver(ImplantGoal a, int ordA, ImplantGoal b, int ordB) =>
        ordA == ordB
        && ((a.ImplantDefName == "BionicLeg" && b.ImplantDefName == "KneeSpike")
            || (a.ImplantDefName == "KneeSpike" && b.ImplantDefName == "BionicLeg"));

    /// A conflicting pick removes only the slots it excludes: the knee
    /// spike keeps its other leg when the first bionic leg is picked, and
    /// disappears entirely once the second leg (added to the EXISTING
    /// bionic-leg goal) takes that slot too. Unrelated goals are untouched.
    [Test]
    public async Task ConflictingPicksRemoveOnlyTheExcludedSlots()
    {
        var model = new PlannerModel();
        model.SetSlotConflictResolver(LegResolver);
        int nextPlan = 1;
        var plan = model.CreatePlan("Test", () => nextPlan++)!;
        model.SetImplantSlot(plan.Id, "KneeSpike", 0, true);
        model.SetImplantSlot(plan.Id, "KneeSpike", 1, true);
        model.SetImplantSlot(plan.Id, "BionicEye", 0, true);

        model.SetImplantSlot(plan.Id, "BionicLeg", 0, true);

        await Assert.That(plan.Implants.Count).IsEqualTo(3);
        await Assert.That(plan.Implants[0].ImplantDefName).IsEqualTo("KneeSpike");
        await Assert.That(plan.Implants[0].SlotOrdinals).IsEquivalentTo(new[] { 1 });
        await Assert.That(plan.Implants[2].ImplantDefName).IsEqualTo("BionicLeg");

        model.SetImplantSlot(plan.Id, "BionicLeg", 1, true);

        await Assert.That(plan.Implants.Count).IsEqualTo(2);
        await Assert.That(plan.Implants[0].ImplantDefName).IsEqualTo("BionicEye");
        await Assert.That(plan.Implants[0].SlotOrdinals).IsEquivalentTo(new[] { 0 });
        await Assert.That(plan.Implants[1].ImplantDefName).IsEqualTo("BionicLeg");
        await Assert.That(plan.Implants[1].SlotOrdinals).IsEquivalentTo(new[] { 0, 1 });
    }

    /// Bladder implants from different mods are separate torso implants to
    /// the game, so no data marks them exclusive. The "Allow multiple
    /// bladder implants" option (on by default) leaves them coexisting;
    /// switched off, picking one bladder overrides the other own pick and
    /// suppresses an inherited one, with no definition-derived resolver
    /// involved at all.
    [Test]
    public async Task BladderOptionOffMakesModdedBladdersExclusive()
    {
        var model = new PlannerModel();
        int nextPlan = 1;
        var basePlan = model.CreatePlan("Base", () => nextPlan++)!;
        model.SetImplantSlot(basePlan.Id, "BionicBladder", 0, true);
        var derived = model.CreatePlan("Derived", () => nextPlan++, basePlan.Id)!;
        model.SetImplantSlot(derived.Id, "BionicBladder", 0, true);
        model.SetImplantSlot(derived.Id, "BionicEye", 0, true);

        // Default: both bladders stay selected side by side.
        await Assert.That(model.AllowMultipleBladders).IsTrue();
        await Assert.That(model.SetImplantSlot(
            derived.Id, "FSFAdvBionicBladder", 0, true)).IsEqualTo(PlannerChange.Plans);
        await Assert.That(derived.Implants.Count).IsEqualTo(3);
        await Assert.That(model.KindsExclusive("BionicBladder", "FSFAdvBionicBladder"))
            .IsFalse();

        await Assert.That(model.SetAllowMultipleBladders(true))
            .IsEqualTo(PlannerChange.None);
        await Assert.That(model.SetAllowMultipleBladders(false))
            .IsEqualTo(PlannerChange.Options);
        await Assert.That(model.KindsExclusive("BionicBladder", "FSFAdvBionicBladder"))
            .IsTrue();
        await Assert.That(model.KindsExclusive("BionicBladder", "BionicEye")).IsFalse();

        // Picks made while the option was on stay stored, but only the
        // first bladder is effective while it is off: nothing is deleted,
        // so switching back on restores the pair.
        await Assert.That(derived.Implants.Count).IsEqualTo(3);
        IReadOnlyList<ImplantGoal> effective = model.EffectiveImplants(derived);
        await Assert.That(effective.Count).IsEqualTo(2);
        await Assert.That(effective[0].ImplantDefName).IsEqualTo("BionicBladder");
        await Assert.That(effective[1].ImplantDefName).IsEqualTo("BionicEye");
        model.SetAllowMultipleBladders(true);
        await Assert.That(model.EffectiveImplants(derived).Count).IsEqualTo(3);
        model.SetAllowMultipleBladders(false);

        // Re-picking the advanced bladder now overrides the bionic one.
        model.SetImplantSlot(derived.Id, "FSFAdvBionicBladder", 0, false);
        model.SetImplantSlot(derived.Id, "FSFAdvBionicBladder", 0, true);
        await Assert.That(derived.Implants.Count).IsEqualTo(2);
        await Assert.That(derived.Implants[0].ImplantDefName).IsEqualTo("BionicEye");
        await Assert.That(derived.Implants[1].ImplantDefName).IsEqualTo("FSFAdvBionicBladder");

        // The base plan's bionic bladder is suppressed, not deleted.
        effective = model.EffectiveImplants(derived);
        await Assert.That(effective.Count).IsEqualTo(2);
        await Assert.That(model.EffectiveImplants(basePlan).Count).IsEqualTo(1);

        // The hygiene enhancer pair has its own toggle, independent of the
        // bladder one.
        await Assert.That(model.KindsExclusive("HygieneEnhancer", "FSFAdvHygieneEnhancer"))
            .IsFalse();
        await Assert.That(model.SetAllowMultipleHygieneEnhancers(false))
            .IsEqualTo(PlannerChange.Options);
        await Assert.That(model.KindsExclusive("HygieneEnhancer", "FSFAdvHygieneEnhancer"))
            .IsTrue();
        await Assert.That(model.KindsExclusive("HygieneEnhancer", "BionicBladder"))
            .IsFalse();
    }

    [Test]
    public async Task DerivedPlanChoiceSuppressesConflictingInheritedGoal()
    {
        var model = new PlannerModel();
        model.SetSlotConflictResolver(StomachResolver);
        int nextPlan = 1;
        var basePlan = model.CreatePlan("Base", () => nextPlan++)!;
        model.SetImplantSlot(basePlan.Id, "NuclearStomach", 0, true);
        model.SetImplantSlot(basePlan.Id, "BionicEye", 0, true);
        var derived = model.CreatePlan("Derived", () => nextPlan++, basePlan.Id)!;
        model.SetImplantSlot(derived.Id, "DetoxifierStomach", 0, true);

        IReadOnlyList<ImplantGoal> effective = model.EffectiveImplants(derived);

        // The derived stomach wins; the unrelated eye still inherits.
        await Assert.That(effective.Count).IsEqualTo(2);
        await Assert.That(effective[0].ImplantDefName).IsEqualTo("DetoxifierStomach");
        await Assert.That(effective[1].ImplantDefName).IsEqualTo("BionicEye");

        // The base plan itself is untouched.
        await Assert.That(model.EffectiveImplants(basePlan).Count).IsEqualTo(2);
    }
}
