using UnityEngine;
using Verse;

namespace RimShared.UiLib
{
    /// A framed segmented tab-menu row: equal-width segments inside a panel
    /// box. Self-contained (palette included) so every mod renders the
    /// control identically.
    ///
    /// All geometry is computed on the physical pixel grid: the frame is
    /// snapped to device pixels, the outline, the padding ring inside it and
    /// the separators between segments are each exactly one device pixel at
    /// any UI scale, and the leftover from the equal width split widens the
    /// leftmost segments by one pixel apiece. Logical-unit math left
    /// fractional physical paddings, so opposite sides of the box rendered
    /// unevenly under fractional UI scales.
    public static class SegmentedControl
    {
        public static readonly Color PanelBackground = new Color(0.08f, 0.08f, 0.08f, 0.9f);
        public static readonly Color PanelOutline = new Color(1f, 1f, 1f, 0.15f);
        private static readonly Color LabelActive = Color.white;
        private static readonly Color LabelInactive = new Color(0.85f, 0.85f, 0.85f);
        private static readonly Color FillActive = new Color(1f, 1f, 1f, 0.16f);
        private static readonly Color FillInactive = new Color(1f, 1f, 1f, 0.04f);

        /// Draws the framed row and returns the clicked segment index, or -1.
        public static int Row(Rect rect, string[] labels, int active)
        {
            float scale = Prefs.UIScale > 0f ? Prefs.UIScale : 1f;
            float left = Mathf.Round(rect.x * scale);
            float top = Mathf.Round(rect.y * scale);
            float right = Mathf.Round(rect.xMax * scale);
            float bottom = Mathf.Round(rect.yMax * scale);

            Widgets.DrawBoxSolid(
                Logical(left, top, right - left, bottom - top, scale),
                PanelBackground);
            DrawEdges(left, top, right, bottom, scale);

            // 1px outline + 1px padding on every side; 1px between segments.
            const float Inset = 2f;
            int count = labels.Length;
            float innerWidth = right - left - 2f * Inset - (count - 1);
            if (innerWidth < count) innerWidth = count;
            float segmentWidth = Mathf.Floor(innerWidth / count);
            int widened = (int)(innerWidth - segmentWidth * count);
            float segmentTop = top + Inset;
            float segmentHeight = Mathf.Max(1f, bottom - top - 2f * Inset);
            int clicked = -1;
            float x = left + Inset;
            for (int i = 0; i < count; i++)
            {
                float width = segmentWidth + (i < widened ? 1f : 0f);
                Rect segment = Logical(x, segmentTop, width, segmentHeight, scale);
                if (Tab(segment, labels[i], i == active))
                    clicked = i;
                x += width + 1f;
            }
            return clicked;
        }

        /// One segment: filled when active, hover-highlighted otherwise.
        /// Returns true on click.
        public static bool Tab(Rect rect, string label, bool active)
        {
            GameFont font = Text.Font;
            TextAnchor anchor = Text.Anchor;
            Color color = GUI.color;
            try
            {
                Widgets.DrawBoxSolid(rect, active ? FillActive : FillInactive);
                if (!active) Widgets.DrawHighlightIfMouseover(rect);
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = active ? LabelActive : LabelInactive;
                Widgets.Label(rect, label);
                return Widgets.ButtonInvisible(rect);
            }
            finally
            {
                Text.Font = font;
                Text.Anchor = anchor;
                GUI.color = color;
            }
        }

        /// One-device-pixel outline drawn over the frame's border, matching
        /// DrawBoxSolidWithOutline's layering (edges over the background).
        private static void DrawEdges(
            float left, float top, float right, float bottom, float scale)
        {
            float width = right - left;
            float height = bottom - top;
            Widgets.DrawBoxSolid(Logical(left, top, width, 1f, scale), PanelOutline);
            Widgets.DrawBoxSolid(Logical(left, bottom - 1f, width, 1f, scale), PanelOutline);
            Widgets.DrawBoxSolid(Logical(left, top + 1f, 1f, height - 2f, scale), PanelOutline);
            Widgets.DrawBoxSolid(Logical(right - 1f, top + 1f, 1f, height - 2f, scale), PanelOutline);
        }

        /// Physical-pixel rect expressed in logical GUI units.
        private static Rect Logical(float x, float y, float width, float height,
            float scale) => new Rect(x / scale, y / scale, width / scale, height / scale);
    }
}
