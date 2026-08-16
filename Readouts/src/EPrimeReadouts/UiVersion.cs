using EPrimeReadouts.Core;
using RimShared.Common;
using Verse;

namespace EPrimeReadouts
{
    /// Monotonic UI cache stamps. Current covers measured text while
    /// LanguageCurrent lets translation-only caches ignore metric-only changes.
    public static class UiVersion
    {
        private static readonly UiMetricRevision revision = new UiMetricRevision();

        public static int Current => revision.Current;
        public static int LanguageCurrent => revision.LanguageCurrent;

        public static void ObserveCurrentMetrics() =>
            revision.Observe(
                Prefs.UIScale,
                Prefs.DisableTinyText,
                LanguageDatabase.activeLanguage?.folderName!); // Observe stores/compares null-safely

        public static void Bump()
        {
            ObserveCurrentMetrics();
            revision.Bump();
        }
    }
}
