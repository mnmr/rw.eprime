using System.Globalization;
using System.Text;
using Implanner.Core.Help;

namespace Implanner.Core.Tests;

/// <summary>
/// Layout uses a monospace fake measurer: plain glyphs are 10 wide (bold 12),
/// spaces 10, line heights Body 20 / H1 30 / H2 26 / H3 24. Metrics are the
/// explicit values built in <see cref="Metrics"/> so expected positions can
/// be hand-checked.
/// </summary>
public class HelpFlowLayoutTests
{
    [Test]
    public async Task WordsWrapOnlyAtSpacesAndStyledJoinsStayTogether()
    {
        // Clusters: "aa" (20), "bb"+bold"cc" (20+24), "dd" (20) at width 70.
        var doc = HelpMarkdown.Parse("aa bb**cc** dd");

        string dump = Dump(Layout(doc, width: 70f));

        await Assert.That(dump).IsEqualTo(
            "T(0,0,20) aa\n" +
            "T(0,20,20) bb\n" +
            "T.B(20,20,24) cc\n" +
            "T(0,40,20) dd\n" +
            "H=60");
    }

    [Test]
    public async Task HeadingsUseTheirFontAndTightenSpacingToTheNextBlock()
    {
        var doc = HelpMarkdown.Parse("# Top\n\nBody.");

        string dump = Dump(Layout(doc, width: 200f));

        await Assert.That(dump).IsEqualTo(
            "T.H1(0,0,30) Top\n" +
            "T(0,34,50) Body.\n" +
            "H=54");
    }

    [Test]
    public async Task ListItemsIndentPastTheirMarkersAndPackTighter()
    {
        var doc = HelpMarkdown.Parse(
            "- one\n" +
            "- two\n" +
            "\n" +
            "3. three\n" +
            "\n" +
            "para");

        string dump = Dump(Layout(doc, width: 200f));

        // Markers center in the text indent: the gap before the marker
        // equals the gap between marker and text ((18-10)/2 = 4 for the
        // bullet); a marker wider than the indent clamps to 0 ("3." = 20).
        await Assert.That(dump).IsEqualTo(
            "M(4,0) •\n" +
            "T(18,0,30) one\n" +
            "M(4,22) •\n" +
            "T(18,22,30) two\n" +
            "M(0,50) 3.\n" +
            "T(18,50,50) three\n" +
            "T(0,78,40) para\n" +
            "H=98");
    }

    [Test]
    public async Task WrappedListLinesContinueAtTheTextIndentWithNormalLinePitch()
    {
        // "aaa"/"bbb" are 30 wide; 18 + 30 + 10 + 30 = 88 > 70 wraps "bbb"
        // back to the text indent, one line height down (no extra spacing).
        var doc = HelpMarkdown.Parse("- aaa bbb");

        string dump = Dump(Layout(doc, width: 70f));

        await Assert.That(dump).IsEqualTo(
            "M(4,0) •\n" +
            "T(18,0,30) aaa\n" +
            "T(18,20,30) bbb\n" +
            "H=40");
    }

    [Test]
    public async Task TopicLinkWordsCarryTheirTargetAndWrapLikeText()
    {
        var doc = HelpMarkdown.Parse("See [the plan](topic:plans) now.");

        string dump = Dump(Layout(doc, width: 300f));

        await Assert.That(dump).IsEqualTo(
            "T(0,0,30) See\n" +
            "L(40,0,30>plans) the\n" +
            "L(80,0,40>plans) plan\n" +
            "T(130,0,40) now.\n" +
            "H=20");
    }

    [Test]
    public async Task ImagesScaleDownToTheContentWidthAndUnresolvedImagesVanish()
    {
        var doc = HelpMarkdown.Parse(
            "before\n" +
            "\n" +
            "![Alt](pic.png)\n" +
            "\n" +
            "![Alt](missing.png)\n" +
            "\n" +
            "after");

        string dump = Dump(Layout(doc, width: 150f,
            imageSize: (string path, out float w, out float h) =>
            {
                w = 200f;
                h = 100f;
                return path == "pic.png";
            }));

        await Assert.That(dump).IsEqualTo(
            "T(0,0,60) before\n" +
            "IMG(0,28,150x75) pic.png\n" +
            "T(0,111,50) after\n" +
            "H=131");
    }

    [Test]
    public async Task InlineImagesFlowWithTextGrowTheLineBoxAndDegradeToAltText()
    {
        // Resolved chip.png is 40x30: the line box grows to 30 and the
        // 20-tall text centers at y=5. gone.png fails to resolve and
        // degrades to its alt word "x".
        var doc = HelpMarkdown.Parse(
            "See ![chip](chip.png) here\n" +
            "\n" +
            "a ![x](gone.png) b");

        var layout = Layout(doc, width: 300f,
            imageSize: (string path, out float w, out float h) =>
            {
                w = 40f;
                h = 30f;
                return path == "chip.png";
            });

        await Assert.That(Dump(layout)).IsEqualTo(
            "T(0,5,30) See\n" +
            "IMG(40,0,40x30) chip.png\n" +
            "T(90,5,40) here\n" +
            "T(0,38,10) a\n" +
            "T(20,38,10) x\n" +
            "T(40,38,10) b\n" +
            "H=58");
    }

    [Test]
    public async Task DemoBlocksReserveTheirRegisteredSizeAndUnknownDemosVanish()
    {
        var doc = HelpMarkdown.Parse(
            "above\n" +
            "\n" +
            "@demo:chip-drag\n" +
            "\n" +
            "@demo:unregistered\n" +
            "\n" +
            "below\n");

        var layout = HelpFlowLayout.Build(doc, 300f, new MonoMeasurer(),
            Metrics(),
            (string _, out float w, out float h) =>
            {
                w = 0f;
                h = 0f;
                return false;
            },
            (string name, out float w, out float h) =>
            {
                w = 200f;
                h = 60f;
                return name == "chip-drag";
            });

        await Assert.That(Dump(layout)).IsEqualTo(
            "T(0,0,50) above\n" +
            "DEMO(0,28,200x60) chip-drag\n" +
            "T(0,96,50) below\n" +
            "H=116");
    }

    [Test]
    public async Task KeyedLabelsResolveToStyledTextBeforeLayoutWithKeyFallback()
    {
        var doc = HelpMarkdown.Parse(
            "Press **{k:IMP_MakeItSo}** or {k:IMP_Missing}.");

        var resolved = HelpDocuments.ResolveKeyedLabels(doc,
            key => key == "IMP_MakeItSo" ? "Make It So" : null);
        string dump = Dump(Layout(resolved, width: 400f));

        await Assert.That(dump).IsEqualTo(
            "T(0,0,50) Press\n" +
            "T.B(60,0,48) Make\n" +
            "T.B(118,0,24) It\n" +
            "T.B(152,0,24) So\n" +
            "T(186,0,20) or\n" +
            "T(216,0,120) IMP_Missing.\n" +
            "H=20");
    }

    private static HelpTopicLayout Layout(HelpDocument doc, float width,
        HelpFlowLayout.ImageSizeResolver? imageSize = null)
    {
        imageSize ??= (string _, out float w, out float h) =>
        {
            w = 0f;
            h = 0f;
            return false;
        };
        return HelpFlowLayout.Build(
            doc, width, new MonoMeasurer(), Metrics(), imageSize);
    }

    private static HelpLayoutMetrics Metrics() => new()
    {
        ParagraphSpacing = 8f,
        HeadingSpacingTop = 12f,
        HeadingSpacingBottom = 4f,
        ListIndent = 18f,
        ListItemSpacing = 2f,
    };

    private sealed class MonoMeasurer : IHelpTextMeasurer
    {
        public float WordWidth(string word, HelpFont font, HelpRunStyle style)
            => word.Length * ((style & HelpRunStyle.Bold) != 0 ? 12f : 10f);

        public float SpaceWidth(HelpFont font, HelpRunStyle style) => 10f;

        public float LineHeight(HelpFont font) => font switch
        {
            HelpFont.H1 => 30f,
            HelpFont.H2 => 26f,
            HelpFont.H3 => 24f,
            _ => 20f,
        };
    }

    private static string Dump(HelpTopicLayout layout)
    {
        var sb = new StringBuilder();
        foreach (var item in layout.Items)
        {
            string x = N(item.X), y = N(item.Y), w = N(item.Width);
            switch (item.Kind)
            {
                case HelpItemKind.ListMarker:
                    sb.Append($"M({x},{y}) {item.Text}");
                    break;
                case HelpItemKind.Image:
                    sb.Append($"IMG({x},{y},{w}x{N(item.Height)}) {item.Text}");
                    break;
                case HelpItemKind.Demo:
                    sb.Append($"DEMO({x},{y},{w}x{N(item.Height)}) {item.Text}");
                    break;
                case HelpItemKind.TopicLink:
                    sb.Append($"L({x},{y},{w}>{item.Target}) {item.Text}");
                    break;
                default:
                    sb.Append("T");
                    if (item.Font != HelpFont.Body)
                        sb.Append('.').Append(item.Font);
                    if (item.Style != HelpRunStyle.None)
                    {
                        sb.Append('.');
                        if ((item.Style & HelpRunStyle.Bold) != 0) sb.Append('B');
                        if ((item.Style & HelpRunStyle.Italic) != 0) sb.Append('I');
                    }
                    sb.Append($"({x},{y},{w}) {item.Text}");
                    break;
            }
            sb.Append('\n');
        }
        sb.Append("H=").Append(N(layout.Height));
        return sb.ToString();
    }

    private static string N(float value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);
}
