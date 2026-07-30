using System.Collections.Generic;

namespace EPrimeReadouts.Core
{
    /// <summary>
    /// Tracks threshold edit fields against the last stored value observed for
    /// the selected token. Unrelated threshold edits preserve an in-progress
    /// draft; a change to the selected token replaces stale fields.
    /// </summary>
    public sealed class ThresholdEditorState
    {
        private string selectedCanonical;
        private int observedRevision = -1;
        private bool observedHasThreshold;
        private int observedLow;
        private int observedCritical;

        public int LowValue;
        public string LowBuffer = "0";
        public int CriticalValue;
        public string CriticalBuffer = "0";

        public void Select(
            string canonical,
            int revision,
            IReadOnlyDictionary<string, ThresholdSpec> thresholds)
        {
            selectedCanonical = canonical;
            observedRevision = revision;
            ReadStored(thresholds, out observedHasThreshold, out observedLow, out observedCritical);
            ApplyStored();
        }

        public void Refresh(
            int revision,
            IReadOnlyDictionary<string, ThresholdSpec> thresholds)
        {
            if (revision == observedRevision) return;

            ReadStored(thresholds, out bool hasThreshold, out int low, out int critical);
            if (hasThreshold != observedHasThreshold
                || low != observedLow
                || critical != observedCritical)
            {
                observedHasThreshold = hasThreshold;
                observedLow = low;
                observedCritical = critical;
                ApplyStored();
            }
            observedRevision = revision;
        }

        private void ReadStored(
            IReadOnlyDictionary<string, ThresholdSpec> thresholds,
            out bool hasThreshold,
            out int low,
            out int critical)
        {
            if (selectedCanonical != null
                && thresholds != null
                && thresholds.TryGetValue(selectedCanonical, out var spec))
            {
                hasThreshold = true;
                low = spec.Low;
                critical = spec.Critical;
                return;
            }

            hasThreshold = false;
            low = 0;
            critical = 0;
        }

        private void ApplyStored()
        {
            LowValue = observedHasThreshold ? observedLow : 0;
            CriticalValue = observedHasThreshold ? observedCritical : 0;
            LowBuffer = LowValue.ToString();
            CriticalBuffer = CriticalValue.ToString();
        }
    }
}
