using System.Collections.Generic;
using Implanner.Core;

namespace Implanner.Core.Tests;

/// The per-kind tallies behind the colony strip's production and surgery
/// tooltips: stock coverage arithmetic and the pipeline partition of
/// missing implant slots, ordered like automation dispatches them.
public class StripBreakdownTests
{
    /// Coverage counts reserved items (they exist and need no production)
    /// but the free figure excludes both hold-back and reservations, and
    /// free stock is never capped to the need.
    [Test]
    public async Task StockCoverageSeparatesHeldBackReservedAndFreeItems()
    {
        StockCoverage coverage = StockCoverage.Of(
            needed: 4, stock: 5, heldBack: 2, reserved: 2);
        await Assert.That(coverage.Covered).IsEqualTo(3);
        await Assert.That(coverage.Free).IsEqualTo(1);
        await Assert.That(coverage.Queued).IsEqualTo(1);

        // Surplus shows in Free while nothing stays queued.
        StockCoverage surplus = StockCoverage.Of(
            needed: 1, stock: 6, heldBack: 0, reserved: 1);
        await Assert.That(surplus.Covered).IsEqualTo(1);
        await Assert.That(surplus.Free).IsEqualTo(5);
        await Assert.That(surplus.Queued).IsEqualTo(0);

        // A reserve larger than the usable stock leaves nothing free and
        // nothing negative.
        StockCoverage tight = StockCoverage.Of(
            needed: 2, stock: 2, heldBack: 1, reserved: 2);
        await Assert.That(tight.Covered).IsEqualTo(1);
        await Assert.That(tight.Free).IsEqualTo(0);
        await Assert.That(tight.Queued).IsEqualTo(1);
    }

    /// Two colonists on one plan: every planned slot lands in exactly one
    /// of installed, waiting, reserved, or scheduled; a slot with both a
    /// reservation and an operation bill counts as scheduled; keys that no
    /// longer resolve to an effective goal are ignored; kinds sort by star
    /// tier, then the player's tier position, then defName.
    [Test]
    public async Task SurgeryKindsPartitionPlannedSlotsByPipelineStage()
    {
        var model = new PlannerModel();
        model.SetImplantStars("BionicLeg", 5);
        var arm = new ImplantGoal(1, "BionicArm", new[] { 0, 1 });
        var leg = new ImplantGoal(1, "BionicLeg", new[] { 0 });
        var goals = new List<ImplantGoal> { arm, leg };

        var pawns = new List<SurgeryPawnInput>
        {
            // One arm installed; the other arm reserved; the leg reserved
            // and scheduled.
            new SurgeryPawnInput(goals,
                new[] { new GoalResult(2, 1, 1), new GoalResult(1, 0, 1) },
                reservedKeys: new[] { "p1:BionicArm:1", "p1:BionicLeg:0", "p9:Gone:0" },
                scheduledKeys: new[] { "p1:BionicLeg:0" }),
            // Nothing reserved: both arm slots wait; the leg is installed.
            new SurgeryPawnInput(goals,
                new[] { new GoalResult(2, 0, 2), new GoalResult(1, 1, 0) },
                reservedKeys: new string[0],
                scheduledKeys: new string[0]),
        };

        List<SurgeryKindTotals> kinds = StripBreakdown.Surgery(pawns, model);

        await Assert.That(kinds.Count).IsEqualTo(2);
        SurgeryKindTotals legTotals = kinds[0];
        await Assert.That(legTotals.Kind).IsEqualTo("BionicLeg");
        await Assert.That(legTotals.Tier).IsEqualTo(0);
        await Assert.That(legTotals.Planned).IsEqualTo(2);
        await Assert.That(legTotals.Installed).IsEqualTo(1);
        await Assert.That(legTotals.Waiting).IsEqualTo(0);
        await Assert.That(legTotals.Reserved).IsEqualTo(0);
        await Assert.That(legTotals.Scheduled).IsEqualTo(1);

        SurgeryKindTotals armTotals = kinds[1];
        await Assert.That(armTotals.Kind).IsEqualTo("BionicArm");
        await Assert.That(armTotals.Tier).IsEqualTo(2);
        await Assert.That(armTotals.Planned).IsEqualTo(4);
        await Assert.That(armTotals.Installed).IsEqualTo(1);
        await Assert.That(armTotals.Waiting).IsEqualTo(2);
        await Assert.That(armTotals.Reserved).IsEqualTo(1);
        await Assert.That(armTotals.Scheduled).IsEqualTo(0);
    }

    /// A reservation left on an already installed slot (it survives until
    /// the next reconcile pass) is confined to its own colonist: in either
    /// colonist order the other colonist's missing slot still reads as
    /// waiting, and the columns keep summing to the planned count. Kinds
    /// within one tier follow the player's arranged position, then defName.
    [Test]
    public async Task StaleReservationOnOneColonistNeverShiftsAnotherColonistsSlot()
    {
        var model = new PlannerModel();
        model.ApplyTierOrder(5, new[] { "BionicLeg", "BionicArm" });
        var goals = new List<ImplantGoal>
        {
            new ImplantGoal(1, "BionicArm", new[] { 0 }),
            new ImplantGoal(1, "BionicLeg", new[] { 0 }),
        };
        var stale = new SurgeryPawnInput(goals,
            new[] { new GoalResult(1, 1, 0), new GoalResult(1, 1, 0) },
            reservedKeys: new[] { "p1:BionicArm:0", "p1:BionicArm:0" },
            scheduledKeys: new string[0]);
        var missing = new SurgeryPawnInput(goals,
            new[] { new GoalResult(1, 0, 1), new GoalResult(1, 0, 1) },
            reservedKeys: new string[0],
            scheduledKeys: new string[0]);

        foreach (var pawns in new[]
                 {
                     new List<SurgeryPawnInput> { stale, missing },
                     new List<SurgeryPawnInput> { missing, stale },
                 })
        {
            List<SurgeryKindTotals> kinds = StripBreakdown.Surgery(pawns, model);
            await Assert.That(kinds[0].Kind).IsEqualTo("BionicLeg");
            await Assert.That(kinds[1].Kind).IsEqualTo("BionicArm");
            SurgeryKindTotals arm = kinds[1];
            await Assert.That(arm.Planned).IsEqualTo(2);
            await Assert.That(arm.Installed).IsEqualTo(1);
            await Assert.That(arm.Waiting).IsEqualTo(1);
            await Assert.That(arm.Reserved).IsEqualTo(0);
            await Assert.That(arm.Scheduled).IsEqualTo(0);
        }
    }

    /// A wholly impossible goal (nothing requested on this body) never
    /// produces a row, and a stale reservation on an installed slot is not
    /// counted: the four columns always sum to the planned count.
    [Test]
    public async Task SurgeryKindsSkipImpossibleGoalsAndStaleKeys()
    {
        var model = new PlannerModel();
        var goals = new List<ImplantGoal>
        {
            new ImplantGoal(1, "BionicEye", new[] { 0 }),
            new ImplantGoal(1, "BionicArm", new[] { 0 }),
        };
        var pawns = new List<SurgeryPawnInput>
        {
            new SurgeryPawnInput(goals,
                new[] { new GoalResult(0, 0, 0), new GoalResult(1, 1, 0) },
                reservedKeys: new[] { "p1:BionicArm:0" },
                scheduledKeys: new string[0]),
        };

        List<SurgeryKindTotals> kinds = StripBreakdown.Surgery(pawns, model);

        await Assert.That(kinds.Count).IsEqualTo(1);
        await Assert.That(kinds[0].Kind).IsEqualTo("BionicArm");
        await Assert.That(kinds[0].Planned).IsEqualTo(1);
        await Assert.That(kinds[0].Installed).IsEqualTo(1);
        await Assert.That(kinds[0].Reserved).IsEqualTo(0);
        await Assert.That(kinds[0].Waiting).IsEqualTo(0);
    }
}
