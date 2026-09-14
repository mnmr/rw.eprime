using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

public class PanelHealthScheduleTests
{
    [Test]
    public async Task ACheckAlsoStaysThirtyTicksAheadOfTheNextKnownCountRefresh()
    {
        var schedule = new PanelHealthSchedule(204);
        // A panel rebuild well into a count period must not put the health
        // check just 16 ticks before the upcoming count refresh at 304.
        await Assert.That(schedule.TryBegin(288, 100, 258)).IsFalse();
        await Assert.That(schedule.TryBegin(333, 304, 306)).IsFalse();
        await Assert.That(schedule.TryBegin(336, 304, 306)).IsTrue();
    }

    [Test]
    public async Task ChecksUseTheRefreshPeriodAndWaitForActualBufferWork()
    {
        var schedule = new PanelHealthSchedule(204);
        // Counts at 100; asynchronous buffer publication finishes at 104.
        await Assert.That(schedule.TryBegin(133, 100, 104)).IsFalse();
        await Assert.That(schedule.TryBegin(134, 100, 104)).IsTrue();
        await Assert.That(schedule.TryBegin(134, 100, 104)).IsFalse();
        await Assert.That(schedule.TryBegin(337, 304, 308)).IsFalse();
        await Assert.That(schedule.TryBegin(338, 304, 308)).IsTrue();
        // A delayed build moves this check, without allowing another early one.
        await Assert.That(schedule.TryBegin(542, 508, 520)).IsFalse();
        await Assert.That(schedule.TryBegin(549, 508, 520)).IsFalse();
        await Assert.That(schedule.TryBegin(550, 508, 520)).IsTrue();
        await Assert.That(schedule.TryBegin(753, 712, 716)).IsFalse();
        await Assert.That(schedule.TryBegin(754, 712, 716)).IsTrue();
    }

    [Test]
    public async Task EqualCountRefreshesStillDeferTheCheckAndPausesDoNotRepeatIt()
    {
        var schedule = new PanelHealthSchedule(204);
        await Assert.That(schedule.TryBegin(130, 100, 100)).IsTrue();
        // No new pixels, but a count-only invalidation did work at tick 330.
        await Assert.That(schedule.TryBegin(334, 330, 100)).IsFalse();
        await Assert.That(schedule.TryBegin(359, 330, 100)).IsFalse();
        await Assert.That(schedule.TryBegin(360, 330, 100)).IsTrue();
        for (int i = 0; i < 20; i++)
            await Assert.That(schedule.TryBegin(360, 330, 100)).IsFalse();
        // A long gap coalesces into one check, never a catch-up loop.
        await Assert.That(schedule.TryBegin(3000, 2900, 2900)).IsTrue();
        await Assert.That(schedule.TryBegin(3000, 2900, 2900)).IsFalse();
    }

    [Test]
    public async Task RewindingTicksAndReplacingOwnersStartANewQuietPeriod()
    {
        var schedule = new PanelHealthSchedule(204);
        await Assert.That(schedule.TryBegin(1030, 1000, 1000)).IsTrue();
        await Assert.That(schedule.TryBegin(50, 1000, 1000)).IsFalse();
        await Assert.That(schedule.TryBegin(79, 50, 50)).IsFalse();
        await Assert.That(schedule.TryBegin(80, 50, 50)).IsTrue();
        var otherMap = new PanelHealthSchedule(204);
        await Assert.That(otherMap.TryBegin(80, 80, 80)).IsFalse();
        await Assert.That(otherMap.TryBegin(110, 80, 80)).IsTrue();
    }

    [Test]
    public async Task TickArithmeticDoesNotOverflowNearTheIntegerLimit()
    {
        var schedule = new PanelHealthSchedule(204);
        await Assert.That(schedule.TryBegin(int.MaxValue - 30, int.MaxValue - 60, int.MaxValue - 60)).IsTrue();
        await Assert.That(schedule.TryBegin(int.MaxValue, int.MaxValue - 60, int.MaxValue - 60)).IsFalse();
        await Assert.That(schedule.TryBegin(int.MinValue, int.MaxValue - 60, int.MaxValue - 60)).IsFalse();
        await Assert.That(schedule.TryBegin(int.MinValue + 30, int.MinValue, int.MinValue)).IsTrue();
    }

    [Test]
    public async Task PersistentFaultExhaustsRepairsButVerifiedRecoveryAllowsALaterEpisode()
    {
        // The retry budget is independently meaningful: an unverified upload
        // must not reset it and turn a broken shader into an endless retry loop.
        var recovery = new PanelBufferRecovery();
        await Assert.That(recovery.IsActive).IsFalse();
        await Assert.That(recovery.NextRepair()).IsEqualTo(BufferRepair.Reupload);
        await Assert.That(recovery.IsActive).IsTrue();
        recovery.ConfirmHealthy();
        await Assert.That(recovery.IsActive).IsFalse();
        await Assert.That(recovery.NextRepair()).IsEqualTo(BufferRepair.Reupload);
        await Assert.That(recovery.NextRepair()).IsEqualTo(BufferRepair.RecreateBackend);
        await Assert.That(recovery.NextRepair()).IsEqualTo(BufferRepair.RebuildSurfaces);
        await Assert.That(recovery.NextRepair()).IsEqualTo(BufferRepair.Exhausted);
        await Assert.That(recovery.NextRepair()).IsEqualTo(BufferRepair.Exhausted);
    }
}
