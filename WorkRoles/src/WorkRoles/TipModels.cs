using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Verse;

namespace WorkRoles
{
    /// A producer-owned structured tooltip. PlainText is computed exactly once
    /// and remains the vanilla TipSignal fallback; activation only associates
    /// the model with the current WorkRoles window generation.
    internal sealed class StructuredTip
    {
        internal StructuredTip(string stableKey, TipModel model)
        {
            StableKey = stableKey ?? throw new ArgumentNullException(nameof(stableKey));
            Model = model ?? throw new ArgumentNullException(nameof(model));
            PlainText = model.ToPlainText();
            RegistryEpoch = Patches.Patch_ActiveTip_TipRect.CurrentRegistryEpoch;
        }

        internal string StableKey { get; }
        internal TipModel Model { get; }
        internal string PlainText { get; }
        internal int RegistryEpoch { get; }

        internal string Activate()
        {
            Patches.Patch_ActiveTip_TipRect.Activate(this);
            return PlainText;
        }
    }

    /// Structured tooltip content: a title/badge line plus sections of rows.
    /// ToPlainText() is both the TipSignal text (failure-safe fallback) and the
    /// draw-registry key (see Patch_ActiveTip); WrTipUI renders the model.
    public sealed class TipModel
    {
        public string Title;
        public string Badge;
        public Color BadgeColor = Color.white;
        /// Extra padding inside the tooltip, around all content (on top of the
        /// vanilla 4px frame).
        public float Padding = 8f;
        public List<TipSection> Sections = new List<TipSection>();

        // WrTipUI's cached geometry; models are immutable after construction, so
        // measurement happens once instead of every hover frame.
        internal object RenderCache;

        public TipSection AddSection(string header = null)
        {
            var section = new TipSection { Header = header };
            Sections.Add(section);
            return section;
        }

        /// Deterministic plain-text rendering; never ends in whitespace so the
        /// text survives ActiveTip's TrimEnd as a registry key.
        public string ToPlainText()
        {
            var sb = new StringBuilder();
            if (!Title.NullOrEmpty())
            {
                sb.Append(Title);
                if (!Badge.NullOrEmpty()) sb.Append(" · ").Append(Badge);
            }
            foreach (var section in Sections)
            {
                if (section.Rows.Count == 0 && section.Header.NullOrEmpty()) continue;
                if (sb.Length > 0) sb.Append("\n\n");
                bool first = true;
                if (!section.Header.NullOrEmpty())
                {
                    sb.Append(section.Header);
                    first = false;
                }
                foreach (var row in section.Rows)
                {
                    if (row is TipRuleRow || row is TipGapRow) continue;
                    if (!first) sb.Append('\n');
                    first = false;
                    switch (row)
                    {
                        case TipTextRow text:
                            sb.Append(text.Text);
                            break;
                        case TipFactRow fact:
                            sb.Append(fact.Label).Append(": ").Append(fact.Value);
                            break;
                        case TipActionRow action:
                            sb.Append(action.InputToken).Append(": ").Append(action.Description);
                            break;
                        case TipColumnsRow columns:
                        {
                            bool firstCell = true;
                            for (int i = 0; i < (columns.Cells?.Count ?? 0); i++)
                            {
                                if (columns.Cells[i].NullOrEmpty()) continue;
                                if (!firstCell) sb.Append(" · ");
                                firstCell = false;
                                sb.Append(columns.Cells[i]);
                            }
                            break;
                        }
                        case TipSpanRow span:
                            sb.Append(span.Text);
                            break;
                    }
                }
            }
            return sb.ToString().TrimEnd();
        }
    }

    public sealed class TipSection
    {
        /// Optional dim header line above the rows.
        public string Header;
        public List<TipRow> Rows = new List<TipRow>();

        public TipSection Text(string text, bool dim = false)
        {
            Rows.Add(new TipTextRow(text, dim));
            return this;
        }

        public TipSection Fact(string label, string value, Color? valueColor = null, Color? labelColor = null)
        {
            Rows.Add(new TipFactRow(label, value, valueColor, labelColor));
            return this;
        }

        public TipSection Action(string inputToken, string description)
        {
            Rows.Add(new TipActionRow(inputToken, description));
            return this;
        }

        public TipSection Columns(
            IReadOnlyList<string> cells, Color? color = null, Texture2D icon = null, bool tight = false)
        {
            Rows.Add(new TipColumnsRow(cells, color, icon, tight));
            return this;
        }

        public TipSection Span(string text, float indent = 0f, bool dim = true, int alignColumn = -1)
        {
            Rows.Add(new TipSpanRow(text, indent, dim, alignColumn));
            return this;
        }

        public TipSection Rule()
        {
            Rows.Add(new TipRuleRow());
            return this;
        }

        public TipSection Gap(float height)
        {
            Rows.Add(new TipGapRow(height));
            return this;
        }
    }

    public abstract class TipRow
    {
    }

    /// Wrapped prose line(s); dim renders in the meta gray.
    public sealed class TipTextRow : TipRow
    {
        public readonly string Text;
        public readonly bool Dim;

        public TipTextRow(string text, bool dim = false)
        {
            Text = text;
            Dim = dim;
        }
    }

    /// Two aligned columns: dim label, white (or colored) value. The label
    /// column width is the max label width within the row's section.
    public sealed class TipFactRow : TipRow
    {
        public readonly string Label;
        public readonly string Value;
        public readonly Color? ValueColor;
        public readonly Color? LabelColor;

        public TipFactRow(string label, string value, Color? valueColor = null, Color? labelColor = null)
        {
            Label = label;
            Value = value;
            ValueColor = valueColor;
            LabelColor = labelColor;
        }
    }

    /// One input gesture per line: token white, description dim.
    public sealed class TipActionRow : TipRow
    {
        public readonly string InputToken;
        public readonly string Description;

        public TipActionRow(string inputToken, string description)
        {
            InputToken = inputToken;
            Description = description;
        }
    }

    /// One table line: cell text per column; row color null = white. Icon (16px)
    /// draws after the first cell's text. Tight rows pull up toward the previous
    /// row so continuation lines read as one group.
    public sealed class TipColumnsRow : TipRow
    {
        public readonly IReadOnlyList<string> Cells;
        public readonly Color? Color;
        public readonly Texture2D Icon;
        public readonly bool Tight;

        public TipColumnsRow(
            IReadOnlyList<string> cells, Color? color = null, Texture2D icon = null, bool tight = false)
        {
            Cells = cells;
            Color = color;
            Icon = icon;
            Tight = tight;
        }
    }

    /// Wrapped text spanning the full table width, inset by Indent from the
    /// table's left edge — or aligned to a table column when AlignColumn >= 0;
    /// dim by default (used for signal descriptions).
    public sealed class TipSpanRow : TipRow
    {
        public readonly string Text;
        public readonly float Indent;
        public readonly bool Dim;
        public readonly int AlignColumn;

        public TipSpanRow(string text, float indent = 0f, bool dim = true, int alignColumn = -1)
        {
            Text = text;
            Indent = indent;
            Dim = dim;
            AlignColumn = alignColumn;
        }
    }

    /// Horizontal separator line spanning the table width.
    public sealed class TipRuleRow : TipRow
    {
    }

    /// Fixed vertical whitespace; contributes nothing to plain text.
    public sealed class TipGapRow : TipRow
    {
        public readonly float Height;

        public TipGapRow(float height)
        {
            Height = height;
        }
    }
}
