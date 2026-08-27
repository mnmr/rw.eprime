using RimShared.Common;

namespace RimShared.Common.Tests;

public class TrackedTargetIdsTests
{
    [Test]
    public async Task RetargetingPrimaryKeepsTheReplacementAsPrimary()
    {
        var tracker = new TrackedTargetIds(new[] { 11, 22 });

        bool changed = tracker.Retarget(11, 33);

        await Assert.That(changed).IsTrue();
        await Assert.That(tracker.Primary).IsEqualTo(33);
        await Assert.That(tracker[0]).IsEqualTo(33);
        await Assert.That(tracker[1]).IsEqualTo(22);
    }

    [Test]
    public async Task RepeatedRetargetingKeepsASecondaryTargetCurrent()
    {
        var tracker = new TrackedTargetIds(new[] { 11, 22 });

        tracker.Retarget(22, 44);
        tracker.Retarget(44, 55);

        await Assert.That(tracker.Primary).IsEqualTo(11);
        await Assert.That(tracker[1]).IsEqualTo(55);
    }
}
