using EPrimeReadouts.Core;
using Verse;

namespace EPrimeReadouts
{
    /// Monotonic UI cache stamp. WrText.FitWidth caches measured widths against
    /// this; bump it if cached text metrics can go stale (e.g. UI scale or font
    /// changes).
    public static class UiVersion
    {
        private static readonly UiMetricRevision revision = new UiMetricRevision();

        public static int Current => revision.Current;

        public static void ObserveCurrentMetrics() =>
            revision.Observe(Prefs.UIScale, Prefs.DisableTinyText);

        public static void Bump()
        {
            ObserveCurrentMetrics();
            revision.Bump();
        }
    }
}
