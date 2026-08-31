using UnityEngine;
using Verse;

namespace RimShared.UiLib
{
    /// <summary>
    /// Two-tier section headers with one visual language: a Primary header
    /// (Medium-font title over a hairline) opens a major section, and Sub
    /// headers (the established Small-font per-mod header style) group
    /// content inside it. A mod may use Sub alone — the tiers only need to
    /// hold their relationship where both appear. Self-contained palette,
    /// PixelBox device-grid hairlines, and a measured Medium line box for
    /// the Primary tier (a declared font size is never used as a text
    /// rectangle; the vanilla per-font line heights are cached, so both
    /// draws are steady-render safe and allocation-free).
    /// </summary>
    public static class SectionHeader
    {
        public static readonly Color LabelColor = new Color(0.85f, 0.85f, 0.85f);
        public static readonly Color RuleColor = new Color(1f, 1f, 1f, 0.18f);

        /// <summary>
        /// Sub-header geometry: 20px label box over a hairline at +22, 26
        /// consumed — the sibling mods' existing section-header footprint.
        /// </summary>
        public const float SubHeight = 26f;
        private const float SubLabelHeight = 20f;
        private const float SubRuleOffset = 22f;

        private const float PrimaryRulePad = 1f;
        private const float PrimaryBottomPad = 3f;

        /// <summary>
        /// The Primary header's consumed height at current UI metrics. Not a
        /// constant: the Medium line box varies with UI scale and font
        /// assets, so layout arithmetic must read it, never hardcode it.
        /// </summary>
        public static float PrimaryHeight =>
            Mathf.Ceil(Text.LineHeightOf(GameFont.Medium))
                + PrimaryRulePad + 1f + PrimaryBottomPad;

        /// <summary>Major section title. Returns the height consumed.</summary>
        public static float Primary(float x, float y, float width, string label) =>
            Primary(x, y, width, label, LabelColor, RuleColor);

        public static float Primary(float x, float y, float width, string label,
            Color labelColor, Color ruleColor)
        {
            float lineHeight = Mathf.Ceil(Text.LineHeightOf(GameFont.Medium));
            using (GuiStateScope.Capture())
            {
                Text.Font = GameFont.Medium;
                Text.Anchor = TextAnchor.MiddleLeft;
                GUI.color = labelColor;
                Widgets.Label(new Rect(x, y, width, lineHeight), label);
                GUI.color = ruleColor;
                GUI.DrawTexture(
                    PixelBox.HairlineHorizontal(
                        x, y + lineHeight + PrimaryRulePad, width),
                    BaseContent.WhiteTex);
            }
            return PrimaryHeight;
        }

        /// <summary>Sub-section title. Returns the height consumed.</summary>
        public static float Sub(float x, float y, float width, string label) =>
            Sub(x, y, width, label, LabelColor, RuleColor);

        public static float Sub(float x, float y, float width, string label,
            Color labelColor, Color ruleColor)
        {
            using (GuiStateScope.Capture())
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                GUI.color = labelColor;
                Widgets.Label(new Rect(x, y, width, SubLabelHeight), label);
                GUI.color = ruleColor;
                GUI.DrawTexture(
                    PixelBox.HairlineHorizontal(x, y + SubRuleOffset, width),
                    BaseContent.WhiteTex);
            }
            return SubHeight;
        }
    }
}
