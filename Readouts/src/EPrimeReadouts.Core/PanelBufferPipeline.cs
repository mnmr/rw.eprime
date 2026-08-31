namespace EPrimeReadouts.Core
{
    public readonly struct BufferBuildTicket
    {
        internal BufferBuildTicket(long generation, bool rebuildBase)
        {
            Generation = generation;
            RebuildBase = rebuildBase;
        }

        public long Generation { get; }
        public bool RebuildBase { get; }
    }

    /// Coordinates CPU publication, inactive-buffer construction, and front
    /// publication without knowing anything about Unity graphics resources.
    public sealed class PanelBufferPipeline
    {
        private long pendingGeneration;
        private long buildingGeneration;
        private long readyGeneration;
        private long frontGeneration;
        private bool baseDirty;

        public long FrontGeneration => frontGeneration;
        public long PendingGeneration => pendingGeneration;
        public bool BaseDirty => baseDirty;

        public void PublishCounts() => AdvancePending();

        public void InvalidateBase()
        {
            baseDirty = true;
            AdvancePending();
        }

        public bool TryBeginBuild(out BufferBuildTicket ticket)
        {
            if (buildingGeneration != 0
                || pendingGeneration <= frontGeneration
                || readyGeneration == pendingGeneration)
            {
                ticket = default;
                return false;
            }

            buildingGeneration = pendingGeneration;
            ticket = new BufferBuildTicket(
                buildingGeneration, baseDirty);
            return true;
        }

        /// Abandons an in-flight build (e.g. an asynchronous publish failed)
        /// without publishing anything: the pending generation stays ahead of
        /// the front, so the next TryBeginBuild retries the same work, and
        /// BaseDirty is preserved for that retry.
        public void AbortBuild(BufferBuildTicket ticket)
        {
            if (ticket.Generation == 0
                || ticket.Generation != buildingGeneration)
                return;
            buildingGeneration = 0;
        }

        public void CompleteBuild(BufferBuildTicket ticket)
        {
            if (ticket.Generation == 0
                || ticket.Generation != buildingGeneration)
                return;

            buildingGeneration = 0;
            if (ticket.Generation != pendingGeneration) return;

            readyGeneration = ticket.Generation;
            if (ticket.RebuildBase) baseDirty = false;
        }

        public bool TrySwapOnRepaint()
        {
            if (readyGeneration == 0
                || readyGeneration != pendingGeneration
                || readyGeneration <= frontGeneration)
                return false;

            frontGeneration = readyGeneration;
            readyGeneration = 0;
            return true;
        }

        private void AdvancePending()
        {
            pendingGeneration++;
            readyGeneration = 0;
        }
    }
}
