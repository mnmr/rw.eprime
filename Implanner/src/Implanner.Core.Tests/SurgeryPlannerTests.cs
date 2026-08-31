using System;
using System.Collections.Generic;
using Implanner.Core;

namespace Implanner.Core.Tests;

/// Surgery batch computation and iteration ordering, and the missing-slot
/// derivation the scheduler consumes. Batching is implied by the iteration
/// strategy: colonist iteration = whole plan, family iteration = one star
/// tier.
public class SurgeryPlannerTests
{
    [Test]
    public async Task ColonistIterationBatchesTheWholePlan()
    {
        var missing = new[] { "i1:0", "i1:1", "i2:0" };

        var batch = SurgeryPlanner.ComputeBatch(missing,
            key => key.StartsWith("i1", StringComparison.Ordinal) ? 0 : 1,
            IterationStrategy.Colonist);

        await Assert.That(batch).IsEquivalentTo(new[] { "i1:0", "i1:1", "i2:0" });
    }

    [Test]
    public async Task FamilyIterationBatchesOnlyTheBestTier()
    {
        // The active tier is the best (lowest index) with missing work —
        // it wins even when listed later.
        var missing = new[] { "i2:0", "i1:0", "i1:1" };

        var batch = SurgeryPlanner.ComputeBatch(missing,
            key => key.StartsWith("i1", StringComparison.Ordinal) ? 0 : 1,
            IterationStrategy.ImplantTier);

        await Assert.That(batch).IsEquivalentTo(new[] { "i1:0", "i1:1" });
    }

    [Test]
    public async Task IterationStrategiesProduceVisiblyDifferentDeterministicResults()
    {
        // Pawn 1 needs tier-1 work, pawn 2 needs tier-0 work. Colonist
        // iteration serves pawn 1 first; family iteration serves tier 0
        // (pawn 2) first.
        var items = new List<SurgeryWorkItem>
        {
            new SurgeryWorkItem(2, PlannerModel.PriorityNormal, 0, "i3:0"),
            new SurgeryWorkItem(1, PlannerModel.PriorityNormal, 1, "i1:0"),
            new SurgeryWorkItem(1, PlannerModel.PriorityNormal, 0, "i2:0"),
        };

        var colonist = new List<SurgeryWorkItem>(items);
        SurgeryPlanner.Order(colonist, IterationStrategy.Colonist);
        await Assert.That(colonist[0].GoalKey).IsEqualTo("i2:0");
        await Assert.That(colonist[1].GoalKey).IsEqualTo("i1:0");
        await Assert.That(colonist[2].GoalKey).IsEqualTo("i3:0");

        var family = new List<SurgeryWorkItem>(items);
        SurgeryPlanner.Order(family, IterationStrategy.ImplantTier);
        await Assert.That(family[0].GoalKey).IsEqualTo("i2:0");
        await Assert.That(family[1].GoalKey).IsEqualTo("i3:0");
        await Assert.That(family[2].GoalKey).IsEqualTo("i1:0");
    }

    [Test]
    public async Task PawnPriorityOutranksPawnIdInBothStrategies()
    {
        var items = new List<SurgeryWorkItem>
        {
            new SurgeryWorkItem(1, PlannerModel.PriorityNormal, 0, "i1:0"),
            new SurgeryWorkItem(9, PlannerModel.PriorityFirst, 0, "i2:0"),
        };

        SurgeryPlanner.Order(items, IterationStrategy.Colonist);
        await Assert.That(items[0].PawnId).IsEqualTo(9);

        SurgeryPlanner.Order(items, IterationStrategy.ImplantTier);
        await Assert.That(items[0].PawnId).IsEqualTo(9);
    }

    [Test]
    public async Task MissingSlotKeysSkipBlockedLatchedAndOccupiedSlots()
    {
        var plan = new Plan(1, "Test");
        plan.Implants.Add(new ImplantGoal(1, "BionicLeg", new[] { 0, 1, 2 }));
        plan.Implants.Add(new ImplantGoal(2, "BionicEye", new[] { 0 }));
        var contexts = new[]
        {
            // Ordinal 2 does not exist on this body: blocked, never demanded.
            new ImplantContext(new[] { "Leg/Left", "Leg/Right" }, 1.25f),
            new ImplantContext(new[] { "Eye/Left" }, 1.25f),
        };
        // Left leg already holds a sufficient (superior) part.
        var installed = new[]
        {
            new InstalledImplant("ArchotechLeg", "Leg/Left", 1.5f),
        };
        // The eye was delivered once and later lost: latched, not re-pursued.
        var latched = new HashSet<string>(StringComparer.Ordinal)
        {
            GoalKeys.ImplantSlot(2, 0),
        };

        var missing = PlanEvaluator.MissingImplantSlotKeys(
            plan.Implants, installed, contexts, latched);

        await Assert.That(missing).IsEquivalentTo(new[] { "i1:1" });
    }
}
