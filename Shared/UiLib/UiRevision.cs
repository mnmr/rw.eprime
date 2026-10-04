using RimShared.Common;
using Verse;

namespace RimShared.UiLib
{
    /// The mod's UI metric revision, one per mod assembly (shared source
    /// compiles into each mod). Current advances when UI scale, the
    /// tiny-font preference or the language changes; a metric change must
    /// not invalidate model or count snapshots, so caches that depend only
    /// on model state gate on their store revisions instead. WorkRoles'
    /// UiVersion is a different stamp (model mutations); WorkRoles uses this
    /// one for text measurement and tooltip geometry.
    internal static class UiRevision
    {
        private static readonly UiMetricRevision revision = new UiMetricRevision();

        public static int Current => revision.Current;

        /// Advances only when the language changes; translation-only caches
        /// gate on this so metric-only changes keep them.
        public static int LanguageCurrent => revision.LanguageCurrent;

        /// Called before drawing or measuring (window passes, tooltip and
        /// snapshot builders): compares the three inputs and advances the
        /// revisions only when one changed.
        public static void ObserveCurrentMetrics() =>
            revision.Observe(
                Prefs.UIScale,
                Prefs.DisableTinyText,
                LanguageDatabase.activeLanguage?.folderName ?? string.Empty);
    }
}
