using RimWorld.Automation.Core;

public class KeySequenceTests
{
    [Test]
    public async Task NamedSpacesAndLiteralSpacesTypeTheSameSearch()
    {
        await Assert.That(new string(KeySequence.Parse("Steel{SPACE}slag").Select(k => k.Character).ToArray()))
            .IsEqualTo("Steel slag");
    }

    [Test]
    public async Task ExistingSearchAndShortcutScriptPreservesTextAndModifierScope()
    {
        var keys = KeySequence.Parse("^aSteel{BACKSPACE}l{ENTER}+(ab){LEFT 2}{F12}{+}#");
        await Assert.That(string.Concat(keys.Select(k => k.Character == '\0' ? $"[{k.Key}]" : k.Character.ToString())))
            .IsEqualTo("aSteel[Backspace]l[Return]AB[LeftArrow][LeftArrow][F12]+#");
        await Assert.That(keys[0].Modifiers).IsEqualTo(KeyModifiers.Control);
        await Assert.That(keys[1].Modifiers).IsEqualTo(KeyModifiers.Shift);
        await Assert.That(keys[9].Modifiers).IsEqualTo(KeyModifiers.Shift);
        await Assert.That(keys[10].Modifiers).IsEqualTo(KeyModifiers.Shift);
        await Assert.That(keys[11].Modifiers).IsEqualTo(KeyModifiers.None);
    }

    [Test]
    public async Task InvalidOrUnboundedSequencesAreRejectedBeforeAnyInputIsReturned()
    {
        foreach (var text in new[] { "abc{UNKNOWN}", "abc{LEFT 1000000}", "^(abc", "abc^", "abc)", "abc{ENTER" })
            await Assert.That(() => KeySequence.Parse(text)).Throws<FormatException>();
    }
}
