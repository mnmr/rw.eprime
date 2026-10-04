using System;
using RimShared.Common;
using UnityEngine;
using Verse;

namespace RimShared.UiLib
{
    /// Per-mod spacing for a HelpFoldout, so each mod keeps the footprint of
    /// its own section-header tier.
    public readonly struct HelpFoldoutMetrics
    {
        /// Advance consumed by the fold header (label box and hairline).
        public readonly float HeaderHeight;
        /// Gap between the header and the caption panel.
        public readonly float PanelOffset;
        /// Caption inset inside the panel frame, every side.
        public readonly float PanelPadding;
        /// Margin after the caption panel while unfolded.
        public readonly float ExpandedBottomMargin;
        /// Margin after the header while folded.
        public readonly float CollapsedBottomMargin;
        /// Header hairline colour.
        public readonly Color RuleColor;
        /// Header label anchor inside its 22px label box.
        public readonly TextAnchor LabelAnchor;

        public HelpFoldoutMetrics(float headerHeight, float panelOffset,
            float panelPadding, float expandedBottomMargin,
            float collapsedBottomMargin, Color ruleColor, TextAnchor labelAnchor)
        {
            HeaderHeight = headerHeight;
            PanelOffset = panelOffset;
            PanelPadding = panelPadding;
            ExpandedBottomMargin = expandedBottomMargin;
            CollapsedBottomMargin = collapsedBottomMargin;
            RuleColor = ruleColor;
            LabelAnchor = labelAnchor;
        }
    }

    /// A collapsible Help group: a fold-arrow header over either a compact
    /// collapsed gap or a framed Tiny-text caption panel. The frame is
    /// device-pixel snapped in the shared panel palette (the vanilla outline
    /// helper draws one-or-two-pixel edges and bleeds past the fill at
    /// fractional UI scales), and the caption follows TinyText's
    /// Small-fallback rule: the fallback ink offset and at least one Tiny
    /// line box. Steady-render safe: the caption height is measured once per
    /// key and UI metric revision.
    public static class HelpFoldout
    {
        private static readonly Color CaptionText = new Color(0.60f, 0.62f, 0.64f);

        private struct CaptionMeasureState
        {
            internal string Caption;
            internal float Width;
        }

        // Cache contract:
        // Owner: process, one per mod assembly (shared source compiles into
        //   each mod, so mods never share entries).
        // Key: caption text, effective Tiny font (TinyText.Metrics.Font,
        //   Small when Tiny falls back) and wrap width.
        // Value: wrapped caption height, rounded up and at least one Tiny
        //   line box; an immutable float.
        // Dependencies: the key plus the caller's UI metric revision
        //   (UiRevision.Current: UI scale, tiny-font preference, language
        //   and font metrics).
        // Refresh policy: immediate; an entry stamped with another revision
        //   re-measures on its next read.
        // Equality policy: an equal key and revision reuses the float.
        // Teardown: Reset clears every entry; mods that draw a foldout call
        //   it from their world teardown (Implanner Patch_WorldTeardown,
        //   Readouts RuntimeTeardown.ResetAll); mods that never draw one
        //   keep an empty copy.
        private static readonly TextHeightCache captionHeights = new TextHeightCache();

        /// Word wrap is ambient GUI state; callers (the Implanner plan editor)
        /// may have it off for single-line rows, so the wrapped measurement
        /// forces it on. Static delegate: measurement never captures.
        private static readonly Func<CaptionMeasureState, float> measureCaptionHeight =
            static state =>
            {
                using (GuiStateScope.Capture())
                {
                    Text.WordWrap = true;
                    return Mathf.Max(TinyText.CalcHeight(state.Caption, state.Width),
                        TinyText.LineHeight);
                }
            };

        /// Wrapped Tiny caption height at the given width, cached.
        public static float CaptionHeight(string caption, float width, int uiRevision) =>
            captionHeights.Get(caption, (int)TinyText.Metrics.Font, width, uiRevision,
                new CaptionMeasureState { Caption = caption, Width = width },
                measureCaptionHeight);

        /// Draws the foldout and returns its complete vertical footprint.
        public static float Draw(float x, float y, float width, string label,
            string caption, ref bool folded, in HelpFoldoutMetrics metrics,
            int uiRevision)
        {
            using (GuiStateScope.Capture())
            {
                var clickRect = new Rect(x, y, width, 22f);
                Widgets.DrawHighlightIfMouseover(clickRect);
                if (Widgets.ButtonInvisible(clickRect)) folded = !folded;
                GUI.DrawTexture(new Rect(x + 1f, y + 3f, 16f, 16f),
                    folded ? TexButton.Reveal : TexButton.Collapse);
                Text.Font = GameFont.Small;
                Text.Anchor = metrics.LabelAnchor;
                GUI.color = SectionHeader.LabelColor;
                Widgets.Label(new Rect(x + 21f, y, Mathf.Max(0f, width - 21f), 22f),
                    label);
                GUI.color = metrics.RuleColor;
                GUI.DrawTexture(PixelBox.HairlineHorizontal(x, y + 24f, width),
                    BaseContent.WhiteTex);
            }
            float used = metrics.HeaderHeight;
            if (folded) return used + metrics.CollapsedBottomMargin;

            float padding = metrics.PanelPadding;
            float textWidth = Mathf.Max(1f, width - 2f * padding);
            float captionHeight = CaptionHeight(caption, textWidth, uiRevision);
            float panelHeight = captionHeight + 2f * padding;
            var panelRect = new Rect(x, y + used + metrics.PanelOffset,
                width, panelHeight);
            using (GuiStateScope.Capture())
            {
                Text.WordWrap = true;
                PixelBox.SolidWithOutline(panelRect,
                    SegmentedControl.PanelBackground, SegmentedControl.PanelOutline);
                GUI.color = CaptionText;
                TinyText.Label(new Rect(
                    panelRect.x + padding,
                    panelRect.y + padding + TinyText.FallbackCaptionOffsetY,
                    textWidth, captionHeight), caption);
            }
            return used + metrics.PanelOffset + panelHeight
                + metrics.ExpandedBottomMargin;
        }

        public static void Reset() => captionHeights.Reset();
    }
}
