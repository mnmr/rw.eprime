namespace RimShared.UiLib
{
    /// WorkRoles' settings for the shared structured tooltip stack
    /// (Shared/UiLib/Tips). Tables run edge to edge (no inset).
    internal static class TipHost
    {
        internal const int WindowId = 0x57525450; // WRTP
        internal const float TableInset = 0f;

        /// Stamps cached tooltip geometry.
        internal static int ObserveMetricRevision()
        {
            UiRevision.ObserveCurrentMetrics();
            return UiRevision.Current;
        }

        /// Translated tips depend only on the language.
        internal static int ObserveRegistryRevision() =>
            WorkRoles.LanguageChangeCoordinator.Revision;
    }
}
