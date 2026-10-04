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
}
