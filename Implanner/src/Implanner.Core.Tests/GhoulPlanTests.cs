using System;
using System.Collections.Generic;
using Implanner.Core;

namespace Implanner.Core.Tests;

/// Ghoul plans (Anomaly): a plan made for ghouls enlists ghouls only, an
/// ordinary plan never enlists a ghoul, ghoul-only implants are outside a
/// human's target, implants ghouls are refused are outside a ghoul's, and
/// the target travels through save/export like the body pin.
public class GhoulPlanTests
{
    int nextPlan = 1;

    int TakePlanId() => nextPlan++;

    static readonly string[] NoTags = Array.Empty<string>();
    static readonly string[] Ghoul = { "Ghoul" };

    static PlannedSlotFacts Facts(string defName, int record, int[] ancestors,
        bool replacement = false, string[]? requires = null, string[]? refuses = null)
        => new PlannedSlotFacts(defName, replacement, record, ancestors,
            NoTags, NoTags, NoTags, mountsOnArtificialParts: false,
            requiredMutants: requires, blacklistedMutants: refuses);

    static (SlotBlock[][] Blocks, string?[][] Blockers) Resolve(
        PlannedSlotFacts?[][] facts, string pawnMutant)
    {
        var blocks = new SlotBlock[facts.Length][];
        var blockers = new string?[facts.Length][];
        for (int g = 0; g < facts.Length; g++)
        {
            blocks[g] = new SlotBlock[facts[g].Length];
            blockers[g] = new string?[facts[g].Length];
        }
        SlotApplicability.Resolve(facts, Array.Empty<ArtificialPart>(), blocks, blockers,
            pawnMutant);
        return (blocks, blockers);
    }

    [Test]
    public async Task AGhoulPlanEnlistsGhoulsOnlyAndAnOrdinaryPlanNeverDoes()
    {
        var model = new PlannerModel();
        var any = model.CreatePlan("Essentials", TakePlanId)!;
        var ghouls = model.CreatePlan("Ghouls", TakePlanId, 0, null, "Ghoul")!;
        await Assert.That(ghouls.Mutant).IsEqualTo("Ghoul");
        await Assert.That(ghouls.IsForMutant).IsTrue();
        await Assert.That(ghouls.IsPinned).IsFalse();

        await Assert.That(ghouls.AcceptsMutant("Ghoul")).IsTrue();
        await Assert.That(ghouls.AcceptsMutant("")).IsFalse();
        await Assert.That(any.AcceptsMutant("")).IsTrue();
        await Assert.That(any.AcceptsMutant("Ghoul")).IsFalse();

        // Extension follows the body rule: a ghoul plan may build on an
        // ordinary plan (ghouls take ordinary bionics) or on a ghoul plan;
        // nothing else may build on a ghoul plan.
        await Assert.That(model.CreatePlan("Ghouls of any", TakePlanId, any.Id, null, "Ghoul")).IsNotNull();
        await Assert.That(model.CreatePlan("Ghouls of ghouls", TakePlanId, ghouls.Id, null, "Ghoul")).IsNotNull();
        await Assert.That(model.CreatePlan("Any of ghouls", TakePlanId, ghouls.Id)).IsNull();
        await Assert.That(model.CreatePlan("Human of ghouls", TakePlanId, ghouls.Id, "Human")).IsNull();
    }

    /// The vanilla ghoul kit: plating (torso, ghoul-only) and the adrenal
    /// heart (ghoul-only replacement) are outside a human's target; the
    /// revenant vertebrae (blacklisted for ghouls) are outside a ghoul's;
    /// a bionic arm is a target for both.
    [Test]
    public async Task MutantOnlyAndMutantRefusedSlotsAreOutsideTheTarget()
    {
        var goals = new List<ImplantGoal>
        {
            new ImplantGoal(1, "GhoulPlating", new[] { new PartAddress("Torso", 0) }),
            new ImplantGoal(1, "AdrenalHeart", new[] { new PartAddress("Heart", 0) }),
            new ImplantGoal(1, "RevenantVertebrae", new[] { new PartAddress("Spine", 0) }),
            new ImplantGoal(1, "BionicArm", new[] { new PartAddress("Shoulder", 0) }),
        };
        var human = new PlannedSlotFacts?[][]
        {
            new[] { Facts("GhoulPlating", 0, Array.Empty<int>(), requires: Ghoul) },
            new[] { Facts("AdrenalHeart", 8, new[] { 0 }, replacement: true, requires: Ghoul) },
            new[] { Facts("RevenantVertebrae", 2, new[] { 0 }, replacement: true, refuses: Ghoul) },
            new[] { Facts("BionicArm", 20, new[] { 0 }, replacement: true) },
        };

        var (forHuman, _) = Resolve(human, "");
        await Assert.That(forHuman[0][0]).IsEqualTo(SlotBlock.RequiresMutant);
        await Assert.That(forHuman[1][0]).IsEqualTo(SlotBlock.RequiresMutant);
        await Assert.That(forHuman[2][0]).IsEqualTo(SlotBlock.None);
        await Assert.That(forHuman[3][0]).IsEqualTo(SlotBlock.None);

        var (forGhoul, blockers) = Resolve(human, "Ghoul");
        await Assert.That(forGhoul[0][0]).IsEqualTo(SlotBlock.None);
        await Assert.That(forGhoul[1][0]).IsEqualTo(SlotBlock.None);
        await Assert.That(forGhoul[2][0]).IsEqualTo(SlotBlock.RefusesMutant);
        await Assert.That(blockers[2][0]).IsNull();
        await Assert.That(forGhoul[3][0]).IsEqualTo(SlotBlock.None);
    }

    [Test]
    public async Task MutantTargetTravelsThroughExportAndImport()
    {
        var model = new PlannerModel();
        var any = model.CreatePlan("Essentials", TakePlanId)!;
        model.SetImplantSlot(any.Id, "BionicArm", new PartAddress("Shoulder", 0), true);
        var ghouls = model.CreatePlan("Ghoul kit", TakePlanId, any.Id, null, "Ghoul")!;
        model.SetImplantSlot(ghouls.Id, "GhoulPlating", new PartAddress("Torso", 0), true);

        string xml = PlansXml.Export(model.Plans);
        await Assert.That(xml).Contains("Mutant=\"Ghoul\"");
        await Assert.That(xml).DoesNotContain("Mutant=\"\"");

        await Assert.That(PlansXml.TryImport(xml, out var parsed, out string? error)).IsTrue();
        await Assert.That(error).IsNull();
        await Assert.That(parsed[0].Mutant).IsEqualTo("");
        await Assert.That(parsed[1].Mutant).IsEqualTo("Ghoul");
        await Assert.That(parsed[1].BasePlanId).IsEqualTo(1);

        // An ordinary plan extending a ghoul plan loses the link, like a
        // body mismatch.
        const string crossed = "<ImplannerPlans>"
            + "<Plan Name=\"Ghouls\" Mutant=\"Ghoul\"/>"
            + "<Plan Name=\"Everyone\" Extends=\"Ghouls\"/>"
            + "</ImplannerPlans>";
        await Assert.That(PlansXml.TryImport(crossed, out var degraded, out error)).IsTrue();
        await Assert.That(degraded[1].BasePlanId).IsEqualTo(0);

        // Hydration (the save path) carries the target too.
        var loaded = new Plan(7, "Ghouls", 0, new List<ImplantGoal>(), 0, "", "Ghoul");
        await Assert.That(loaded.Mutant).IsEqualTo("Ghoul");
        await Assert.That(new Plan(8, "Old", 0, new List<ImplantGoal>()).Mutant).IsEqualTo("");
    }
}
