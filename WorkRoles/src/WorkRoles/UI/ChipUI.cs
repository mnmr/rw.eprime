using RimWorld;
using UnityEngine;
using Verse;

namespace WorkRoles.UI
{
    /// One chip drawing contract: every chip surface (roles, bands, paths)
    /// composes a spec so box/outline/label/X/grips can never drift apart.
    public struct ChipSpec
    {
        public Color Bg;
        public Color Outline;      // selection/highlight = recolor, never a second ring
        public Color LabelColor;
        public string Label;       // null = no label (Minimal)
        public bool ShowRemove;
        public bool Grips;         // inner band-resize grips at both ends
        public float LabelInsetLeft, LabelInsetRight; // computed by composers (markers etc.)
        public bool StrikeThrough;
    }

    /// Core chip renderer + the shared geometry (remove slot, grip zones).
    public static class ChipUI
    {
        public const float RemoveSize = 16f;
        public const float BandHandleW = 7f;
        // 1px of chip surface outside each grip zone: edge grabs stop landing
        // on the chip outline.
        public const float BandOuterPad = 1f;

        // Grip bars: two 1px verticals centered in a handle zone.
        private static readonly Color GripColor = new Color(1f, 1f, 1f, 0.45f);
        private static readonly Color StrikeColor = new Color(1f, 0.3f, 0.3f, 0.75f);

        /// The remove-icon rect Draw uses on grip-less chips — for callers
        /// that run their own hit-testing on non-interactive chips.
        public static Rect RemoveRect(Rect chipRect) =>
            new Rect(chipRect.xMax - RemoveSize - 3f, chipRect.y + (chipRect.height - RemoveSize) / 2f,
                RemoveSize, RemoveSize);

        /// Grip zones inside a band chip's ends (outer pad included); the
        /// remove X sits left of the right grip so resize and dismiss cannot collide.
        public static Rect BandLeftHandle(Rect chipRect) =>
            new Rect(chipRect.x, chipRect.y, BandOuterPad + BandHandleW, chipRect.height);
        public static Rect BandRightHandle(Rect chipRect) =>
            new Rect(chipRect.xMax - BandHandleW - BandOuterPad, chipRect.y,
                BandHandleW + BandOuterPad, chipRect.height);
        public static Rect BandRemoveRect(Rect chipRect) =>
            new Rect(chipRect.xMax - BandOuterPad - BandHandleW - 2f - RemoveSize,
                chipRect.y + (chipRect.height - RemoveSize) / 2f, RemoveSize, RemoveSize);

        private static void DrawGrip(Rect zone)
        {
            Widgets.DrawBoxSolid(new Rect(zone.center.x - 2f, zone.y + 5f, 1f, zone.height - 10f), GripColor);
            Widgets.DrawBoxSolid(new Rect(zone.center.x + 1f, zone.y + 5f, 1f, zone.height - 10f), GripColor);
        }

        /// Draws one chip from a spec: box, grips, label, strike, remove X.
        /// Display-only; composers own hit-testing and interaction.
        public static void Draw(Rect rect, in ChipSpec spec)
        {
            Widgets.DrawBoxSolidWithOutline(rect, spec.Bg, spec.Outline);

            if (spec.Grips)
            {
                // Grip bars sit BandOuterPad inside the drawn edge (the hit
                // zones start at the edge).
                DrawGrip(new Rect(rect.x + BandOuterPad, rect.y, BandHandleW, rect.height));
                DrawGrip(new Rect(rect.xMax - BandOuterPad - BandHandleW, rect.y, BandHandleW, rect.height));
            }

            var labelRect = new Rect(rect.x + spec.LabelInsetLeft, rect.y,
                Mathf.Max(rect.width - spec.LabelInsetLeft - spec.LabelInsetRight, 0f), rect.height);

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            // Never wrap: a rect that comes up a pixel short at fractional UI
            // scales must clip the last glyph, not spill a second line out of
            // the chip.
            bool wrap = Text.WordWrap;
            Text.WordWrap = false;
            GUI.color = spec.LabelColor;
            if (spec.Label != null)
                Widgets.Label(labelRect, spec.Label);
            GUI.color = Color.white;
            Text.WordWrap = wrap;
            Text.Anchor = TextAnchor.UpperLeft;

            if (spec.StrikeThrough)
            {
                GUI.color = StrikeColor;
                WrText.LineHorizontal(labelRect.x, rect.y + rect.height / 2f, labelRect.width);
                GUI.color = Color.white;
            }

            if (spec.ShowRemove)
                GUI.DrawTexture(spec.Grips ? BandRemoveRect(rect) : RemoveRect(rect), TexButton.Delete);
        }
    }
}
