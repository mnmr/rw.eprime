namespace RimShared.Common.Tests;

public class ContextualDrainQueueTests
{
    [Test]
    public async Task DrainingOneContextLeavesOtherContextsPending()
    {
        var mapPawn = new PawnToken("map");
        var worldPawn = new PawnToken("world");
        var queue = new ContextualDrainQueue<PawnToken>();
        queue.Enqueue(mapPawn);
        queue.Enqueue(worldPawn);

        var mapBatch = queue.Drain(pawn => pawn.Name == "map");
        var worldBatch = queue.Drain(pawn => pawn.Name == "world");

        await Assert.That(mapBatch).IsEquivalentTo([mapPawn]);
        await Assert.That(worldBatch).IsEquivalentTo([worldPawn]);
        await Assert.That(queue.Count).IsEqualTo(0);
    }

    [Test]
    public async Task DuplicateEnqueuesReconcileAnOwnerOnce()
    {
        var pawn = new PawnToken("map");
        var queue = new ContextualDrainQueue<PawnToken>();

        queue.Enqueue(pawn);
        queue.Enqueue(pawn);

        await Assert.That(queue.Drain(_ => true)).IsEquivalentTo([pawn]);
    }

    private sealed class PawnToken
    {
        public PawnToken(string name) => Name = name;

        public string Name { get; }
    }
}
