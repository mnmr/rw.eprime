namespace EPrimeReadouts.Core
{
    public enum BufferRepair { Reupload, RecreateBackend, RebuildSurfaces, Exhausted }

    /// Bounds retries for one continuous rendering fault. A verified healthy
    /// output ends the episode; merely issuing a repair does not.
    public sealed class PanelBufferRecovery
    {
        private int attempts;
        public bool IsActive => attempts != 0;
        public BufferRepair NextRepair()
        {
            if (attempts >= 3) return BufferRepair.Exhausted;
            return (BufferRepair)attempts++;
        }
        public void ConfirmHealthy() => attempts = 0;
    }
}
