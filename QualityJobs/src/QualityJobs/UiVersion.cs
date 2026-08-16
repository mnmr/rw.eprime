using QualityJobs.Core;
using RimShared.Common;
using Verse;

namespace QualityJobs
{
    /// Monotonic UI cache stamp. WrText.FitWidth and WrTips cache against
    /// this; it advances when UI scale, tiny-text preference, or the active
    /// language changes. (Ported from EPrimeReadouts; keep in lockstep.)
    public static class UiVersion
    {
        private static readonly UiMetricRevision revision = new UiMetricRevision();

        public static int Current => revision.Current;

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
