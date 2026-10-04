using RimShared.UiLib;
using UnityEngine;
using Verse;

namespace EPrimeReadouts.UI
{
    /// Shared dialog styling (WorkRoles-derived palette).
    internal static class EprStyle
    {
        internal const float SectionHeaderHeight = 28f;

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

        /// Help foldout spacing (shared HelpFoldout) on this mod's 28px
        /// section-header tier.
        internal static readonly HelpFoldoutMetrics HelpMetrics = new HelpFoldoutMetrics(
            headerHeight: SectionHeaderHeight, panelOffset: 8f, panelPadding: 8f,
            expandedBottomMargin: 20f, collapsedBottomMargin: 8f,
            ruleColor: HeaderRule, labelAnchor: TextAnchor.UpperLeft);

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
                    y + used + TinyText.FallbackCaptionOffsetY,
                    width,
                    capH), caption!);
                GUI.color = Color.white;
                used += capH + 4f;
            }
            return used;
            }
        }

        /// Wrapped Tiny caption height, sharing the Help foldout's caption
        /// measurement cache.
        internal static float CaptionHeight(string caption, float width)
        {
            UiRevision.ObserveCurrentMetrics();
            return HelpFoldout.CaptionHeight(caption, width, UiRevision.Current);
        }
    }
}
