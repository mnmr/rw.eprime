using UnityEngine;
using Verse;

namespace WorkRoles.UI
{
    public static class WrText
    {
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
