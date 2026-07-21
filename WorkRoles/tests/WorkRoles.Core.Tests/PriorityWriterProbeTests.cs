using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

public class PriorityWriterProbeTests
{
    [Test]
    public async Task FirstWriteInspectsAndNextInspectionWaitsFiveHundredTicks()
    {
        var probe = new PriorityWriterProbe();

        await Assert.That(probe.ObserveBlockedWrite(10)).IsTrue();
        probe.RecordInspection(10, PriorityWriterSampleKind.Unknown);

        await Assert.That(probe.ObserveBlockedWrite(509)).IsFalse();
        await Assert.That(probe.ObserveBlockedWrite(510)).IsTrue();
    }

    [Test]
    public async Task NewSourceReportsNextTickAndConsumesCountOnlyWhenShown()
    {
        var probe = new PriorityWriterProbe();
        probe.ObserveBlockedWrite(20);
        probe.RecordInspection(20, PriorityWriterSampleKind.NewSource);
        probe.ObserveBlockedWrite(20);
        probe.ObserveBlockedWrite(20);

        await Assert.That(probe.TryConsumeReport(20, out _)).IsFalse();
        await Assert.That(probe.TryConsumeReport(21, out long count)).IsTrue();
        await Assert.That(count).IsEqualTo(3L);
        await Assert.That(probe.TryConsumeReport(22, out _)).IsFalse();
    }

    [Test]
    public async Task CancellingPendingReportPreservesCountForTheNextDialog()
    {
        var probe = new PriorityWriterProbe();
        probe.ObserveBlockedWrite(0);
        probe.RecordInspection(0, PriorityWriterSampleKind.NewSource);
        probe.ObserveBlockedWrite(1);

        probe.CancelPendingReport();

        await Assert.That(probe.TryConsumeReport(1, out _)).IsFalse();
        await Assert.That(probe.ObserveBlockedWrite(500)).IsTrue();
        probe.RecordInspection(500, PriorityWriterSampleKind.NewSource);
        await Assert.That(probe.TryConsumeReport(501, out long count)).IsTrue();
        await Assert.That(count).IsEqualTo(3L);
    }

    [Test]
    public async Task KnownSourcesNeverReportAndThreeConsecutiveStopMonitoring()
    {
        var probe = new PriorityWriterProbe();
        foreach (int tick in new[] { 0, 500, 1000 })
        {
            await Assert.That(probe.ObserveBlockedWrite(tick)).IsTrue();
            probe.RecordInspection(tick, PriorityWriterSampleKind.KnownSource);
        }

        await Assert.That(probe.Stopped).IsTrue();
        await Assert.That(probe.HasPendingReport).IsFalse();
        await Assert.That(probe.ObserveBlockedWrite(1500)).IsFalse();
        await Assert.That(probe.TryConsumeReport(1501, out _)).IsFalse();
    }

    [Test]
    public async Task NewAndUnknownSourcesResetTheKnownStreak()
    {
        var probe = new PriorityWriterProbe();
        Observe(probe, 0, PriorityWriterSampleKind.KnownSource);
        Observe(probe, 500, PriorityWriterSampleKind.KnownSource);
        Observe(probe, 1000, PriorityWriterSampleKind.Unknown);
        Observe(probe, 1500, PriorityWriterSampleKind.KnownSource);
        Observe(probe, 2000, PriorityWriterSampleKind.NewSource);

        await Assert.That(probe.Stopped).IsFalse();
    }

    [Test]
    public async Task ResetStartsAFreshSession()
    {
        var probe = new PriorityWriterProbe();
        Observe(probe, 0, PriorityWriterSampleKind.KnownSource);
        Observe(probe, 500, PriorityWriterSampleKind.KnownSource);
        Observe(probe, 1000, PriorityWriterSampleKind.KnownSource);

        probe.Reset();

        await Assert.That(probe.Stopped).IsFalse();
        await Assert.That(probe.HasPendingReport).IsFalse();
        await Assert.That(probe.ObserveBlockedWrite(0)).IsTrue();
    }

    private static void Observe(PriorityWriterProbe probe, int tick,
        PriorityWriterSampleKind kind)
    {
        if (!probe.ObserveBlockedWrite(tick))
            throw new InvalidOperationException("Expected an inspection at tick " + tick);
        probe.RecordInspection(tick, kind);
    }
}
