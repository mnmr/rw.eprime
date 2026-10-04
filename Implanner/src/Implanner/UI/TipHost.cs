namespace RimShared.UiLib
{
    /// Implanner's settings for the shared structured tooltip stack
    /// (Shared/UiLib/Tips).
    internal static class TipHost
    {
        internal const int WindowId = 0x494D5054; // IMPT
        internal const float TableInset = 16f;

        /// Stamps cached tooltip geometry.
        internal static int ObserveMetricRevision()
        {
            UiRevision.ObserveCurrentMetrics();
            return UiRevision.Current;
        }

        /// Clears the text-tip registry when it moves.
        internal static int ObserveRegistryRevision() => ObserveMetricRevision();
    }
}
