using System;
using EPrimeReadouts.Core;
using UnityEngine;
using Verse;

namespace EPrimeReadouts.UI
{
    /// Shared dialog styling (WorkRoles-derived palette).
    internal static class EprStyle
    {
        private struct CaptionMeasureState
        {
            internal string Caption;
            internal float Width;
        }

        // Cache contract:
        // Owner: process/current UI presentation.
        // Key: caption text, Tiny font, width and UiVersion.Current.
        // Value: wrapped caption height.
        // Dependencies: the complete measurement key above.
        // Refresh policy: immediate when a key component changes.
        // Equality policy: equal keys reuse the measured float.
        // Teardown: Reset clears all caption measurements.
        private static readonly TextHeightCache captionHeights = new TextHeightCache();
        private static readonly Func<CaptionMeasureState, float> measureCaptionHeight =
            state => Text.CalcHeight(state.Caption, state.Width);

        internal static readonly Color PanelBackground = new Color(0.08f, 0.08f, 0.08f, 0.9f);
        internal static readonly Color PanelOutline = new Color(1f, 1f, 1f, 0.15f);
        internal static readonly Color HeaderText = new Color(0.85f, 0.85f, 0.85f);
        internal static readonly Color HeaderRule = new Color(1f, 1f, 1f, 0.25f);
        internal static readonly Color CaptionText = new Color(0.60f, 0.62f, 0.64f);
        internal static readonly Color SelectionTint = new Color(1f, 0.95f, 0.55f);

        /// Plain underlined header (no fold toggle, no caption). Returns the
        /// height consumed.
        internal static float SectionHeader(float x, float y, float width, string label)
        {
            bool folded = false;
            return SectionHeader(x, y, width, label, null, ref folded, foldable: false);
        }

        /// Underlined section header. When <paramref name="foldable"/>, clicking
        /// toggles the folded flag. While unfolded (or not foldable), wraps the
        /// caption below in Tiny caption text and returns the total height
        /// consumed; folded returns just the header height.
        /// <paramref name="clickableWidth"/> limits the width of the invisible
        /// button that toggles folding (defaults to full <paramref name="width"/>),
        /// allowing the caller to place controls (e.g. a rename pencil) to the
        /// right of the clickable region without triggering the fold toggle.
        internal static float SectionHeader(float x, float y, float width, string label,
            string caption, ref bool folded, float clickableWidth = -1f, bool foldable = true)
        {
            using (new GuiStateScope())
            {
            Text.Font = GameFont.Small;
            var labelRect = new Rect(x, y, width, 22f);
            GUI.color = HeaderText;
            Widgets.Label(labelRect, label);
            GUI.color = HeaderRule;
            WrText.LineHorizontal(x, y + 24f, width);
            GUI.color = Color.white;
            if (foldable)
            {
                float clickW = clickableWidth > 0f ? clickableWidth : width;
                var clickRect = new Rect(x, y, clickW, 22f);
                Widgets.DrawHighlightIfMouseover(clickRect);
                if (Widgets.ButtonInvisible(clickRect)) folded = !folded;
            }
            float used = 28f;
            if ((!foldable || !folded) && !caption.NullOrEmpty())
            {
                Text.Font = GameFont.Tiny;
                GUI.color = CaptionText;
                float capH = CaptionHeight(caption, width);
                Widgets.Label(new Rect(x, y + used, width, capH), caption);
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                used += capH + 4f;
            }
            return used;
            }
        }

        internal static float CaptionHeight(string caption, float width)
        {
            UiVersion.ObserveCurrentMetrics();
            GameFont previousFont = Text.Font;
            Text.Font = GameFont.Tiny;
            try
            {
                return captionHeights.Get(
                    caption,
                    (int)GameFont.Tiny,
                    width,
                    UiVersion.Current,
                    new CaptionMeasureState { Caption = caption, Width = width },
                    measureCaptionHeight);
            }
            finally
            {
                Text.Font = previousFont;
            }
        }

        internal static void Reset() => captionHeights.Reset();
    }
}
