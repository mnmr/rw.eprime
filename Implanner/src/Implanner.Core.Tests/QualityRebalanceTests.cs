using System.Collections.Generic;
using Implanner.Core;

namespace Implanner.Core.Tests;

/// "Better implants to high-priority colonists" for one implant kind at one
/// colony. Qualities: 0 Awful, 1 Poor, 2 Normal, 3 Good, 4 Excellent,
/// 5 Masterwork. Priorities: 0 First, 1 High, 2 Normal, 3 Low.
public class QualityRebalanceTests
{
    const string Arm = "p1:BionicArm:0";

    static QualityHolder Installed(int priority, int pawnId, int quality) =>
        new QualityHolder(priority, pawnId, Arm, quality, itemId: -1, minimum: 0, available: true);

    static QualityHolder Reserved(int priority, int pawnId, int itemId, int quality,
        int minimum, bool scheduled = false) =>
        new QualityHolder(priority, pawnId, Arm, quality, itemId, minimum, available: !scheduled);

    static Dictionary<int, int> Moves(List<QualityMove> moves)
    {
        var result = new Dictionary<int, int>();
        foreach (QualityMove move in moves) result[move.Holder] = move.ItemId;
        return result;
    }

    /// The owner's example: a masterwork arm waits for Ben, next in line,
    /// while Ann (higher priority) wears a Normal one. Ann gets the
    /// masterwork instead and Ben takes the free Good arm. Dee's operation
    /// is already scheduled, so her item stays put however high she ranks,
    /// and Cal's Poor arm has no better free item left to swap to.
    [Test]
    public async Task AHigherPriorityColonistIsUpgradedWithTheItemReservedForTheNextInLine()
    {
        var holders = new List<QualityHolder>
        {
            Installed(priority: 1, pawnId: 1, quality: 2),                      // Ann
            Reserved(priority: 2, pawnId: 2, itemId: 10, quality: 5, minimum: 2), // Ben
            Reserved(priority: 2, pawnId: 3, itemId: 11, quality: 1, minimum: 1), // Cal
            Reserved(priority: 0, pawnId: 4, itemId: 13, quality: 4, minimum: 2,
                scheduled: true),                                               // Dee
        };
        var spare = new List<SpareItem> { new SpareItem(12, 3) };

        Dictionary<int, int> moves = Moves(QualityRebalance.Plan(holders, spare, upgradeSlots: 1));

        await Assert.That(moves.Count).IsEqualTo(2);
        await Assert.That(moves[0]).IsEqualTo(10); // Ann: replaced by the masterwork
        await Assert.That(moves[1]).IsEqualTo(12); // Ben: the Good arm meanwhile
    }

    /// Reservations swap for free before anyone is operated on: Eve (higher
    /// priority) trades her Normal arm for Fay's Excellent one, Fay takes the
    /// free Good arm over the Normal she was handed, and nothing is left
    /// over for an upgrade. Gus wears a Poor arm, but no colonist may start
    /// an upgrade surgery right now, so he keeps it.
    [Test]
    public async Task ReservationsSwapBeforeAnyUpgradeSurgery()
    {
        var holders = new List<QualityHolder>
        {
            Reserved(priority: 2, pawnId: 1, itemId: 20, quality: 2, minimum: 2), // Eve
            Reserved(priority: 3, pawnId: 2, itemId: 21, quality: 4, minimum: 1), // Fay
            Installed(priority: 0, pawnId: 3, quality: 1),                      // Gus
        };
        var spare = new List<SpareItem> { new SpareItem(22, 3) };

        Dictionary<int, int> moves = Moves(QualityRebalance.Plan(holders, spare, upgradeSlots: 0));

        await Assert.That(moves.Count).IsEqualTo(2);
        await Assert.That(moves[0]).IsEqualTo(21);
        await Assert.That(moves[1]).IsEqualTo(22);

        // Settled: planning again from the result moves nothing.
        var settled = new List<QualityHolder>
        {
            Reserved(priority: 2, pawnId: 1, itemId: 21, quality: 4, minimum: 2),
            Reserved(priority: 3, pawnId: 2, itemId: 22, quality: 3, minimum: 1),
            Installed(priority: 0, pawnId: 3, quality: 1),
        };
        await Assert.That(QualityRebalance.Plan(settled,
            new List<SpareItem> { new SpareItem(20, 2) }, upgradeSlots: 0).Count).IsEqualTo(0);
    }
}
