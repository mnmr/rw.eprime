using System;
using System.Collections.Generic;
using Implanner.Core;

namespace Implanner.Core.Tests;

/// A plan applies to colonists of every race, so each pawn's slots are
/// judged on that pawn's own body: a slot the surgery can never reach
/// there is outside the target instead of waiting forever with an item
/// held for it.
public class SlotApplicabilityTests
{
    static readonly string[] NoTags = Array.Empty<string>();

    static PlannedSlotFacts Facts(string defName, int record, int[] ancestors,
        bool replacement = false, bool mounts = false)
        => new PlannedSlotFacts(defName, replacement, record, ancestors,
            NoTags, NoTags, NoTags, mounts);

    static ImplantContext Context(IReadOnlyList<PlannedSlotFacts?> facts,
        SlotBlock[] blocks, string?[] blockers)
    {
        var keys = new string?[facts.Count];
        for (int j = 0; j < keys.Length; j++)
            keys[j] = facts[j]?.SlotRecord.ToString();
        return new ImplantContext(keys, 1f, blocks, blockers);
    }

    static (SlotBlock[][] Blocks, string?[][] Blockers, ImplantContext[] Contexts) Resolve(
        PlannedSlotFacts?[][] facts, params ArtificialPart[] artificial)
    {
        var blocks = new SlotBlock[facts.Length][];
        var blockers = new string?[facts.Length][];
        for (int g = 0; g < facts.Length; g++)
        {
            blocks[g] = new SlotBlock[facts[g].Length];
            blockers[g] = new string?[facts[g].Length];
        }
        SlotApplicability.Resolve(facts, artificial, blocks, blockers);
        var contexts = new ImplantContext[facts.Length];
        for (int g = 0; g < facts.Length; g++)
            contexts[g] = Context(facts[g], blocks[g], blockers[g]);
        return (blocks, blockers, contexts);
    }

    /// Royalty alone reaches this: a colonist already carrying a bionic arm
    /// (record 20, the shoulder) is assigned a plan with an elbow blade
    /// (the arm, record 21, under the shoulder). Vanilla's implant worker
    /// refuses a part under an artificial one, so the slot is blocked by the
    /// arm, excluded from the target, and never a missing key; a module
    /// whose worker mounts on artificial parts stays deliverable. Sibling
    /// organs on the human body (kidney 9, liver 11 under the torso) are
    /// unaffected by each other's replacement.
    [Test]
    public async Task SlotUnderAnInstalledArtificialPartIsOutsideTheTarget()
    {
        var goals = new List<ImplantGoal>
        {
            new ImplantGoal(1, "ElbowBlade", new[] { new PartAddress("Arm", 0) }),
            new ImplantGoal(1, "ArmModule", new[] { new PartAddress("Arm", 0) }),
            new ImplantGoal(1, "ArchotechKidney", new[] { new PartAddress("Kidney", 0) }),
            new ImplantGoal(1, "ArchotechLiver", new[] { new PartAddress("Liver", 0) }),
        };
        var human = new PlannedSlotFacts?[][]
        {
            new[] { Facts("ElbowBlade", 21, new[] { 20, 0 }) },
            new[] { Facts("ArmModule", 21, new[] { 20, 0 }, mounts: true) },
            new[] { Facts("ArchotechKidney", 9, new[] { 0 }, replacement: true) },
            new[] { Facts("ArchotechLiver", 11, new[] { 0 }, replacement: true) },
        };
        var bionicArm = new ArtificialPart(20, "BionicArm");

        var (blocks, blockers, contexts) = Resolve(human, bionicArm);
        await Assert.That(blocks[0][0]).IsEqualTo(SlotBlock.UnderArtificialPart);
        await Assert.That(blockers[0][0]).IsEqualTo("BionicArm");
        await Assert.That(blocks[1][0]).IsEqualTo(SlotBlock.None);
        await Assert.That(blocks[2][0]).IsEqualTo(SlotBlock.None);
        await Assert.That(blocks[3][0]).IsEqualTo(SlotBlock.None);

        PlanEvaluation evaluation = PlanEvaluator.Evaluate(goals,
            Array.Empty<InstalledImplant>(), contexts, away: false);
        await Assert.That(evaluation.Implants[0].Requested).IsEqualTo(0);
        await Assert.That(evaluation.TotalUnits).IsEqualTo(3);
        await Assert.That(evaluation.Contexts![0].Block(0)).IsEqualTo(SlotBlock.UnderArtificialPart);
        await Assert.That(evaluation.Contexts[0].Blocker(0)).IsEqualTo("BionicArm");
        List<string> missing = PlanEvaluator.MissingImplantSlotKeys(goals,
            Array.Empty<InstalledImplant>(), contexts);
        await Assert.That(missing).IsEquivalentTo(new[]
        {
            "p1:ArmModule:Arm#0", "p1:ArchotechKidney:Kidney#0", "p1:ArchotechLiver:Liver#0",
        });

        // Without the arm nothing is blocked and every slot is a target.
        var (bare, _, bareContexts) = Resolve(human);
        await Assert.That(bare[0][0]).IsEqualTo(SlotBlock.None);
        await Assert.That(PlanEvaluator.Evaluate(goals, Array.Empty<InstalledImplant>(),
            bareContexts, away: false).TotalUnits).IsEqualTo(4);
    }

    /// Plans are validated on the human body, where kidney and liver are
    /// siblings. A race whose body nests the liver (record 10) under the
    /// first kidney (record 9) can never carry both archotech organs: the
    /// planned kidney replacement blocks the liver slot on that pawn, the
    /// kidney stays a target, and a part the body lacks reads as missing
    /// anatomy, not as blocked by an implant.
    [Test]
    public async Task SlotUnderAPlannedReplacementIsOutsideTheTargetOnThatBody()
    {
        var oddRace = new PlannedSlotFacts?[][]
        {
            new[] { Facts("ArchotechKidney", 9, new[] { 0 }, replacement: true) },
            new[] { Facts("ArchotechLiver", 10, new[] { 9, 0 }, replacement: true) },
            new PlannedSlotFacts?[] { null },
        };
        var (blocks, blockers, contexts) = Resolve(oddRace);
        await Assert.That(blocks[0][0]).IsEqualTo(SlotBlock.None);
        await Assert.That(blocks[1][0]).IsEqualTo(SlotBlock.UnderPlannedReplacement);
        await Assert.That(blockers[1][0]).IsEqualTo("ArchotechKidney");
        await Assert.That(blocks[2][0]).IsEqualTo(SlotBlock.NoAnatomy);
        await Assert.That(blockers[2][0]).IsNull();
        await Assert.That(contexts[1].SlotKey(0)).IsNull();
        await Assert.That(contexts[0].SlotKey(0)).IsEqualTo("9");

        // Once the kidney is in, the installed part is what blocks the
        // liver, named over the plan goal.
        var (installed, installedBlockers, _) = Resolve(oddRace,
            new ArtificialPart(9, "ArchotechKidney"));
        await Assert.That(installed[1][0]).IsEqualTo(SlotBlock.UnderArtificialPart);
        await Assert.That(installedBlockers[1][0]).IsEqualTo("ArchotechKidney");
    }
}
