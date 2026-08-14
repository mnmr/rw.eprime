using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Verse;

namespace WorkRoles
{
    /// A producer-owned structured tooltip rendered by StructuredTipPresenter.
    /// PlainText remains available to the public diagnostic tooltip APIs.
    internal sealed class StructuredTip
    {
        internal StructuredTip(string stableKey, TipModel model)
        {
            StableKey = stableKey ?? throw new ArgumentNullException(nameof(stableKey));
            Model = model ?? throw new ArgumentNullException(nameof(model));
            PlainText = model.ToPlainText();
        }

        internal string StableKey { get; }
        internal TipModel Model { get; }
        internal string PlainText { get; }

        /// Exact comparison of every published field that can affect
        /// structured-tip rendering. RenderCache is derived geometry and is
        /// deliberately excluded.
        internal bool ContentEquals(StructuredTip other)
        {
            if (ReferenceEquals(this, other)) return true;
            return other != null
                && string.Equals(StableKey, other.StableKey,
                    StringComparison.Ordinal)
                && ModelEquals(Model, other.Model);
        }

        private static bool ModelEquals(TipModel left, TipModel right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left == null || right == null
                || !string.Equals(left.Title, right.Title,
                    StringComparison.Ordinal)
                || !string.Equals(left.Badge, right.Badge,
                    StringComparison.Ordinal)
                || !ColorEquals(left.BadgeColor, right.BadgeColor)
                || left.Padding != right.Padding
                || left.Sections.Count != right.Sections.Count)
                return false;
            for (int sectionIndex = 0;
                    sectionIndex < left.Sections.Count; sectionIndex++)
            {
                TipSection leftSection = left.Sections[sectionIndex];
                TipSection rightSection = right.Sections[sectionIndex];
                if (!string.Equals(leftSection.Header, rightSection.Header,
                        StringComparison.Ordinal)
                    || leftSection.Rows.Count != rightSection.Rows.Count)
                    return false;
                for (int rowIndex = 0;
                        rowIndex < leftSection.Rows.Count; rowIndex++)
                    if (!RowEquals(leftSection.Rows[rowIndex],
                            rightSection.Rows[rowIndex])) return false;
            }
            return true;
        }

        private static bool RowEquals(TipRow left, TipRow right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left == null || right == null) return false;
            if (left is TipTextRow leftText)
            {
                var rightText = right as TipTextRow;
                return rightText != null && leftText.Dim == rightText.Dim
                    && string.Equals(leftText.Text, rightText.Text,
                        StringComparison.Ordinal);
            }
            if (left is TipFactRow leftFact)
            {
                var rightFact = right as TipFactRow;
                return rightFact != null
                    && string.Equals(leftFact.Label, rightFact.Label,
                           StringComparison.Ordinal)
                    && string.Equals(leftFact.Value, rightFact.Value,
                        StringComparison.Ordinal)
                    && NullableColorEquals(leftFact.ValueColor,
                        rightFact.ValueColor)
                    && NullableColorEquals(leftFact.LabelColor,
                        rightFact.LabelColor);
            }
            if (left is TipActionRow leftAction)
            {
                var rightAction = right as TipActionRow;
                return rightAction != null
                    && string.Equals(leftAction.InputToken,
                           rightAction.InputToken, StringComparison.Ordinal)
                    && string.Equals(leftAction.Description,
                        rightAction.Description, StringComparison.Ordinal);
            }
            if (left is TipColumnsRow leftColumns)
            {
                var rightColumns = right as TipColumnsRow;
                if (rightColumns == null
                    || !NullableColorEquals(leftColumns.Color,
                        rightColumns.Color)
                    || !ReferenceEquals(leftColumns.Icon, rightColumns.Icon)
                    || leftColumns.Tight != rightColumns.Tight)
                    return false;
                int leftCount = leftColumns.Cells?.Count ?? 0;
                int rightCount = rightColumns.Cells?.Count ?? 0;
                if (leftCount != rightCount) return false;
                for (int i = 0; i < leftCount; i++)
                    if (!string.Equals(leftColumns.Cells[i],
                            rightColumns.Cells[i], StringComparison.Ordinal))
                        return false;
                return true;
            }
            if (left is TipSpanRow leftSpan)
            {
                var rightSpan = right as TipSpanRow;
                return rightSpan != null
                    && string.Equals(leftSpan.Text, rightSpan.Text,
                           StringComparison.Ordinal)
                    && leftSpan.Indent == rightSpan.Indent
                    && leftSpan.Dim == rightSpan.Dim
                    && leftSpan.AlignColumn == rightSpan.AlignColumn;
            }
            if (left is TipInlineRow leftInline)
            {
                var rightInline = right as TipInlineRow;
                if (rightInline == null
                    || !string.Equals(leftInline.Label, rightInline.Label,
                        StringComparison.Ordinal)) return false;
                int leftCount = leftInline.Segments?.Count ?? 0;
                int rightCount = rightInline.Segments?.Count ?? 0;
                if (leftCount != rightCount) return false;
                for (int i = 0; i < leftCount; i++)
                {
                    TipInlineSegment leftSegment = leftInline.Segments[i];
                    TipInlineSegment rightSegment = rightInline.Segments[i];
                    if (!string.Equals(leftSegment.Text, rightSegment.Text,
                            StringComparison.Ordinal)
                        || !ReferenceEquals(leftSegment.Icon,
                            rightSegment.Icon)
                        || !ColorEquals(leftSegment.Color,
                            rightSegment.Color)
                        || leftSegment.Gap != rightSegment.Gap)
                        return false;
                }
                return true;
            }
            if (left is TipGapRow leftGap)
            {
                var rightGap = right as TipGapRow;
                return rightGap != null && leftGap.Height == rightGap.Height;
            }
            return left is TipRuleRow && right is TipRuleRow;
        }

        private static bool NullableColorEquals(Color? left, Color? right)
        {
            if (left.HasValue != right.HasValue) return false;
            return !left.HasValue || ColorEquals(left.Value, right.Value);
        }

        private static bool ColorEquals(Color left, Color right) =>
            left.r == right.r && left.g == right.g
            && left.b == right.b && left.a == right.a;
    }

    /// Structured tooltip content: a title/badge line plus sections of rows.
    /// ToPlainText() supports public diagnostic tooltip APIs; WrTipUI renders
    /// the model directly from cached snapshots.
    public sealed class TipModel
    {
        public string Title;
        public string Badge;
        public Color BadgeColor = Color.white;
        /// Extra inset beyond the tooltip renderer's 4px frame inset, for a
        /// total content inset of 8px.
        public float Padding = 4f;
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

        /// Deterministic plain-text rendering with no trailing whitespace.
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
                        case TipInlineRow inline:
                        {
                            if (!inline.Label.NullOrEmpty())
                                sb.Append(inline.Label).Append(": ");
                            for (int i = 0; i < (inline.Segments?.Count ?? 0); i++)
                                if (inline.Segments[i].Text != null)
                                    sb.Append(inline.Segments[i].Text);
                            break;
                        }
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

        public TipSection Inline(IReadOnlyList<TipInlineSegment> segments, string label = null)
        {
            Rows.Add(new TipInlineRow(segments, label));
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

    /// One unwrapped line of inline segments: colored text runs and small
    /// tinted icons (e.g. the role tip's per-skill star pairs). A non-null
    /// Label renders like a fact row's label and aligns the segments to the
    /// shared value column ("" = aligned continuation line).
    public sealed class TipInlineRow : TipRow
    {
        public readonly IReadOnlyList<TipInlineSegment> Segments;
        public readonly string Label;

        public TipInlineRow(IReadOnlyList<TipInlineSegment> segments, string label = null)
        {
            Segments = segments;
            Label = label;
        }
    }

    /// Text (Icon null) or icon (Text null) segment; Gap is the leading space
    /// before the segment.
    public readonly struct TipInlineSegment
    {
        public readonly string Text;
        public readonly Texture2D Icon;
        public readonly Color Color;
        public readonly float Gap;

        public TipInlineSegment(string text, Color color, float gap = 0f)
        {
            Text = text;
            Icon = null;
            Color = color;
            Gap = gap;
        }

        public TipInlineSegment(Texture2D icon, Color color, float gap = 0f)
        {
            Text = null;
            Icon = icon;
            Color = color;
            Gap = gap;
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
