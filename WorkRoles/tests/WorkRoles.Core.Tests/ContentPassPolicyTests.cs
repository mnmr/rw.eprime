using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

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
    public async Task ConsumedEventKindsAlwaysDraw(ContentPassKind kind)
    {
        await Assert.That(ContentPassPolicy.DrawsContent(kind, nativeControlOwnsDrag: false)).IsTrue();
        await Assert.That(ContentPassPolicy.DrawsContent(kind, nativeControlOwnsDrag: true)).IsTrue();
    }

    [Test]
    [Arguments(ContentPassKind.Layout)]
    [Arguments(ContentPassKind.Other)]
    public async Task LayoutAndUnrecognizedKindsNeverDraw(ContentPassKind kind)
    {
        await Assert.That(ContentPassPolicy.DrawsContent(kind, nativeControlOwnsDrag: false)).IsFalse();
        await Assert.That(ContentPassPolicy.DrawsContent(kind, nativeControlOwnsDrag: true)).IsFalse();
    }

    [Test]
    public async Task MouseDragDrawsOnlyWhileANativeControlOwnsTheDrag()
    {
        await Assert.That(ContentPassPolicy.DrawsContent(
            ContentPassKind.MouseDrag, nativeControlOwnsDrag: false)).IsFalse();
        await Assert.That(ContentPassPolicy.DrawsContent(
            ContentPassKind.MouseDrag, nativeControlOwnsDrag: true)).IsTrue();
    }
}
