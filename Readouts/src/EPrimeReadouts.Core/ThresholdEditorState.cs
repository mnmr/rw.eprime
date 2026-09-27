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
        private string? selectedCanonical;
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

        /// Applies the text a field returned this pass. Blank reads as 0 (the
        /// value an unset field shows) so the submitted value always matches
        /// the field; non-digits and out-of-range numbers are rejected.
        public void EditLow(string edited) => Edit(edited, ref LowValue, ref LowBuffer);

        public void EditCritical(string edited) =>
            Edit(edited, ref CriticalValue, ref CriticalBuffer);

        private static void Edit(string edited, ref int value, ref string buffer)
        {
            if (string.Equals(edited, buffer, System.StringComparison.Ordinal)) return;
            if (edited.Length == 0)
            {
                buffer = "";
                value = 0;
                return;
            }
            for (int i = 0; i < edited.Length; i++)
                if (edited[i] < '0' || edited[i] > '9')
                    return;
            if (!int.TryParse(edited, out int parsed)) return;
            if (parsed < 0 || parsed > 999999) return;
            buffer = edited;
            value = parsed;
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
