using System;
using EPrimeReadouts.Core;
using RimShared.Common;
using RimShared.UiLib;
using UnityEngine;
using Verse;

namespace EPrimeReadouts.UI
{
    /// Shared dialog styling (WorkRoles-derived palette).
    internal static class EprStyle
    {
        internal const float SectionHeaderHeight = 28f;
        internal const float HelpPanelOffset = 8f;
        internal const float HelpPanelPadding = 8f;
        internal const float HelpExpandedBottomMargin = 20f;
        internal const float HelpCollapsedBottomMargin = 8f;

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

        // Cache contract:
        // Owner: process/current UI presentation.
        // Key: UiVersion.Current.
        // Value: whole-pixel line geometry for a Tiny request after RimWorld's
        // possible Small-font substitution.
        // Dependencies: UI scale, tiny-text preference, language font support,
        // and platform as represented by the UI metric revision.
        // Refresh policy: first read in a UI revision; every later dialog read
        // reuses the measured value.
        // Teardown: Reset clears the stamp and value.
        private static ResolvedTinyTextMetrics tinyTextMetrics;
        private static int tinyTextMetricsVersion = -1;

        internal static readonly Color PanelBackground = new Color(0.08f, 0.08f, 0.08f, 0.9f);
        internal static readonly Color PanelOutline = new Color(1f, 1f, 1f, 0.15f);
        internal static readonly Color HeaderText = new Color(0.85f, 0.85f, 0.85f);
        /// The mod name in the panel header. Pure white on purpose: the
        /// buffered header recovers glyph coverage from the red channel, so
        /// the direct path uses the same color to stay identical.
        internal static readonly Color PanelTitleText = Color.white;
        internal static readonly Color HeaderRule = new Color(1f, 1f, 1f, 0.25f);
        internal static readonly Color CaptionText = new Color(0.60f, 0.62f, 0.64f);
        internal static readonly Color SelectionTint = new Color(1f, 0.95f, 0.55f);
        /// Default tint for the resource-pools branch, distinct from the
        /// yellow SelectionTint so a selection highlight remains visible on it.
        internal static readonly Color PoolTint = new Color(0.55f, 0.8f, 1f);
        /// Tree entries already assigned to the selected group: dimmed, but
        /// bright enough to stay readable next to unassigned rows.
        internal static readonly Color AssignedTint = new Color(1f, 1f, 1f, 0.6f);

        internal static ResolvedTinyTextMetrics TinyTextMetrics
        {
            get
            {
                UiVersion.ObserveCurrentMetrics();
                if (tinyTextMetricsVersion == UiVersion.Current)
                    return tinyTextMetrics;
                TinyTextMetrics shared = TinyText.Metrics;
                tinyTextMetrics = new ResolvedTinyTextMetrics(
                    shared.LineHeight, shared.Font == GameFont.Small);
                tinyTextMetricsVersion = UiVersion.Current;
                return tinyTextMetrics;
            }
        }

        /// Plain underlined header (no fold toggle, no caption). Returns the
        /// height consumed.
        internal static float SectionHeader(float x, float y, float width, string label)
        {
            bool folded = false;
            return SectionHeader(x, y, width, label, null, ref folded, foldable: false);
        }

        /// A secondary Help foldout followed by either a compact collapsed gap
        /// or a framed caption panel. Returns the complete vertical footprint.
        internal static float HelpGroup(float x, float y, float width,
            string label, string caption, ref bool folded)
        {
            float used = FoldoutHeader(x, y, width, label, ref folded);
            if (folded) return used + HelpCollapsedBottomMargin;

            float textWidth = Mathf.Max(1f, width - 2f * HelpPanelPadding);
            float captionHeight = CaptionHeight(caption, textWidth);
            ResolvedTinyTextMetrics metrics = TinyTextMetrics;
            float panelHeight = captionHeight + 2f * HelpPanelPadding;
            var panelRect = new Rect(
                x,
                y + used + HelpPanelOffset,
                width,
                panelHeight);

            using (GuiStateScope.Capture())
            {
                Widgets.DrawBoxSolidWithOutline(
                    panelRect, PanelBackground, PanelOutline);
                GUI.color = CaptionText;
                TinyText.Label(new Rect(
                    panelRect.x + HelpPanelPadding,
                    panelRect.y + HelpPanelPadding + metrics.CaptionOffsetY,
                    textWidth,
                    captionHeight), caption);
            }

            return used + HelpPanelOffset + panelHeight
                + HelpExpandedBottomMargin;
        }

        /// Complete vertical footprint of a Help foldout without drawing it.
        internal static float HelpGroupHeight(float width, string caption, bool folded)
        {
            if (folded) return SectionHeaderHeight + HelpCollapsedBottomMargin;

            float textWidth = Mathf.Max(1f, width - 2f * HelpPanelPadding);
            return SectionHeaderHeight
                + HelpPanelOffset
                + CaptionHeight(caption, textWidth)
                + 2f * HelpPanelPadding
                + HelpExpandedBottomMargin;
        }

        private static float FoldoutHeader(float x, float y, float width,
            string label, ref bool folded)
        {
            using (GuiStateScope.Capture())
            {
                var clickRect = new Rect(x, y, width, 22f);
                Widgets.DrawHighlightIfMouseover(clickRect);
                if (Widgets.ButtonInvisible(clickRect)) folded = !folded;

                GUI.DrawTexture(
                    new Rect(x + 1f, y + 3f, 16f, 16f),
                    folded ? TexButton.Reveal : TexButton.Collapse);

                Text.Font = GameFont.Small;
                GUI.color = HeaderText;
                Widgets.Label(new Rect(x + 21f, y, Mathf.Max(0f, width - 21f), 22f),
                    label);
                GUI.color = HeaderRule;
                WrText.LineHorizontal(x, y + 24f, width);
            }
            return SectionHeaderHeight;
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
            string? caption, ref bool folded, float clickableWidth = -1f, bool foldable = true)
        {
            using (GuiStateScope.Capture())
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
            float used = SectionHeaderHeight;
            if ((!foldable || !folded) && !caption.NullOrEmpty())
            {
                GUI.color = CaptionText;
                float capH = CaptionHeight(caption!, width); // NullOrEmpty checked above
                TinyText.Label(new Rect(
                    x,
                    y + used + TinyTextMetrics.CaptionOffsetY,
                    width,
                    capH), caption!);
                GUI.color = Color.white;
                used += capH + 4f;
            }
            return used;
            }
        }

        internal static float CaptionHeight(string caption, float width)
        {
            UiVersion.ObserveCurrentMetrics();
            using (TinyText.UseFont())
            {
                float measured = captionHeights.Get(
                    caption,
                    (int)TinyText.Metrics.Font,
                    width,
                    UiVersion.Current,
                    new CaptionMeasureState { Caption = caption, Width = width },
                    measureCaptionHeight);
                return TinyTextMetrics.MinHeight(Mathf.Ceil(measured));
            }
        }

        internal static void Reset()
        {
            captionHeights.Reset();
            tinyTextMetrics = default;
            tinyTextMetricsVersion = -1;
        }
    }
}
