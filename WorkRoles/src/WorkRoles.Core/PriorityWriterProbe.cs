namespace WorkRoles.Core
{
    public enum PriorityWriterSampleKind { Unknown, NewSource, KnownSource }

    /// Client-local scheduling state for sparse attribution of blocked priority writes.
    /// The game adapter owns caller resolution and presentation data.
    public sealed class PriorityWriterProbe
    {
        private const uint InspectionInterval = 500;
        private bool hasInspected;
        private int lastInspectionTick;
        private int knownSamples;
        private long blockedSinceReport;
        private bool reportPending;
        private int reportSampleTick;

        public bool Stopped { get; private set; }
        public bool HasPendingReport => reportPending;

        /// Counts one meaningful blocked write and says whether this call may
        /// perform the session's first or next 500-tick caller inspection.
        public bool ObserveBlockedWrite(int tick)
        {
            if (Stopped) return false;
            blockedSinceReport++;
            return !hasInspected
                || unchecked((uint)(tick - lastInspectionTick)) >= InspectionInterval;
        }

        public void RecordInspection(int tick, PriorityWriterSampleKind kind)
        {
            if (Stopped) return;
            hasInspected = true;
            lastInspectionTick = tick;
            if (kind == PriorityWriterSampleKind.KnownSource)
            {
                knownSamples++;
                if (knownSamples < 3) return;
                Stopped = true;
                blockedSinceReport = 0;
                reportPending = false;
                return;
            }

            knownSamples = 0;
            if (kind != PriorityWriterSampleKind.NewSource) return;
            reportPending = true;
            reportSampleTick = tick;
        }

        /// Consuming represents actually showing a new-source dialog. It is
        /// deliberately the only operation that reports and resets the count.
        public bool TryConsumeReport(int tick, out long blockedWrites)
        {
            blockedWrites = 0;
            if (!reportPending || Stopped
                || unchecked((uint)(tick - reportSampleTick)) < 1u)
                return false;
            reportPending = false;
            blockedWrites = blockedSinceReport;
            blockedSinceReport = 0;
            return true;
        }

        /// Suppresses a now-ineligible dialog without losing its accumulated count.
        public void CancelPendingReport() => reportPending = false;

        public void Reset()
        {
            hasInspected = false;
            lastInspectionTick = 0;
            knownSamples = 0;
            blockedSinceReport = 0;
            reportPending = false;
            reportSampleTick = 0;
            Stopped = false;
        }
    }
}
