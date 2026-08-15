using System;

namespace EPrimeReadouts.Core
{
    /// <summary>
    /// Monotonic UI revisions. Current covers every input that can change
    /// measured text; LanguageCurrent advances only for translated content.
    /// </summary>
    public sealed class UiMetricRevision
    {
        private bool initialized;
        private float uiScale;
        private bool disableTinyText;
        private string language;

        public int Current { get; private set; }
        public int LanguageCurrent { get; private set; }

        public void Observe(float scale, bool disableTinyText, string language)
        {
            if (!initialized)
            {
                uiScale = scale;
                this.disableTinyText = disableTinyText;
                this.language = language;
                initialized = true;
                return;
            }

            bool languageChanged = !string.Equals(
                this.language, language, StringComparison.Ordinal);
            if (uiScale.Equals(scale) && this.disableTinyText == disableTinyText
                && !languageChanged) return;

            uiScale = scale;
            this.disableTinyText = disableTinyText;
            this.language = language;
            Current++;
            if (languageChanged) LanguageCurrent++;
        }

        public void Bump() => Current++;
    }
}
