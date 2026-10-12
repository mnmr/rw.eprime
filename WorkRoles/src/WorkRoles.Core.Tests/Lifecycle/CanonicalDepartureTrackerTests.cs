using WorkRoles.Core;

namespace WorkRoles.Core.Tests.Lifecycle;

// The independently meaningful event-coalescing contract protects scope
// snapshots even when a same-location return has identical persisted facts.
public class CanonicalDepartureTrackerTests
{
    [Test]
    public async Task FloorTransfersStayQuietButSettledDeparturesAndLaterReturnsPublish()
    {
        var tracker = new CanonicalDepartureTracker<object>();
        var pawn = new object();
        List<object> departed = [];

        tracker.Despawned(pawn, "home");
        await Assert.That(tracker.Spawned(pawn, "home")).IsFalse();
        tracker.Drain(_ => true, departed.Add);
        await Assert.That(departed).IsEmpty();

        tracker.Despawned(pawn, "home");
        await Assert.That(tracker.Spawned(pawn, "outpost")).IsTrue();
        tracker.Drain(_ => true, departed.Add);
        await Assert.That(departed).IsEmpty();

        tracker.Despawned(pawn, "outpost");
        tracker.Despawned(pawn, "outpost");
        tracker.Drain(_ => true, departed.Add);
        await Assert.That(departed).IsEquivalentTo([pawn]);
        await Assert.That(tracker.Spawned(pawn, "outpost")).IsTrue();
        await Assert.That(tracker.PendingCount).IsEqualTo(0);
    }

    [Test]
    public async Task NewArrivalsPublishAndTeardownCannotSuppressAnotherStoresArrival()
    {
        var tracker = new CanonicalDepartureTracker<object>();
        var pawn = new object();
        await Assert.That(tracker.Spawned(pawn, "home")).IsTrue();
        tracker.Despawned(pawn, "home");
        tracker.Clear();
        tracker.Clear();
        await Assert.That(tracker.Spawned(pawn, "home")).IsTrue();
        tracker.Despawned(pawn, "home");
        tracker.StopTracking(pawn);
        await Assert.That(tracker.PendingCount).IsEqualTo(0);
    }
}
