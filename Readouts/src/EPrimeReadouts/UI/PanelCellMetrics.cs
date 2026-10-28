using EPrimeReadouts.Core;
using RimShared.Common;
using RimShared.UiLib;
using Verse;

namespace EPrimeReadouts.UI
{
    /// <summary>
    /// Publishes the CellMetrics for the current UI presentation. Counters
    /// use the explicit Tiny/Small choice, defaulting to Tiny. Search captions keep
    /// the game's font independently, including their own measured line box.
    /// Must be read inside OnGUI: measuring uses Verse.Text.
    /// </summary>
    // Cache contract:
    // Owner: process/current UI presentation.
    // Key: none (single value).
    // Value: immutable CellMetrics struct.
    // Dependencies: UiRevision.Current (UI scale, tiny-text preference,
    // language), resolved counter font — the same inputs the layout keys on, so a metric
    // change and the rebuild that consumes it always travel together.
    // Refresh policy: immediate on UI revision or resolved counter-font change.
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
        private static GameFont counterFont;

        internal static GameFont CounterFont => EPrimeReadoutsMod.Settings.renderCountersWithTinyFont
            ? GameFont.Tiny : GameFont.Small;

        public static CellMetrics Current
        {
            get
            {
                GameFont font = CounterFont;
                if (stamp == UiRevision.Current && counterFont == font) return cached;
                using (GuiStateScope.Capture())
                using (TinyText.UseFont(font))
                {
                    // Widths and height use the same selected counter font;
                    // the rounded line box protects fractional-scale glyphs.
                    float maxW = 0f;
                    for (int i = 0; i < WideSamples.Length; i++)
                    {
                        float w = WrText.FitWidth(WideSamples[i]);
                        if (w > maxW) maxW = w;
                    }
                    cached = new CellMetrics(maxW,
                        TinyText.MeasureMetrics(font).LineHeight,
                        TinyText.Metrics.LineHeight);
                }
                counterFont = font;
                stamp = UiRevision.Current;
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
