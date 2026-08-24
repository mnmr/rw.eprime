using EPrimeReadouts.Core;
using RimShared.Common;
using RimShared.UiLib;
using Verse;

namespace EPrimeReadouts.UI
{
    /// <summary>
    /// Publishes the CellMetrics for the current UI presentation. Counters
    /// render in GameFont.Tiny, but RimWorld substitutes the Small font when
    /// tiny text is unavailable ("disable tiny text", a language without
    /// tiny-font support, Steam Deck); the layout must then widen and heighten
    /// counter boxes to the resolved font or numbers get clipped.
    /// Must be read inside OnGUI: measuring uses Verse.Text.
    /// </summary>
    // Cache contract:
    // Owner: process/current UI presentation.
    // Key: none (single value).
    // Value: immutable CellMetrics struct.
    // Dependencies: UiVersion.Current (UI scale, tiny-text preference,
    // language) — the same revision the layout rebuild keys on, so a metric
    // change and the rebuild that consumes it always travel together.
    // Refresh policy: immediate on UI revision change.
    // Equality policy: value struct; equal rebuilds are naturally identical.
    // Teardown: Reset clears the stamp and cached value.
    public static class PanelCellMetrics
    {
        /// Widest counter strings CountFormat.Compact can emit per range:
        /// full digits to 10000, one-decimal k/M, and the int.MaxValue "M"
        /// form. The measured maximum sizes the counter cell.
        private static readonly string[] WideSamples = { "10000", "99.9k", "2147M" };

        private static CellMetrics cached;
        private static int stamp = -1;

        public static CellMetrics Current
        {
            get
            {
                if (stamp == UiVersion.Current) return cached;
                using (new GuiStateScope())
                {
                    // Resolves to Small when tiny text is unavailable; both
                    // FitWidth and LineHeight then measure the resolved font;
                    // the published line box is rounded up to avoid fractional
                    // glyph-row clipping.
                    float maxW = 0f;
                    for (int i = 0; i < WideSamples.Length; i++)
                    {
                        float w = WrText.FitTinyWidth(WideSamples[i]);
                        if (w > maxW) maxW = w;
                    }
                    TinyTextMetrics shared = TinyText.Metrics;
                    var textMetrics = new ResolvedTinyTextMetrics(
                        shared.LineHeight,
                        shared.Font == GameFont.Small);
                    cached = new CellMetrics(maxW, textMetrics.LineHeight);
                }
                stamp = UiVersion.Current;
                return cached;
            }
        }

        internal static void Reset()
        {
            cached = default;
            stamp = -1;
        }
    }
}
