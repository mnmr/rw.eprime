using System;

namespace QualityJobs.Core
{
    /// <summary>
    /// Monotonic text-metric revision driven by the runtime preferences that
    /// can change measured UI text without changing its logical width.
    /// (Ported from EPrimeReadouts; keep in lockstep.)
    /// </summary>
    public sealed class UiMetricRevision
    {
        private bool initialized;
        private float uiScale;
        private bool disableTinyText;
        private string? language;

        public int Current { get; private set; }

        public void Observe(float scale, bool disableTinyText, string? language)
        {
            if (!initialized)
            {
                uiScale = scale;
                this.disableTinyText = disableTinyText;
                this.language = language;
                initialized = true;
                return;
            }

            if (uiScale.Equals(scale) && this.disableTinyText == disableTinyText
                && string.Equals(this.language, language, StringComparison.Ordinal)) return;

            uiScale = scale;
            this.disableTinyText = disableTinyText;
            this.language = language;
            Current++;
        }

        public void Bump() => Current++;
    }
}
