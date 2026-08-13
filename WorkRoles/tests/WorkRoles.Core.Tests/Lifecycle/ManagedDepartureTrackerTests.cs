namespace WorkRoles.Core.Tests.Lifecycle;

public class ManagedDepartureTrackerTests
{
    [Test]
    public async Task UnmanagedOwnersAreNeverTracked()
    {
        var tracker = new ManagedDepartureTracker<object>();
        var owner = new object();

        bool spawned = tracker.Spawned(owner, managed: false);
        bool despawned = tracker.Despawned(owner, managed: false);

        await Assert.That(spawned).IsFalse();
        await Assert.That(despawned).IsFalse();
        await Assert.That(tracker.PendingCount).IsEqualTo(0);
    }

    [Test]
    public async Task RespawnCancelsPendingDeparture()
    {
        var tracker = new ManagedDepartureTracker<object>();
        var owner = new object();
        List<object> departed = [];

        tracker.Despawned(owner, managed: true);
        bool spawned = tracker.Spawned(owner, managed: true);
        tracker.Drain(_ => true, departed.Add);

        await Assert.That(spawned).IsTrue();
        await Assert.That(departed).IsEmpty();
    }

    [Test]
    public async Task DrainRechecksWhetherOwnerIsStillManagedAndOffMap()
    {
        var tracker = new ManagedDepartureTracker<object>();
        List<object> departed = [];
        var stillManaged = new object();
        var unmanagedBeforeDrain = new object();

        tracker.Despawned(stillManaged, managed: true);
        tracker.Despawned(unmanagedBeforeDrain, managed: true);
        tracker.Drain(owner => ReferenceEquals(owner, stillManaged), departed.Add);

        await Assert.That(departed).IsEquivalentTo([stillManaged]);
        await Assert.That(tracker.PendingCount).IsEqualTo(0);
    }

    [Test]
    public async Task StopTrackingCancelsPendingDeparture()
    {
        var tracker = new ManagedDepartureTracker<object>();
        var owner = new object();
        List<object> departed = [];

        tracker.Despawned(owner, managed: true);
        tracker.StopTracking(owner);
        tracker.Drain(_ => true, departed.Add);

        await Assert.That(departed).IsEmpty();
    }
}
