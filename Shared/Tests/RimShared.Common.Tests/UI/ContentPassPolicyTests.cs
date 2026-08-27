namespace RimShared.Common.Tests;

public class ContentPassPolicyTests
{
    [Test]
    [Arguments(ContentPassKind.Repaint)]
    [Arguments(ContentPassKind.MouseDown)]
    [Arguments(ContentPassKind.MouseUp)]
    [Arguments(ContentPassKind.KeyDown)]
    [Arguments(ContentPassKind.ScrollWheel)]
    [Arguments(ContentPassKind.Command)]
    [Arguments(ContentPassKind.ContextClick)]
    public async Task ConsumedEventKindDraws(ContentPassKind kind)
    {
        bool draws = ContentPassPolicy.DrawsContent(kind, nativeControlOwnsDrag: false);

        await Assert.That(draws).IsTrue();
    }

    [Test]
    [Arguments(ContentPassKind.Layout)]
    [Arguments(ContentPassKind.Other)]
    public async Task NonContentPassDoesNotDraw(ContentPassKind kind)
    {
        bool draws = ContentPassPolicy.DrawsContent(kind, nativeControlOwnsDrag: false);

        await Assert.That(draws).IsFalse();
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(true, true)]
    public async Task MouseDragDrawsOnlyWhileANativeControlOwnsTheDrag(bool nativeControlOwnsDrag, bool expected)
    {
        bool draws = ContentPassPolicy.DrawsContent(ContentPassKind.MouseDrag, nativeControlOwnsDrag);

        await Assert.That(draws).IsEqualTo(expected);
    }
}
