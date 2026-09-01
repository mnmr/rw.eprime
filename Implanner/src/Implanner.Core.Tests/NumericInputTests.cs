using Implanner.Core;

namespace Implanner.Core.Tests;

/// The reserve fields' typing rules: a partially typed number keeps the
/// buffer without touching the value, a fully typed number commits the
/// clamped value and normalizes the buffer, and rejected text leaves both
/// alone (the mod-owned field mirrors the game's own numeric field here,
/// under the caller's control name).
public class NumericInputTests
{
    [Test]
    public async Task TypingSequenceCommitsOnlyFullyTypedNumbers()
    {
        int value = 20;
        string buffer = "20";

        // Clearing the field is a partial edit: the buffer empties, the
        // value stays until something parseable is typed.
        await Assert.That(NumericInput.Apply("", 0, 999, ref value, ref buffer)).IsTrue();
        await Assert.That(buffer).IsEqualTo("");
        await Assert.That(value).IsEqualTo(20);

        // Digits commit immediately and the buffer echoes the value.
        await Assert.That(NumericInput.Apply("7", 0, 999, ref value, ref buffer)).IsTrue();
        await Assert.That(value).IsEqualTo(7);
        await Assert.That(buffer).IsEqualTo("7");

        // Out-of-range input clamps to the field's maximum.
        await Assert.That(NumericInput.Apply("5000", 0, 999, ref value, ref buffer)).IsTrue();
        await Assert.That(value).IsEqualTo(999);
        await Assert.That(buffer).IsEqualTo("999");

        // Unchanged text is a no-op.
        await Assert.That(NumericInput.Apply("999", 0, 999, ref value, ref buffer)).IsFalse();
    }

    [Test]
    public async Task RejectedTextLeavesBufferAndValueAlone()
    {
        int value = 5;
        string buffer = "5";

        // A minus sign is not typeable on a non-negative field, letters
        // never are, a doubled leading zero and over-long text are refused.
        await Assert.That(NumericInput.Apply("-1", 0, 999, ref value, ref buffer)).IsFalse();
        await Assert.That(NumericInput.Apply("5a", 0, 999, ref value, ref buffer)).IsFalse();
        await Assert.That(NumericInput.Apply("00", 0, 999, ref value, ref buffer)).IsFalse();
        await Assert.That(NumericInput.Apply("1234567890123", 0, 999, ref value, ref buffer)).IsFalse();
        await Assert.That(value).IsEqualTo(5);
        await Assert.That(buffer).IsEqualTo("5");

        // A negative field accepts the sign as a partial edit, then commits
        // the clamped negative value.
        await Assert.That(NumericInput.Apply("-", -10, 10, ref value, ref buffer)).IsTrue();
        await Assert.That(buffer).IsEqualTo("-");
        await Assert.That(value).IsEqualTo(5);
        await Assert.That(NumericInput.Apply("-40", -10, 10, ref value, ref buffer)).IsTrue();
        await Assert.That(value).IsEqualTo(-10);
        await Assert.That(buffer).IsEqualTo("-10");
    }

    /// Digits that overflow an int keep the typed text visible without
    /// committing a value, exactly like the game's field.
    [Test]
    public async Task OverflowingDigitsKeepTheTextWithoutCommitting()
    {
        int value = 3;
        string buffer = "3";

        await Assert.That(NumericInput.Apply("99999999999", 0, 999999, ref value, ref buffer)).IsTrue();
        await Assert.That(buffer).IsEqualTo("99999999999");
        await Assert.That(value).IsEqualTo(3);
    }
}
