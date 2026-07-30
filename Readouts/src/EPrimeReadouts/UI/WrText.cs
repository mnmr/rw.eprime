using UnityEngine;
using Verse;

namespace EPrimeReadouts.UI
{
    public static class WrText
    {
        /// Pixel-snapped 1px horizontal line, tinted by the ambient GUI.color:
        /// an unsnapped hairline blurs (or doubles) at fractional UI scales.
        public static void LineHorizontal(float x, float y, float length)
            => GUI.DrawTexture(new Rect(x, y, length, 1f), BaseContent.WhiteTex);

        /// Width that safely fits a single-line label at any UI scale, measured
        /// with the CURRENT font. Text.CalcSize measures in virtual units, but at
        /// fractional UI scales (0.9, 1.25, …) physical-pixel glyph rounding can
        /// render text a few pixels wider than measured — an exact-fit rect then
        /// wraps or clips. 2% + 2px absorbs the drift; ceil lands on whole pixels.
        /// Memoized: CalcSize is the bottom of every chip/label measurement and
        /// runs thousands of times per frame otherwise (see UiVersion).
        // Cache contract:
        // Owner: process/current UI presentation.
        // Key: GameFont and exact text.
        // Value: measured single-line width.
        // Dependencies: key plus UiVersion.Current (scale/font/language metrics).
        // Refresh policy: immediate clear on UI revision change.
        // Equality policy: unchanged keys return the cached float.
        // Teardown: Reset clears all measurements.
        private static readonly System.Collections.Generic.Dictionary<(GameFont, string), float> fitWidths
            = new System.Collections.Generic.Dictionary<(GameFont, string), float>();
        private static int fitWidthsStamp = -1;

        public static float FitWidth(string text)
        {
            if (fitWidthsStamp != UiVersion.Current)
            {
                fitWidths.Clear();
                fitWidthsStamp = UiVersion.Current;
            }
            var key = (Text.Font, text);
            if (!fitWidths.TryGetValue(key, out float width))
                fitWidths[key] = width = Mathf.Ceil(Text.CalcSize(text).x * 1.02f + 2f);
            return width;
        }


        internal static void Reset()
        {
            fitWidths.Clear();
            fitWidthsStamp = -1;
        }
    }
}
