using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RimShared.UiLib
{
    /// Fit-width measurement and device-pixel hairlines shared by every mod.
    internal static class WrText
    {
        /// Device-pixel hairlines, tinted by the ambient GUI.color: exactly
        /// one physical pixel thick at any UI scale. An unsnapped hairline
        /// blurs (or doubles) at fractional UI scales, so the geometry comes
        /// from the shared device-grid helper.
        public static void LineHorizontal(float x, float y, float length)
            => GUI.DrawTexture(PixelBox.HairlineHorizontal(x, y, length),
                BaseContent.WhiteTex);

        public static void LineVertical(float x, float y, float length)
            => GUI.DrawTexture(PixelBox.HairlineVertical(x, y, length),
                BaseContent.WhiteTex);

        /// Width that safely fits a single-line label at any UI scale, measured
        /// with the CURRENT font. Text.CalcSize measures in virtual units, but at
        /// fractional UI scales (0.9, 1.25, ...) physical-pixel glyph rounding can
        /// render text a few pixels wider than measured; an exact-fit rect then
        /// wraps or clips. 2% + 2px absorbs the drift; ceil lands on whole pixels.
        /// Memoized: CalcSize is the bottom of every chip/label measurement.
        // Cache contract:
        // Owner: process, one per mod assembly (shared source compiles into
        //   each mod, so mods never share entries).
        // Key: GameFont and exact text.
        // Value: measured single-line width; an immutable float.
        // Dependencies: key plus UiRevision.Current (UI scale, tiny-font
        //   preference, language), which each mod observes before drawing.
        // Refresh policy: immediate clear on the first read after the revision
        //   moves.
        // Equality policy: unchanged keys return the cached float.
        // Teardown: Reset clears all measurements (each mod's world teardown;
        //   WorkRoles also on window data release).
        private static readonly Dictionary<(GameFont, string), float> fitWidths
            = new Dictionary<(GameFont, string), float>();
        private static int fitWidthsStamp = -1;

        public static float FitWidth(string text)
        {
            int revision = UiRevision.Current;
            if (fitWidthsStamp != revision)
            {
                fitWidths.Clear();
                fitWidthsStamp = revision;
            }
            var key = (Text.Font, text);
            if (!fitWidths.TryGetValue(key, out float width))
                fitWidths[key] = width = MeasureFitWidth(text);
            return width;
        }

        /// The same fit measurement without the memo: for snapshot-generated
        /// sentences that are measured once inside their revision-gated
        /// builder and stored on the snapshot, so they never grow the static
        /// key set above. The caller establishes Text.Font first.
        public static float MeasureFitWidth(string text) =>
            Mathf.Ceil(Text.CalcSize(text).x * 1.02f + 2f);

        /// Width measured with TinyText's effective font, including the Small
        /// fallback, through the same memo.
        public static float FitTinyWidth(string text)
        {
            using (TinyText.UseFont())
                return FitWidth(text);
        }

        public static void Reset()
        {
            fitWidths.Clear();
            fitWidthsStamp = -1;
        }
    }
}
