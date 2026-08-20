using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

public class DialogInteractionPolicyTests
{
    [Test]
    [Arguments(true, false, DialogEscapeAction.ClearInput)]
    [Arguments(true, true, DialogEscapeAction.UnfocusInput)]
    [Arguments(false, true, DialogEscapeAction.CloseDialog)]
    public async Task EscapeUsesTheFocusedInputState(
        bool inputFocused, bool inputEmpty, DialogEscapeAction expected)
    {
        await Assert.That(DialogInteractionPolicy.Escape(inputFocused, inputEmpty)).IsEqualTo(expected);
    }

    [Test]
    [Arguments(10f, 100f)]
    [Arguments(180f, 220f)]
    [Arguments(900f, 480f)]
    public async Task CompactDialogHeightIsContentSizedAndClamped(float bodyHeight, float expected)
    {
        await Assert.That(CompactDialogLayout.Height(bodyHeight, chromeHeight: 40f, minHeight: 100f, maxHeight: 480f))
            .IsEqualTo(expected);
    }
}
