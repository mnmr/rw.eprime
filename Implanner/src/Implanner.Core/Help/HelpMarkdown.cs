using System;
using System.Collections.Generic;
using System.Text;

namespace Implanner.Core.Help
{
    public enum HelpBlockKind
    {
        Paragraph,
        Heading,
        Bullet,
        Numbered,
        Image,
        /// <summary>An embedded interactive demo control; the name travels
        /// in ImagePath. Renderers register game-side by name.</summary>
        Demo,
    }

    public enum HelpRunKind
    {
        Text,
        TopicLink,
        KeyedLabel,
        /// <summary>Inline image: Text is the alt text (used when the image
        /// cannot be resolved), Target is the image path.</summary>
        Image,
    }

    [Flags]
    public enum HelpRunStyle
    {
        None = 0,
        Bold = 1,
        Italic = 2,
    }

    /// <summary>
    /// One inline span of a block: plain/styled text, a link to another help
    /// topic (Target = topic slug), or a keyed-string splice (Target = key,
    /// resolved to the translated label when the display snapshot is built).
    /// </summary>
    public readonly struct HelpRun
    {
        public HelpRun(HelpRunKind kind, HelpRunStyle style,
            string text, string target)
        {
            Kind = kind;
            Style = style;
            Text = text;
            Target = target;
        }

        public HelpRunKind Kind { get; }
        public HelpRunStyle Style { get; }
        public string Text { get; }
        public string Target { get; }
    }

    /// <summary>
    /// One block-level element. Level is the heading level for headings and
    /// the item number for numbered list items; ImagePath is set for images
    /// (with the alt text carried in Runs).
    /// </summary>
    public sealed class HelpBlock
    {
        public HelpBlock(HelpBlockKind kind, int level,
            HelpRun[] runs, string imagePath)
        {
            Kind = kind;
            Level = level;
            Runs = runs;
            ImagePath = imagePath;
        }

        public HelpBlockKind Kind { get; }
        public int Level { get; }
        public HelpRun[] Runs { get; }
        public string ImagePath { get; }
    }

    /// <summary>
    /// Parsed, immutable representation of one help topic file. Title comes
    /// from the front-matter block and is empty when absent.
    /// </summary>
    public sealed class HelpDocument
    {
        public HelpDocument(string title, HelpBlock[] blocks)
        {
            Title = title;
            Blocks = blocks;
        }

        public string Title { get; }
        public HelpBlock[] Blocks { get; }
    }

    /// <summary>
    /// Parser for the help markdown subset: front-matter title, headings,
    /// paragraphs, bullet/numbered lists, standalone images, bold/italic,
    /// topic links and keyed-label splices. Unknown syntax degrades to
    /// literal text rather than failing.
    /// </summary>
    public static class HelpMarkdown
    {
        private const string TopicLinkScheme = "topic:";

        public static HelpDocument Parse(string text)
        {
            if (string.IsNullOrEmpty(text))
                return new HelpDocument("", Array.Empty<HelpBlock>());

            string[] lines = text.Split('\n');
            int index = 0;
            string title = ParseFrontMatter(lines, ref index);

            var blocks = new List<HelpBlock>();
            // Pending multi-line block: a paragraph, or a list item that
            // plain follow-up lines lazily continue (markdown-style) until a
            // blank line or the next block marker.
            var pendingLines = new List<string>();
            HelpBlockKind pendingKind = HelpBlockKind.Paragraph;
            int pendingLevel = 0;

            void Flush()
            {
                if (pendingLines.Count == 0) return;
                string joined = string.Join(" ", pendingLines.ToArray());
                blocks.Add(new HelpBlock(pendingKind, pendingLevel,
                    ParseInline(joined), ""));
                pendingLines.Clear();
                pendingKind = HelpBlockKind.Paragraph;
                pendingLevel = 0;
            }

            for (; index < lines.Length; index++)
            {
                string line = lines[index].Trim();
                if (line.Length == 0)
                {
                    Flush();
                    continue;
                }

                int headingLevel = HeadingLevel(line);
                if (headingLevel > 0)
                {
                    Flush();
                    blocks.Add(new HelpBlock(HelpBlockKind.Heading, headingLevel,
                        ParseInline(line.Substring(headingLevel + 1).Trim()), ""));
                    continue;
                }

                if (line.StartsWith("- ", StringComparison.Ordinal))
                {
                    Flush();
                    pendingKind = HelpBlockKind.Bullet;
                    pendingLines.Add(line.Substring(2).Trim());
                    continue;
                }

                int itemNumber = NumberedItemNumber(line, out int bodyStart);
                if (itemNumber > 0)
                {
                    Flush();
                    pendingKind = HelpBlockKind.Numbered;
                    pendingLevel = itemNumber;
                    pendingLines.Add(line.Substring(bodyStart).Trim());
                    continue;
                }

                const string DemoPrefix = "@demo:";
                if (line.StartsWith(DemoPrefix, StringComparison.Ordinal)
                    && line.Length > DemoPrefix.Length)
                {
                    Flush();
                    blocks.Add(new HelpBlock(HelpBlockKind.Demo, 0,
                        Array.Empty<HelpRun>(),
                        line.Substring(DemoPrefix.Length).Trim()));
                    continue;
                }

                if (TryParseStandaloneImage(line, out string alt, out string path))
                {
                    Flush();
                    HelpRun[] altRuns = alt.Length == 0
                        ? Array.Empty<HelpRun>()
                        : new[] { new HelpRun(HelpRunKind.Text,
                            HelpRunStyle.None, alt, "") };
                    blocks.Add(new HelpBlock(HelpBlockKind.Image, 0,
                        altRuns, path));
                    continue;
                }

                pendingLines.Add(line);
            }
            Flush();

            return new HelpDocument(title, blocks.ToArray());
        }

        /// <summary>
        /// Reads a leading front-matter fence and returns its title value.
        /// Without a closing fence nothing is consumed: the document falls
        /// back to being parsed entirely as content.
        /// </summary>
        private static string ParseFrontMatter(string[] lines, ref int index)
        {
            if (lines.Length == 0 || lines[0].Trim() != "---") return "";

            for (int close = 1; close < lines.Length; close++)
            {
                if (lines[close].Trim() != "---") continue;

                string title = "";
                for (int i = 1; i < close; i++)
                {
                    string line = lines[i].Trim();
                    if (line.StartsWith("title:", StringComparison.Ordinal))
                        title = line.Substring(6).Trim();
                }
                index = close + 1;
                return title;
            }
            return "";
        }

        private static int HeadingLevel(string line)
        {
            int level = 0;
            while (level < line.Length && line[level] == '#') level++;
            bool valid = level >= 1 && level <= 6
                && level < line.Length && line[level] == ' ';
            return valid ? level : 0;
        }

        private static int NumberedItemNumber(string line, out int bodyStart)
        {
            bodyStart = 0;
            int digits = 0;
            while (digits < line.Length && char.IsDigit(line[digits])) digits++;
            bool valid = digits >= 1
                && digits + 1 < line.Length
                && line[digits] == '.' && line[digits + 1] == ' ';
            if (!valid) return 0;
            if (!int.TryParse(line.Substring(0, digits), out int number)
                || number <= 0)
                return 0;
            bodyStart = digits + 2;
            return number;
        }

        private static bool TryParseStandaloneImage(
            string line, out string alt, out string path)
        {
            alt = "";
            path = "";
            if (!line.StartsWith("![", StringComparison.Ordinal)) return false;
            if (!line.EndsWith(")", StringComparison.Ordinal)) return false;
            int split = line.IndexOf("](", StringComparison.Ordinal);
            if (split < 0) return false;

            alt = line.Substring(2, split - 2).Trim();
            path = line.Substring(split + 2, line.Length - split - 3).Trim();
            return path.Length > 0;
        }

        private static HelpRun[] ParseInline(string text)
        {
            if (text.Length == 0) return Array.Empty<HelpRun>();
            var runs = new List<HelpRun>();
            AppendInline(text, HelpRunStyle.None, runs);
            return runs.ToArray();
        }

        private static void AppendInline(
            string text, HelpRunStyle style, List<HelpRun> runs)
        {
            var literal = new StringBuilder();
            int i = 0;
            while (i < text.Length)
            {
                char c = text[i];
                if (c == '*')
                {
                    bool bold = i + 1 < text.Length && text[i + 1] == '*';
                    string marker = bold ? "**" : "*";
                    int close = text.IndexOf(marker, i + marker.Length,
                        StringComparison.Ordinal);
                    if (close < 0)
                    {
                        literal.Append(marker);
                        i += marker.Length;
                        continue;
                    }
                    FlushLiteral(literal, style, runs);
                    string inner = text.Substring(i + marker.Length,
                        close - i - marker.Length);
                    AppendInline(inner, style | (bold
                        ? HelpRunStyle.Bold : HelpRunStyle.Italic), runs);
                    i = close + marker.Length;
                    continue;
                }

                if (c == '!' && i + 1 < text.Length && text[i + 1] == '['
                    && TryMatchBracketPair(text, i + 1,
                        out string alt, out string imagePath,
                        out int imageEnd))
                {
                    FlushLiteral(literal, style, runs);
                    runs.Add(new HelpRun(HelpRunKind.Image, style,
                        alt, imagePath));
                    i = imageEnd;
                    continue;
                }

                if (c == '[' && TryMatchBracketPair(text, i,
                        out string display, out string target, out int linkEnd))
                {
                    if (target.StartsWith(TopicLinkScheme,
                            StringComparison.Ordinal))
                    {
                        FlushLiteral(literal, style, runs);
                        runs.Add(new HelpRun(HelpRunKind.TopicLink, style,
                            display,
                            target.Substring(TopicLinkScheme.Length)));
                    }
                    else
                    {
                        // Unsupported link target: degrade to the display
                        // text alone.
                        literal.Append(display);
                    }
                    i = linkEnd;
                    continue;
                }

                if (c == '{' && TryMatchKeyedLabel(text, i,
                        out string key, out int keyEnd))
                {
                    FlushLiteral(literal, style, runs);
                    runs.Add(new HelpRun(HelpRunKind.KeyedLabel, style,
                        "", key));
                    i = keyEnd;
                    continue;
                }

                literal.Append(c);
                i++;
            }
            FlushLiteral(literal, style, runs);
        }

        /// <summary>
        /// Matches "[display](target)" starting at the '[' and reports the
        /// index one past the closing ')'.
        /// </summary>
        private static bool TryMatchBracketPair(string text, int start,
            out string display, out string target, out int end)
        {
            display = "";
            target = "";
            end = 0;
            int split = text.IndexOf("](", start + 1, StringComparison.Ordinal);
            if (split < 0) return false;
            int close = text.IndexOf(')', split + 2);
            if (close < 0) return false;

            display = text.Substring(start + 1, split - start - 1);
            target = text.Substring(split + 2, close - split - 2);
            end = close + 1;
            return true;
        }

        /// <summary>
        /// Matches "{k:Key}" starting at the '{' and reports the index one
        /// past the closing '}'.
        /// </summary>
        private static bool TryMatchKeyedLabel(string text, int start,
            out string key, out int end)
        {
            key = "";
            end = 0;
            if (start + 2 >= text.Length
                || text[start + 1] != 'k' || text[start + 2] != ':')
                return false;
            int close = text.IndexOf('}', start + 3);
            if (close < 0) return false;

            key = text.Substring(start + 3, close - start - 3).Trim();
            if (key.Length == 0) return false;
            end = close + 1;
            return true;
        }

        private static void FlushLiteral(
            StringBuilder literal, HelpRunStyle style, List<HelpRun> runs)
        {
            if (literal.Length == 0) return;
            runs.Add(new HelpRun(HelpRunKind.Text, style,
                literal.ToString(), ""));
            literal.Length = 0;
        }
    }
}
