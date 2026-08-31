using RimShared.Common;
using Verse;

namespace Implanner
{
    /// Presentation stamp for UI caches: advances on Implanner mutations and
    /// on UI metric changes (UI scale, tiny-font preference, language). A
    /// metric change must not invalidate model or count snapshots — consumers
    /// that only depend on model state gate on the store revisions instead.
    public static class UiVersion
    {
        private static readonly UiMetricRevision revision = new UiMetricRevision();

        public static int Current => revision.Current;

        /// Advances only when the language changes; definition catalogs gate
        /// on this instead of Current so planner mutations don't rebuild them.
        public static int LanguageCurrent => revision.LanguageCurrent;

        /// Observed once per frame from the window before drawing.
        public static void ObserveCurrentMetrics() =>
            revision.Observe(
                Prefs.UIScale,
                Prefs.DisableTinyText,
                LanguageDatabase.activeLanguage?.folderName ?? string.Empty);

        public static void Bump()
        {
            ObserveCurrentMetrics();
            revision.Bump();
        }
    }
}
