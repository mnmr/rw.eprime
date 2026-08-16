using System;
using HarmonyLib;
using LudeonTK;
using RimShared.Common;
using UnityEngine;
using Verse;
using WorkRoles.Core;

namespace WorkRoles.UI
{
    public static class WrText
    {
        private static Func<Vector2, Vector2>? unclip;
        private static bool unclipResolved;

        /// Label rising at an angle out of a column header, its lower-left
        /// corner anchored to the column's bottom-right, underlined. Adapted
        /// from CaptainArbitrary's CompactWorkTab (MIT).
        public static void InclinedLabel(
            Rect columnRect,
            string label,
            Vector2 labelSize,
            InclinedLabelGeometry geometry,
            float degrees,
            Color? labelColor = null)
        {
            var oldFont = Text.Font;
            var oldColor = GUI.color;
            var oldAnchor = Text.Anchor;
            bool oldWrap = Text.WordWrap;
            Matrix4x4 oldMatrix = GUI.matrix;
            try
            {
                Text.Font = GameFont.Small;
                var rotated = new Rect(0f, 0f, labelSize.x, labelSize.y)
                {
                    center = new Vector2(
                        columnRect.xMax + geometry.AnchorToCenterX,
                        columnRect.yMax + geometry.AnchorToCenterY)
                };

                float theta = Mathf.Deg2Rad * degrees;
                if (!TryApplyInclinedTransform(
                        rotated.center, degrees, out _))
                    return;

                GUI.color = labelColor ?? new Color(0.8f, 0.8f, 0.8f);
                Text.Anchor = TextAnchor.MiddleLeft;
                Text.WordWrap = false;
                // Text sits 2px SCREEN-right of the line's position so it clears the
                // preceding column's separator; in the pre-rotation frame a screen
                // offset needs the inverse rotation applied.
                var textRect = rotated;
                textRect.x += 2f * Mathf.Cos(theta);
                textRect.y += 2f * Mathf.Sin(theta);
                Widgets.Label(textRect, label);
                GUI.color = oldColor;
                Vector2 lineStart = new Vector2(rotated.xMin, rotated.yMax);
                Widgets.DrawLine(lineStart,
                    new Vector2(lineStart.x + columnRect.height, lineStart.y),
                    new Color(1f, 1f, 1f, 0.2f), 1f);
            }
            finally
            {
                Text.WordWrap = oldWrap;
                Text.Anchor = oldAnchor;
                GUI.color = oldColor;
                GUI.matrix = oldMatrix;
                Text.Font = oldFont;
            }
        }

        /// Invisible hit target transformed exactly like the inclined label, so
        /// clicking the visible text selects its column rather than whichever
        /// vertical strip happens to lie beneath that part of the label.
        public static bool InclinedLabelButton(
            Rect columnRect,
            Vector2 labelSize,
            InclinedLabelGeometry geometry,
            float degrees)
        {
            Matrix4x4 oldMatrix = GUI.matrix;
            var rotated = new Rect(0f, 0f, labelSize.x, labelSize.y)
            {
                center = new Vector2(
                    columnRect.xMax + geometry.AnchorToCenterX,
                    columnRect.yMax + geometry.AnchorToCenterY)
            };
            if (!TryApplyInclinedTransform(
                    rotated.center, degrees, out _))
                return false;
            try
            {
                return Widgets.ButtonInvisible(rotated);
            }
            finally
            {
                GUI.matrix = oldMatrix;
            }
        }

        private static bool TryApplyInclinedTransform(
            Vector2 localPivot,
            float degrees,
            out Matrix4x4 originalMatrix)
        {
            // Compact Work Tab's critical workaround: GUIClip.Unclip must run
            // while GUI.matrix is identity. That converts the group-local pivot
            // into the true screen coordinate expected by the matrix rotation.
            originalMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.identity;
            if (!TryUnclip(localPivot, out Vector2 screenPivot))
            {
                GUI.matrix = originalMatrix;
                return false;
            }

            Matrix4x4 transform = originalMatrix;
            transform *= Matrix4x4.TRS(
                screenPivot, Quaternion.identity, Vector3.one);
            transform *= Matrix4x4.TRS(
                Vector3.zero,
                Quaternion.Euler(0f, 0f, -degrees),
                Vector3.one);
            transform *= Matrix4x4.TRS(
                -screenPivot, Quaternion.identity, Vector3.one);
            GUI.matrix = transform;
            return true;
        }

        private static bool TryUnclip(Vector2 point, out Vector2 result)
        {
            result = default;
            if (!unclipResolved)
            {
                unclipResolved = true;
                try
                {
                    Type guiClip = typeof(GUI).Assembly.GetType("UnityEngine.GUIClip");
                    var method = AccessTools.Method(
                        guiClip, "Unclip", new[] { typeof(Vector2) });
                    if (method == null || !method.IsStatic
                        || method.ReturnType != typeof(Vector2))
                        throw new MissingMethodException(
                            "UnityEngine.GUIClip", "Unclip(Vector2) -> Vector2");
                    unclip = AccessTools.MethodDelegate<Func<Vector2, Vector2>>(method);
                }
                catch (Exception exception)
                {
                    Log.Warning("[WorkRoles] Inclined priority-grid headers disabled: "
                        + exception.Message);
                }
            }

            if (unclip == null) return false;
            try
            {
                result = unclip(point);
                return true;
            }
            catch (Exception exception)
            {
                unclip = null;
                Log.Warning("[WorkRoles] Inclined priority-grid headers disabled "
                    + "after GUIClip.Unclip failed: " + exception.Message);
                return false;
            }
        }

        // Compatibility path for callers outside WorkRoles. The priority grid
        // uses the premeasured overload above and never measures during draw.
        public static void InclinedLabel(Rect columnRect, string label, float degrees)
        {
            var oldFont = Text.Font;
            Vector2 labelSize;
            try
            {
                Text.Font = GameFont.Small;
                labelSize = Text.CalcSize(label);
            }
            finally
            {
                Text.Font = oldFont;
            }
            InclinedLabel(columnRect, label, labelSize,
                InclinedLabelGeometry.Calculate(labelSize.x, labelSize.y, degrees),
                degrees);
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
        /// Memoized: CalcSize is the bottom of every chip/label measurement and
        /// runs thousands of times per frame otherwise (see UiVersion).
        private static readonly System.Collections.Generic.Dictionary<(GameFont, string), float> fitWidths
            = new System.Collections.Generic.Dictionary<(GameFont, string), float>();
        private static int fitWidthsStamp = -1;

        internal static void ClearFitWidthCache()
        {
            fitWidths.Clear();
            fitWidthsStamp = -1;
        }

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
            try
            {
                Text.Font = GameFont.Medium;
                Widgets.Label(new Rect(rect.x, rect.y - MediumTopBearing,
                    rect.width, rect.height + MediumTopBearing), text);
            }
            finally
            {
                Text.Font = oldFont;
            }
        }
    }
}
