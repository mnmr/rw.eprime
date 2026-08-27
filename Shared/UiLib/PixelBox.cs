using UnityEngine;
using Verse;

namespace RimShared.UiLib
{
    /// Pixel-snapped box drawing. Logical-unit outlines
    /// (Widgets.DrawBoxSolidWithOutline / Widgets.DrawBox) cover one or two
    /// physical pixels per edge under fractional UI scales, and an edge quad
    /// can rasterize past the fill quad, tinting the surface behind the box.
    /// These helpers snap the frame to device pixels and draw every edge as
    /// exactly one device pixel inside the fill at any UI scale, matching
    /// SegmentedControl's physical-grid geometry.
    public static class PixelBox
    {
        /// Solid fill with a one-device-pixel outline drawn over its edges.
        public static void SolidWithOutline(Rect rect, Color fill, Color outline)
        {
            float scale = Prefs.UIScale > 0f ? Prefs.UIScale : 1f;
            float left = Mathf.Round(rect.x * scale);
            float top = Mathf.Round(rect.y * scale);
            float right = Mathf.Round(rect.xMax * scale);
            float bottom = Mathf.Round(rect.yMax * scale);
            Widgets.DrawBoxSolid(
                Logical(left, top, right - left, bottom - top, scale), fill);
            DrawEdges(left, top, right, bottom, scale, outline);
        }

        /// One-device-pixel outline only (no fill), snapped like
        /// SolidWithOutline so both can layer on the same rect.
        public static void Outline(Rect rect, Color color)
        {
            float scale = Prefs.UIScale > 0f ? Prefs.UIScale : 1f;
            DrawEdges(
                Mathf.Round(rect.x * scale), Mathf.Round(rect.y * scale),
                Mathf.Round(rect.xMax * scale), Mathf.Round(rect.yMax * scale),
                scale, color);
        }

        /// Logical rect snapped to the device grid and extended one device
        /// pixel past the bottom edge: a row highlight built from it starts
        /// on the row's own top hairline separator and ends on the next
        /// row's, so both separators sit on highlighted background.
        public static Rect RowHighlightSpan(Rect rect)
        {
            float scale = Prefs.UIScale > 0f ? Prefs.UIScale : 1f;
            float left = Mathf.Round(rect.x * scale);
            float top = Mathf.Round(rect.y * scale);
            float right = Mathf.Round(rect.xMax * scale);
            float bottom = Mathf.Round(rect.yMax * scale) + 1f;
            return Logical(left, top, right - left, bottom - top, scale);
        }

        /// Logical rect for a horizontal hairline: snapped to the device
        /// grid and exactly one device pixel thick at any UI scale.
        /// (Vanilla's AdjustRectToUIScaling floors one edge and ceils the
        /// other, so a one-logical-unit line doubles at fractional scales.)
        public static Rect HairlineHorizontal(float x, float y, float length)
        {
            float scale = Prefs.UIScale > 0f ? Prefs.UIScale : 1f;
            float left = Mathf.Round(x * scale);
            float right = Mathf.Round((x + length) * scale);
            return Logical(left, Mathf.Round(y * scale), right - left, 1f, scale);
        }

        /// Logical rect for a vertical hairline; see HairlineHorizontal.
        public static Rect HairlineVertical(float x, float y, float length)
        {
            float scale = Prefs.UIScale > 0f ? Prefs.UIScale : 1f;
            float top = Mathf.Round(y * scale);
            float bottom = Mathf.Round((y + length) * scale);
            return Logical(Mathf.Round(x * scale), top, 1f, bottom - top, scale);
        }

        private static void DrawEdges(
            float left, float top, float right, float bottom, float scale,
            Color color)
        {
            float width = right - left;
            float height = bottom - top;
            Widgets.DrawBoxSolid(Logical(left, top, width, 1f, scale), color);
            Widgets.DrawBoxSolid(
                Logical(left, bottom - 1f, width, 1f, scale), color);
            Widgets.DrawBoxSolid(
                Logical(left, top + 1f, 1f, height - 2f, scale), color);
            Widgets.DrawBoxSolid(
                Logical(right - 1f, top + 1f, 1f, height - 2f, scale), color);
        }

        /// Physical-pixel rect expressed in logical GUI units.
        private static Rect Logical(float x, float y, float width, float height,
            float scale) => new Rect(x / scale, y / scale, width / scale, height / scale);
    }
}
