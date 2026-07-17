using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Verse;

namespace WorkRoles
{
    /// Structured tooltip content: a title/badge line plus sections of rows.
    /// ToPlainText() is both the TipSignal text (failure-safe fallback) and the
    /// draw-registry key (see Patch_ActiveTip); WrTipUI renders the model.
    public sealed class TipModel
    {
        public string Title;
        public string Badge;
        public Color BadgeColor = Color.white;
        public List<TipSection> Sections = new List<TipSection>();

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
                if (!Badge.NullOrEmpty()) sb.Append(" — ").Append(Badge);
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

        public TipSection Fact(string label, string value, Color? valueColor = null)
        {
            Rows.Add(new TipFactRow(label, value, valueColor));
            return this;
        }

        public TipSection Action(string inputToken, string description)
        {
            Rows.Add(new TipActionRow(inputToken, description));
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

        public TipFactRow(string label, string value, Color? valueColor = null)
        {
            Label = label;
            Value = value;
            ValueColor = valueColor;
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
}
