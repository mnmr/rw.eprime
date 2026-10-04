using RimShared.UiLib;
using UnityEngine;
using Verse;

namespace Implanner.UI
{
    /// Shared dialog styling (palette derived from EPrimeReadouts' EprStyle).
    /// Section headers carry a 0.85-white label over a faint hairline; the
    /// Plans selection tree renders in the vanilla storage-filter style and
    /// needs no header treatment of its own.
    internal static class PlannerStyle
    {
        internal const float SectionHeaderHeight = 26f;

        /// Vertical margin between section blocks inside a pane (never after
        /// the last one).
        internal const float SectionGap = 8f;

        internal static readonly Color HeaderText = new Color(0.85f, 0.85f, 0.85f);
        internal static readonly Color CaptionText = new Color(0.60f, 0.62f, 0.64f);

        /// Tier headers are symbols, not translations; drawn gold wherever
        /// star tiers appear (plan editor, colonist details).
        internal static readonly string[] TierStars =
            { "★★★★★", "★★★★",
              "★★★", "★★", "★" };
        internal static readonly Color TierStarColor = new Color(1f, 0.9f, 0.5f);
        private static readonly Color HeaderRule = new Color(1f, 1f, 1f, 0.18f);

        /// Text tones for implant work: gold while pending (missing or
        /// crafting), blue while automation is actively moving it
        /// (reserved, ready, or scheduled), orange when the doctor floor
        /// blocks it.
        internal static readonly Color PendingText = new Color(1f, 0.85f, 0.4f);
        internal static readonly Color ActiveText = new Color(0.55f, 0.78f, 1f);
        internal static readonly Color BlockedText = new Color(1f, 0.55f, 0.4f);

        /// Flat content panel: the vanilla menu-section FILL, unchanged,
        /// under an exactly one-device-pixel outline (only the thick
        /// scale-blurred vanilla border is replaced).
        internal static void Panel(Rect rect) =>
            PixelBox.SolidWithOutline(rect,
                Widgets.MenuSectionBGFillColor, SegmentedControl.PanelOutline);

        /// Panel body shading: midway between the plain fill and the
        /// details header band (the band is the fill under 25% black, so
        /// midway is 12.5% black), laid inside the outline.
        internal static readonly Color PanelTint = new Color(0f, 0f, 0f, 0.125f);

        /// Panel with the body tint over its whole inner area (Colony
        /// panel, Implant tiers panel; the details panel tints only below
        /// its darker header band).
        internal static void ShadedPanel(Rect rect)
        {
            Panel(rect);
            Widgets.DrawBoxSolid(new Rect(rect.x + 1f, rect.y + 1f,
                rect.width - 2f, rect.height - 2f), PanelTint);
        }

        /// Colonist progress bar: a muted fill keeps the overlaid white
        /// count readable over both the filled and empty portions.
        private static readonly Color ProgressFill =
            new Color(0.26f, 0.38f, 0.54f, 0.9f);
        private static readonly Color ProgressBack = new Color(1f, 1f, 1f, 0.07f);
        private static readonly Color ProgressOutline =
            new Color(1f, 1f, 1f, 0.15f);

        internal static void ProgressBar(Rect rect, float fraction)
        {
            Widgets.DrawBoxSolid(rect, ProgressBack);
            if (fraction > 0f)
                Widgets.DrawBoxSolid(new Rect(rect.x, rect.y,
                    rect.width * Mathf.Clamp01(fraction), rect.height),
                    ProgressFill);
            PixelBox.Outline(rect, ProgressOutline);
        }

        /// Plain section header over a faint hairline: the shared sub-header
        /// tier. Returns the height consumed.
        internal static float SectionHeader(float x, float y, float width, string label) =>
            RimShared.UiLib.SectionHeader.Sub(x, y, width, label, HeaderText, HeaderRule);

        /// Help foldout spacing (shared HelpFoldout) on this mod's 26px
        /// section-header tier.
        internal static readonly HelpFoldoutMetrics HelpMetrics = new HelpFoldoutMetrics(
            headerHeight: SectionHeaderHeight, panelOffset: 6f, panelPadding: 8f,
            expandedBottomMargin: 12f, collapsedBottomMargin: 6f,
            ruleColor: HeaderRule, labelAnchor: TextAnchor.MiddleLeft);
    }
}
