using System;
using System.Collections.Generic;
using Implanner.Core;

namespace Implanner.Core.Tests;

/// Surgery batch computation and iteration ordering, and the missing-slot
/// derivation the scheduler consumes. Batching is implied by the iteration
/// strategy: colonist iteration = whole plan, tier iteration = one star
/// tier, best tier first.
public class SurgeryPlannerTests
{
    /// One plan ranking three kinds across the star tiers (5, default 3,
    /// 1) plus a key no goal resolves. Missing keys are listed in
    /// deliberately shuffled order so batch membership, not list order,
    /// is what the assertions prove.
    static PlannerModel RankedModel(out List<ImplantGoal> goals, out string[] missing)
    {
        var model = new PlannerModel();
        model.SetImplantStars("BionicLeg", 5);
        model.SetImplantStars("PowerClaw", 1);
        goals = new List<ImplantGoal>
        {
            new ImplantGoal(1, "BionicEye", new[] { 0 }),
            new ImplantGoal(1, "BionicLeg", new[] { 0, 1 }),
            new ImplantGoal(1, "PowerClaw", new[] { 0 }),
        };
        missing = new[]
        {
            "p1:PowerClaw:0", "p1:BionicEye:0", "p9:Unknown:0",
            "p1:BionicLeg:1", "p1:BionicLeg:0",
        };
        return model;
    }

    [Test]
    public async Task ColonistIterationBatchesTheWholePlan()
    {
        var model = RankedModel(out var goals, out string[] missing);

        var batch = SurgeryPlanner.ComputeBatch(missing, model, goals,
            IterationStrategy.Colonist);

        await Assert.That(batch).IsEquivalentTo(missing);
    }

    /// Tier iteration dispatches the best tier with missing work first:
    /// five stars, then the three-star default, then one star. A key that
    /// resolves to no goal sorts behind every ranked tier and never joins a
    /// better tier's batch.
    [Test]
    public async Task TierIterationDispatchesTiersBestFirstAndUnresolvableKeysLast()
    {
        var model = RankedModel(out var goals, out string[] missing);
        var remaining = new List<string>(missing);
        var dispatched = new List<string[]>();
        while (remaining.Count > 0)
        {
            List<string> batch = SurgeryPlanner.ComputeBatch(remaining, model, goals,
                IterationStrategy.ImplantTier);
            dispatched.Add(batch.ToArray());
            for (int i = 0; i < batch.Count; i++) remaining.Remove(batch[i]);
        }

        await Assert.That(dispatched.Count).IsEqualTo(4);
        await Assert.That(dispatched[0]).IsEquivalentTo(new[] { "p1:BionicLeg:1", "p1:BionicLeg:0" });
        await Assert.That(dispatched[1]).IsEquivalentTo(new[] { "p1:BionicEye:0" });
        await Assert.That(dispatched[2]).IsEquivalentTo(new[] { "p1:PowerClaw:0" });
        await Assert.That(dispatched[3]).IsEquivalentTo(new[] { "p9:Unknown:0" });
    }

    [Test]
    public async Task IterationStrategiesProduceVisiblyDifferentDeterministicResults()
    {
        // Pawn 1 needs tier-1 work, pawn 2 needs tier-0 work. Colonist
        // iteration serves pawn 1 first; tier iteration serves tier 0
        // (pawn 2) first.
        var items = new List<SurgeryWorkItem>
        {
            new SurgeryWorkItem(2, PlannerModel.PriorityNormal, 0, "p1:BionicLeg:0"),
            new SurgeryWorkItem(1, PlannerModel.PriorityNormal, 1, "p1:BionicEye:0"),
            new SurgeryWorkItem(1, PlannerModel.PriorityNormal, 0, "p1:BionicArm:0"),
        };

        var colonist = new List<SurgeryWorkItem>(items);
        SurgeryPlanner.Order(colonist, IterationStrategy.Colonist);
        await Assert.That(colonist[0].GoalKey).IsEqualTo("p1:BionicArm:0");
        await Assert.That(colonist[1].GoalKey).IsEqualTo("p1:BionicEye:0");
        await Assert.That(colonist[2].GoalKey).IsEqualTo("p1:BionicLeg:0");

        var tier = new List<SurgeryWorkItem>(items);
        SurgeryPlanner.Order(tier, IterationStrategy.ImplantTier);
        await Assert.That(tier[0].GoalKey).IsEqualTo("p1:BionicArm:0");
        await Assert.That(tier[1].GoalKey).IsEqualTo("p1:BionicLeg:0");
        await Assert.That(tier[2].GoalKey).IsEqualTo("p1:BionicEye:0");
    }

    [Test]
    public async Task PawnPriorityOutranksPawnIdInBothStrategies()
    {
        var items = new List<SurgeryWorkItem>
        {
            new SurgeryWorkItem(1, PlannerModel.PriorityNormal, 0, "p1:BionicLeg:0"),
            new SurgeryWorkItem(9, PlannerModel.PriorityFirst, 0, "p1:BionicArm:0"),
        };

        SurgeryPlanner.Order(items, IterationStrategy.Colonist);
        await Assert.That(items[0].PawnId).IsEqualTo(9);

        SurgeryPlanner.Order(items, IterationStrategy.ImplantTier);
        await Assert.That(items[0].PawnId).IsEqualTo(9);
    }

    /// ASAP has no batch gate: every missing key is the batch, and each
    /// implant kind in stock goes to the best candidate. Priority still
    /// wins outright; at equal priority legs go to the slowest colonist
    /// and arms to a melee fighter, then to the better crafter or
    /// researcher; anything else falls back to colonist id.
    [Test]
    public async Task AsapRanksCandidatesPerImplantKindAtEqualPriority()
    {
        var model = RankedModel(out var goals, out string[] missing);
        await Assert.That(SurgeryPlanner.ComputeBatch(missing, model, goals,
            IterationStrategy.Asap)).IsEquivalentTo(missing);

        // Tank: slow melee brawler, low skills. Scout: fast, ranged, high
        // skills. Smith: medium speed, ranged, best skills, lowest id.
        var tank = new SurgeryCandidate(3.2f, hasMeleeWeapon: true, armSkills: 4);
        var scout = new SurgeryCandidate(5.1f, hasMeleeWeapon: false, armSkills: 18);
        var smith = new SurgeryCandidate(4.6f, hasMeleeWeapon: false, armSkills: 24);
        var items = new List<SurgeryWorkItem>
        {
            Item(7, PlannerModel.PriorityNormal, "BionicLeg", LimbKind.Leg, scout),
            Item(7, PlannerModel.PriorityNormal, "BionicArm", LimbKind.Arm, scout),
            Item(7, PlannerModel.PriorityNormal, "BionicEye", LimbKind.None, scout),
            Item(5, PlannerModel.PriorityNormal, "BionicLeg", LimbKind.Leg, tank),
            Item(5, PlannerModel.PriorityNormal, "BionicArm", LimbKind.Arm, tank),
            Item(5, PlannerModel.PriorityNormal, "BionicEye", LimbKind.None, tank),
            Item(2, PlannerModel.PriorityNormal, "BionicLeg", LimbKind.Leg, smith),
            Item(2, PlannerModel.PriorityNormal, "BionicArm", LimbKind.Arm, smith),
            Item(2, PlannerModel.PriorityNormal, "BionicEye", LimbKind.None, smith),
            // A first-priority colonist outranks every ranking rule.
            Item(9, PlannerModel.PriorityFirst, "BionicLeg", LimbKind.Leg, scout),
        };

        SurgeryPlanner.Order(items, IterationStrategy.Asap);

        await Assert.That(PawnsFor(items, "BionicLeg")).IsEquivalentTo(new[] { 9, 5, 2, 7 });
        await Assert.That(PawnsFor(items, "BionicArm")).IsEquivalentTo(new[] { 5, 2, 7 });
        await Assert.That(PawnsFor(items, "BionicEye")).IsEquivalentTo(new[] { 2, 5, 7 });
    }

    /// Batch strategies release a colonist's operations only once the
    /// whole batch is reserved on site; ASAP releases whatever is ready.
    [Test]
    public async Task AsapReleasesReadyKeysWhileBatchStrategiesWaitForTheWholeBatch()
    {
        var batch = new List<string> { "p1:BionicLeg:0", "p1:BionicLeg:1", "p1:BionicArm:0" };
        var ready = new[] { true, false, true };

        await Assert.That(SurgeryPlanner.Releasable(batch, ready, IterationStrategy.ImplantTier))
            .IsEmpty();
        await Assert.That(SurgeryPlanner.Releasable(batch, ready, IterationStrategy.Colonist))
            .IsEmpty();
        await Assert.That(SurgeryPlanner.Releasable(batch, ready, IterationStrategy.Asap))
            .IsEquivalentTo(new[] { "p1:BionicLeg:0", "p1:BionicArm:0" });

        var allReady = new[] { true, true, true };
        await Assert.That(SurgeryPlanner.Releasable(batch, allReady, IterationStrategy.ImplantTier))
            .IsEquivalentTo(batch);
    }

    /// Purchase-only implants (no crafting recipe, so the colony cannot
    /// make the item) never hold a batch up: they join every batch so
    /// stock is used the moment it exists, the active tier is chosen by
    /// the craftable keys alone (a purchase-only-only remainder still
    /// becomes the active tier), an unready one never blocks release, and
    /// a ready one releases together with the rest.
    [Test]
    public async Task PurchaseOnlyKeysNeverHoldUpTheBatch()
    {
        var model = new PlannerModel();
        model.SetImplantStars("BionicLeg", 5);
        model.SetImplantStars("ArchotechLeg", 5);
        var goals = new List<ImplantGoal>
        {
            new ImplantGoal(1, "ArchotechLeg", new[] { 0 }),
            new ImplantGoal(1, "BionicLeg", new[] { 1 }),
            new ImplantGoal(1, "BionicEye", new[] { 0 }),
        };
        string[] missing = { "p1:ArchotechLeg:0", "p1:BionicLeg:1", "p1:BionicEye:0" };
        bool[] optional = { true, false, false };

        List<string> batch = SurgeryPlanner.ComputeBatch(
            missing, model, goals, IterationStrategy.ImplantTier, optional);
        await Assert.That(batch).IsEquivalentTo(new[] { "p1:ArchotechLeg:0", "p1:BionicLeg:1" });

        bool[] batchOptional = { true, false };
        await Assert.That(SurgeryPlanner.Releasable(batch, new[] { false, true },
            IterationStrategy.ImplantTier, batchOptional)).IsEquivalentTo(new[] { "p1:BionicLeg:1" });
        await Assert.That(SurgeryPlanner.Releasable(batch, new[] { true, true },
            IterationStrategy.ImplantTier, batchOptional)).IsEquivalentTo(batch);
        await Assert.That(SurgeryPlanner.Releasable(batch, new[] { true, false },
            IterationStrategy.Colonist, batchOptional)).IsEmpty();

        // The bionic leg delivered: the active tier moves down to the eye
        // while the archotech leg still rides along.
        string[] later = { "p1:ArchotechLeg:0", "p1:BionicEye:0" };
        await Assert.That(SurgeryPlanner.ComputeBatch(later, model, goals,
            IterationStrategy.ImplantTier, new[] { true, false }))
            .IsEquivalentTo(later);

        // Only the purchase-only key left: it is the batch, and it is
        // released as soon as stock makes it ready.
        string[] last = { "p1:ArchotechLeg:0" };
        List<string> lastBatch = SurgeryPlanner.ComputeBatch(last, model, goals,
            IterationStrategy.ImplantTier, new[] { true });
        await Assert.That(lastBatch).IsEquivalentTo(last);
        await Assert.That(SurgeryPlanner.Releasable(lastBatch, new[] { false },
            IterationStrategy.ImplantTier, new[] { true })).IsEmpty();
        await Assert.That(SurgeryPlanner.Releasable(lastBatch, new[] { true },
            IterationStrategy.ImplantTier, new[] { true })).IsEquivalentTo(last);
    }

    static SurgeryWorkItem Item(int pawnId, int priority, string kind,
        LimbKind limb, SurgeryCandidate candidate) =>
        new SurgeryWorkItem(pawnId, priority, StarRanking.TierOf(3),
            "p1:" + kind + ":0", kind, limb, candidate);

    static List<int> PawnsFor(List<SurgeryWorkItem> items, string kind)
    {
        var pawns = new List<int>();
        for (int i = 0; i < items.Count; i++)
            if (items[i].ImplantDefName == kind) pawns.Add(items[i].PawnId);
        return pawns;
    }

    [Test]
    public async Task MissingSlotKeysSkipBlockedAndOccupiedSlots()
    {
        var plan = new Plan(1, "Test", 0, new List<ImplantGoal>
        {
            new ImplantGoal(1, "BionicLeg", new[] { 0, 1, 2 }),
            new ImplantGoal(1, "BionicEye", new[] { 0 }),
        });
        var contexts = new[]
        {
            // Ordinal 2 does not exist on this body: blocked, never demanded.
            new ImplantContext(new[] { "Leg/Left", "Leg/Right" }, 1.25f),
            new ImplantContext(new[] { "Eye/Left" }, 1.25f),
        };
        // Left leg already holds a sufficient (superior) part; the eye and
        // right leg stay demanded — a lost implant simply becomes missing
        // again and is re-pursued automatically.
        var installed = new[]
        {
            new InstalledImplant("ArchotechLeg", "Leg/Left", 1.5f),
        };

        var missing = PlanEvaluator.MissingImplantSlotKeys(
            plan.Implants, installed, contexts);

        await Assert.That(missing).IsEquivalentTo(
            new[] { "p1:BionicLeg:1", "p1:BionicEye:0" });
    }
}
