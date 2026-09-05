using RimShared.Common;

namespace RimShared.Common.Tests;

/// A caption that cannot fit keeps its END behind an ellipsis: "overrides
/// Bionic leg" squeezed into a narrow slot reads "...ionic leg", not
/// "overrides Bio...". Widths come from a monospace stand-in (8 per char).
public class TailTruncationTests
{
    static float Mono(string text) => text.Length * 8f;

    [Test]
    public async Task FittingTextIsReturnedUnchanged()
    {
        string fitted = TailTruncation.Fit("overrides Bionic leg", 160f, Mono, out float width);

        await Assert.That(fitted).IsEqualTo("overrides Bionic leg");
        await Assert.That(width).IsEqualTo(160f);
    }

    [Test]
    public async Task OverflowKeepsTheLongestTailBehindAnEllipsis()
    {
        // 100 units hold 12 chars: three for the ellipsis, nine of tail.
        string fitted = TailTruncation.Fit("overrides Bionic leg", 100f, Mono, out float width);

        await Assert.That(fitted).IsEqualTo("...ionic leg");
        await Assert.That(width).IsEqualTo(96f);
    }

    [Test]
    public async Task TailNeverStartsWithASpace()
    {
        // 56 units hold 7 chars; the four-char tail " leg" would start with
        // a space, so it is trimmed rather than shown as "... leg".
        string fitted = TailTruncation.Fit("overrides leg", 56f, Mono, out float width);

        await Assert.That(fitted).IsEqualTo("...leg");
        await Assert.That(width).IsEqualTo(48f);
    }

    [Test]
    public async Task ImpossiblyNarrowSlotStillShowsTheLastCharacter()
    {
        string fitted = TailTruncation.Fit("overrides Bionic leg", 10f, Mono, out float width);

        await Assert.That(fitted).IsEqualTo("...g");
        await Assert.That(width).IsEqualTo(32f);
    }

    [Test]
    public async Task EmptyTextStaysEmpty()
    {
        string fitted = TailTruncation.Fit("", 10f, Mono, out float width);

        await Assert.That(fitted).IsEqualTo("");
        await Assert.That(width).IsEqualTo(0f);
    }
}
