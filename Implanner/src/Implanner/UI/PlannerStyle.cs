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
        private static readonly Color HeaderRule = new Color(1f, 1f, 1f, 0.18f);

        /// Plain section header over a faint hairline: the shared sub-header
        /// tier. Returns the height consumed.
        internal static float SectionHeader(float x, float y, float width, string label) =>
            RimShared.UiLib.SectionHeader.Sub(x, y, width, label, HeaderText, HeaderRule);

        // ------------------------------------------------------ Help foldout
        // (Ported from EPrimeReadouts' EprStyle help group.)

        private const float HelpPanelOffset = 6f;
        private const float HelpPanelPadding = 8f;
        private const float HelpExpandedBottomMargin = 12f;
        private const float HelpCollapsedBottomMargin = 6f;

        // Cache contract:
        // Owner: process/current UI presentation.
        // Key: caption text, effective (Tiny) font, and wrap width — via the
        //   shared RimShared.Common.TextHeightCache.
        // Value: Tiny-font wrapped caption height.
        // Dependencies: key plus UiVersion.Current (scale/font/language
        //   metrics) as the revision.
        // Refresh policy: immediate re-measure on UI revision change.
        // Equality policy: unchanged keys return the cached float.
        // Teardown: bounded key set (help captions); the revision gate
        //   handles refreshes.
        private static readonly RimShared.Common.TextHeightCache captionHeights =
            new RimShared.Common.TextHeightCache();

        /// Word wrap is ambient GUI state; callers (the plan editor) may
        /// have it off for single-line rows, so the wrapped measurement
        /// forces it on. Static delegate: measurement never captures.
        private static readonly System.Func<(string Caption, float Width), float>
            MeasureCaption = static key =>
            {
                using (GuiStateScope.Capture())
                {
                    Text.WordWrap = true;
                    return TinyText.CalcHeight(key.Caption, key.Width);
                }
            };

        private static float CaptionHeight(string caption, float width) =>
            captionHeights.Get(caption, (int)GameFont.Tiny, width,
                UiVersion.Current, (caption, width), MeasureCaption);

        /// A collapsible Help foldout: a fold-arrow header over either a
        /// compact collapsed gap or a framed Tiny-text caption panel.
        /// Returns the complete vertical footprint.
        internal static float HelpGroup(float x, float y, float width,
            string label, string caption, ref bool folded)
        {
            using (GuiStateScope.Capture())
            {
                var clickRect = new Rect(x, y, width, 22f);
                Widgets.DrawHighlightIfMouseover(clickRect);
                if (Widgets.ButtonInvisible(clickRect)) folded = !folded;
                GUI.DrawTexture(new Rect(x + 1f, y + 3f, 16f, 16f),
                    folded ? TexButton.Reveal : TexButton.Collapse);
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                GUI.color = HeaderText;
                Widgets.Label(new Rect(x + 21f, y, Mathf.Max(0f, width - 21f), 22f),
                    label);
                GUI.color = HeaderRule;
                WrText.LineHorizontal(x, y + 24f, width);
            }
            float used = SectionHeaderHeight;
            if (folded) return used + HelpCollapsedBottomMargin;

            float textWidth = Mathf.Max(1f, width - 2f * HelpPanelPadding);
            float captionHeight = CaptionHeight(caption, textWidth);
            float panelHeight = captionHeight + 2f * HelpPanelPadding;
            var panelRect = new Rect(x, y + used + HelpPanelOffset,
                width, panelHeight);
            using (GuiStateScope.Capture())
            {
                Text.WordWrap = true;
                // Device-pixel-snapped frame in the shared panel palette:
                // the vanilla outline helper draws one-or-two-pixel edges
                // and bleeds past the fill at fractional UI scales.
                PixelBox.SolidWithOutline(panelRect,
                    SegmentedControl.PanelBackground,
                    SegmentedControl.PanelOutline);
                GUI.color = CaptionText;
                TinyText.Label(new Rect(
                    panelRect.x + HelpPanelPadding,
                    panelRect.y + HelpPanelPadding,
                    textWidth, captionHeight), caption);
            }
            return used + HelpPanelOffset + panelHeight + HelpExpandedBottomMargin;
        }
    }
}
