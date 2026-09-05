using System;
using System.Collections.Generic;

namespace RimShared.Common.Help
{
    public enum HelpFont
    {
        Body,
        H1,
        H2,
        H3,
    }

    public enum HelpItemKind
    {
        Text,
        TopicLink,
        ListMarker,
        Image,
        Demo,
    }

    /// <summary>
    /// Measurement provider for layout. Implementations cache their widths
    /// behind the UI metric and language revisions; the layout itself never
    /// measures.
    /// </summary>
    public interface IHelpTextMeasurer
    {
        float WordWidth(string word, HelpFont font, HelpRunStyle style);
        float SpaceWidth(HelpFont font, HelpRunStyle style);
        float LineHeight(HelpFont font);
    }

    /// <summary>Spacing constants for topic layout, in logical GUI units.</summary>
    public sealed class HelpLayoutMetrics
    {
        public float ParagraphSpacing = 8f;
        public float HeadingSpacingTop = 12f;
        public float HeadingSpacingBottom = 4f;
        public float ListIndent = 24f;
        public float ListItemSpacing = 2f;
    }

    /// <summary>
    /// One positioned draw item: a word (Text/TopicLink), a list marker, or
    /// an image. Text is the word, marker glyph, or image path; Target is the
    /// topic slug for links. Text item height is the font line height.
    /// </summary>
    public sealed class HelpLayoutItem
    {
        public HelpLayoutItem(HelpItemKind kind, float x, float y,
            float width, float height, HelpFont font, HelpRunStyle style,
            string text, string target)
        {
            Kind = kind;
            X = x;
            Y = y;
            Width = width;
            Height = height;
            Font = font;
            Style = style;
            Text = text;
            Target = target;
        }

        public HelpItemKind Kind { get; }
        public float X { get; }
        public float Y { get; }
        public float Width { get; }
        public float Height { get; }
        public HelpFont Font { get; }
        public HelpRunStyle Style { get; }
        public string Text { get; }
        public string Target { get; }
    }

    /// <summary>Immutable positioned layout of one help topic.</summary>
    public sealed class HelpTopicLayout
    {
        public HelpTopicLayout(HelpLayoutItem[] items, float height)
        {
            Items = items;
            Height = height;
        }

        public HelpLayoutItem[] Items { get; }
        public float Height { get; }
    }

    /// <summary>Pre-layout document transforms.</summary>
    public static class HelpDocuments
    {
        /// <summary>
        /// Replaces every keyed-label run with a plain text run holding the
        /// resolved translation (or the key itself when the resolver returns
        /// null, so authors can see the broken reference). Styles carry over.
        /// </summary>
        public static HelpDocument ResolveKeyedLabels(
            HelpDocument document, Func<string, string?> resolve)
        {
            bool anyKeyed = false;
            for (int i = 0; i < document.Blocks.Length && !anyKeyed; i++)
            {
                HelpRun[] runs = document.Blocks[i].Runs;
                for (int j = 0; j < runs.Length; j++)
                {
                    if (runs[j].Kind != HelpRunKind.KeyedLabel) continue;
                    anyKeyed = true;
                    break;
                }
            }
            if (!anyKeyed) return document;

            var blocks = new HelpBlock[document.Blocks.Length];
            for (int i = 0; i < blocks.Length; i++)
            {
                HelpBlock block = document.Blocks[i];
                HelpRun[] runs = block.Runs;
                HelpRun[]? resolved = null;
                for (int j = 0; j < runs.Length; j++)
                {
                    if (runs[j].Kind != HelpRunKind.KeyedLabel) continue;
                    resolved ??= (HelpRun[])runs.Clone();
                    string label = resolve(runs[j].Target) ?? runs[j].Target;
                    resolved[j] = new HelpRun(HelpRunKind.Text,
                        runs[j].Style, label, "");
                }
                blocks[i] = resolved == null
                    ? block
                    : new HelpBlock(block.Kind, block.Level, resolved,
                        block.ImagePath);
            }
            return new HelpDocument(document.Title, blocks);
        }
    }

    /// <summary>
    /// Word-level flow layout for a resolved help document: wraps at spaces
    /// and between CJK glyphs (see <see cref="LineBreakRules"/>), keeps
    /// space-free Latin style joins on one line, merges adjacent same-styled
    /// glyphs that land on one line into a single draw item, indents list
    /// items past their markers, and scales images down to the content
    /// width. All output geometry is deterministic data; rendering draws it
    /// by index.
    /// </summary>
    public static class HelpFlowLayout
    {
        public delegate bool ImageSizeResolver(
            string path, out float width, out float height);

        public delegate bool DemoSizeResolver(
            string name, out float width, out float height);

        public static HelpTopicLayout Build(HelpDocument document,
            float width, IHelpTextMeasurer measurer,
            HelpLayoutMetrics metrics, ImageSizeResolver imageSize,
            DemoSizeResolver? demoSize = null)
        {
            var items = new List<HelpLayoutItem>();
            var segments = new List<Segment>();
            var line = new List<LineItem>();
            float y = 0f;
            bool anyEmitted = false;
            HelpBlockKind previousKind = HelpBlockKind.Paragraph;
            for (int i = 0; i < document.Blocks.Length; i++)
            {
                HelpBlock block = document.Blocks[i];
                if (block.Kind == HelpBlockKind.Image
                    || block.Kind == HelpBlockKind.Demo)
                {
                    bool isDemo = block.Kind == HelpBlockKind.Demo;
                    float naturalWidth = 0f;
                    float naturalHeight = 0f;
                    bool resolved = isDemo
                        ? demoSize != null && demoSize(block.ImagePath,
                            out naturalWidth, out naturalHeight)
                        : imageSize(block.ImagePath,
                            out naturalWidth, out naturalHeight);
                    if (!resolved || naturalWidth <= 0f || naturalHeight <= 0f)
                        continue;
                    if (anyEmitted)
                        y += SpacingBefore(block.Kind, previousKind, metrics);
                    float scale = naturalWidth > width
                        ? width / naturalWidth : 1f;
                    items.Add(new HelpLayoutItem(
                        isDemo ? HelpItemKind.Demo : HelpItemKind.Image,
                        0f, y, naturalWidth * scale, naturalHeight * scale,
                        HelpFont.Body, HelpRunStyle.None, block.ImagePath, ""));
                    y += naturalHeight * scale;
                }
                else
                {
                    if (anyEmitted)
                        y += SpacingBefore(block.Kind, previousKind, metrics);
                    y = FlowTextBlock(block, y, width, measurer, metrics,
                        imageSize, items, segments, line);
                }
                previousKind = block.Kind;
                anyEmitted = true;
            }
            return new HelpTopicLayout(items.ToArray(), y);
        }

        private static float SpacingBefore(HelpBlockKind kind,
            HelpBlockKind previousKind, HelpLayoutMetrics metrics)
        {
            if (kind == HelpBlockKind.Heading) return metrics.HeadingSpacingTop;
            if (previousKind == HelpBlockKind.Heading)
                return metrics.HeadingSpacingBottom;
            bool tightList = kind == previousKind
                && (kind == HelpBlockKind.Bullet
                    || kind == HelpBlockKind.Numbered);
            return tightList ? metrics.ListItemSpacing : metrics.ParagraphSpacing;
        }

        /// <summary>Flows one text block's words and returns the new y.
        /// Lines buffer their items so an inline image can grow the line box;
        /// shorter items center vertically within the finished line.</summary>
        private static float FlowTextBlock(HelpBlock block, float top,
            float width, IHelpTextMeasurer measurer, HelpLayoutMetrics metrics,
            HelpFlowLayout.ImageSizeResolver imageSize,
            List<HelpLayoutItem> items, List<Segment> segments,
            List<LineItem> line)
        {
            HelpFont font = BlockFont(block);
            float lineHeight = measurer.LineHeight(font);
            bool isList = block.Kind == HelpBlockKind.Bullet
                || block.Kind == HelpBlockKind.Numbered;
            float indent = 0f;
            if (isList)
            {
                string marker = block.Kind == HelpBlockKind.Bullet
                    ? "•"
                    : block.Level.ToString() + ".";
                float markerWidth = measurer.WordWidth(
                    marker, font, HelpRunStyle.None);
                // Centered in the indent: the gap before the marker equals
                // the gap between marker and text.
                float markerX = Math.Max(
                    0f, (metrics.ListIndent - markerWidth) / 2f);
                line.Add(new LineItem
                {
                    Kind = HelpItemKind.ListMarker,
                    Text = marker,
                    Target = "",
                    Style = HelpRunStyle.None,
                    X = markerX,
                    Width = markerWidth,
                    Height = lineHeight,
                });
                indent = metrics.ListIndent;
            }

            Tokenize(block.Runs, segments);
            for (int i = 0; i < segments.Count; i++)
            {
                Segment segment = segments[i];
                if (segment.IsImage)
                {
                    if (imageSize(segment.Target,
                            out float imageWidth, out float imageHeight)
                        && imageWidth > 0f && imageHeight > 0f)
                    {
                        segment.Width = imageWidth;
                        segment.ImageHeight = imageHeight;
                    }
                    else
                    {
                        // Unresolvable inline image: fall back to alt text.
                        segment.IsImage = false;
                        segment.Target = "";
                        segment.Width = measurer.WordWidth(
                            segment.Text, font, segment.Style);
                    }
                }
                else
                {
                    segment.Width = measurer.WordWidth(
                        segment.Text, font, segment.Style);
                }
                segments[i] = segment;
            }

            float x = indent;
            float lineY = top;
            bool anyLine = line.Count > 0;
            int clusterStart = 0;
            while (clusterStart < segments.Count)
            {
                int clusterEnd = clusterStart;
                float clusterWidth = 0f;
                while (clusterEnd < segments.Count)
                {
                    Segment segment = segments[clusterEnd];
                    clusterWidth += segment.Width;
                    clusterEnd++;
                    if (segment.ClusterBreakAfter) break;
                }

                // A glyph break carries no gap; only a source space does.
                bool gapBefore = clusterStart > 0
                    && segments[clusterStart - 1].GapAfter;
                bool adjacent = false;
                if (x > indent)
                {
                    float spaceWidth = gapBefore
                        ? measurer.SpaceWidth(font, segments[clusterStart].Style)
                        : 0f;
                    if (x + spaceWidth + clusterWidth > width)
                    {
                        lineY += FlushLine(items, line, lineY,
                            font, lineHeight);
                        x = indent;
                    }
                    else
                    {
                        x += spaceWidth;
                        adjacent = !gapBefore;
                    }
                }

                for (int i = clusterStart; i < clusterEnd; i++)
                {
                    Segment segment = segments[i];
                    HelpItemKind kind = segment.IsImage
                        ? HelpItemKind.Image
                        : segment.Target.Length > 0
                            ? HelpItemKind.TopicLink
                            : HelpItemKind.Text;
                    AddLineItem(line, new LineItem
                    {
                        Kind = kind,
                        Text = segment.IsImage ? segment.Target : segment.Text,
                        Target = segment.IsImage ? "" : segment.Target,
                        Style = segment.Style,
                        X = x,
                        Width = segment.Width,
                        Height = segment.IsImage
                            ? segment.ImageHeight : lineHeight,
                    }, adjacent || i > clusterStart);
                    x += segment.Width;
                }
                anyLine = true;
                clusterStart = clusterEnd;
            }
            segments.Clear();
            if (!anyLine) return lineY + lineHeight;   // empty block
            return lineY + FlushLine(items, line, lineY, font, lineHeight);
        }

        /// <summary>Appends a line item, extending the previous item instead
        /// when the two touch (no gap between them) and share kind, style,
        /// and target. Character-broken CJK text arrives one glyph per
        /// segment; without this merge every glyph would be its own draw
        /// call. Widths add, which is exact for the unkerned CJK glyphs that
        /// produce adjacency.</summary>
        private static void AddLineItem(
            List<LineItem> line, LineItem item, bool adjacent)
        {
            if (adjacent && line.Count > 0)
            {
                LineItem last = line[line.Count - 1];
                bool mergeable = last.Kind == item.Kind
                    && item.Kind != HelpItemKind.Image
                    && item.Kind != HelpItemKind.ListMarker
                    && last.Style == item.Style
                    && last.Target == item.Target;
                if (mergeable)
                {
                    last.Text += item.Text;
                    last.Width += item.Width;
                    line[line.Count - 1] = last;
                    return;
                }
            }
            line.Add(item);
        }

        /// <summary>Emits the buffered line, centering shorter items in the
        /// line box, and returns the line box height.</summary>
        private static float FlushLine(List<HelpLayoutItem> items,
            List<LineItem> line, float lineY, HelpFont font,
            float lineHeight)
        {
            if (line.Count == 0) return lineHeight;
            float maxHeight = lineHeight;
            for (int i = 0; i < line.Count; i++)
            {
                if (line[i].Height > maxHeight) maxHeight = line[i].Height;
            }
            for (int i = 0; i < line.Count; i++)
            {
                LineItem item = line[i];
                float offset = (float)Math.Floor(
                    (maxHeight - item.Height) / 2f);
                items.Add(new HelpLayoutItem(item.Kind, item.X,
                    lineY + offset, item.Width, item.Height, font,
                    item.Style, item.Text, item.Target));
            }
            line.Clear();
            return maxHeight;
        }

        private struct LineItem
        {
            public HelpItemKind Kind;
            public string Text;
            public string Target;
            public HelpRunStyle Style;
            public float X;
            public float Width;
            public float Height;
        }

        private static HelpFont BlockFont(HelpBlock block)
        {
            if (block.Kind != HelpBlockKind.Heading) return HelpFont.Body;
            if (block.Level <= 1) return HelpFont.H1;
            return block.Level == 2 ? HelpFont.H2 : HelpFont.H3;
        }

        /// <summary>
        /// Splits a block's runs into segments. Latin words break only where
        /// the source had a space (a gap break); adjacent same-styled words
        /// without a space merge into one segment. Character-breakable CJK
        /// glyphs each end a segment with a gapless break, except that a
        /// line-start-forbidden glyph joins the segment before it and a
        /// line-end-forbidden glyph keeps the following glyph attached.
        /// </summary>
        private static void Tokenize(HelpRun[] runs, List<Segment> segments)
        {
            for (int i = 0; i < runs.Length; i++)
            {
                HelpRun run = runs[i];
                if (run.Kind == HelpRunKind.Image)
                {
                    // Atomic segment: alt text for fallback, path as target.
                    segments.Add(new Segment
                    {
                        Text = run.Text,
                        Style = run.Style,
                        Target = run.Target,
                        IsImage = true,
                    });
                    continue;
                }
                string text = run.Kind == HelpRunKind.KeyedLabel
                    ? run.Target : run.Text;   // unresolved keys degrade
                if (text.Length == 0) continue;
                string target = run.Kind == HelpRunKind.TopicLink
                    ? run.Target : "";

                int wordStart = -1;
                for (int j = 0; j <= text.Length; j++)
                {
                    bool atEnd = j == text.Length;
                    char c = atEnd ? ' ' : text[j];
                    bool isSpace = LineBreakRules.IsSpace(c);
                    bool isGlyph = !isSpace
                        && LineBreakRules.IsCharacterBreakable(c);
                    if (!isSpace && !isGlyph)
                    {
                        if (wordStart < 0) wordStart = j;
                        continue;
                    }
                    if (wordStart >= 0)
                    {
                        AppendWord(segments,
                            text.Substring(wordStart, j - wordStart),
                            run.Style, target);
                        wordStart = -1;
                        if (isGlyph) MarkBreak(segments, gap: false);
                    }
                    if (isSpace)
                    {
                        if (!atEnd && segments.Count > 0)
                            MarkBreak(segments, gap: true);
                        continue;
                    }
                    if (LineBreakRules.ForbidsLineStart(c))
                        UnmarkGlyphBreak(segments);
                    AppendWord(segments, c.ToString(), run.Style, target);
                    if (!LineBreakRules.ForbidsLineEnd(c))
                        MarkBreak(segments, gap: false);
                }
            }
        }

        private static void AppendWord(List<Segment> segments, string word,
            HelpRunStyle style, string target)
        {
            if (segments.Count > 0)
            {
                Segment last = segments[segments.Count - 1];
                if (!last.ClusterBreakAfter && !last.IsImage
                    && last.Style == style && last.Target == target)
                {
                    last.Text += word;
                    segments[segments.Count - 1] = last;
                    return;
                }
            }
            segments.Add(new Segment
            {
                Text = word,
                Style = style,
                Target = target,
            });
        }

        private static void MarkBreak(List<Segment> segments, bool gap)
        {
            Segment last = segments[segments.Count - 1];
            last.ClusterBreakAfter = true;
            last.GapAfter = gap;
            segments[segments.Count - 1] = last;
        }

        /// <summary>Retracts a gapless glyph break so a line-start-forbidden
        /// glyph stays with its predecessor. A space-gap break is kept: the
        /// author put whitespace there on purpose.</summary>
        private static void UnmarkGlyphBreak(List<Segment> segments)
        {
            if (segments.Count == 0) return;
            Segment last = segments[segments.Count - 1];
            if (!last.ClusterBreakAfter || last.GapAfter) return;
            last.ClusterBreakAfter = false;
            segments[segments.Count - 1] = last;
        }

        private struct Segment
        {
            public string Text;
            public HelpRunStyle Style;
            public string Target;
            public float Width;
            /// <summary>A line may break after this segment.</summary>
            public bool ClusterBreakAfter;
            /// <summary>The break after this segment came from a source
            /// space and renders one space advance when not wrapped.</summary>
            public bool GapAfter;
            public bool IsImage;
            public float ImageHeight;
        }
    }
}
