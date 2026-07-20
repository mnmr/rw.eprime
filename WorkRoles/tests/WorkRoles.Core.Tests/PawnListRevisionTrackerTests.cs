using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

public class PawnListRevisionTrackerTests
{
    [Test]
    public async Task InitialMapObservationDoesNotAdvanceRevision()
    {
        var tracker = new PawnListRevisionTracker();

        ScopeCacheStamp stamp = tracker.Stamp(uiRevision: 8, mapId: 101);

        await Assert.That(tracker.Revision).IsEqualTo(0);
        await Assert.That(stamp).IsEqualTo(new ScopeCacheStamp(8, 0));
    }

    [Test]
    public async Task RepeatedMapObservationDoesNotAdvanceRevision()
    {
        var tracker = new PawnListRevisionTracker();
        tracker.Stamp(uiRevision: 8, mapId: 101);

        ScopeCacheStamp repeated = tracker.Stamp(uiRevision: 8, mapId: 101);

        await Assert.That(tracker.Revision).IsEqualTo(0);
        await Assert.That(repeated.PawnListRevision).IsEqualTo(0);
    }

    [Test]
    public async Task ExplicitInvalidationAdvancesWithoutAnyPawnCacheState()
    {
        var tracker = new PawnListRevisionTracker();

        tracker.Invalidate();
        ScopeCacheStamp stamp = tracker.Stamp(uiRevision: 8, mapId: 101);

        await Assert.That(tracker.Revision).IsEqualTo(1);
        await Assert.That(stamp.PawnListRevision).IsEqualTo(1);
    }

    [Test]
    public async Task MapTransitionAdvancesExactlyOnce()
    {
        var tracker = new PawnListRevisionTracker();
        tracker.Stamp(uiRevision: 8, mapId: 101);

        ScopeCacheStamp changed = tracker.Stamp(uiRevision: 8, mapId: 202);
        ScopeCacheStamp repeated = tracker.Stamp(uiRevision: 8, mapId: 202);

        await Assert.That(changed.PawnListRevision).IsEqualTo(1);
        await Assert.That(repeated.PawnListRevision).IsEqualTo(1);
        await Assert.That(tracker.Revision).IsEqualTo(1);
    }

    [Test]
    public async Task ReturnedStampIncludesObservedMapTransition()
    {
        var tracker = new PawnListRevisionTracker();
        ScopeCacheStamp before = tracker.Stamp(uiRevision: 8, mapId: 101);

        ScopeCacheStamp after = tracker.Stamp(uiRevision: 8, mapId: 202);

        await Assert.That(after).IsNotEqualTo(before);
        await Assert.That(after)
            .IsEqualTo(new ScopeCacheStamp(uiRevision: 8, pawnListRevision: 1));
    }
}
