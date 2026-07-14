using System;
using HarmonyLib;
using LudeonTK;
using UnityEngine;
using Verse;

namespace WorkRoles.UI
{
    public static class WrText
    {
        // GUIClip is Unity-internal; Unclip converts group-local to screen
        // coordinates — matrix rotation inside a GUI window needs it (vanilla's
        // UI.RotateAroundPivot only compensates for UI scale, not group offsets).
        private static readonly Func<Vector2, Vector2> Unclip =
            AccessTools.MethodDelegate<Func<Vector2, Vector2>>(
                AccessTools.Method(typeof(GUI).Assembly.GetType("UnityEngine.GUIClip"),
                    "Unclip", new[] { typeof(Vector2) }));

        /// Label rising at an angle out of a column header, its lower-left
        /// corner anchored to the column's bottom-right, underlined. Adapted
        /// from CaptainArbitrary's CompactWorkTab (MIT).
        public static void InclinedLabel(Rect columnRect, string label, float degrees)
        {
            var oldFont = Text.Font;
            Text.Font = GameFont.Small;
            Vector2 labelSize = Text.CalcSize(label);
            var rotated = new Rect(0f, 0f, columnRect.height, labelSize.y) { center = columnRect.center };

            // Offset so the label's bottom-left corner lands on the column's
            // bottom-right after rotation.
            float theta = Mathf.Deg2Rad * degrees;
            Vector2 center = rotated.center;
            var cRelative = new Vector2(-rotated.width / 2f, -rotated.height / 2f);
            var cPrime = new Vector2(
                Mathf.Cos(theta) * cRelative.x - Mathf.Sin(theta) * cRelative.y + center.x,
                Mathf.Sin(theta) * cRelative.x + Mathf.Cos(theta) * cRelative.y + center.y);
            rotated.x += columnRect.xMax - cPrime.x;

            Matrix4x4 originalMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.identity;
            Vector2 pivot = Unclip(rotated.center);
            Matrix4x4 transform = originalMatrix;
            transform *= Matrix4x4.TRS(pivot, Quaternion.identity, Vector3.one);
            transform *= Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, 0f, -degrees), Vector3.one);
            transform *= Matrix4x4.TRS(-pivot, Quaternion.identity, Vector3.one);
            GUI.matrix = transform;

            var oldColor = GUI.color;
            var oldAnchor = Text.Anchor;
            bool oldWrap = Text.WordWrap;
            GUI.color = new Color(0.8f, 0.8f, 0.8f);
            Text.Anchor = TextAnchor.MiddleLeft;
            Text.WordWrap = false;
            // Text sits 2px SCREEN-right of the line's position so it clears the
            // preceding column's separator; in the pre-rotation frame a screen
            // offset needs the inverse rotation applied.
            var textRect = rotated;
            textRect.x += 2f * Mathf.Cos(theta);
            textRect.y += 2f * Mathf.Sin(theta);
            Widgets.Label(textRect, label);
            Widgets.DrawLine(new Vector2(rotated.xMax, rotated.yMax),
                new Vector2(rotated.xMin, rotated.yMax), new Color(1f, 1f, 1f, 0.2f), 1f);
            Text.WordWrap = oldWrap;
            Text.Anchor = oldAnchor;
            GUI.color = oldColor;
            GUI.matrix = originalMatrix;
            Text.Font = oldFont;
        }

        /// Pixel-snapped 1px lines, tinted by the ambient GUI.color: an
        /// unsnapped hairline blurs (or doubles) at fractional UI scales.
        public static void LineVertical(float x, float y, float length)
            => GUI.DrawTexture(UIScaling.AdjustRectToUIScaling(new Rect(x, y, 1f, length)),
                BaseContent.WhiteTex);

        public static void LineHorizontal(float x, float y, float length)
            => GUI.DrawTexture(UIScaling.AdjustRectToUIScaling(new Rect(x, y, length, 1f)),
                BaseContent.WhiteTex);

        /// Width that safely fits a single-line label at any UI scale, measured
        /// with the CURRENT font. Text.CalcSize measures in virtual units, but at
        /// fractional UI scales (0.9, 1.25, …) physical-pixel glyph rounding can
        /// render text a few pixels wider than measured — an exact-fit rect then
        /// wraps or clips. 2% + 2px absorbs the drift; ceil lands on whole pixels.
        public static float FitWidth(string text)
            => Mathf.Ceil(Text.CalcSize(text).x * 1.02f + 2f);

        /// Medium-font glyphs start ~8px below the label rect's top (internal
        /// leading), measured against the stats panel's portrait frame. Public:
        /// callers that WANT that leading as visible spacing (headers directly
        /// under a panel edge) add it back onto rect.y.
        internal const float MediumTopBearing = 8f;

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
