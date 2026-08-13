using System.Collections.Generic;
using UnityEngine;
using Verse;
using WorkRoles.Core;

namespace WorkRoles.UI
{
    /// Renders TipModels inside this mod's owned tooltip windows:
    /// title + right-aligned badge, dim section headers, per-section aligned
    /// fact/action columns, unwrapped signal tables, wrapped prose spans,
    /// pixel-snapped separators. All text measurement happens once per model
    /// (cached on it); Draw only replays positioned primitives.
    public static class WrTipUI
    {
        private const float Pad = 4f;          // tooltip frame inset
        private const float ColGap = 10f;      // label column -> value column
        private const float BadgeGap = 12f;    // title -> right-aligned badge
        private const float TitleGap = 4f;     // title line -> first section
        private const float SectionGap = 5f;   // section -> separator -> section
        private const float TableColGap = 20f; // between table columns
        private const float CellIconSize = 16f;
        private const float CellIconGap = 2f;  // first-cell text -> icon
        private const float RuleGapAbove = 2f; // table rule hugs the row above
        private const float RuleGapBelow = 3f;
        private const float RowTighten = 4f;   // tight rows pull toward their parent
        private const float InlineIconSize = 10f; // star pairs match the chip draw size
        internal const float MaxContentWidth = 800f;

        private static readonly Color SeparatorColor = new Color(1f, 1f, 1f, 0.2f);

        // Content-relative primitives; a null Text with a null Icon is a rule
        // line whose Rect carries x/y/length.
        private struct Cmd
        {
            public Rect Rect;
            public Color Color;
            public string Text;
            public Texture2D Icon;
            public bool NoWrap;
        }

        private sealed class Geometry
        {
            public float MaxWidth;
            public Vector2 Size;
            public readonly List<Cmd> Cmds = new List<Cmd>();
        }

        /// Full tip rect size (content plus the complete frame inset).
        public static Vector2 Measure(TipModel model, float maxWidth) =>
            Ensure(model, maxWidth).Size;

        public static void Draw(Rect bgRect, TipModel model)
        {
            Geometry geo = Ensure(model, MaxContentWidth);
            var oldFont = Text.Font;
            var oldAnchor = Text.Anchor;
            var oldColor = GUI.color;
            bool oldWrap = Text.WordWrap;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            float ox = bgRect.x + Pad + model.Padding;
            float oy = bgRect.y + Pad + model.Padding;
            foreach (Cmd cmd in geo.Cmds)
            {
                GUI.color = cmd.Color;
                if (cmd.Icon != null)
                {
                    GUI.DrawTexture(new Rect(ox + cmd.Rect.x, oy + cmd.Rect.y,
                        cmd.Rect.width, cmd.Rect.height), cmd.Icon);
                }
                else if (cmd.Text == null)
                {
                    WrText.LineHorizontal(ox + cmd.Rect.x, oy + cmd.Rect.y, cmd.Rect.width);
                }
                else
                {
                    Text.WordWrap = !cmd.NoWrap;
                    Widgets.Label(new Rect(ox + cmd.Rect.x, oy + cmd.Rect.y,
                        cmd.Rect.width, cmd.Rect.height), cmd.Text);
                }
            }
            Text.WordWrap = oldWrap;
            GUI.color = oldColor;
            Text.Anchor = oldAnchor;
            Text.Font = oldFont;
        }

        private static Geometry Ensure(TipModel model, float maxWidth)
        {
            if (model.RenderCache is Geometry cached && cached.MaxWidth == maxWidth)
                return cached;
            var geo = new Geometry { MaxWidth = maxWidth };
            var oldFont = Text.Font;
            Text.Font = GameFont.Small;
            float frame = Pad + model.Padding;
            float contentMax = Mathf.Min(maxWidth, MaxContentWidth);
            float contentW = Mathf.Min(NaturalWidth(model), contentMax);
            float contentH = Compose(model, contentW, geo);
            // Shape balance: wide-and-short tips narrow toward the √-area
            // target and recompose once; both passes sit inside this
            // cache-gated builder.
            float balanced = TipBalancePolicy.BalancedWidth(contentW, contentH,
                Mathf.Min(FloorWidth(model), contentMax));
            if (balanced < contentW)
            {
                geo.Cmds.Clear();
                contentW = balanced;
                contentH = Compose(model, contentW, geo);
            }
            Text.Font = oldFont;
            geo.Size = new Vector2(Mathf.Ceil(contentW + frame * 2f), Mathf.Ceil(contentH + frame * 2f));
            model.RenderCache = geo;
            return geo;
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
                float factCol = LabelColumnWidth(model);
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
                        case TipInlineRow inline:
                            w = Mathf.Max(w, InlineWidth(inline)
                                + (inline.Label != null ? factCol + ColGap : 0f));
                            break;
                    }
                float[] cols = ColumnWidths(section);
                if (cols != null)
                {
                    float tableW = TableColGap * (cols.Length - 1);
                    foreach (float col in cols) tableW += col;
                    w = Mathf.Max(w, tableW);
                }
            }
            return Mathf.Max(w, 24f);
        }

        /// Widest element that cannot wrap (title/badge line, tables, the
        /// shared label column plus a minimal value width): narrowing below
        /// this would clip content, so it floors the balanced width.
        private static float FloorWidth(TipModel model)
        {
            float w = 24f;
            if (!model.Title.NullOrEmpty())
            {
                float titleW = WrText.FitWidth(model.Title);
                if (!model.Badge.NullOrEmpty()) titleW += BadgeGap + WrText.FitWidth(model.Badge);
                w = Mathf.Max(w, titleW);
            }
            float factCol = LabelColumnWidth(model);
            if (factCol > 0f) w = Mathf.Max(w, factCol + ColGap + 24f);
            foreach (var section in model.Sections)
            {
                float[] cols = ColumnWidths(section);
                if (cols != null)
                {
                    float tableW = TableColGap * (cols.Length - 1);
                    foreach (float col in cols) tableW += col;
                    w = Mathf.Max(w, tableW);
                }
                foreach (var row in section.Rows)
                    if (row is TipInlineRow inline)
                        w = Mathf.Max(w, InlineWidth(inline)
                            + (inline.Label != null ? factCol + ColGap : 0f));
            }
            return w;
        }

        /// The unwrappable inline row's natural width (current font: Small).
        private static float InlineWidth(TipInlineRow inline)
        {
            float w = 0f;
            for (int i = 0; i < (inline.Segments?.Count ?? 0); i++)
            {
                TipInlineSegment segment = inline.Segments[i];
                w += segment.Gap + (segment.Text != null
                    ? WrText.FitWidth(segment.Text)
                    : InlineIconSize);
            }
            return w;
        }

        /// Natural per-column widths across a section's columns rows, or null if
        /// none; column 0 reserves icon space when any row carries one so text
        /// alignment holds and icons trail the text.
        private static float[] ColumnWidths(TipSection section)
        {
            int count = 0;
            foreach (var row in section.Rows)
                if (row is TipColumnsRow cols)
                    count = Mathf.Max(count, cols.Cells?.Count ?? 0);
            if (count == 0) return null;
            var widths = new float[count];
            bool anyIcon = false;
            foreach (var row in section.Rows)
                if (row is TipColumnsRow cols)
                {
                    anyIcon |= cols.Icon != null;
                    for (int i = 0; i < (cols.Cells?.Count ?? 0); i++)
                        if (!cols.Cells[i].NullOrEmpty())
                            widths[i] = Mathf.Max(widths[i], WrText.FitWidth(cols.Cells[i]));
                }
            if (anyIcon) widths[0] += CellIconGap + CellIconSize;
            return widths;
        }

        /// Shared label/token column across the whole model: fact and action
        /// sections align as one table.
        private static float LabelColumnWidth(TipModel model)
        {
            float w = 0f;
            foreach (var section in model.Sections)
                foreach (var row in section.Rows)
                    switch (row)
                    {
                        case TipFactRow fact:
                            w = Mathf.Max(w, WrText.FitWidth(fact.Label));
                            break;
                        case TipActionRow action:
                            w = Mathf.Max(w, WrText.FitWidth(action.InputToken));
                            break;
                        case TipInlineRow inline when !inline.Label.NullOrEmpty():
                            w = Mathf.Max(w, WrText.FitWidth(inline.Label));
                            break;
                    }
            return w;
        }

        /// Emits every primitive at its content-relative position; returns the
        /// content height. Runs once per model, so measurement cost is one-time.
        private static float Compose(TipModel model, float contentW, Geometry geo)
        {
            float lineH = Text.LineHeightOf(GameFont.Small);
            float y = 0f;

            if (!model.Title.NullOrEmpty())
            {
                float badgeW = model.Badge.NullOrEmpty() ? 0f : WrText.FitWidth(model.Badge);
                geo.Cmds.Add(new Cmd
                {
                    Rect = new Rect(0f, y,
                        Mathf.Max(0f, contentW - (badgeW > 0f ? badgeW + BadgeGap : 0f)), lineH),
                    Color = Color.white,
                    Text = model.Title,
                    NoWrap = true,
                });
                if (badgeW > 0f)
                {
                    geo.Cmds.Add(new Cmd
                    {
                        Rect = new Rect(contentW - badgeW, y, badgeW, lineH),
                        Color = model.BadgeColor,
                        Text = model.Badge,
                        NoWrap = true,
                    });
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
                    geo.Cmds.Add(new Cmd
                    {
                        Rect = new Rect(0f, y, contentW, 0f),
                        Color = SeparatorColor,
                    });
                    y += 1f + SectionGap;
                }
                firstSection = false;

                if (!section.Header.NullOrEmpty())
                {
                    geo.Cmds.Add(new Cmd
                    {
                        Rect = new Rect(0f, y, contentW, lineH),
                        Color = TipText.DimColor,
                        Text = section.Header,
                    });
                    y += lineH;
                }

                float labelCol = LabelColumnWidth(model);
                float valueX = labelCol + ColGap;
                float valueW = Mathf.Max(24f, contentW - valueX);

                float[] tableCols = ColumnWidths(section);
                float tableLineW;
                if (tableCols != null)
                {
                    tableLineW = TableColGap * (tableCols.Length - 1);
                    foreach (float col in tableCols) tableLineW += col;
                }
                else tableLineW = contentW;

                foreach (var row in section.Rows)
                {
                    switch (row)
                    {
                        case TipTextRow text:
                        {
                            float h = Text.CalcHeight(text.Text, contentW);
                            geo.Cmds.Add(new Cmd
                            {
                                Rect = new Rect(0f, y, contentW, h),
                                Color = text.Dim ? TipText.DimColor : Color.white,
                                Text = text.Text,
                            });
                            y += h;
                            break;
                        }
                        case TipFactRow fact:
                        {
                            float h = Mathf.Max(lineH, Text.CalcHeight(fact.Value, valueW));
                            geo.Cmds.Add(new Cmd
                            {
                                Rect = new Rect(0f, y, labelCol, lineH),
                                Color = fact.LabelColor ?? TipText.DimColor,
                                Text = fact.Label,
                            });
                            geo.Cmds.Add(new Cmd
                            {
                                Rect = new Rect(valueX, y, valueW, h),
                                Color = fact.ValueColor ?? Color.white,
                                Text = fact.Value,
                            });
                            y += h;
                            break;
                        }
                        case TipActionRow action:
                        {
                            float h = Mathf.Max(lineH, Text.CalcHeight(action.Description, valueW));
                            geo.Cmds.Add(new Cmd
                            {
                                Rect = new Rect(0f, y, labelCol, lineH),
                                Color = Color.white,
                                Text = action.InputToken,
                            });
                            geo.Cmds.Add(new Cmd
                            {
                                Rect = new Rect(valueX, y, valueW, h),
                                Color = TipText.DimColor,
                                Text = action.Description,
                            });
                            y += h;
                            break;
                        }
                        case TipColumnsRow cols:
                        {
                            if (tableCols == null) break;
                            if (cols.Tight) y -= RowTighten;
                            int cellCount = cols.Cells?.Count ?? 0;
                            var rowColor = cols.Color ?? Color.white;
                            float cx = 0f;
                            for (int i = 0; i < tableCols.Length; i++)
                            {
                                string cell = i < cellCount ? cols.Cells[i] : null;
                                if (!cell.NullOrEmpty())
                                {
                                    geo.Cmds.Add(new Cmd
                                    {
                                        Rect = new Rect(cx, y, tableCols[i], lineH),
                                        Color = rowColor,
                                        Text = cell,
                                        NoWrap = true,
                                    });
                                }
                                if (i == 0 && cols.Icon != null)
                                {
                                    float textW = cell.NullOrEmpty() ? 0f : WrText.FitWidth(cell);
                                    float iconX = cx + CellIconGap
                                        + Mathf.Min(textW, tableCols[0] - (CellIconGap + CellIconSize));
                                    geo.Cmds.Add(new Cmd
                                    {
                                        Rect = new Rect(iconX, y + (lineH - CellIconSize) / 2f,
                                            CellIconSize, CellIconSize),
                                        Color = Color.white,
                                        Icon = cols.Icon,
                                    });
                                }
                                cx += tableCols[i] + TableColGap;
                            }
                            y += lineH;
                            break;
                        }
                        case TipInlineRow inline:
                        {
                            float cx = 0f;
                            if (inline.Label != null)
                            {
                                if (!inline.Label.NullOrEmpty())
                                    geo.Cmds.Add(new Cmd
                                    {
                                        Rect = new Rect(0f, y, labelCol, lineH),
                                        Color = TipText.DimColor,
                                        Text = inline.Label,
                                    });
                                cx = valueX;
                            }
                            for (int i = 0; i < (inline.Segments?.Count ?? 0); i++)
                            {
                                TipInlineSegment segment = inline.Segments[i];
                                cx += segment.Gap;
                                if (segment.Text != null)
                                {
                                    float textW = WrText.FitWidth(segment.Text);
                                    geo.Cmds.Add(new Cmd
                                    {
                                        Rect = new Rect(cx, y, textW, lineH),
                                        Color = segment.Color,
                                        Text = segment.Text,
                                        NoWrap = true,
                                    });
                                    cx += textW;
                                }
                                else if (segment.Icon != null)
                                {
                                    geo.Cmds.Add(new Cmd
                                    {
                                        Rect = new Rect(cx, y + (lineH - InlineIconSize) / 2f,
                                            InlineIconSize, InlineIconSize),
                                        Color = segment.Color,
                                        Icon = segment.Icon,
                                    });
                                    cx += InlineIconSize;
                                }
                            }
                            y += lineH;
                            break;
                        }
                        case TipSpanRow span:
                        {
                            float indent = span.Indent;
                            if (span.AlignColumn > 0 && tableCols != null)
                            {
                                indent = 0f;
                                for (int i = 0; i < span.AlignColumn && i < tableCols.Length; i++)
                                    indent += tableCols[i] + TableColGap;
                            }
                            float spanW = Mathf.Max(24f, tableLineW - indent);
                            float h = Text.CalcHeight(span.Text, spanW);
                            y -= RowTighten;
                            geo.Cmds.Add(new Cmd
                            {
                                Rect = new Rect(indent, y, spanW, h),
                                Color = span.Dim ? TipText.DimColor : Color.white,
                                Text = span.Text,
                            });
                            y += h;
                            break;
                        }
                        case TipGapRow gap:
                        {
                            y += gap.Height;
                            break;
                        }
                        case TipRuleRow _:
                        {
                            y += RuleGapAbove;
                            geo.Cmds.Add(new Cmd
                            {
                                Rect = new Rect(0f, y, tableLineW, 0f),
                                Color = SeparatorColor,
                            });
                            y += 1f + RuleGapBelow;
                            break;
                        }
                    }
                }
            }
            return y;
        }
    }
}
