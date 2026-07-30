namespace EPrimeReadouts.Core
{
    /// <summary>
    /// Monotonic text-metric revision driven by the runtime preferences that
    /// can change measured UI text without changing its logical width.
    /// </summary>
    public sealed class UiMetricRevision
    {
        private bool initialized;
        private float uiScale;
        private bool disableTinyText;

        public int Current { get; private set; }

        public void Observe(float scale, bool disableTinyText)
        {
            if (!initialized)
            {
                uiScale = scale;
                this.disableTinyText = disableTinyText;
                initialized = true;
                return;
            }

            if (uiScale.Equals(scale) && this.disableTinyText == disableTinyText) return;

            uiScale = scale;
            this.disableTinyText = disableTinyText;
            Current++;
        }

        public void Bump() => Current++;
    }
}
