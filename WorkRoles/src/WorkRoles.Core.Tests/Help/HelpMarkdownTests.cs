using System.Text;
using WorkRoles.Core.Help;

namespace WorkRoles.Core.Tests.Help;

public class HelpMarkdownTests
{
    [Test]
    public async Task FrontMatterSuppliesTheTitleAndBlankLinesSeparateParagraphs()
    {
        var doc = HelpMarkdown.Parse(
            "---\n" +
            "title: Pinning roles\n" +
            "---\n" +
            "First paragraph spans\n" +
            "two source lines.\n" +
            "\n" +
            "Second paragraph.\n");

        await Assert.That(doc.Title).IsEqualTo("Pinning roles");
        await Assert.That(Dump(doc)).IsEqualTo(
            "P First paragraph spans two source lines.\n" +
            "P Second paragraph.");
    }

    [Test]
    public async Task MissingFrontMatterYieldsAnEmptyTitleAndKeepsAllContent()
    {
        var doc = HelpMarkdown.Parse("Just text.");

        await Assert.That(doc.Title).IsEqualTo("");
        await Assert.That(Dump(doc)).IsEqualTo("P Just text.");
    }

    [Test]
    public async Task HeadingsCarryTheirLevel()
    {
        var doc = HelpMarkdown.Parse(
            "# Top\n" +
            "\n" +
            "## Filters\n" +
            "Body.\n");

        await Assert.That(Dump(doc)).IsEqualTo(
            "H1 Top\n" +
            "H2 Filters\n" +
            "P Body.");
    }

    [Test]
    public async Task EmphasisSplitsRunsAndUnclosedMarkersStayLiteral()
    {
        var doc = HelpMarkdown.Parse(
            "Mix **bold** and *italic* text.\n" +
            "\n" +
            "Unclosed **marker stays literal.\n");

        await Assert.That(Dump(doc)).IsEqualTo(
            "P Mix ·B(bold)· and ·I(italic)· text.\n" +
            "P Unclosed **marker stays literal.");
    }

    [Test]
    public async Task TopicLinksAndKeyedLabelsBecomeTypedRunsInheritingStyle()
    {
        var doc = HelpMarkdown.Parse(
            "See [pinning roles](topic:pinning) and press **{k:WR_MakeItSo}**.\n" +
            "\n" +
            "External [site](https://example.com) degrades to text.\n");

        await Assert.That(Dump(doc)).IsEqualTo(
            "P See ·LINK(pinning roles→pinning)· and press ·B-KEY(WR_MakeItSo)·.\n" +
            "P External site degrades to text.");
    }

    [Test]
    public async Task BulletAndNumberedListsPreserveOrderAndNumbers()
    {
        var doc = HelpMarkdown.Parse(
            "- First bullet\n" +
            "- Second bullet\n" +
            "\n" +
            "1. Step one\n" +
            "2. Step two\n");

        await Assert.That(Dump(doc)).IsEqualTo(
            "* First bullet\n" +
            "* Second bullet\n" +
            "1. Step one\n" +
            "2. Step two");
    }

    [Test]
    public async Task PlainLinesAfterListItemsContinueTheItem()
    {
        var doc = HelpMarkdown.Parse(
            "- First bullet\n" +
            "continues here\n" +
            "- Second bullet\n" +
            "\n" +
            "1. Step one\n" +
            "also continues\n" +
            "\n" +
            "New paragraph.\n");

        await Assert.That(Dump(doc)).IsEqualTo(
            "* First bullet continues here\n" +
            "* Second bullet\n" +
            "1. Step one also continues\n" +
            "P New paragraph.");
    }

    [Test]
    public async Task StandaloneImageLinesBecomeBlocksAndInlineImagesBecomeRuns()
    {
        var doc = HelpMarkdown.Parse(
            "![Colonist table](colonists-table.png)\n" +
            "\n" +
            "An inline ![chip](x.png) run.\n");

        await Assert.That(Dump(doc)).IsEqualTo(
            "IMG colonists-table.png (Colonist table)\n" +
            "P An inline ·IMG(chip→x.png)· run.");
    }

    [Test]
    public async Task StandaloneDemoLinesBecomeDemoBlocksAndUnknownAtLinesStayText()
    {
        var doc = HelpMarkdown.Parse(
            "@demo:chip-drag\n" +
            "\n" +
            "email me @demo:nope inline stays text\n" +
            "\n" +
            "@unknown directive\n");

        await Assert.That(Dump(doc)).IsEqualTo(
            "DEMO chip-drag\n" +
            "P email me @demo:nope inline stays text\n" +
            "P @unknown directive");
    }

    [Test]
    public async Task EmptyAndWhitespaceDocumentsYieldNoBlocks()
    {
        await Assert.That(Dump(HelpMarkdown.Parse(""))).IsEqualTo("");
        await Assert.That(Dump(HelpMarkdown.Parse("  \n\n \t\n"))).IsEqualTo("");
    }

    /// <summary>
    /// Canonical dump: one line per block. Headings "H2 ", bullets "* ",
    /// numbered items "3. ", images "IMG path (alt)", paragraphs "P ".
    /// Styled or non-text runs render as ·STYLE-KIND(payload)· where plain
    /// unstyled text runs render bare.
    /// </summary>
    private static string Dump(HelpDocument doc)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < doc.Blocks.Length; i++)
        {
            if (i > 0) sb.Append('\n');
            var block = doc.Blocks[i];
            switch (block.Kind)
            {
                case HelpBlockKind.Heading:
                    sb.Append('H').Append(block.Level).Append(' ');
                    break;
                case HelpBlockKind.Bullet:
                    sb.Append("* ");
                    break;
                case HelpBlockKind.Numbered:
                    sb.Append(block.Level).Append(". ");
                    break;
                case HelpBlockKind.Image:
                    sb.Append("IMG ").Append(block.ImagePath).Append(" (");
                    AppendRuns(sb, block.Runs);
                    sb.Append(')');
                    continue;
                case HelpBlockKind.Demo:
                    sb.Append("DEMO ").Append(block.ImagePath);
                    continue;
                default:
                    sb.Append("P ");
                    break;
            }
            AppendRuns(sb, block.Runs);
        }
        return sb.ToString();
    }

    private static void AppendRuns(StringBuilder sb, HelpRun[] runs)
    {
        foreach (var run in runs)
        {
            bool plain = run.Kind == HelpRunKind.Text
                && run.Style == HelpRunStyle.None;
            if (plain)
            {
                sb.Append(run.Text);
                continue;
            }
            sb.Append('·');
            if ((run.Style & HelpRunStyle.Bold) != 0) sb.Append("B");
            if ((run.Style & HelpRunStyle.Italic) != 0) sb.Append("I");
            if (run.Kind != HelpRunKind.Text && run.Style != HelpRunStyle.None)
                sb.Append('-');
            switch (run.Kind)
            {
                case HelpRunKind.TopicLink:
                    sb.Append("LINK(").Append(run.Text).Append('→')
                        .Append(run.Target).Append(')');
                    break;
                case HelpRunKind.Image:
                    sb.Append("IMG(").Append(run.Text).Append('→')
                        .Append(run.Target).Append(')');
                    break;
                case HelpRunKind.KeyedLabel:
                    sb.Append("KEY(").Append(run.Target).Append(')');
                    break;
                default:
                    sb.Append('(').Append(run.Text).Append(')');
                    break;
            }
            sb.Append('·');
        }
    }
}
