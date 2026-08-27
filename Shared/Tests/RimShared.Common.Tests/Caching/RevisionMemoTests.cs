using RimShared.Common;

namespace RimShared.Common.Tests;

public class RevisionMemoTests
{
    [Test]
    public async Task SameDependencyRevisionReusesTheStoredValue()
    {
        var memo = new RevisionMemo<string, object>();
        var value = new object();
        memo.Store(7, "steel", value);

        bool found = memo.TryGet(7, "steel", out object? cached);

        await Assert.That(found).IsTrue();
        await Assert.That(cached).IsSameReferenceAs(value);
    }

    [Test]
    public async Task DependencyRevisionChangeDropsAllAnswers()
    {
        var memo = new RevisionMemo<string, int>();
        memo.Store(7, "steel", 42);
        memo.Store(7, "wood", 9);

        bool found = memo.TryGet(8, "steel", out _);

        await Assert.That(found).IsFalse();
        await Assert.That(memo.Count).IsEqualTo(0);
    }

    [Test]
    public async Task RevisionRewindCannotServeAStaleAnswer()
    {
        var memo = new RevisionMemo<string, int>();
        memo.Store(12, "steel", 42);

        await Assert.That(memo.TryGet(2, "steel", out _)).IsFalse();
    }

    [Test]
    public async Task ClearIsIdempotentAndDropsTheStamp()
    {
        var memo = new RevisionMemo<string, int>();
        memo.Store(3, "steel", 42);

        memo.Clear();
        memo.Clear();

        await Assert.That(memo.Count).IsEqualTo(0);
        await Assert.That(memo.TryGet(3, "steel", out _)).IsFalse();
    }
}
