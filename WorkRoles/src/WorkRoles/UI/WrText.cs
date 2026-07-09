using UnityEngine;
using Verse;

namespace WorkRoles.UI
{
    public static class WrText
    {
        /// Width that safely fits a single-line label at any UI scale, measured
        /// with the CURRENT font. Text.CalcSize measures in virtual units, but at
        /// fractional UI scales (0.9, 1.25, …) physical-pixel glyph rounding can
        /// render text a few pixels wider than measured — an exact-fit rect then
        /// wraps or clips. 2% + 2px absorbs the drift; ceil lands on whole pixels.
        public static float FitWidth(string text)
            => Mathf.Ceil(Text.CalcSize(text).x * 1.02f + 2f);

        /// Medium-font glyphs start ~8px below the label rect's top (internal
        /// leading), measured against the stats panel's portrait frame.
        private const float MediumTopBearing = 8f;

        /// Section header: Medium font, drawn plainly (no matrix scaling — a scale
        /// pivot drifts with the header's on-screen position and UI scale), shifted
        /// up by the font's top bearing so the VISIBLE text top sits at rect.y.
        public static void HeaderLabel(Rect rect, string text)
        {
            var oldFont = Text.Font;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, rect.y - MediumTopBearing, rect.width, rect.height + MediumTopBearing), text);
            Text.Font = oldFont;
        }
    }
}
