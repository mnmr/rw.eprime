namespace QualityJobs.Core.Tests;

using QualityJobs.Core;

/// Answers that are expensive to compute and constant for the duration of one
/// game tick: a caller that asks the same question many times within a single
/// pass pays for it once.
public class TickMemoTests
{
    private static TickMemo<string, int> NewMemo() => new();

    [Test]
    public async Task AnUnaskedKeyMisses()
    {
        await Assert.That(NewMemo().TryGet(10, "steel", out _)).IsFalse();
    }

    [Test]
    public async Task AStoredAnswerIsReusedWithinTheSameTick()
    {
        var memo = NewMemo();
        memo.TryGet(10, "steel", out _);
        memo.Store("steel", 42);

        await Assert.That(memo.TryGet(10, "steel", out int value)).IsTrue();
        await Assert.That(value).IsEqualTo(42);
    }

    [Test]
    public async Task AnAnswerDoesNotSurviveIntoTheNextTick()
    {
        var memo = NewMemo();
        memo.TryGet(10, "steel", out _);
        memo.Store("steel", 42);

        await Assert.That(memo.TryGet(11, "steel", out _)).IsFalse();
    }

    [Test]
    public async Task AnAnswerDoesNotSurviveIntoAnEarlierTick()
    {
        // Loading a save rewinds the clock; a stale answer must not leak.
        var memo = NewMemo();
        memo.TryGet(5000, "steel", out _);
        memo.Store("steel", 42);

        await Assert.That(memo.TryGet(9, "steel", out _)).IsFalse();
    }

    [Test]
    public async Task MovingToANewTickDropsEveryEntry()
    {
        var memo = NewMemo();
        memo.TryGet(10, "steel", out _);
        memo.Store("steel", 1);
        memo.Store("wood", 2);

        memo.TryGet(11, "steel", out _);

        await Assert.That(memo.Count).IsEqualTo(0);
    }

    [Test]
    public async Task DistinctKeysAreAnsweredIndependently()
    {
        var memo = NewMemo();
        memo.TryGet(10, "steel", out _);
        memo.Store("steel", 1);
        memo.Store("wood", 2);

        await Assert.That(memo.TryGet(10, "wood", out int wood)).IsTrue();
        await Assert.That(wood).IsEqualTo(2);
        await Assert.That(memo.TryGet(10, "steel", out int steel)).IsTrue();
        await Assert.That(steel).IsEqualTo(1);
    }

    [Test]
    public async Task StoringTwiceKeepsTheLatestAnswer()
    {
        var memo = NewMemo();
        memo.TryGet(10, "steel", out _);
        memo.Store("steel", 1);
        memo.Store("steel", 7);

        memo.TryGet(10, "steel", out int value);
        await Assert.That(value).IsEqualTo(7);
    }

    [Test]
    public async Task ClearDropsEverythingAndTheTickStamp()
    {
        var memo = NewMemo();
        memo.TryGet(10, "steel", out _);
        memo.Store("steel", 42);

        memo.Clear();

        await Assert.That(memo.Count).IsEqualTo(0);
        await Assert.That(memo.TryGet(10, "steel", out _)).IsFalse();
    }

    [Test]
    public async Task ClearIsSafeOnAnEmptyMemo()
    {
        var memo = NewMemo();
        memo.Clear();
        memo.Clear();

        await Assert.That(memo.Count).IsEqualTo(0);
    }

    [Test]
    public async Task RepeatedReadsWithinATickDoNotDropTheEntry()
    {
        var memo = NewMemo();
        memo.TryGet(10, "steel", out _);
        memo.Store("steel", 42);

        memo.TryGet(10, "steel", out _);
        memo.TryGet(10, "steel", out _);

        await Assert.That(memo.Count).IsEqualTo(1);
    }
}
