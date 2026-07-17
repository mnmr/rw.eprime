using UnityEngine;
using Verse;

namespace WorkRoles.UI
{
    /// Renders TipModels inside vanilla tooltip rects (see Patch_ActiveTip):
    /// title + right-aligned badge, dim section headers, per-section aligned
    /// fact/action columns, wrapped prose, pixel-snapped separators.
    public static class WrTipUI
    {
        private const float Pad = 4f;          // vanilla DrawInner contract
        private const float ColGap = 10f;      // label column -> value column
        private const float BadgeGap = 12f;    // title -> right-aligned badge
        private const float TitleGap = 4f;     // title line -> first section
        private const float SectionGap = 5f;   // section -> separator -> section
        private const float MaxContentWidth = 460f;

        private static readonly Color SeparatorColor = new Color(1f, 1f, 1f, 0.2f);

        /// Full tip rect size (content + vanilla 4f padding all around).
        public static Vector2 Measure(TipModel model, float maxWidth)
        {
            var oldFont = Text.Font;
            Text.Font = GameFont.Small;
            float contentMax = Mathf.Min(maxWidth, MaxContentWidth) - Pad * 2f;
            float contentW = Mathf.Min(NaturalWidth(model), contentMax);
            float contentH = Layout(model, new Rect(0f, 0f, contentW, 0f), draw: false);
            Text.Font = oldFont;
            return new Vector2(Mathf.Ceil(contentW + Pad * 2f), Mathf.Ceil(contentH + Pad * 2f));
        }

        public static void Draw(Rect bgRect, TipModel model)
        {
            var oldFont = Text.Font;
            var oldAnchor = Text.Anchor;
            var oldColor = GUI.color;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            Layout(model, bgRect.ContractedBy(Pad), draw: true);
            GUI.color = oldColor;
            Text.Anchor = oldAnchor;
            Text.Font = oldFont;
        }

        /// Widest unwrapped row across the model (current font: Small).
        private static float NaturalWidth(TipModel model)
        {
            float w = 0f;
            if (!model.Title.NullOrEmpty())
            {
                float titleW = WrText.FitWidth(model.Title);
                if (!model.Badge.NullOrEmpty()) titleW += BadgeGap + WrText.FitWidth(model.Badge);
                w = titleW;
            }
            foreach (var section in model.Sections)
            {
                if (!section.Header.NullOrEmpty()) w = Mathf.Max(w, WrText.FitWidth(section.Header));
                float factCol = LabelColumnWidth(section);
                foreach (var row in section.Rows)
                    switch (row)
                    {
                        case TipTextRow text:
                            w = Mathf.Max(w, WrText.FitWidth(text.Text));
                            break;
                        case TipFactRow fact:
                            w = Mathf.Max(w, factCol + ColGap + WrText.FitWidth(fact.Value));
                            break;
                        case TipActionRow action:
                            w = Mathf.Max(w, factCol + ColGap + WrText.FitWidth(action.Description));
                            break;
                    }
            }
            return Mathf.Max(w, 24f);
        }

        /// Shared label/token column per section: alignment reads as one table.
        private static float LabelColumnWidth(TipSection section)
        {
            float w = 0f;
            foreach (var row in section.Rows)
                switch (row)
                {
                    case TipFactRow fact:
                        w = Mathf.Max(w, WrText.FitWidth(fact.Label));
                        break;
                    case TipActionRow action:
                        w = Mathf.Max(w, WrText.FitWidth(action.InputToken));
                        break;
                }
            return w;
        }

        /// One pass serves measure and draw so they can never disagree.
        private static float Layout(TipModel model, Rect content, bool draw)
        {
            float lineH = Text.LineHeightOf(GameFont.Small);
            float y = content.y;

            if (!model.Title.NullOrEmpty())
            {
                float badgeW = model.Badge.NullOrEmpty() ? 0f : WrText.FitWidth(model.Badge);
                if (draw)
                {
                    bool oldWrap = Text.WordWrap;
                    Text.WordWrap = false;
                    GUI.color = Color.white;
                    Widgets.Label(new Rect(content.x, y,
                        Mathf.Max(0f, content.width - (badgeW > 0f ? badgeW + BadgeGap : 0f)), lineH), model.Title);
                    if (badgeW > 0f)
                    {
                        GUI.color = model.BadgeColor;
                        Widgets.Label(new Rect(content.xMax - badgeW, y, badgeW, lineH), model.Badge);
                        GUI.color = Color.white;
                    }
                    Text.WordWrap = oldWrap;
                }
                y += lineH + TitleGap;
            }

            bool firstSection = true;
            foreach (var section in model.Sections)
            {
                if (section.Rows.Count == 0 && section.Header.NullOrEmpty()) continue;
                if (!firstSection)
                {
                    y += SectionGap;
                    if (draw)
                    {
                        GUI.color = SeparatorColor;
                        WrText.LineHorizontal(content.x, y, content.width);
                        GUI.color = Color.white;
                    }
                    y += 1f + SectionGap;
                }
                firstSection = false;

                if (!section.Header.NullOrEmpty())
                {
                    if (draw)
                    {
                        GUI.color = TipText.DimColor;
                        Widgets.Label(new Rect(content.x, y, content.width, lineH), section.Header);
                        GUI.color = Color.white;
                    }
                    y += lineH;
                }

                float labelCol = LabelColumnWidth(section);
                float valueX = content.x + labelCol + ColGap;
                float valueW = Mathf.Max(24f, content.xMax - valueX);
                foreach (var row in section.Rows)
                {
                    switch (row)
                    {
                        case TipTextRow text:
                        {
                            float h = Text.CalcHeight(text.Text, content.width);
                            if (draw)
                            {
                                GUI.color = text.Dim ? TipText.DimColor : Color.white;
                                Widgets.Label(new Rect(content.x, y, content.width, h), text.Text);
                                GUI.color = Color.white;
                            }
                            y += h;
                            break;
                        }
                        case TipFactRow fact:
                        {
                            float h = Mathf.Max(lineH, Text.CalcHeight(fact.Value, valueW));
                            if (draw)
                            {
                                GUI.color = TipText.DimColor;
                                Widgets.Label(new Rect(content.x, y, labelCol, lineH), fact.Label);
                                GUI.color = fact.ValueColor ?? Color.white;
                                Widgets.Label(new Rect(valueX, y, valueW, h), fact.Value);
                                GUI.color = Color.white;
                            }
                            y += h;
                            break;
                        }
                        case TipActionRow action:
                        {
                            float h = Mathf.Max(lineH, Text.CalcHeight(action.Description, valueW));
                            if (draw)
                            {
                                GUI.color = Color.white;
                                Widgets.Label(new Rect(content.x, y, labelCol, lineH), action.InputToken);
                                GUI.color = TipText.DimColor;
                                Widgets.Label(new Rect(valueX, y, valueW, h), action.Description);
                                GUI.color = Color.white;
                            }
                            y += h;
                            break;
                        }
                    }
                }
            }
            return y - content.y;
        }
    }
}
